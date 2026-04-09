# Roslyn Feedback Triage Automation — Detailed Plan

## Overview

Automate the **intelligence-gathering phase** of triaging Visual Studio feedback items from Azure DevOps using a VS Code Copilot Agent backed by an Azure DevOps MCP (Model Context Protocol) server. The agent is **read-only** — it gathers intel and presents findings. A human reviewer makes all triage decisions and updates.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                    VS Code + Copilot Agent Mode                     │
│                                                                     │
│  User: "@triage-intel triage first 10 items"                        │
│         or "@triage-intel triage FB-12345"                          │
│                                                                     │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │                   triage-intel.agent.md                        │  │
│  │  Orchestrates the full research workflow per feedback item     │  │
│  └──────┬──────────────┬──────────────┬──────────────┬───────────┘  │
│         │              │              │              │               │
│    ┌────▼────┐   ┌─────▼─────┐  ┌────▼─────┐  ┌────▼──────────┐   │
│    │ Azure   │   │ GitHub    │  │ Dev      │  │ Local Roslyn  │   │
│    │ DevOps  │   │ MCP       │  │ Community│  │  Workspace    │   │
│    │ MCP     │   │ Server    │  │ (web)    │  │  (code+git)   │   │
│    │ Server  │   │           │  │          │  │               │   │
│    └────┬────┘   └─────┬─────┘  └────┬─────┘  └────┬──────────┘   │
│         │              │              │              │               │
└─────────┼──────────────┼──────────────┼──────────────┼───────────────┘
          │              │              │              │
          ▼              ▼              ▼              ▼
   Azure DevOps     GitHub API    Developer       Local git
   REST API         (issues,      Community       history,
   (read-only)      PRs, search)  website         code search
```

---

## Data Flow (Per Feedback Item)

```
Step 1: FETCH
  Azure DevOps MCP → GET work item by ID or query
  Returns: Title, Description, Repro Steps, Area Path, State, Tags
  ⛔ Does NOT fetch: Attachments, ETL files, dump files, screenshots

Step 2: SEARCH DEVELOPER COMMUNITY
  fetch_webpage → https://developercommunity.visualstudio.com/search?q={keywords}
  Returns: Similar reported issues, their status, vote counts

Step 3: SEARCH GITHUB
  GitHub MCP / fetch_webpage → github.com/dotnet/roslyn/issues?q={keywords}
  Returns: Related open/closed issues, linked PRs, labels

Step 4: CLASSIFY
  Agent analyzes the item content against known Roslyn areas:
    - Compiler (syntax, semantics, emit, lowering)
    - IDE (IntelliSense, refactoring, code fixes, formatting)
    - Analyzers (diagnostics, code style)
    - Language Server (LSP, VS Code integration)
    - Not Roslyn (transfer candidate)
  Returns: Suggested area classification + confidence

Step 5: FIND RESPONSIBLE CODE (if Roslyn-related)
  grep_search / semantic_search → local Roslyn workspace
  Returns: Relevant source files, classes, methods

Step 6: FIND RELATED CHANGES
  git log → search commit history for relevant file changes
  GitHub MCP → find PRs that modified those files
  Returns: Recent PRs, commit authors, change dates

Step 7: REPORT
  Agent compiles a structured intel report for the human reviewer
```

---

## Agent Capabilities

### What the Agent CAN Do

| Capability | Tool Used | Data Source |
|---|---|---|
| Read work item metadata (title, description, repro steps) | Azure DevOps MCP Server | Azure DevOps REST API |
| Search for similar Developer Community feedback | `fetch_webpage` | developercommunity.visualstudio.com |
| Search for related GitHub issues | GitHub MCP / `fetch_webpage` | github.com/dotnet/roslyn |
| Search for related GitHub PRs | GitHub MCP / `fetch_webpage` | github.com/dotnet/roslyn |
| Search Roslyn source code for relevant files | `grep_search`, `semantic_search` | Local workspace |
| Search git history for related commits | `run_in_terminal` (git log) | Local git repo |
| Classify feedback into Roslyn area vs. non-Roslyn | Agent reasoning | Work item content |
| Suggest area path for transfer (if not Roslyn) | Agent reasoning | Work item content + area mappings |
| Generate structured intel report | Agent output | All sources above |

### What the Agent CANNOT Do (By Design)

| Restriction | Reason |
|---|---|
| ❌ Update work items in Azure DevOps | Read-only PAT; human makes all updates |
| ❌ Download or analyze customer attachments | PII/compliance; see Data Protection section |
| ❌ Access ETL traces, dump files, screenshots | Customer diagnostic data; manual review only |
| ❌ Create or modify GitHub issues | Out of scope; human decision |
| ❌ Assign work items to individuals | Human decision |
| ❌ Close or resolve feedback items | Human decision |

---

## PII and Customer Data Protection

### Classification of Feedback Data

| Data Type | PII Risk | Agent Access | Handling |
|---|---|---|---|
| Work item title | Low (usually technical) | ✅ Yes | Passed to agent for search queries |
| Work item description | Low-Medium (may contain user text) | ✅ Yes | Passed to agent; agent instructed to ignore PII if present |
| Repro steps | Low (usually technical) | ✅ Yes | Passed to agent for code search |
| Area path, state, tags | None | ✅ Yes | Metadata only |
| Customer screenshots | **High** (may show user names, code, data) | ⛔ No | Manual review only; never downloaded by agent |
| ETL trace files | **High** (system traces, may include user activity) | ⛔ No | Analyze with PerfView/WPA on local machine only |
| Dump files | **Critical** (process memory — tokens, PII, secrets) | ⛔ No | Analyze with VS Debugger or dotnet-dump only |
| Customer name/email | **High** | ⛔ No | MCP server strips from API response before passing to agent |

### Protection Measures

1. **Read-Only PAT**: The Azure DevOps PAT has `Work Items → Read` scope only. No writes possible even if compromised.

2. **No Attachment Downloads**: The MCP server is designed to **never** call the attachments API endpoint. Customer files stay in Azure DevOps.

3. **Field Filtering**: The MCP server only returns these work item fields to the agent:
   - `System.Title`
   - `System.Description`
   - `Microsoft.VSTS.TCM.ReproSteps`
   - `System.AreaPath`
   - `System.State`
   - `System.Tags`
   - `System.WorkItemType`
   - `System.Id`
   - `System.CreatedDate`

4. **PII Scrubbing** (optional enhancement): The MCP server can strip email addresses and known PII patterns from description/repro steps before passing to the agent.

5. **Local Processing**: All code search happens against the local Roslyn workspace. No customer data leaves the developer machine via code search.

6. **PAT Storage**: The PAT is stored in VS Code's `settings.json` (user-level, not workspace-level) or in environment variables. It is **never** committed to the repository.

### Compliance Alignment

- Follows Microsoft Privacy & Data Handling policies for internal tooling
- Customer diagnostic data never sent to AI/LLM services
- Work item metadata (title, description) is considered low-risk for internal search purposes
- Agent outputs (intel reports) do not persist customer data — they are transient chat responses

---

## What Needs to Be Built

### Component 1: Azure DevOps MCP Server

**Purpose**: Lightweight bridge between VS Code Copilot Agent and Azure DevOps REST API.

**Technology**: Node.js (TypeScript) or Python — runs locally on developer machine.

**Endpoints / Tools to Implement**:

| MCP Tool Name | Azure DevOps API | Description |
|---|---|---|
| `query_work_items` | `POST /_apis/wit/wiql` | Run the saved query or a custom WIQL query to get untriaged item IDs |
| `get_work_item` | `GET /_apis/wit/workitems/{id}` | Get a single work item's metadata (filtered fields only) |
| `get_work_items_batch` | `POST /_apis/wit/workitemsbatch` | Get multiple work items in one call (for "triage first 10") |
| `search_work_items` | `POST /_apis/search/workitemsearchresults` | Full-text search across work items |

**Key Design Decisions**:
- All tools are **read-only** (GET/POST queries only, no PATCH)
- **Field allowlist** enforced server-side (only safe fields returned)
- **No attachment endpoints** exposed
- PAT passed via environment variable `AZDO_PAT`
- Organization and project hardcoded or configurable: `devdiv` / `DevDiv`

**File Structure**:

```
tools/triage-mcp-server/
├── package.json
├── tsconfig.json
├── src/
│   ├── index.ts              # MCP server entry point
│   ├── azdo-client.ts        # Azure DevOps REST API client
│   ├── tools/
│   │   ├── query-work-items.ts
│   │   ├── get-work-item.ts
│   │   └── get-work-items-batch.ts
│   ├── filters/
│   │   ├── field-allowlist.ts   # Only return safe fields
│   │   └── pii-scrubber.ts      # Strip emails, names from text
│   └── types.ts
├── README.md
└── .env.example              # Template for PAT configuration
```

**Estimated Implementation Size**: ~300-400 lines of TypeScript.

### Component 2: Triage Intel Agent Definition

**Purpose**: Defines the agent's behavior, tools, and workflow instructions.

**File**: `.github/agents/triage-intel.agent.md`

**Agent Instructions Include**:
- Step-by-step triage workflow (as described in Data Flow section)
- Area path classification rules (Roslyn vs. non-Roslyn heuristics)
- Roslyn sub-area mappings (Compiler, IDE, Analyzers, LSP, etc.)
- Output format template for intel reports
- PII handling instructions (ignore/redact if seen)

**Required Tools**:

```yaml
tools:
  - mcp_azdo_query_work_items     # Fetch untriaged items
  - mcp_azdo_get_work_item        # Get item details
  - mcp_azdo_get_work_items_batch # Batch fetch
  - mcp_gitkraken_*               # GitHub issue/PR search
  - fetch_webpage                  # Developer Community search
  - grep_search                    # Local code search
  - semantic_search                # Semantic code search
  - run_in_terminal                # Git log queries
```

### Component 3: VS Code Configuration

**MCP Server Registration** (in VS Code `settings.json` or `.vscode/mcp.json`):

```jsonc
{
  "mcp": {
    "servers": {
      "azdo-triage": {
        "type": "stdio",
        "command": "node",
        "args": ["tools/triage-mcp-server/dist/index.js"],
        "env": {
          "AZDO_PAT": "${env:AZDO_PAT}",
          "AZDO_ORG": "devdiv",
          "AZDO_PROJECT": "DevDiv"
        }
      }
    }
  }
}
```

---

## Usage Examples

### Triage a Single Item

```
User: @triage-intel triage feedback 12345

Agent:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📋 Feedback #12345: "IntelliSense crashes when typing in razor file"
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Classification: Roslyn IDE — Completion/IntelliSense
Confidence: High

Similar Developer Community Items:
  - DC#456789 (Fixed, VS 17.12) — "IntelliSense freeze in .razor"
  - DC#456123 (Active) — "Completion list not showing in Razor"

Related GitHub Issues:
  - dotnet/roslyn#67890 (Closed) — "CompletionService throws in Razor context"
  - dotnet/roslyn#68001 (Open) — "Razor completion provider null ref"

Relevant Code:
  - src/Features/CSharp/Portable/Completion/CSharpCompletionService.cs
  - src/Features/Core/Portable/Completion/CompletionService.cs

Recent Related PRs:
  - #71234 (merged 2026-02-15) — "Fix null check in completion provider"
    Author: @developer-name

Suggested Action:
  → Likely duplicate of dotnet/roslyn#68001
  → Area path: Roslyn-IDE\Completion
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### Triage a Batch

```
User: @triage-intel triage first 5 untriaged items

Agent:
[Fetches 5 items from saved query]
[Produces intel report for each item]
[Summarizes at end]:

Summary:
  #12345 — Roslyn IDE/Completion (likely dup of #68001)
  #12346 — NOT Roslyn → suggest transfer to VS-Platform\Editor
  #12347 — Roslyn Compiler/Emit (new issue, needs investigation)
  #12348 — Roslyn IDE/Formatting (similar to #65432, fixed in 17.13)
  #12349 — NOT Roslyn → suggest transfer to VS-Debugger
```

---

## Setup Steps (for Developer)

### Step 1: Generate Azure DevOps PAT

1. Go to `https://devdiv.visualstudio.com/_usersSettings/tokens`
2. Click **New Token**
3. Name: `triage-intel-readonly`
4. Organization: `devdiv`
5. Expiration: 90 days (or your org's max)
6. Scopes: **Custom defined** → check only **Work Items → Read**
7. Click **Create** and copy the token
8. Set environment variable:
   ```powershell
   # Add to your PowerShell profile or system environment variables
   $env:AZDO_PAT = "your-pat-here"
   ```

### Step 2: Build the MCP Server

```powershell
cd c:\Users\v-deerathore\dev\roslyn\tools\triage-mcp-server
npm install
npm run build
```

### Step 3: Register MCP Server in VS Code

Add to `.vscode/mcp.json` (workspace level) or user settings:

```jsonc
{
  "mcp": {
    "servers": {
      "azdo-triage": {
        "type": "stdio",
        "command": "node",
        "args": ["${workspaceFolder}/tools/triage-mcp-server/dist/index.js"],
        "env": {
          "AZDO_PAT": "${env:AZDO_PAT}",
          "AZDO_ORG": "devdiv",
          "AZDO_PROJECT": "DevDiv",
          "AZDO_QUERY_ID": "97f5b56b-d39b-4253-afe2-de4e64d8fec9"
        }
      }
    }
  }
}
```

### Step 4: Verify Setup

1. Open VS Code in the Roslyn workspace
2. Open Copilot Chat
3. Type: `@triage-intel triage feedback 12345` (use a real feedback ID)
4. Verify the agent fetches the item and produces an intel report

### Step 5: Iterate

- Adjust area path classification rules in the agent definition
- Add keyword → area mappings based on your triage experience
- Tune the search queries for better GitHub/DevComm matches

---

## Security Checklist

- [ ] PAT has **read-only** scope (Work Items → Read)
- [ ] PAT is stored in environment variable, **not** in any committed file
- [ ] `.env` files are in `.gitignore`
- [ ] MCP server does **not** expose attachment download endpoints
- [ ] MCP server applies field allowlist before returning data
- [ ] Agent instructions explicitly prohibit requesting customer files
- [ ] No customer diagnostic data (ETL, dumps, screenshots) is processed by the agent
- [ ] PAT expiration is set (90 days max) and renewal is calendared

---

## Future Enhancements (Optional)

| Enhancement | Effort | Value |
|---|---|---|
| Cache previous triage results to avoid duplicate research | Medium | Saves time on re-triaged items |
| Auto-detect stack traces in description and map to code | Low | Better code identification |
| Integration with VS Feedback system directly | High | Skip Azure DevOps query copy |
| Weekly summary report of triage patterns | Medium | Identify systemic issues |
| Keyword → area path ML classifier | High | Better auto-classification |
