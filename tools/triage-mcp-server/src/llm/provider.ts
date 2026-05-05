/**
 * Pluggable LLM provider layer.
 *
 * Three backends are supported:
 *   1. github-models     — public GitHub Models API (uses GITHUB_TOKEN)
 *   2. vscode-lm-bridge  — proxies to a tiny VS Code extension that exposes
 *                          `vscode.lm.*` (the same Copilot models you see in chat)
 *   3. heuristic         — a sentinel "no LLM" provider (always available)
 *
 * Keys/secrets stay server-side. The browser only chooses a model id.
 */

export type ChatRole = "system" | "user" | "assistant";

export interface ChatMessage {
  role: ChatRole;
  content: string;
}

export interface ChatRequest {
  /** Provider-qualified model id, e.g. "github-models/gpt-4o-mini". */
  model: string;
  messages: ChatMessage[];
  temperature?: number;
  maxTokens?: number;
  /** Optional response_format hint — providers may ignore. */
  responseFormat?: "text" | "json_object";
  /** Cap end-to-end wall time for the call. */
  timeoutMs?: number;
}

export interface ChatUsage {
  promptTokens?: number;
  completionTokens?: number;
}

export interface ChatResult {
  text: string;
  usage?: ChatUsage;
  /** Raw model id the provider actually used (after any aliasing). */
  modelUsed: string;
}

export interface ModelInfo {
  /** Provider-qualified id used when calling chat() — e.g. "github-models/gpt-4o-mini". */
  id: string;
  /** Bare id within the provider, e.g. "gpt-4o-mini". */
  modelId: string;
  /** Human label shown in the UI dropdown. */
  label: string;
  /** Provider id, e.g. "github-models". */
  provider: string;
  /** Provider's pretty label, e.g. "GitHub Models". */
  providerLabel: string;
  /** True if this model can actually be used right now. */
  ready: boolean;
  /** When ready=false, why (one short line, shown as a tooltip). */
  reason?: string;
  /** Optional rough context-window length (informational). */
  contextLen?: number;
  /** True if the provider recommends this as the default. */
  recommended?: boolean;
}

export interface ProviderStatus {
  id: string;
  label: string;
  ready: boolean;
  reason?: string;
  models: ModelInfo[];
}

export interface ChatProvider {
  /** Stable provider id, also used as the namespace prefix in model ids. */
  readonly id: string;
  /** Friendly label. */
  readonly label: string;
  /** True if env / auth is configured for this provider to work. */
  isReady(): Promise<{ ready: boolean; reason?: string }>;
  /** List the models this provider exposes. */
  listModels(): Promise<ModelInfo[]>;
  /** Run a chat completion. */
  chat(req: ChatRequest, onToken?: (delta: string) => void): Promise<ChatResult>;
}

/** Strip the provider prefix from a model id ("github-models/gpt-4o" -> "gpt-4o"). */
export function stripProvider(modelId: string): string {
  const i = modelId.indexOf("/");
  return i < 0 ? modelId : modelId.slice(i + 1);
}

/** Get the provider id from a qualified model id ("github-models/gpt-4o" -> "github-models"). */
export function providerFromId(modelId: string): string | null {
  const i = modelId.indexOf("/");
  return i < 0 ? null : modelId.slice(0, i);
}
