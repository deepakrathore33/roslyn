import fetch from "node-fetch";

export interface GithubIssue {
  number: number;
  title: string;
  state: string;
  url: string;
  isPr: boolean;
  closedAt?: string | null;
  labels: string[];
}

/**
 * Search GitHub Issues + PRs in a repository. No auth required for
 * low-volume reads (rate-limited to 10 req/min unauthenticated).
 *
 * If GITHUB_TOKEN is set we'll use it to lift the rate limit.
 */
export async function searchGithub(
  repo: string,
  terms: string[],
  options: { kind?: "issue" | "pr"; max?: number; timeoutMs?: number } = {}
): Promise<GithubIssue[]> {
  const max = options.max ?? 5;
  const kindFilter = options.kind ? `is:${options.kind}` : "";
  // Use OR between terms — broader recall, then we re-rank locally by similarity.
  const q = encodeURIComponent(
    `repo:${repo} ${kindFilter} ${terms.slice(0, 5).map((t) => `"${t}"`).join(" OR ")}`.trim()
  );
  const url = `https://api.github.com/search/issues?q=${q}&per_page=${max}&sort=updated`;

  const headers: Record<string, string> = {
    Accept: "application/vnd.github+json",
    "User-Agent": "roslyn-triage-intel",
  };
  if (process.env.GITHUB_TOKEN) {
    headers.Authorization = `Bearer ${process.env.GITHUB_TOKEN}`;
  }

  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), options.timeoutMs ?? 10_000);
  let resp;
  try {
    resp = await fetch(url, { headers, signal: ctrl.signal as unknown as undefined });
  } finally {
    clearTimeout(timer);
  }

  if (!resp.ok) {
    throw new Error(`GitHub search failed (${resp.status}): ${await resp.text().catch(() => "")}`);
  }
  const data = (await resp.json()) as {
    items: Array<{
      number: number;
      title: string;
      state: string;
      html_url: string;
      pull_request?: unknown;
      closed_at?: string | null;
      labels?: Array<{ name: string }>;
    }>;
  };
  return (data.items ?? []).map((it) => ({
    number: it.number,
    title: it.title,
    state: it.state,
    url: it.html_url,
    isPr: !!it.pull_request,
    closedAt: it.closed_at ?? null,
    labels: (it.labels ?? []).map((l) => l.name),
  }));
}
