/**
 * Optional HTTP/UI surface for the triage MCP server.
 *
 * Reuses the same AzdoClient + tool functions as the stdio MCP server,
 * so PII scrubbing and field allowlisting are inherited.
 *
 * Run:
 *   npm run web                       # default port 5173
 *   PORT=8080 npm run web             # custom port
 */

import express, { Request, Response, NextFunction } from "express";
import * as path from "path";
import { AzdoClient } from "./azdo-client";
import { AzdoConfig } from "./types";
import { queryWorkItems } from "./tools/query-work-items";
import { getWorkItem } from "./tools/get-work-item";
import { getWorkItemsBatch } from "./tools/get-work-items-batch";
import { listAttachments } from "./tools/list-attachments";
import { getComments } from "./tools/get-comments";
import { filterWorkItem } from "./filters/field-allowlist";
import { scrubPii } from "./filters/pii-scrubber";
import { TriagePipeline, StepEvent, TokenEvent } from "./triage/pipeline";
import { findWorkspaceRoot } from "./triage/workspace";
import { ProviderRegistry } from "./llm/registry";
import { providerFromId } from "./llm/provider";

// ---- Configuration (mirrors index.ts) ----
// Default to az cli auth so users don't get bitten by expired PATs.
// AZDO_AUTH=azcli forces az cli AND discards any AZDO_PAT in env (so a stale
// user-scope PAT can never silently shadow az cli with a 401).
const forceAzCli = process.env.AZDO_AUTH === "azcli";
const useAzCli = forceAzCli || (!process.env.AZDO_PAT && process.env.AZDO_AUTH !== "pat");
const effectivePat = forceAzCli ? undefined : process.env.AZDO_PAT;
const config: AzdoConfig = {
  pat: effectivePat,
  org: process.env.AZDO_ORG ?? "devdiv",
  project: process.env.AZDO_PROJECT ?? "DevDiv",
  queryId: process.env.AZDO_QUERY_ID,
  useAzCli,
};

if (!config.pat && !config.useAzCli) {
  console.error("ERROR: No auth configured. Run 'az login' or set AZDO_PAT.");
  process.exit(1);
}

const client = new AzdoClient(config);
const llmRegistry = ProviderRegistry.defaults();
const app = express();

app.use(express.json());

// ---- Static UI ----
// __dirname after build = tools/triage-mcp-server/dist
// Web assets live at tools/triage-mcp-server/web
const webDir = path.resolve(__dirname, "..", "web");
app.use(express.static(webDir));

// ---- Helpers ----
function asyncHandler(fn: (req: Request, res: Response) => Promise<unknown>) {
  return (req: Request, res: Response, next: NextFunction) => {
    fn(req, res).catch(next);
  };
}

function parseId(raw: string | undefined): number | null {
  if (!raw) return null;
  const trimmed = raw.trim();
  if (!trimmed) return null;

  // Pure numeric
  if (/^\d+$/.test(trimmed)) {
    const n = Number(trimmed);
    return Number.isFinite(n) && n > 0 ? n : null;
  }

  // AzDO URL: .../_workitems/edit/12345
  const azdoMatch = trimmed.match(/_workitems\/edit\/(\d+)/i);
  if (azdoMatch) return Number(azdoMatch[1]);

  // Developer Community URL: .../t/<slug>/12345
  const devComMatch = trimmed.match(/developercommunity[^?#]*?\/(\d+)(?:\D|$)/i);
  if (devComMatch) return Number(devComMatch[1]);

  // Fallback: last group of digits >= 4 chars
  const fallback = trimmed.match(/(\d{4,})(?!.*\d{4,})/);
  if (fallback) return Number(fallback[1]);

  return null;
}

function parseQueryId(raw: string | undefined): string | null {
  if (!raw) return null;
  const trimmed = raw.trim();
  if (!trimmed) return null;

  // GUID
  if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(trimmed)) {
    return trimmed;
  }

  // URL with /query/<guid> or queryId=<guid>
  const m = trimmed.match(/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})/i);
  return m ? m[1] : null;
}

// ---- API routes ----

/** Health + config snapshot (no secrets). */
app.get("/api/health", (_req: Request, res: Response) => {
  res.json({
    ok: true,
    auth: config.useAzCli ? "azcli" : "pat",
    org: config.org,
    project: config.project,
    defaultQueryId: config.queryId ?? null,
  });
});

/**
 * List LLM providers + models the server can use.
 * Used by the model-selector dropdown in the UI.
 */
app.get("/api/models", asyncHandler(async (_req: Request, res: Response) => {
  const statuses = await llmRegistry.statuses();
  res.json({
    providers: statuses,
    defaultModel: process.env.LLM_DEFAULT_MODEL ?? null,
  });
}));

/** Resolve a free-form input to a work item ID (used by client-side parser too). */
app.post("/api/resolve", (req: Request, res: Response) => {
  const id = parseId(req.body?.input);
  res.json({ id });
});

/**
 * Full triage view of a single work item: filtered fields + comments
 * + attachments + AzDO/DevCom links — all inherits PII scrubbing.
 */
app.get(
  "/api/work-item/:id",
  asyncHandler(async (req, res) => {
    const id = parseId(req.params.id);
    if (!id) {
      res.status(400).json({ error: "Invalid work item id" });
      return;
    }

    const [full, attachments] = await Promise.all([
      client.getWorkItemFull(id),
      client.listAttachments(id).catch((err) => {
        console.error(`[web] listAttachments(${id}) failed:`, err);
        return { attachments: [] };
      }),
    ]);

    const filteredItem = filterWorkItem(full.item);
    const scrubbedComments = full.comments.comments.map((c) => ({
      ...c,
      text: scrubPii(c.text),
    }));

    res.json({
      id,
      item: filteredItem,
      comments: {
        totalCount: full.comments.totalCount,
        comments: scrubbedComments,
      },
      attachments: attachments.attachments,
      devCommunityLink: full.devCommunityLink,
      azdoLink: full.azdoLink,
    });
  })
);

/** Comments only (used for refresh after expanding the panel). */
app.get(
  "/api/work-item/:id/comments",
  asyncHandler(async (req, res) => {
    const id = parseId(req.params.id);
    if (!id) {
      res.status(400).json({ error: "Invalid work item id" });
      return;
    }
    const result = await getComments(client, { id });
    res.json(result);
  })
);

/** Attachment list only. */
app.get(
  "/api/work-item/:id/attachments",
  asyncHandler(async (req, res) => {
    const id = parseId(req.params.id);
    if (!id) {
      res.status(400).json({ error: "Invalid work item id" });
      return;
    }
    const result = await listAttachments(client, { id });
    res.json(result);
  })
);

/**
 * Batch / query route. Accepts ANY of:
 *   ?queryId=<guid|url>
 *   ?ids=12345,67890
 *   ?wiql=SELECT ...
 *   (defaults to AZDO_QUERY_ID)
 */
app.get(
  "/api/work-items",
  asyncHandler(async (req, res) => {
    const maxItems = Math.min(Math.max(Number(req.query.maxItems ?? 25), 1), 50);
    const idsParam = typeof req.query.ids === "string" ? req.query.ids : "";
    const queryIdParam = typeof req.query.queryId === "string" ? req.query.queryId : "";
    const wiqlParam = typeof req.query.wiql === "string" ? req.query.wiql : "";

    if (idsParam) {
      const ids = idsParam
        .split(/[,\s]+/)
        .map((s) => parseId(s))
        .filter((n): n is number => n !== null)
        .slice(0, maxItems);
      if (ids.length === 0) {
        res.status(400).json({ error: "No valid IDs found in 'ids' parameter" });
        return;
      }
      const result = await getWorkItemsBatch(client, { ids });
      res.json({ items: result, totalCount: result.length });
      return;
    }

    if (wiqlParam) {
      const result = await queryWorkItems(client, { wiql: wiqlParam, maxItems });
      res.json(result);
      return;
    }

    const queryId = parseQueryId(queryIdParam) ?? config.queryId;
    if (!queryId) {
      res.status(400).json({
        error: "Provide queryId, ids, or wiql (or set AZDO_QUERY_ID).",
      });
      return;
    }

    const result = await queryWorkItems(client, { queryId, maxItems });
    res.json(result);
  })
);

/**
 * Streaming triage pipeline. Server-Sent Events:
 *   event: step    → individual step status updates
 *   event: token   → streamed LLM token (when using a model-augmented step)
 *   event: result  → final TriageReport (after all steps)
 *   event: error   → fatal error (pipeline aborted)
 *   event: done    → terminator
 *
 * Query params:
 *   ?model=<provider/modelId>  e.g. "github-models/openai/gpt-4o-mini"
 *                              or  "vscode-lm/copilot-gpt-4o"
 *                              When omitted, runs heuristic-only.
 */
app.get("/api/triage/:id", async (req: Request, res: Response) => {
  const id = parseId(req.params.id);
  if (!id) {
    res.status(400).json({ error: "Invalid work item id" });
    return;
  }

  // Resolve optional model selection.
  const modelParam = typeof req.query.model === "string" ? req.query.model.trim() : "";
  let llmConfig: ConstructorParameters<typeof TriagePipeline>[0]["llm"] | undefined;
  if (modelParam && modelParam !== "none" && modelParam !== "heuristic") {
    const pid = providerFromId(modelParam);
    const provider = pid ? llmRegistry.get(pid) : undefined;
    if (!provider) {
      res.status(400).json({ error: `Unknown LLM provider in model id "${modelParam}"` });
      return;
    }
    const ready = await provider.isReady();
    if (!ready.ready) {
      res.status(400).json({
        error: `LLM provider "${provider.label}" is not ready: ${ready.reason ?? "unknown reason"}`,
      });
      return;
    }
    // Build a friendly label to show on each step row.
    const models = await provider.listModels();
    const found = models.find((m) => m.id === modelParam);
    llmConfig = {
      provider,
      model: modelParam,
      label: found?.label ?? modelParam,
    };
  }

  res.status(200).set({
    "Content-Type": "text/event-stream",
    "Cache-Control": "no-cache, no-transform",
    Connection: "keep-alive",
    "X-Accel-Buffering": "no",
  });
  // Flush headers immediately.
  if (typeof (res as unknown as { flushHeaders?: () => void }).flushHeaders === "function") {
    (res as unknown as { flushHeaders: () => void }).flushHeaders();
  }

  const send = (event: string, data: unknown) => {
    res.write(`event: ${event}\n`);
    res.write(`data: ${JSON.stringify(data)}\n\n`);
  };

  // Heartbeat to keep proxies happy.
  const heartbeat = setInterval(() => {
    res.write(`: ping ${Date.now()}\n\n`);
  }, 15_000);

  let aborted = false;
  req.on("close", () => { aborted = true; });

  try {
    const pipeline = new TriagePipeline({
      client,
      id,
      workspaceRoot: findWorkspaceRoot(),
      llm: llmConfig,
      emit: (ev: StepEvent) => { if (!aborted) send("step", ev); },
      emitToken: (ev: TokenEvent) => { if (!aborted) send("token", ev); },
    });
    const report = await pipeline.run();
    if (!aborted) send("result", report);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    if (!aborted) send("error", { message: msg });
  } finally {
    clearInterval(heartbeat);
    if (!aborted) send("done", {});
    res.end();
  }
});

// ---- Error handler ----
app.use((err: Error, _req: Request, res: Response, _next: NextFunction) => {
  console.error("[web] error:", err);
  res.status(500).json({ error: err.message ?? String(err) });
});

// ---- Start ----
const PORT = Number(process.env.PORT ?? 5173);
app.listen(PORT, () => {
  console.error(`Triage UI listening on http://localhost:${PORT}`);
  console.error(`  auth=${config.useAzCli ? "azcli" : "pat"} org=${config.org} project=${config.project}`);
  if (config.queryId) {
    console.error(`  defaultQueryId=${config.queryId}`);
  }
});
