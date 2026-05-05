/**
 * Lightweight tokenization + similarity utilities.
 * No NLP deps — purely heuristic, but good enough for triage triage-time
 * "is this a duplicate?" / "are these two issues related?" answers.
 */

const STOPWORDS = new Set([
  "the","a","an","and","or","but","if","then","else","when","while","for","to","of","in","on","at",
  "by","with","from","into","over","under","is","are","was","were","be","been","being","do","does",
  "did","done","has","have","had","this","that","these","those","it","its","i","we","you","they",
  "them","my","our","your","their","not","no","yes","so","as","also","any","some","all","each",
  "more","most","much","many","very","can","cant","cannot","could","would","should","may","might",
  "will","wont","just","get","got","getting","new","old","still","again","very","really","please",
  "issue","bug","problem","feedback","feature","request","question","crash","error","fail","failed",
  "vs","visual","studio","ide","cs","cpp","net","dotnet","work","working","works","seems","occurs",
  "happens","try","trying","tried","use","using","used","make","made","one","two","three","first",
  "second","third","last","next","previous","prev","also","like","want","need","fix","fixed",
  "regression","repro","steps","expected","actual",
]);

const TOKEN_REGEX = /[A-Za-z][A-Za-z0-9_]{2,}/g;

/** Lower-case, tokenize, drop stop words, dedupe. */
export function tokenize(text: string): string[] {
  if (!text) return [];
  const matches = text.toLowerCase().match(TOKEN_REGEX) ?? [];
  const out: string[] = [];
  const seen = new Set<string>();
  for (const t of matches) {
    if (STOPWORDS.has(t)) continue;
    if (t.length < 3 || t.length > 40) continue;
    if (seen.has(t)) continue;
    seen.add(t);
    out.push(t);
  }
  return out;
}

/** Jaccard similarity of two token sets — 0..1. */
export function similarity(aTokens: string[], bTokens: string[]): number {
  if (aTokens.length === 0 || bTokens.length === 0) return 0;
  const a = new Set(aTokens);
  const b = new Set(bTokens);
  let inter = 0;
  for (const t of a) if (b.has(t)) inter++;
  const union = a.size + b.size - inter;
  return union === 0 ? 0 : inter / union;
}

/**
 * Pick the most "distinctive" tokens for use as a search query.
 * Heuristic: prefer longer tokens and those containing camelCase / digits / dots.
 */
export function pickSearchTerms(tokens: string[], max = 6): string[] {
  const score = (t: string) => {
    let s = t.length;
    if (/\d/.test(t)) s += 3;
    if (/[A-Z]/.test(t)) s += 2;
    return s;
  };
  return [...tokens].sort((a, b) => score(b) - score(a)).slice(0, max);
}

/** Strip HTML and collapse whitespace. */
export function stripHtml(text: unknown): string {
  if (typeof text !== "string") return "";
  return text
    .replace(/<style[\s\S]*?<\/style>/gi, " ")
    .replace(/<script[\s\S]*?<\/script>/gi, " ")
    .replace(/<[^>]*>/g, " ")
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/\s+/g, " ")
    .trim();
}
