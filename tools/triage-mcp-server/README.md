# triage-mcp-server

Read-only MCP server for Azure DevOps feedback triage.

## Setup

```bash
npm install
npm run build
```

## Environment Variables

- `AZDO_PAT` — Azure DevOps Personal Access Token (Work Items: Read scope)
- `AZDO_ORG` — Organization name (default: `devdiv`)
- `AZDO_PROJECT` — Project name (default: `DevDiv`)
- `AZDO_QUERY_ID` — Saved query ID for untriaged items

## Usage

Registered as an MCP server in `.vscode/mcp.json`. Used by the `@triage-intel` agent.
