/**
 * GitHub Models provider (https://models.github.ai).
 *
 * Auth: GITHUB_TOKEN (PAT with "models:read"; or any classic PAT for free tier).
 * Endpoint shape mirrors OpenAI chat completions.
 */

import fetch from "node-fetch";
import {
  ChatProvider, ChatRequest, ChatResult, ModelInfo, stripProvider,
} from "./provider";

const ENDPOINT = process.env.GITHUB_MODELS_ENDPOINT ?? "https://models.github.ai/inference";

interface CatalogEntry {
  modelId: string;     // bare id, e.g. "gpt-4o-mini"
  label: string;       // pretty label
  contextLen?: number;
  recommended?: boolean;
}

/**
 * Curated subset — keeps the dropdown short. Anything beyond this is still
 * usable by setting LLM_DEFAULT_MODEL to the bare id.
 */
const CATALOG: CatalogEntry[] = [
  { modelId: "openai/gpt-4o-mini",       label: "GPT-4o mini",        contextLen: 128_000, recommended: true },
  { modelId: "openai/gpt-4o",            label: "GPT-4o",             contextLen: 128_000 },
  { modelId: "openai/o3-mini",           label: "o3-mini (reasoning)", contextLen: 200_000 },
  { modelId: "openai/o1-mini",           label: "o1-mini (reasoning)", contextLen: 128_000 },
  { modelId: "meta/Meta-Llama-3.1-70B-Instruct", label: "Llama 3.1 70B", contextLen: 128_000 },
  { modelId: "meta/Meta-Llama-3.1-8B-Instruct",  label: "Llama 3.1 8B",  contextLen: 128_000 },
  { modelId: "mistral-ai/Mistral-large-2407",    label: "Mistral Large", contextLen: 128_000 },
  { modelId: "microsoft/Phi-3.5-mini-instruct",  label: "Phi-3.5 mini",  contextLen: 128_000 },
];

export class GithubModelsProvider implements ChatProvider {
  readonly id = "github-models";
  readonly label = "GitHub Models";

  async isReady(): Promise<{ ready: boolean; reason?: string }> {
    if (!process.env.GITHUB_TOKEN) {
      return { ready: false, reason: "set GITHUB_TOKEN (PAT with models:read)" };
    }
    return { ready: true };
  }

  async listModels(): Promise<ModelInfo[]> {
    const ready = await this.isReady();
    return CATALOG.map((c) => ({
      id: `${this.id}/${c.modelId}`,
      modelId: c.modelId,
      label: c.label,
      provider: this.id,
      providerLabel: this.label,
      ready: ready.ready,
      reason: ready.reason,
      contextLen: c.contextLen,
      recommended: c.recommended,
    }));
  }

  async chat(req: ChatRequest, onToken?: (delta: string) => void): Promise<ChatResult> {
    const token = process.env.GITHUB_TOKEN;
    if (!token) throw new Error("GITHUB_TOKEN is not set");

    const modelId = stripProvider(req.model);
    const url = `${ENDPOINT.replace(/\/+$/, "")}/chat/completions`;

    const body = {
      model: modelId,
      messages: req.messages,
      temperature: req.temperature ?? 0.2,
      max_tokens: req.maxTokens ?? 800,
      stream: !!onToken,
      ...(req.responseFormat === "json_object"
        ? { response_format: { type: "json_object" } }
        : {}),
    };

    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), req.timeoutMs ?? 60_000);

    try {
      const resp = await fetch(url, {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
          Accept: onToken ? "text/event-stream" : "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify(body),
        signal: controller.signal as unknown as AbortSignal,
      });

      if (!resp.ok) {
        const text = await resp.text();
        throw new Error(`GitHub Models ${resp.status}: ${text.slice(0, 400)}`);
      }

      if (!onToken) {
        const json = await resp.json() as {
          choices: Array<{ message: { content: string } }>;
          usage?: { prompt_tokens?: number; completion_tokens?: number };
        };
        const text = json.choices?.[0]?.message?.content ?? "";
        return {
          text,
          modelUsed: modelId,
          usage: json.usage ? {
            promptTokens: json.usage.prompt_tokens,
            completionTokens: json.usage.completion_tokens,
          } : undefined,
        };
      }

      // Streaming SSE — read the body line by line.
      let buffer = "";
      let collected = "";
      const decoder = new TextDecoder();
      const stream = resp.body as unknown as NodeJS.ReadableStream;
      for await (const chunk of stream) {
        buffer += typeof chunk === "string"
          ? chunk
          : decoder.decode(chunk as Buffer, { stream: true });

        let nlIdx: number;
        while ((nlIdx = buffer.indexOf("\n")) >= 0) {
          const rawLine = buffer.slice(0, nlIdx).replace(/\r$/, "");
          buffer = buffer.slice(nlIdx + 1);
          if (!rawLine.startsWith("data:")) continue;
          const payload = rawLine.slice(5).trim();
          if (!payload || payload === "[DONE]") continue;
          try {
            const json = JSON.parse(payload) as {
              choices?: Array<{ delta?: { content?: string } }>;
            };
            const delta = json.choices?.[0]?.delta?.content;
            if (delta) {
              collected += delta;
              onToken(delta);
            }
          } catch {
            /* ignore malformed chunks */
          }
        }
      }
      return { text: collected, modelUsed: modelId };
    } finally {
      clearTimeout(timeout);
    }
  }
}
