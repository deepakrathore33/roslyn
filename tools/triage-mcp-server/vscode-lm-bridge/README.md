# Roslyn Triage LM Bridge

A tiny VS Code extension that exposes `vscode.lm.*` (the Copilot chat models you
already see in VS Code Chat) over **localhost HTTP** so the [Roslyn Triage
Intel](../README.md) Node web server can use them.

## Why this exists

`vscode.lm` is only callable from inside a VS Code extension host. The triage
UI runs as a plain Node web server, so this bridge gives it a path to use the
same Copilot models you already pay for — with **no extra auth**, since
Copilot's existing session is piggy-backed on.

## Endpoints (default port 5174, 127.0.0.1 only)

| Method | Path      | Returns |
|--------|-----------|---------|
| GET    | `/`       | A short HTML status page |
| GET    | `/health` | `{ ok: true, models: <count> }` |
| GET    | `/models` | `{ models: [{ id, label, vendor, family, version, maxInputTokens }] }` |
| POST   | `/chat`   | SSE stream — `event: token` deltas + final `event: done` (or `event: error`) |

## Build

```powershell
cd tools/triage-mcp-server/vscode-lm-bridge
npm install
npm run build
```

## Run inside VS Code (development)

1. Open `tools/triage-mcp-server/vscode-lm-bridge` as a folder in VS Code.
2. Press <kbd>F5</kbd> ("Run Extension") — a new Extension Development Host
   window opens with the bridge active.
3. The status bar shows `📡 Triage LM (N)` where N is the model count.
4. Open the triage UI at <http://localhost:5173>; the **VS Code (Copilot)**
   provider section in the model dropdown should now light up.

## Settings

| Setting                              | Default | Notes |
|--------------------------------------|---------|-------|
| `roslynTriageBridge.port`            | `5174`  | Localhost port |
| `roslynTriageBridge.autoStart`       | `true`  | Start automatically on VS Code launch |

## Commands

* `Triage LM Bridge: Start`
* `Triage LM Bridge: Stop`
* `Triage LM Bridge: Show Status`

## Security

* Listens on `127.0.0.1` only — never exposed off-host.
* Allows browser CORS only from `http://localhost:*` / `http://127.0.0.1:*`.
* Does not persist or log prompts.
