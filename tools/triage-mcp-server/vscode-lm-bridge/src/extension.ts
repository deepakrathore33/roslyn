/**
 * Roslyn Triage LM Bridge
 * -----------------------
 * A tiny VS Code extension that exposes `vscode.lm.*` (Copilot chat models)
 * to a *separate* Node web server (the Roslyn Triage Intel UI).
 *
 * Why: `vscode.lm` is only callable from inside an extension host. Our triage
 * web server runs as a plain Node process so it would otherwise have no way
 * to use the Copilot models the user already pays for.
 *
 * Endpoints (default port 5174, localhost-only):
 *
 *   GET  /                  -> small status page
 *   GET  /health            -> { ok: true, models: <count> }
 *   GET  /models            -> { models: [{id,label,vendor,family,version,maxInputTokens}] }
 *   POST /chat              -> SSE stream of `event: token` then `event: done`
 *
 * Security: only binds to 127.0.0.1. Cross-origin allowed for http://localhost:*
 * since the triage UI is served from a different localhost port.
 */

import * as http from "http";
import * as vscode from "vscode";

let server: http.Server | null = null;
let statusBar: vscode.StatusBarItem | null = null;

const ALLOWED_ORIGIN_RE = /^https?:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/i;

interface ChatMessage { role: "system" | "user" | "assistant"; content: string; }
interface ChatRequest {
  model: string;
  messages: ChatMessage[];
  temperature?: number;
  maxTokens?: number;
}

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  context.subscriptions.push(
    vscode.commands.registerCommand("roslynTriageBridge.start", () => start(context)),
    vscode.commands.registerCommand("roslynTriageBridge.stop", () => stop()),
    vscode.commands.registerCommand("roslynTriageBridge.status", () => showStatus()),
  );

  statusBar = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 1000);
  statusBar.command = "roslynTriageBridge.status";
  context.subscriptions.push(statusBar);
  updateStatusBar(false, 0);

  context.subscriptions.push({ dispose: () => stop() });

  if (vscode.workspace.getConfiguration("roslynTriageBridge").get<boolean>("autoStart", true)) {
    await start(context);
  }
}

export function deactivate(): void {
  stop();
}

async function start(_context: vscode.ExtensionContext): Promise<void> {
  if (server) {
    vscode.window.showInformationMessage("Triage LM Bridge is already running.");
    return;
  }
  const port = vscode.workspace.getConfiguration("roslynTriageBridge").get<number>("port", 5174);

  server = http.createServer((req, res) => handleRequest(req, res));

  await new Promise<void>((resolve, reject) => {
    server!.once("error", reject);
    server!.listen(port, "127.0.0.1", () => {
      server!.removeListener("error", reject);
      resolve();
    });
  }).catch((err) => {
    server = null;
    vscode.window.showErrorMessage(`Triage LM Bridge: failed to listen on ${port}: ${err.message}`);
    throw err;
  });

  const count = await safeListModels();
  updateStatusBar(true, count.length);

  vscode.window.setStatusBarMessage(
    `$(broadcast) Triage LM Bridge listening on http://127.0.0.1:${port} (${count.length} model${count.length === 1 ? "" : "s"})`,
    5000,
  );
}

function stop(): void {
  if (server) {
    server.close();
    server = null;
  }
  updateStatusBar(false, 0);
}

async function showStatus(): Promise<void> {
  const port = vscode.workspace.getConfiguration("roslynTriageBridge").get<number>("port", 5174);
  if (!server) {
    const choice = await vscode.window.showInformationMessage(
      "Triage LM Bridge is stopped.",
      "Start",
    );
    if (choice === "Start") await vscode.commands.executeCommand("roslynTriageBridge.start");
    return;
  }
  const models = await safeListModels();
  const lines = models.length === 0
    ? "No Copilot models available. Sign in to GitHub Copilot first."
    : models.map((m) => `  • ${m.id}`).join("\n");
  await vscode.window.showInformationMessage(
    `Triage LM Bridge running on http://127.0.0.1:${port}\n\nAvailable models (${models.length}):\n${lines}`,
    { modal: true },
  );
}

function updateStatusBar(running: boolean, modelCount: number): void {
  if (!statusBar) return;
  if (running) {
    statusBar.text = `$(broadcast) Triage LM (${modelCount})`;
    statusBar.tooltip = `Triage LM Bridge running with ${modelCount} model(s). Click for details.`;
  } else {
    statusBar.text = "$(circle-slash) Triage LM";
    statusBar.tooltip = "Triage LM Bridge stopped. Click to start.";
  }
  statusBar.show();
}

// ---- HTTP routing ----

function setCors(req: http.IncomingMessage, res: http.ServerResponse): void {
  const origin = req.headers.origin;
  if (typeof origin === "string" && ALLOWED_ORIGIN_RE.test(origin)) {
    res.setHeader("Access-Control-Allow-Origin", origin);
    res.setHeader("Vary", "Origin");
  } else {
    // Permit same-origin / non-browser callers (Node fetch, curl)
    res.setHeader("Access-Control-Allow-Origin", "http://127.0.0.1");
  }
  res.setHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
  res.setHeader("Access-Control-Allow-Headers", "Content-Type, Accept");
}

async function handleRequest(req: http.IncomingMessage, res: http.ServerResponse): Promise<void> {
  setCors(req, res);
  if (req.method === "OPTIONS") { res.statusCode = 204; res.end(); return; }

  const url = req.url ?? "/";
  try {
    if (url === "/" && req.method === "GET")           return sendHomePage(res);
    if (url === "/health" && req.method === "GET")     return sendHealth(res);
    if (url === "/models" && req.method === "GET")     return sendModels(res);
    if (url === "/chat"   && req.method === "POST")    return handleChat(req, res);
    res.statusCode = 404;
    res.end(JSON.stringify({ error: "Not found" }));
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    res.statusCode = 500;
    res.end(JSON.stringify({ error: msg }));
  }
}

function sendHomePage(res: http.ServerResponse): void {
  res.statusCode = 200;
  res.setHeader("Content-Type", "text/html; charset=utf-8");
  res.end(`<!doctype html>
<html><body style="font-family:system-ui;padding:24px;max-width:640px">
<h2>Roslyn Triage LM Bridge</h2>
<p>This local HTTP server proxies <code>vscode.lm.*</code> Copilot chat models
to the Roslyn Triage Intel web UI.</p>
<ul>
  <li><a href="/health">/health</a></li>
  <li><a href="/models">/models</a></li>
  <li><code>POST /chat</code> — JSON: { model, messages, temperature?, maxTokens? } → SSE</li>
</ul>
</body></html>`);
}

async function sendHealth(res: http.ServerResponse): Promise<void> {
  const models = await safeListModels();
  res.statusCode = 200;
  res.setHeader("Content-Type", "application/json");
  res.end(JSON.stringify({ ok: true, models: models.length }));
}

async function sendModels(res: http.ServerResponse): Promise<void> {
  const models = await safeListModels();
  res.statusCode = 200;
  res.setHeader("Content-Type", "application/json");
  res.end(JSON.stringify({
    models: models.map((m) => ({
      id: m.id,
      label: `${m.vendor}/${m.family}${m.version ? ":" + m.version : ""}`,
      vendor: m.vendor,
      family: m.family,
      version: m.version,
      maxInputTokens: m.maxInputTokens,
    })),
  }));
}

async function handleChat(req: http.IncomingMessage, res: http.ServerResponse): Promise<void> {
  let body = "";
  for await (const chunk of req) body += chunk;
  let payload: ChatRequest;
  try {
    payload = JSON.parse(body) as ChatRequest;
  } catch {
    res.statusCode = 400;
    res.end(JSON.stringify({ error: "Invalid JSON body" }));
    return;
  }

  if (!payload.model || !Array.isArray(payload.messages) || payload.messages.length === 0) {
    res.statusCode = 400;
    res.end(JSON.stringify({ error: "Missing model or messages" }));
    return;
  }

  const models = await safeListModels();
  const model = models.find((m) => m.id === payload.model)
    ?? models.find((m) => m.family === payload.model);
  if (!model) {
    res.statusCode = 404;
    res.end(JSON.stringify({
      error: `Model "${payload.model}" not available. Try one of: ${models.map((m) => m.id).join(", ")}`,
    }));
    return;
  }

  // Switch to SSE.
  res.statusCode = 200;
  res.setHeader("Content-Type", "text/event-stream");
  res.setHeader("Cache-Control", "no-cache, no-transform");
  res.setHeader("Connection", "keep-alive");
  res.flushHeaders?.();

  const send = (event: string, data: unknown) => {
    res.write(`event: ${event}\n`);
    res.write(`data: ${JSON.stringify(data)}\n\n`);
  };

  const cancelTokenSource = new vscode.CancellationTokenSource();
  req.on("close", () => cancelTokenSource.cancel());

  try {
    const lmMessages = payload.messages.map((m) => {
      switch (m.role) {
        case "system":
        case "user":
          return vscode.LanguageModelChatMessage.User(m.content);
        case "assistant":
          return vscode.LanguageModelChatMessage.Assistant(m.content);
        default:
          return vscode.LanguageModelChatMessage.User(m.content);
      }
    });

    const opts: vscode.LanguageModelChatRequestOptions = {
      justification: "Roslyn Triage Intel: summarising / classifying a feedback ticket.",
      modelOptions: {
        temperature: payload.temperature,
        // vscode.lm doesn't standardise maxTokens; family-specific setups may consult this.
        ...(payload.maxTokens ? { max_tokens: payload.maxTokens } : {}),
      },
    };

    const response = await model.sendRequest(lmMessages, opts, cancelTokenSource.token);

    let collected = "";
    for await (const fragment of response.text) {
      if (cancelTokenSource.token.isCancellationRequested) break;
      collected += fragment;
      send("token", { delta: fragment });
    }
    send("done", { text: collected });
  } catch (err) {
    let msg: string;
    if (err instanceof vscode.LanguageModelError) {
      msg = `[${err.code}] ${err.message}`;
    } else {
      msg = err instanceof Error ? err.message : String(err);
    }
    send("error", { message: msg });
  } finally {
    cancelTokenSource.dispose();
    res.end();
  }
}

// ---- vscode.lm helpers ----

async function safeListModels(): Promise<vscode.LanguageModelChat[]> {
  try {
    // Empty selector returns every model the user has access to (Copilot, etc.)
    const all = await vscode.lm.selectChatModels({});
    return all;
  } catch {
    return [];
  }
}
