import { AzdoClient } from "../azdo-client";
import { filterWorkItem } from "../filters/field-allowlist";
import { scrubPii } from "../filters/pii-scrubber";
import { tokenize, similarity, pickSearchTerms, stripHtml } from "./text";
import { searchGithub, GithubIssue } from "./github";
import { findWorkspaceRoot, gitGrep, gitLogForFiles } from "./workspace";
import { ChatProvider } from "../llm/provider";
import {
  SUMMARIZE_SYSTEM, summarizePrompt,
  CLASSIFY_SYSTEM, classifyPrompt,
  ROOTCAUSE_SYSTEM, rootcausePrompt,
  DUPLICATE_RERANK_SYSTEM, duplicateRerankPrompt,
  tryParseJson,
} from "../llm/prompts";

// ---- Step / event types streamed over SSE ----

export type StepId =
  | "fetch" | "summarize" | "classify" | "duplicates"
  | "code" | "prs" | "rootcause";

export type StepStatus = "queued" | "running" | "done" | "skipped" | "error";

export interface StepEvent {
  step: StepId;
  status: StepStatus;
  /** Human-readable label for the UI. */
  label: string;
  /** Short detail to show under the step (e.g. "found 3 candidates"). */
  detail?: string;
  /** ms since pipeline start when this event was emitted. */
  elapsedMs?: number;
  /** "heuristic" for built-in code, "llm" when augmented by a model. */
  mode?: "heuristic" | "llm";
  /** Bare model id used for this step (when mode === "llm"). */
  model?: string;
  /** Token usage hint, when the provider returned one. */
  usage?: { promptTokens?: number; completionTokens?: number };
}

export interface TokenEvent {
  step: StepId;
  delta: string;
}

export interface DuplicateCandidate {
  source: "github" | "comments";
  number?: number;
  title: string;
  url?: string;
  state?: string;
  /** 0..1 */
  similarity: number;
  isPr?: boolean;
  closedAt?: string | null;
}

export interface CodePathHit { file: string; matchedTerms: string[]; }
export interface RelatedCommit {
  sha: string;
  subject: string;
  date: string;
  pr: number | null;
  prUrl?: string | null;
  file: string;
}

export interface ClassificationVerdict {
  isRoslyn: boolean;
  /** 0..1 */
  confidence: number;
  area: string;          // canonical short label, e.g. "IDE / Completion"
  rawAreaPath: string;
  reasons: string[];
  suggestedTransfer: string | null;
}

export interface RootCauseHit {
  description: string;
  evidence: string[];   // short comment snippets
  prRefs: number[];
}

export interface TriageReport {
  id: number;
  title: string;
  azdoLink: string;
  devCommunityLink: string | null;

  state: string;
  type: string;
  badges: string[];      // pre-formatted "Sev 2", "P1", "Votes ▲ 4" etc.
  summary: string;       // simple-language, scrubbed
  expectedActual?: { expected?: string; actual?: string };

  classification: ClassificationVerdict;
  duplicates: DuplicateCandidate[];
  codePaths: CodePathHit[];
  pullRequests: RelatedCommit[];
  rootCause: RootCauseHit | null;

  steps: StepEvent[];    // final status snapshot
}

// ---- Heuristics ----

function findExpectedActual(text: string): { expected?: string; actual?: string } | undefined {
  if (!text) return undefined;
  const lower = text.toLowerCase();
  const out: { expected?: string; actual?: string } = {};
  const pickAfter = (label: string): string | undefined => {
    const i = lower.indexOf(label);
    if (i < 0) return undefined;
    const tail = text.slice(i + label.length);
    const stop = tail.search(/(\n|expected[:\s]|actual[:\s]|repro|steps to reproduce|workaround)/i);
    const slice = stop > 0 ? tail.slice(0, stop) : tail.slice(0, 280);
    return slice.replace(/^[:\s\-]+/, "").trim().slice(0, 240);
  };
  out.expected = pickAfter("expected");
  out.actual = pickAfter("actual") ?? pickAfter("instead");
  if (!out.expected && !out.actual) return undefined;
  return out;
}

function summarize(rawDescription: string, title: string): string {
  const text = stripHtml(rawDescription || "");
  if (!text) return title || "(no description)";
  // Take first 2 substantive sentences.
  const sentences = text
    .split(/(?<=[.!?])\s+(?=[A-Z\[("'])/g)
    .map((s) => s.trim())
    .filter((s) => s.length > 12 && !/^(hi|hello|hey|thanks|please|thank you)\b/i.test(s));
  const picked = sentences.slice(0, 2).join(" ");
  const out = picked || text.slice(0, 280);
  return scrubPii(out.length > 360 ? out.slice(0, 357) + "…" : out) as string;
}

const AREA_KEYWORDS: Array<{ area: string; words: string[] }> = [
  { area: "IDE / Completion", words: ["intellisense","completion","autocomplete","completionlist","preselect"] },
  { area: "IDE / Refactoring", words: ["refactor","extract method","rename","code action","quick action"] },
  { area: "IDE / Formatting", words: ["format","indent","indentation","wrapping","spacing","newline","whitespace"] },
  { area: "IDE / Diagnostics", words: ["analyzer","diagnostic","ide0","unnecessary","code style","ide warning"] },
  { area: "IDE / Quick Info", words: ["quick info","tooltip","hover","signature help"] },
  { area: "IDE / Navigation", words: ["go to definition","find references","find all references","gtd"] },
  { area: "Compiler / Errors", words: ["cs0","cs1","cs2","compiler error","compilation error","emit"] },
  { area: "Compiler / Nullable", words: ["nullable","cs8","null reference","#nullable","oblivious"] },
  { area: "Compiler / Generics", words: ["generic","constraint","type inference","substitution"] },
  { area: "Compiler / Pattern Matching", words: ["pattern","switch expression","is pattern","property pattern"] },
  { area: "Compiler / LINQ", words: ["linq","iqueryable","expression tree"] },
  { area: "Language Server", words: ["lsp","language server","vs code","vscode"] },
  { area: "Razor", words: ["razor","blazor","cshtml","tag helper"] },
  { area: "Performance", words: ["slow","hang","unresponsive","performance","cpu","memory leak","latency"] },
  { area: "Crash", words: ["crash","fatal error","watson","exception","stackoverflow"] },
];

const TRANSFER_HINTS: Array<{ to: string; words: string[] }> = [
  { to: "VS-Debugger", words: ["debugger","breakpoint","watch window","step into","step over","f5"] },
  { to: "VS-Platform", words: ["solution explorer","dialog","menu","window","theme","toolbar"] },
  { to: "VS-ProjectSystem", words: ["msbuild","csproj","project file","sdk style","nuget restore","build target"] },
  { to: "NuGet", words: ["nuget","package manager","package restore"] },
  { to: "dotnet/razor", words: ["razor","blazor","cshtml"] },
  { to: "dotnet/fsharp", words: ["f#","fsharp","fsi"] },
  { to: "WPF/WinForms", words: ["xaml designer","winforms designer","wpf designer"] },
];

function classify(item: ReturnType<typeof filterWorkItem>, summaryText: string): ClassificationVerdict {
  const f = item.fields ?? {};
  const rawAreaPath = String(f["System.AreaPath"] ?? "");
  const tags = String(f["System.Tags"] ?? "");
  const blob = `${f["System.Title"] ?? ""} ${summaryText} ${tags}`.toLowerCase();
  const reasons: string[] = [];

  // Area path is the strongest signal.
  let area = "Unknown";
  if (rawAreaPath) {
    const segments = rawAreaPath.split("\\").filter(Boolean);
    area = segments.slice(-2).join(" / ") || segments[segments.length - 1] || "Unknown";
    reasons.push(`AreaPath: ${rawAreaPath}`);
  }

  // Keyword refinement
  for (const k of AREA_KEYWORDS) {
    if (k.words.some((w) => blob.includes(w))) {
      // If area path didn't already point us at the right place, override.
      if (area === "Unknown" || !rawAreaPath.toLowerCase().includes(k.area.split(" / ")[0].toLowerCase())) {
        area = k.area;
      }
      reasons.push(`keyword: ${k.words.find((w) => blob.includes(w))}`);
      break;
    }
  }

  const roslynHit = /\broslyn\b|\bcsc\b|\bvbc\b|\bcompiler\b|\bide\b|microsoft\.codeanalysis/i.test(blob)
    || rawAreaPath.toLowerCase().includes("roslyn")
    || rawAreaPath.toLowerCase().includes("vs editor\\compiler")
    || rawAreaPath.toLowerCase().includes("c# language");
  if (roslynHit) reasons.push("Roslyn-related keywords/area path");

  let suggestedTransfer: string | null = null;
  for (const t of TRANSFER_HINTS) {
    if (t.words.some((w) => blob.includes(w))) {
      suggestedTransfer = t.to;
      reasons.push(`transfer hint: ${t.to}`);
      break;
    }
  }

  // Confidence is rough: more reasons + clearer area => higher.
  let confidence = 0.3;
  if (rawAreaPath) confidence += 0.25;
  if (area !== "Unknown") confidence += 0.15;
  if (reasons.length >= 3) confidence += 0.15;
  if (roslynHit && !suggestedTransfer) confidence += 0.15;
  confidence = Math.min(0.99, confidence);

  return {
    isRoslyn: roslynHit && !suggestedTransfer,
    confidence,
    area,
    rawAreaPath,
    reasons: reasons.slice(0, 6),
    suggestedTransfer,
  };
}

const PR_REF_REGEX = /(?:#|pr[\s/_-]?|pull[\s/_-]?(?:request)?[\s/_-]?)(\d{4,6})/gi;
const ROSLYN_PR_REGEX = /(?:dotnet\/roslyn(?:\/pull)?|github\.com\/dotnet\/roslyn\/pull)\/(\d{3,6})/gi;
const FIX_PHRASES = [
  "fixed in", "fixed by", "fix is in", "this is fixed", "now fixed",
  "root cause", "caused by", "regression in", "regression from",
  "resolved by", "merged in", "checked in", "this is a duplicate of",
  "duplicate of", "tracked by",
];

function extractPrRefs(text: string): number[] {
  const out = new Set<number>();
  for (const re of [PR_REF_REGEX, ROSLYN_PR_REGEX]) {
    re.lastIndex = 0;
    let m: RegExpExecArray | null;
    while ((m = re.exec(text)) !== null) out.add(Number(m[1]));
  }
  return [...out].filter((n) => n >= 1000 && n <= 999999).slice(0, 8);
}

function extractRootCause(comments: Array<{ text: string; author: string; date: string }>): RootCauseHit | null {
  const evidence: string[] = [];
  const prRefs = new Set<number>();
  const blob: string[] = [];

  for (const c of comments) {
    const lower = (c.text || "").toLowerCase();
    if (FIX_PHRASES.some((p) => lower.includes(p))) {
      const snippet = c.text.replace(/\s+/g, " ").slice(0, 240).trim();
      evidence.push(`${c.author}: "${snippet}${snippet.length >= 240 ? "…" : ""}"`);
      blob.push(c.text);
      for (const n of extractPrRefs(c.text)) prRefs.add(n);
    }
  }

  if (evidence.length === 0) return null;

  // Pick the strongest phrase as a one-liner description.
  const text = blob.join(" ").toLowerCase();
  let description = "Possible resolution mentioned in comments";
  if (text.includes("regression"))           description = "Likely a regression flagged in comments";
  else if (text.includes("duplicate of"))    description = "Marked as a duplicate in comments";
  else if (text.includes("root cause"))      description = "Root cause discussed in comments";
  else if (text.includes("fixed in") || text.includes("fixed by") || text.includes("resolved by"))
                                             description = "Fix appears to have been merged";

  return { description, evidence: evidence.slice(0, 4), prRefs: [...prRefs] };
}

// ---- Pipeline orchestrator ----

export interface LlmConfig {
  provider: ChatProvider;
  /** Provider-qualified id, e.g. "github-models/gpt-4o-mini" or "vscode-lm/copilot-gpt-4o". */
  model: string;
  /** Pretty label to show in the step row (e.g. "GPT-4o mini"). */
  label: string;
  /** Per-step opt-in. If omitted, LLM is used for the default subset. */
  steps?: Partial<Record<StepId, boolean>>;
  temperature?: number;
  maxTokens?: number;
}

export interface PipelineOptions {
  client: AzdoClient;
  id: number;
  emit: (event: StepEvent) => void;
  /** Streamed LLM tokens (for steps running in llm mode). Optional. */
  emitToken?: (event: TokenEvent) => void;
  workspaceRoot?: string | null;
  llm?: LlmConfig;
}

const DEFAULT_LLM_STEPS: Record<StepId, boolean> = {
  fetch:      false,
  summarize:  true,
  classify:   true,
  duplicates: true,
  code:       false,
  prs:        false,
  rootcause:  true,
};

interface RunningStep { event: StepEvent; start: number; }

export class TriagePipeline {
  private readonly start = Date.now();
  private readonly steps = new Map<StepId, StepEvent>();
  constructor(private readonly opts: PipelineOptions) {}

  private now() { return Date.now() - this.start; }

  private setStatus(step: StepId, status: StepStatus, label: string, detail?: string, extra?: Partial<StepEvent>) {
    const prev = this.steps.get(step);
    const ev: StepEvent = {
      step, status, label, detail,
      elapsedMs: this.now(),
      mode: extra?.mode ?? prev?.mode,
      model: extra?.model ?? prev?.model,
      usage: extra?.usage ?? prev?.usage,
    };
    this.steps.set(step, ev);
    this.opts.emit(ev);
  }

  private startStep(step: StepId, label: string, extra?: Partial<StepEvent>): RunningStep {
    this.setStatus(step, "running", label, undefined, extra);
    return { event: this.steps.get(step)!, start: Date.now() };
  }

  private finishStep(step: StepId, label: string, detail?: string, extra?: Partial<StepEvent>) {
    this.setStatus(step, "done", label, detail, extra);
  }

  private skipStep(step: StepId, label: string, reason: string) {
    this.setStatus(step, "skipped", label, reason);
  }

  private errorStep(step: StepId, label: string, error: unknown) {
    const msg = error instanceof Error ? error.message : String(error);
    this.setStatus(step, "error", label, msg);
  }

  /** Will the LLM be used for this step? */
  private llmEnabled(step: StepId): boolean {
    if (!this.opts.llm) return false;
    const overrides = this.opts.llm.steps;
    if (overrides && Object.prototype.hasOwnProperty.call(overrides, step)) {
      return !!overrides[step];
    }
    return DEFAULT_LLM_STEPS[step];
  }

  /**
   * Call the configured LLM, emitting `token` events while it streams.
   * On any failure (timeout, network, malformed JSON) returns `null` so the
   * caller can fall back to heuristic.
   */
  private async runLlm(
    step: StepId,
    system: string,
    user: string,
    opts: { json?: boolean; maxTokens?: number } = {},
  ): Promise<{ text: string; usage?: { promptTokens?: number; completionTokens?: number } } | null> {
    if (!this.opts.llm) return null;
    const llm = this.opts.llm;
    try {
      const result = await llm.provider.chat(
        {
          model: llm.model,
          messages: [
            { role: "system", content: system },
            { role: "user", content: user },
          ],
          temperature: llm.temperature ?? 0.2,
          maxTokens: opts.maxTokens ?? llm.maxTokens ?? 800,
          responseFormat: opts.json ? "json_object" : "text",
        },
        (delta) => this.opts.emitToken?.({ step, delta }),
      );
      return { text: result.text, usage: result.usage };
    } catch (err) {
      // Surface as a non-fatal note; caller falls back to heuristic.
      const msg = err instanceof Error ? err.message : String(err);
      this.opts.emit({
        step, status: "running", label: this.steps.get(step)?.label ?? "",
        detail: `LLM failed (${msg.slice(0, 120)}) — falling back to heuristic`,
        elapsedMs: this.now(),
        mode: "llm", model: llm.model,
      });
      return null;
    }
  }

  async run(): Promise<TriageReport> {
    const id = this.opts.id;
    const client = this.opts.client;

    // Pre-declare all steps so the UI can render placeholders.
    (["fetch","summarize","classify","duplicates","code","prs","rootcause"] as StepId[])
      .forEach((s) => this.setStatus(s, "queued", labelFor(s)));

    // ---- 1. Fetch ----
    this.startStep("fetch", "Fetching work item from Azure DevOps");
    let full;
    let attachments: Array<{ name: string; type: string; size: number; url: string }> = [];
    try {
      const [f, a] = await Promise.all([
        client.getWorkItemFull(id),
        client.listAttachments(id).catch(() => ({ attachments: [] })),
      ]);
      full = f;
      attachments = a.attachments ?? [];
      this.finishStep("fetch", "Work item fetched",
        `Title: "${(full.item.fields["System.Title"] as string)?.slice(0, 80) || "?"}" · ${full.comments.totalCount} comment(s) · ${attachments.length} attachment(s)`);
    } catch (err) {
      this.errorStep("fetch", "Fetch failed", err);
      throw err;
    }

    const filtered = filterWorkItem(full.item);
    const f = filtered.fields;
    const title = (f["System.Title"] as string) ?? "(no title)";
    const rawDescription = (f["System.Description"] as string) ?? "";
    const comments = full.comments.comments.map((c) => ({
      ...c,
      text: scrubPii(c.text) as string,
    }));

    // ---- 2. Summarize ----
    const summarizeUsesLlm = this.llmEnabled("summarize");
    this.startStep(
      "summarize",
      summarizeUsesLlm ? `Summarising with ${this.opts.llm!.label}` : "Summarising description",
      summarizeUsesLlm ? { mode: "llm", model: this.opts.llm!.model } : { mode: "heuristic" },
    );

    let summary: string;
    let summaryUsage: { promptTokens?: number; completionTokens?: number } | undefined;
    if (summarizeUsesLlm) {
      const llmOut = await this.runLlm(
        "summarize",
        SUMMARIZE_SYSTEM,
        summarizePrompt({ title, description: scrubPii(stripHtml(rawDescription)) as string }),
        { maxTokens: 220 },
      );
      if (llmOut && llmOut.text.trim()) {
        summary = scrubPii(llmOut.text.trim()) as string;
        summaryUsage = llmOut.usage;
      } else {
        // Fallback to heuristic.
        summary = summarize(rawDescription, title);
      }
    } else {
      summary = summarize(rawDescription, title);
    }
    const expectedActual = findExpectedActual(stripHtml(rawDescription));
    this.finishStep(
      "summarize",
      "Summary ready",
      `${summary.length} chars${expectedActual ? " · expected/actual extracted" : ""}`,
      summaryUsage ? { usage: summaryUsage } : undefined,
    );

    // ---- 3. Classify ----
    const classifyUsesLlm = this.llmEnabled("classify");
    this.startStep(
      "classify",
      classifyUsesLlm ? `Classifying with ${this.opts.llm!.label}` : "Classifying area & Roslyn relevance",
      classifyUsesLlm ? { mode: "llm", model: this.opts.llm!.model } : { mode: "heuristic" },
    );

    let classification = classify(filtered, summary);
    let classifyUsage: { promptTokens?: number; completionTokens?: number } | undefined;
    if (classifyUsesLlm) {
      const llmOut = await this.runLlm(
        "classify",
        CLASSIFY_SYSTEM,
        classifyPrompt({
          title,
          summary,
          rawAreaPath: classification.rawAreaPath,
          tags: String(f["System.Tags"] ?? ""),
        }),
        { json: true, maxTokens: 400 },
      );
      if (llmOut) {
        classifyUsage = llmOut.usage;
        const parsed = tryParseJson<{
          area?: string; isRoslyn?: boolean; confidence?: number;
          suggestedTransfer?: string | null; reasons?: string[];
        }>(llmOut.text);
        if (parsed && typeof parsed.area === "string") {
          classification = {
            isRoslyn: !!parsed.isRoslyn,
            confidence: typeof parsed.confidence === "number"
              ? Math.max(0, Math.min(1, parsed.confidence))
              : classification.confidence,
            area: parsed.area || classification.area,
            rawAreaPath: classification.rawAreaPath,
            reasons: Array.isArray(parsed.reasons)
              ? parsed.reasons.filter((r) => typeof r === "string").slice(0, 6)
              : classification.reasons,
            suggestedTransfer: parsed.suggestedTransfer ?? null,
          };
        }
      }
    }
    this.finishStep(
      "classify",
      "Classification ready",
      `${classification.area} · ${classification.isRoslyn ? "Roslyn" : "NOT Roslyn"} (${Math.round(classification.confidence * 100)}%)${classification.suggestedTransfer ? ` · suggest transfer to ${classification.suggestedTransfer}` : ""}`,
      classifyUsage ? { usage: classifyUsage } : undefined,
    );

    // Extract search terms once, used by next several steps.
    const titleTokens = tokenize(title);
    const descTokens = tokenize(stripHtml(rawDescription).slice(0, 1000));
    const searchTerms = pickSearchTerms([...titleTokens, ...descTokens], 6);

    // ---- 4. Duplicates ----
    this.startStep("duplicates", "Searching for duplicates");
    const duplicates: DuplicateCandidate[] = [];

    // 4a — comments often link "duplicate of #NNN"
    for (const c of comments) {
      if (/duplicate\s+of/i.test(c.text)) {
        for (const n of extractPrRefs(c.text)) {
          duplicates.push({
            source: "comments",
            number: n,
            title: `Mentioned as duplicate in comment by ${c.author}`,
            url: `https://github.com/dotnet/roslyn/issues/${n}`,
            similarity: 0.9,
          });
        }
      }
    }

    // 4b — GitHub search
    let githubItems: GithubIssue[] = [];
    if (searchTerms.length > 0) {
      try {
        githubItems = await searchGithub("dotnet/roslyn", searchTerms, { kind: "issue", max: 8 });
        const refTokens = new Set([...titleTokens, ...descTokens]);
        for (const it of githubItems) {
          const sim = similarity([...refTokens], tokenize(it.title));
          if (sim < 0.08) continue;
          duplicates.push({
            source: "github",
            number: it.number,
            title: it.title,
            url: it.url,
            state: it.state,
            similarity: sim,
            isPr: it.isPr,
            closedAt: it.closedAt,
          });
        }
      } catch (err) {
        this.errorStep("duplicates", "Duplicate search partially failed", err);
      }
    }

    duplicates.sort((a, b) => b.similarity - a.similarity);
    let topDuplicates = duplicates.slice(0, 6);

    // 4c — optional LLM re-rank.
    const dupUsesLlm = this.llmEnabled("duplicates");
    let dupUsage: { promptTokens?: number; completionTokens?: number } | undefined;
    if (dupUsesLlm && topDuplicates.length > 1) {
      // Re-emit running so the UI can show the LLM badge before waiting on the model.
      this.opts.emit({
        step: "duplicates", status: "running",
        label: `Re-ranking duplicates with ${this.opts.llm!.label}`,
        elapsedMs: this.now(), mode: "llm", model: this.opts.llm!.model,
      });
      const llmOut = await this.runLlm(
        "duplicates",
        DUPLICATE_RERANK_SYSTEM,
        duplicateRerankPrompt({
          title, summary,
          candidates: topDuplicates
            .filter((d) => d.number != null)
            .map((d) => ({
              number: d.number!,
              title: d.title,
              state: d.state,
              isPr: d.isPr,
              url: d.url,
            })),
        }),
        { json: true, maxTokens: 600 },
      );
      if (llmOut) {
        dupUsage = llmOut.usage;
        const parsed = tryParseJson<{
          ranked?: Array<{ number: number; similarity: number; why?: string }>;
        }>(llmOut.text);
        if (parsed?.ranked && Array.isArray(parsed.ranked)) {
          const byNum = new Map(topDuplicates.filter((d) => d.number != null).map((d) => [d.number!, d]));
          const reranked: DuplicateCandidate[] = [];
          for (const r of parsed.ranked) {
            const orig = byNum.get(r.number);
            if (!orig) continue;
            reranked.push({
              ...orig,
              similarity: typeof r.similarity === "number"
                ? Math.max(0, Math.min(1, r.similarity))
                : orig.similarity,
              title: r.why ? `${orig.title}  —  ${r.why}` : orig.title,
            });
          }
          if (reranked.length > 0) {
            reranked.sort((a, b) => b.similarity - a.similarity);
            topDuplicates = reranked;
          }
        }
      }
    }

    if (this.steps.get("duplicates")!.status !== "error") {
      this.finishStep("duplicates", "Duplicate search complete",
        topDuplicates.length === 0
          ? "no strong matches"
          : `${topDuplicates.length} candidate(s); top match ${(topDuplicates[0].similarity * 100).toFixed(0)}%${dupUsesLlm ? " · re-ranked by LLM" : ""}`,
        dupUsage ? { usage: dupUsage } : undefined,
      );
    }

    // ---- 5. Code paths ----
    this.startStep("code", "Searching workspace for related code");
    const wsRoot = this.opts.workspaceRoot ?? findWorkspaceRoot();
    let codePaths: CodePathHit[] = [];
    if (!wsRoot) {
      this.skipStep("code", "Code search skipped", "Roslyn workspace root not found");
    } else if (!classification.isRoslyn) {
      this.skipStep("code", "Code search skipped", "classified as NOT Roslyn");
    } else {
      try {
        codePaths = await gitGrep(wsRoot, searchTerms, { maxFiles: 10 });
        this.finishStep("code", "Code search complete",
          codePaths.length === 0 ? "no matching files" : `${codePaths.length} file(s)`);
      } catch (err) {
        this.errorStep("code", "Code search failed", err);
      }
    }

    // ---- 6. Related PRs / commits ----
    this.startStep("prs", "Looking for related PRs & commits");
    const prRefs = new Set<number>();
    for (const c of comments) for (const n of extractPrRefs(c.text)) prRefs.add(n);
    githubItems.filter((g) => g.isPr).forEach((g) => prRefs.add(g.number));

    let relatedCommits: RelatedCommit[] = [];
    if (wsRoot && codePaths.length > 0) {
      try {
        const log = await gitLogForFiles(wsRoot, codePaths.map((c) => c.file), 8);
        relatedCommits = log.map((c) => ({
          ...c,
          prUrl: c.pr ? `https://github.com/dotnet/roslyn/pull/${c.pr}` : null,
        }));
        for (const c of relatedCommits) if (c.pr) prRefs.add(c.pr);
      } catch (err) {
        this.errorStep("prs", "git log failed", err);
      }
    }

    if (this.steps.get("prs")!.status !== "error") {
      this.finishStep("prs", "PR search complete",
        `${prRefs.size} referenced PR(s) · ${relatedCommits.length} recent commit(s) on related files`);
    }

    // ---- 7. Root cause ----
    const rcUsesLlm = this.llmEnabled("rootcause");
    this.startStep(
      "rootcause",
      rcUsesLlm ? `Looking for root cause with ${this.opts.llm!.label}` : "Looking for root cause / resolution hints",
      rcUsesLlm ? { mode: "llm", model: this.opts.llm!.model } : { mode: "heuristic" },
    );

    let rootCause = extractRootCause(comments);
    let rcUsage: { promptTokens?: number; completionTokens?: number } | undefined;
    if (rcUsesLlm && comments.length > 0) {
      const llmOut = await this.runLlm(
        "rootcause",
        ROOTCAUSE_SYSTEM,
        rootcausePrompt({ title, summary, comments }),
        { json: true, maxTokens: 500 },
      );
      if (llmOut) {
        rcUsage = llmOut.usage;
        const parsed = tryParseJson<{
          found?: boolean; description?: string | null;
          prRefs?: number[]; evidence?: string[];
        }>(llmOut.text);
        if (parsed && parsed.found && parsed.description) {
          rootCause = {
            description: parsed.description,
            evidence: Array.isArray(parsed.evidence)
              ? parsed.evidence.filter((e) => typeof e === "string").slice(0, 4).map((e) => scrubPii(e) as string)
              : (rootCause?.evidence ?? []),
            prRefs: Array.isArray(parsed.prRefs)
              ? parsed.prRefs.filter((n) => typeof n === "number" && n >= 100 && n <= 999999).slice(0, 8)
              : (rootCause?.prRefs ?? []),
          };
        } else if (parsed && parsed.found === false) {
          rootCause = null;
        }
      }
    }

    if (rootCause) {
      this.finishStep("rootcause", "Root cause hints found",
        `${rootCause.description}${rootCause.prRefs.length ? ` · PRs: ${rootCause.prRefs.map((n) => `#${n}`).join(", ")}` : ""}`,
        rcUsage ? { usage: rcUsage } : undefined,
      );
    } else {
      this.skipStep("rootcause", "No root cause yet", "no resolution-style language in comments");
    }

    // ---- Build final report ----
    const report: TriageReport = {
      id,
      title,
      azdoLink: full.azdoLink,
      devCommunityLink: full.devCommunityLink,
      state: String(f["System.State"] ?? ""),
      type: String(f["System.WorkItemType"] ?? ""),
      badges: buildBadges(filtered),
      summary,
      expectedActual,
      classification,
      duplicates: topDuplicates,
      codePaths,
      pullRequests: relatedCommits,
      rootCause,
      steps: [...this.steps.values()],
    };

    return report;
  }
}

function labelFor(step: StepId): string {
  switch (step) {
    case "fetch":      return "Fetch work item";
    case "summarize":  return "Summarise description";
    case "classify":   return "Classify area & Roslyn relevance";
    case "duplicates": return "Search for duplicates";
    case "code":       return "Find related code paths";
    case "prs":        return "Find related PRs / commits";
    case "rootcause":  return "Detect root-cause hints";
  }
}

function buildBadges(item: ReturnType<typeof filterWorkItem>): string[] {
  const f = item.fields ?? {};
  const out: string[] = [];
  if (f["System.WorkItemType"]) out.push(`type:${f["System.WorkItemType"]}`);
  if (f["System.State"])        out.push(`state:${f["System.State"]}`);
  if (f["Microsoft.VSTS.Common.Severity"]) out.push(`sev:${f["Microsoft.VSTS.Common.Severity"]}`);
  if (f["Microsoft.VSTS.Common.Priority"] != null && f["Microsoft.VSTS.Common.Priority"] !== "")
    out.push(`pri:${f["Microsoft.VSTS.Common.Priority"]}`);
  if (f["Microsoft.DevDiv.Source"])   out.push(`source:${f["Microsoft.DevDiv.Source"]}`);
  if (f["Microsoft.DevDiv.Product"])  out.push(`product:${f["Microsoft.DevDiv.Product"]}`);
  if (f["Microsoft.DevDiv.Votes"])    out.push(`votes:${f["Microsoft.DevDiv.Votes"]}`);
  if (f["System.AreaPath"])           out.push(`area:${String(f["System.AreaPath"]).split("\\").slice(-2).join(" / ")}`);
  return out;
}
