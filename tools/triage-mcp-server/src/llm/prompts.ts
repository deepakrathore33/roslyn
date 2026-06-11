/**
 * Canonical prompts for each LLM-augmented pipeline step.
 *
 * Kept short on purpose: every byte goes over the wire and counts toward
 * latency/cost. Outputs are constrained so heuristics can fall back cleanly.
 * 
 * fdjsfl
 */

export const SYSTEM_BASE = [
  "You are a triage assistant for the dotnet/roslyn repository.",
  "You help engineers quickly understand a single feedback ticket.",
  "Be concise, neutral, and never invent facts. If unsure, say so.",
  "Never include the user's name, email, machine name, or other PII.",
].join(" ");

export interface SummarizeInput {
  title: string;
  description: string;
}

export const SUMMARIZE_SYSTEM = [
  SYSTEM_BASE,
  "When given a feedback ticket, write ONE plain-English paragraph (max 3 sentences, ~80 words)",
  "describing what the user is reporting. Focus on observable behaviour, not their tone.",
  "Do not start with 'The user' — just state the issue.",
].join(" ");

export function summarizePrompt(input: SummarizeInput): string {
  return [
    `Title: ${input.title}`,
    "",
    "Description:",
    input.description.slice(0, 4000),
  ].join("\n");
}

export interface ClassifyInput {
  title: string;
  summary: string;
  rawAreaPath: string;
  tags: string;
}

export const CLASSIFY_SYSTEM = [
  SYSTEM_BASE,
  "Classify the ticket and respond with STRICT JSON only — no prose, no fences.",
  'Schema: {"area":"<short label>","isRoslyn":<bool>,"confidence":<0..1>,"suggestedTransfer":<string|null>,"reasons":[<string>]}',
  "isRoslyn=true ONLY if the ticket is owned by dotnet/roslyn (compiler, IDE features, language server, analyzers).",
  "If the issue clearly belongs to another team (Debugger, Project System, NuGet, Razor, F#, WPF/WinForms, VS Platform), return isRoslyn=false and suggestedTransfer=that team.",
  "area should be a short label like 'IDE / Completion' or 'Compiler / Nullable'.",
  "reasons: 1-4 short bullet phrases (each <80 chars) explaining your call.",
].join(" ");

export function classifyPrompt(input: ClassifyInput): string {
  return [
    `Title: ${input.title}`,
    `AreaPath: ${input.rawAreaPath || "(unknown)"}`,
    `Tags: ${input.tags || "(none)"}`,
    "",
    "Summary:",
    input.summary.slice(0, 1200),
  ].join("\n");
}

export interface RootCauseInput {
  title: string;
  summary: string;
  comments: Array<{ author: string; date: string; text: string }>;
}

export const ROOTCAUSE_SYSTEM = [
  SYSTEM_BASE,
  "Read the ticket comments and decide whether anyone has identified the cause or fix.",
  "Respond with STRICT JSON only — no prose, no fences.",
  'Schema: {"found":<bool>,"description":<string|null>,"prRefs":[<int>],"evidence":[<string>]}',
  "Set found=true ONLY if a comment explicitly identifies a regression, a fix PR, or a duplicate.",
  "description: ONE sentence (<140 chars) summarising the cause/fix in plain English.",
  "evidence: up to 3 short comment quotes (<200 chars each), prefixed with the author.",
  "prRefs: integer PR/issue numbers mentioned in the relevant comments.",
].join(" ");

export function rootcausePrompt(input: RootCauseInput): string {
  const trimmed = input.comments.slice(-12).map((c) => {
    const text = c.text.replace(/\s+/g, " ").slice(0, 800);
    return `- [${c.date} ${c.author}] ${text}`;
  });
  return [
    `Title: ${input.title}`,
    "",
    "Summary:",
    input.summary.slice(0, 800),
    "",
    "Comments (most recent last):",
    trimmed.join("\n"),
  ].join("\n");
}

export interface DuplicateRerankCandidate {
  number: number;
  title: string;
  state?: string;
  isPr?: boolean;
  url?: string;
}

export interface DuplicateRerankInput {
  title: string;
  summary: string;
  candidates: DuplicateRerankCandidate[];
}

export const DUPLICATE_RERANK_SYSTEM = [
  SYSTEM_BASE,
  "Given a feedback ticket and a list of candidate GitHub issues/PRs, rank candidates by likelihood of being a true duplicate.",
  "Respond with STRICT JSON only — no prose, no fences.",
  'Schema: {"ranked":[{"number":<int>,"similarity":<0..1>,"why":<string>}]}',
  "similarity: 0..1, where >=0.7 means very likely the same bug.",
  "why: ONE short phrase (<100 chars) explaining the relationship.",
  "Drop candidates with similarity below 0.15.",
].join(" ");

export function duplicateRerankPrompt(input: DuplicateRerankInput): string {
  const list = input.candidates.slice(0, 12).map((c) => {
    const tag = c.isPr ? "PR" : "issue";
    const state = c.state ? ` (${c.state})` : "";
    return `- #${c.number} [${tag}${state}]: ${c.title.slice(0, 200)}`;
  });
  return [
    `Ticket: ${input.title}`,
    "",
    "Summary:",
    input.summary.slice(0, 800),
    "",
    "Candidates:",
    list.join("\n"),
  ].join("\n");
}

/** Best-effort JSON extraction — handles models that wrap output in fences. */
export function tryParseJson<T = unknown>(raw: string): T | null {
  if (!raw) return null;
  let text = raw.trim();
  // Strip ```json ... ``` fences if any sneak through.
  const fence = text.match(/```(?:json)?\s*([\s\S]+?)\s*```/i);
  if (fence) text = fence[1].trim();
  // Find the first { ... } block.
  const start = text.indexOf("{");
  const end = text.lastIndexOf("}");
  if (start < 0 || end < start) return null;
  try {
    return JSON.parse(text.slice(start, end + 1)) as T;
  } catch {
    return null;
  }
}
