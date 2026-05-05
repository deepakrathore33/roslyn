import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { AzdoClient } from "./azdo-client";
import { AzdoConfig } from "./types";
import { queryWorkItems } from "./tools/query-work-items";
import { getWorkItem } from "./tools/get-work-item";
import { getWorkItemsBatch } from "./tools/get-work-items-batch";
import { listAttachments } from "./tools/list-attachments";
import { downloadAttachment } from "./tools/download-attachment";
import { downloadDiagnostics } from "./tools/download-diagnostics";
import { analyzeDump, analyzeEtl } from "./tools/analyze-diagnostics";
import { getComments } from "./tools/get-comments";

// --- Configuration from environment ---
// AZDO_AUTH=azcli forces az cli mode (and explicitly DISCARDS any AZDO_PAT in
// env, so an expired user-scope PAT can never silently shadow the az cli token).
// Otherwise:  PAT if set, else az cli.
const forceAzCli = process.env.AZDO_AUTH === "azcli";
const useAzCli = forceAzCli || !process.env.AZDO_PAT;
const effectivePat = forceAzCli ? undefined : process.env.AZDO_PAT;

const config: AzdoConfig = {
  pat: effectivePat,
  org: process.env.AZDO_ORG ?? "devdiv",
  project: process.env.AZDO_PROJECT ?? "DevDiv",
  queryId: process.env.AZDO_QUERY_ID,
  useAzCli,
};

if (!config.pat && !config.useAzCli) {
  console.error("ERROR: No auth configured. Either:");
  console.error("  1. Run 'az login' (recommended — auto-renewing)");
  console.error("  2. Set AZDO_PAT environment variable");
  process.exit(1);
}

if (config.useAzCli) {
  console.error(
    forceAzCli && process.env.AZDO_PAT
      ? "[config] Using az cli bearer token for Azure DevOps auth (AZDO_AUTH=azcli; AZDO_PAT in env IGNORED)"
      : "[config] Using az cli bearer token for Azure DevOps auth",
  );
} else {
  console.error("[config] Using PAT for Azure DevOps auth");
}

const client = new AzdoClient(config);

// --- MCP Server ---
const server = new McpServer({
  name: "azdo-triage",
  version: "1.0.0",
});

// Tool 1: query_work_items
server.tool(
  "query_work_items",
  "Run a saved Azure DevOps query or custom WIQL to fetch untriaged feedback items. Returns work item metadata (title, description, repro steps, area path) with PII scrubbed. Does NOT return customer attachments, screenshots, ETL or dump files.",
  {
    queryId: z.string().optional().describe(
      "Saved query ID (GUID). If omitted, uses the default configured query."
    ),
    wiql: z.string().optional().describe(
      "Custom WIQL query string. Used only if queryId is not provided."
    ),
    maxItems: z.number().optional().default(10).describe(
      "Maximum number of items to return (1-50, default 10)."
    ),
  },
  async (args) => {
    try {
      const effectiveQueryId = args.queryId ?? config.queryId;
      const result = await queryWorkItems(client, {
        queryId: effectiveQueryId,
        wiql: args.wiql,
        maxItems: args.maxItems,
      });
      return {
        content: [
          {
            type: "text" as const,
            text: JSON.stringify(result, null, 2),
          },
        ],
      };
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      return {
        content: [{ type: "text" as const, text: `Error: ${message}` }],
        isError: true,
      };
    }
  }
);

// Tool 2: get_work_item
server.tool(
  "get_work_item",
  "Fetch a single Azure DevOps work item by ID. Returns only safe metadata fields (title, description, repro steps, area path, state, tags). PII is scrubbed. Customer attachments are never returned.",
  {
    id: z.number().describe("The work item ID to fetch."),
  },
  async (args) => {
    try {
      const result = await getWorkItem(client, { id: args.id });
      return {
        content: [
          {
            type: "text" as const,
            text: JSON.stringify(result, null, 2),
          },
        ],
      };
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      return {
        content: [{ type: "text" as const, text: `Error: ${message}` }],
        isError: true,
      };
    }
  }
);

// Tool 3: get_work_items_batch
server.tool(
  "get_work_items_batch",
  "Fetch multiple Azure DevOps work items by their IDs in a single call. Returns only safe metadata fields with PII scrubbed. Max 50 items per call. Customer attachments are never returned.",
  {
    ids: z.array(z.number()).describe("Array of work item IDs to fetch (max 50)."),
  },
  async (args) => {
    try {
      const result = await getWorkItemsBatch(client, { ids: args.ids });
      return {
        content: [
          {
            type: "text" as const,
            text: JSON.stringify(result, null, 2),
          },
        ],
      };
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      return {
        content: [{ type: "text" as const, text: `Error: ${message}` }],
        isError: true,
      };
    }
  }
);

// Tool 4: get_comments
server.tool(
  "get_comments",
  "Fetch all comments on a work item — internal engineer discussion, user comments, and AI summaries. Returns author, date, and text (HTML stripped, PII scrubbed). Up to 100 comments.",
  {
    id: z.number().describe("The work item ID to fetch comments for."),
  },
  async (args) => {
    try {
      const result = await getComments(client, { id: args.id });
      return {
        content: [
          {
            type: "text" as const,
            text: JSON.stringify(result, null, 2),
          },
        ],
      };
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      return {
        content: [{ type: "text" as const, text: `Error: ${message}` }],
        isError: true,
      };
    }
  }
);

// Tool 5: get_work_item_full
server.tool(
  "get_work_item_full",
  "Fetch a work item with ALL triage-relevant data in one call: metadata fields, all comments (internal + user), Developer Community link, and Azure DevOps link. Use this instead of separate get_work_item + get_comments calls.",
  {
    id: z.number().describe("The work item ID to fetch."),
  },
  async (args) => {
    try {
      const result = await client.getWorkItemFull(args.id);
      // Apply field filtering and PII scrubbing to the item
      const { filterWorkItem } = await import("./filters/field-allowlist");
      const filteredItem = filterWorkItem(result.item);

      // Scrub PII from comments
      const { scrubPii } = await import("./filters/pii-scrubber");
      const scrubbedComments = result.comments.comments.map((c) => ({
        ...c,
        text: scrubPii(c.text) as string,
      }));

      return {
        content: [
          {
            type: "text" as const,
            text: JSON.stringify({
              item: filteredItem,
              comments: {
                totalCount: result.comments.totalCount,
                comments: scrubbedComments,
              },
              devCommunityLink: result.devCommunityLink,
              azdoLink: result.azdoLink,
            }, null, 2),
          },
        ],
      };
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      return {
        content: [{ type: "text" as const, text: `Error: ${message}` }],
        isError: true,
      };
    }
  }
);

// Tool 6: list_attachments
server.tool(
  "list_attachments",
  "List all attachments on a work item, categorized by type (image, dump, etl, log, other). Use this to see what diagnostic files are available before downloading.",
  {
    id: z.number().describe("The work item ID to list attachments for."),
  },
  async (args) => {
    try {
      const result = await listAttachments(client, { id: args.id });
      return {
        content: [
          {
            type: "text" as const,
            text: JSON.stringify(result, null, 2),
          },
        ],
      };
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      return {
        content: [{ type: "text" as const, text: `Error: ${message}` }],
        isError: true,
      };
    }
  }
);

// Tool 5: download_attachment
server.tool(
  "download_attachment",
  "Download a specific attachment from a work item to local disk. Returns the local file path. For images, use view_image on the returned path. For dumps, use analyze_dump.",
  {
    workItemId: z.number().describe("The work item ID."),
    fileName: z.string().describe("The exact file name of the attachment to download."),
  },
  async (args) => {
    try {
      const result = await downloadAttachment(client, {
        workItemId: args.workItemId,
        fileName: args.fileName,
      });
      return {
        content: [
          {
            type: "text" as const,
            text: JSON.stringify(result, null, 2),
          },
        ],
      };
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      return {
        content: [{ type: "text" as const, text: `Error: ${message}` }],
        isError: true,
      };
    }
  }
);

// Tool 6: download_diagnostics
server.tool(
  "download_diagnostics",
  "Download ALL diagnostic attachments from a work item at once (images, dumps, ETLs, logs). Returns local file paths grouped by type. Optionally filter by type.",
  {
    workItemId: z.number().describe("The work item ID."),
    types: z.array(z.string()).optional().describe(
      'Filter by file types: "image", "dump", "etl", "log", "other". Omit to download all.'
    ),
  },
  async (args) => {
    try {
      const result = await downloadDiagnostics(client, {
        workItemId: args.workItemId,
        types: args.types,
      });
      return {
        content: [
          {
            type: "text" as const,
            text: JSON.stringify(result, null, 2),
          },
        ],
      };
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      return {
        content: [{ type: "text" as const, text: `Error: ${message}` }],
        isError: true,
      };
    }
  }
);

// Tool 7: analyze_dump
server.tool(
  "analyze_dump",
  "Analyze a crash dump (.dmp) file using dotnet-dump. Extracts thread stacks, exception info, and loaded modules. The dump must be downloaded first using download_attachment or download_diagnostics.",
  {
    dumpPath: z.string().describe("Local file path to the .dmp file."),
    commands: z.array(z.string()).optional().describe(
      'dotnet-dump commands to run. Defaults to: ["clrstack", "pe", "threads", "clrmodules"]'
    ),
  },
  async (args) => {
    try {
      const result = await analyzeDump({
        dumpPath: args.dumpPath,
        commands: args.commands,
      });
      return {
        content: [
          {
            type: "text" as const,
            text: JSON.stringify(result, null, 2),
          },
        ],
      };
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      return {
        content: [{ type: "text" as const, text: `Error: ${message}` }],
        isError: true,
      };
    }
  }
);

// Tool 8: analyze_etl
server.tool(
  "analyze_etl",
  "Get basic info about an ETL trace file (size, location). Full ETL analysis requires PerfView or WPA. The ETL must be downloaded first.",
  {
    etlPath: z.string().describe("Local file path to the .etl file."),
  },
  async (args) => {
    try {
      const result = await analyzeEtl({ etlPath: args.etlPath });
      return {
        content: [
          {
            type: "text" as const,
            text: JSON.stringify(result, null, 2),
          },
        ],
      };
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      return {
        content: [{ type: "text" as const, text: `Error: ${message}` }],
        isError: true,
      };
    }
  }
);

// --- Start server ---
async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("Azure DevOps Triage MCP Server running (read-only)");
}

main().catch((err) => {
  console.error("Fatal error:", err);
  process.exit(1);
});
