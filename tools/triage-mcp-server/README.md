# triage-mcp-server

Read-only MCP server for Azure DevOps feedback triage, **plus an optional local web UI**
that talks to the same `AzdoClient` and inherits the same PII scrubbing / field
allow-listing.

## Setup

```bash
npm install
npm run build
```

## Environment Variables

### Azure DevOps

- `AZDO_PAT` — Azure DevOps Personal Access Token (Work Items: Read scope)
- `AZDO_AUTH=azcli` — Force `az cli` bearer-token auth (default if no PAT is set)
- `AZDO_ORG` — Organization name (default: `devdiv`)
- `AZDO_PROJECT` — Project name (default: `DevDiv`)
- `AZDO_QUERY_ID` — Saved query ID for untriaged items
- `PORT` — Port for the web UI (default `5173`)

### LLM (optional — pipeline runs heuristic-only when none are set)

- `GITHUB_TOKEN` — Enables the **GitHub Models** provider (gpt-4o, Llama, Mistral, …).
  Free tier is fine; any classic PAT or one with the `models:read` scope works.
- `LLM_DEFAULT_MODEL` — Provider-qualified id (e.g. `github-models/openai/gpt-4o-mini`)
  to pre-select in the model dropdown.
- `VSCODE_LM_BRIDGE_URL` — Override the bridge URL (default `http://127.0.0.1:5174`).

To use **the same Copilot models you see in VS Code Chat**, install the
companion bridge extension under
[`vscode-lm-bridge/`](./vscode-lm-bridge/README.md) — it exposes
`vscode.lm.*` over a localhost HTTP server that this web app proxies to.
No extra auth required (Copilot's existing session is reused).

## Usage

### As an MCP server

Registered in [`.vscode/mcp.json`](../../.vscode/mcp.json) as `azdo-triage`.
Used by the `@triage-intel` agent defined in
[`.github/agents/triage-intel.agent.md`](../../.github/agents/triage-intel.agent.md).

### As a web UI

```bash
npm run web
# → Triage UI listening on http://localhost:5173
```

Open [http://localhost:5173](http://localhost:5173) in a browser. Features:

- **Single Item** tab — paste a feedback ID, AzDO `_workitems/edit/<id>` URL,
  or a Developer Community URL.
- **Batch / Query** tab — saved query ID/URL, comma-separated IDs, or custom WIQL.
- Color-coded badges for State / Type / Area / Severity / Priority / Source /
  Product / Votes / Tags.
- A **Triage Readiness** gauge (0–100) with a confidence label
  (High / Medium / Low) computed from heuristics:
  has description, has repro steps, has comments, has attachments,
  linked to Developer Community, specific area path, severity/priority set.
- Collapsible sections for Description, Repro Steps, Comments
  (each comment also collapsible), Attachments (grouped by type with
  size info), and the raw allow-listed field bag.
- Dark / light theme toggle (persisted in `localStorage`).
- Deep links back to AzDO and Developer Community.

The web UI calls these REST endpoints (all read-only, all backed by the
same allow-list + PII scrubber):

| Endpoint                                  | Backed by                             |
| ----------------------------------------- | ------------------------------------- |
| `GET /api/health`                         | n/a — config snapshot                 |
| `GET /api/models`                         | LLM provider registry                 |
| `GET /api/work-item/:id`                  | `getWorkItemFull` + `listAttachments` |
| `GET /api/work-item/:id/comments`         | `getComments`                         |
| `GET /api/work-item/:id/attachments`      | `listAttachments`                     |
| `GET /api/work-items?queryId=…&maxItems=` | `queryWorkItems`                      |
| `GET /api/work-items?ids=1,2,3`           | `getWorkItemsBatch`                   |
| `GET /api/work-items?wiql=…`              | `queryWorkItems`                      |
| `GET /api/triage/:id?model=…`             | `TriagePipeline` (SSE — `step` / `token` / `result` / `error` / `done`) |

> The MCP server (stdio) and the web server (HTTP) are independent processes —
> running one does not require running the other.
