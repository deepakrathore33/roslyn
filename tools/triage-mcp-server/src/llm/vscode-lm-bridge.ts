/**
 * VS Code Language Model bridge.
 *
 * The companion extension under `tools/triage-mcp-server/vscode-lm-bridge/`
 * starts a localhost HTTP server that proxies `vscode.lm.selectChatModels` +
 * `model.sendRequest` from inside the VS Code extension host.
 *
 * That gives this Node web server access to **the same Copilot models you see
 * in VS Code Chat** without any extra auth — Copilot's existing session is
 * piggy-backed on.
 *
 * Endpoint contract (default port 5174):
 *   GET  /models                          -> { models: [{id,label,vendor,family,version,maxInputTokens}] }
 *   POST /chat   { model, messages, ...} -> SSE: event:token data:{delta}
 *                                                event:done  data:{text,usage?}
 *                                                event:error data:{message}
 */

import fetch from "node-fetch";
import {
  ChatProvider, ChatRequest, ChatResult, ModelInfo, stripProvider,
} from "./provider";

const BRIDGE_URL = process.env.VSCODE_LM_BRIDGE_URL ?? "http://127.0.0.1:5174";
const BRIDGE_TIMEOUT_MS = 1500;

interface BridgeModel {
  id: string;
  label?: string;
  vendor?: string;
  family?: string;
  version?: string;
  maxInputTokens?: number;
}

export class VSCodeLmBridgeProvider implements ChatProvider {
  readonly id = "vscode-lm";
  readonly label = "VS Code (Copilot)";

  private cachedModels: BridgeModel[] | null = null;
  private cachedAt = 0;

  async isReady(): Promise<{ ready: boolean; reason?: string }> {
    try {
      const models = await this.fetchModels();
      if (models.length === 0) {
        return {
          ready: false,
          reason: "Bridge running but Copilot has no chat models available",
        };
      }
      return { ready: true };
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      return {
        ready: false,
        reason: `Bridge not reachable at ${BRIDGE_URL} — install & enable the "Roslyn Triage LM Bridge" extension. (${msg})`,
      };
    }
  }

  async listModels(): Promise<ModelInfo[]> {
    let bridgeModels: BridgeModel[];
    let ready = true;
    let reason: string | undefined;
    try {
      bridgeModels = await this.fetchModels();
    } catch (err) {
      bridgeModels = [];
      ready = false;
      reason = err instanceof Error ? err.message : String(err);
    }

    if (bridgeModels.length === 0) {
      // Show a single placeholder row so the UI can explain how to enable it.
      return [{
        id: `${this.id}/(unavailable)`,
        modelId: "(unavailable)",
        label: "Install Triage LM Bridge extension",
        provider: this.id,
        providerLabel: this.label,
        ready: false,
        reason: reason ?? "VS Code LM bridge not running",
      }];
    }

    return bridgeModels.map((m, idx) => ({
      id: `${this.id}/${m.id}`,
      modelId: m.id,
      label: m.label ?? `${m.vendor ?? ""} ${m.family ?? m.id}`.trim(),
      provider: this.id,
      providerLabel: this.label,
      ready,
      reason,
      contextLen: m.maxInputTokens,
      // Recommend the first GPT-4-class model if present.
      recommended: idx === 0,
    }));
  }

  async chat(req: ChatRequest, onToken?: (delta: string) => void): Promise<ChatResult> {
    const modelId = stripProvider(req.model);
    if (modelId === "(unavailable)") {
      throw new Error("VS Code LM bridge is not running. Install and enable the Triage LM Bridge extension.");
    }
    const url = `${BRIDGE_URL.replace(/\/+$/, "")}/chat`;
    const body = {
      model: modelId,
      messages: req.messages,
      temperature: req.temperature ?? 0.2,
      maxTokens: req.maxTokens ?? 800,
    };

    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), req.timeoutMs ?? 90_000);

    try {
      const resp = await fetch(url, {
        method: "POST",
        headers: {
          Accept: "text/event-stream",
          "Content-Type": "application/json",
        },
        body: JSON.stringify(body),
        signal: controller.signal as unknown as AbortSignal,
      });

      if (!resp.ok) {
        const text = await resp.text();
        throw new Error(`VS Code LM bridge ${resp.status}: ${text.slice(0, 400)}`);
      }

      let buffer = "";
      let collected = "";
      let usage: { promptTokens?: number; completionTokens?: number } | undefined;
      const decoder = new TextDecoder();
      const stream = resp.body as unknown as NodeJS.ReadableStream;

      let currentEvent = "message";
      for await (const chunk of stream) {
        buffer += typeof chunk === "string"
          ? chunk
          : decoder.decode(chunk as Buffer, { stream: true });

        let nlIdx: number;
        while ((nlIdx = buffer.indexOf("\n")) >= 0) {
          const rawLine = buffer.slice(0, nlIdx).replace(/\r$/, "");
          buffer = buffer.slice(nlIdx + 1);

          if (rawLine === "") {
            currentEvent = "message";
            continue;
          }
          if (rawLine.startsWith(":")) continue;
          if (rawLine.startsWith("event:")) {
            currentEvent = rawLine.slice(6).trim();
            continue;
          }
          if (!rawLine.startsWith("data:")) continue;
          const payload = rawLine.slice(5).trim();
          if (!payload) continue;

          try {
            const json = JSON.parse(payload);
            if (currentEvent === "token") {
              const delta = String(json.delta ?? "");
              if (delta) {
                collected += delta;
                onToken?.(delta);
              }
            } else if (currentEvent === "done") {
              if (typeof json.text === "string" && !collected) collected = json.text;
              if (json.usage) usage = {
                promptTokens: json.usage.promptTokens,
                completionTokens: json.usage.completionTokens,
              };
            } else if (currentEvent === "error") {
              throw new Error(String(json.message ?? "Bridge error"));
            }
          } catch (err) {
            if (currentEvent === "error") throw err;
            /* ignore malformed chunks */
          }
        }
      }

      return { text: collected, modelUsed: modelId, usage };
    } finally {
      clearTimeout(timeout);
    }
  }

  // ---- internals ----
  private async fetchModels(): Promise<BridgeModel[]> {
    // Cache for 30s — listing is cheap but the UI hits it on every model selector open.
    if (this.cachedModels && Date.now() - this.cachedAt < 30_000) {
      return this.cachedModels;
    }
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), BRIDGE_TIMEOUT_MS);
    try {
      const resp = await fetch(`${BRIDGE_URL.replace(/\/+$/, "")}/models`, {
        signal: controller.signal as unknown as AbortSignal,
      });
      if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
      const json = await resp.json() as { models?: BridgeModel[] };
      const models = json.models ?? [];
      this.cachedModels = models;
      this.cachedAt = Date.now();
      return models;
    } finally {
      clearTimeout(timeout);
    }
  }
}
