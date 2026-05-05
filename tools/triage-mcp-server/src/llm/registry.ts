/**
 * Registry of LLM providers. Resolves a qualified model id ("provider/model")
 * to the right ChatProvider instance and forwards chat() / listModels().
 */

import {
  ChatProvider, ChatRequest, ChatResult, ModelInfo, ProviderStatus, providerFromId,
} from "./provider";
import { GithubModelsProvider } from "./github-models";
import { VSCodeLmBridgeProvider } from "./vscode-lm-bridge";

export class ProviderRegistry {
  private readonly byId = new Map<string, ChatProvider>();

  constructor(providers: ChatProvider[]) {
    for (const p of providers) this.byId.set(p.id, p);
  }

  static defaults(): ProviderRegistry {
    return new ProviderRegistry([
      new VSCodeLmBridgeProvider(),
      new GithubModelsProvider(),
    ]);
  }

  list(): ChatProvider[] { return [...this.byId.values()]; }

  get(providerId: string): ChatProvider | undefined { return this.byId.get(providerId); }

  /** Get all models from all providers (used by /api/models). */
  async statuses(): Promise<ProviderStatus[]> {
    const out: ProviderStatus[] = [];
    await Promise.all([...this.byId.values()].map(async (p) => {
      const ready = await p.isReady();
      let models: ModelInfo[] = [];
      try { models = await p.listModels(); } catch { /* leave empty */ }
      out.push({
        id: p.id,
        label: p.label,
        ready: ready.ready,
        reason: ready.reason,
        models,
      });
    }));
    // Stable order: bridge (Copilot) first, github-models second.
    out.sort((a, b) => {
      const order = (id: string) => id === "vscode-lm" ? 0 : id === "github-models" ? 1 : 2;
      return order(a.id) - order(b.id);
    });
    return out;
  }

  /** Dispatch a chat call to whichever provider owns the model id. */
  async chat(req: ChatRequest, onToken?: (delta: string) => void): Promise<ChatResult> {
    const pid = providerFromId(req.model);
    if (!pid) throw new Error(`Model id "${req.model}" is missing a provider prefix.`);
    const provider = this.byId.get(pid);
    if (!provider) throw new Error(`Unknown provider "${pid}".`);
    return provider.chat(req, onToken);
  }
}
