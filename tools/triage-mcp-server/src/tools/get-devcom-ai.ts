import * as fs from "fs";
import * as os from "os";
import * as path from "path";
import { AzdoClient } from "../azdo-client";
import { scrubPii } from "../filters/pii-scrubber";
import { DevComAiResult } from "../types";

const MAX_ANALYSIS_CHARS = 20_000;

interface SummaryResponse { text?: string | null; error?: string | null; date?: string }
interface LogAnalysisResponse {
  Text?: string | null;
  Error?: string | null;
  GeneratedAt?: string;
  Sources?: Array<{ FileName: string; ProcessorName?: string }>;
}
interface GuidanceResponse { nmi?: boolean; ai_ask_markdown?: string }
interface SimilarResponse {
  searchScoreF: number;
  title: string;
  state: string;
  intUrl?: string;
  tags?: string[];
}

const clean = (s: string | null | undefined): string | null =>
  s == null ? null : scrubPii(s);

/**
 * MCP tool: get_devcom_ai
 *
 * Returns the AI Summary, AI Log Analysis, AI guidance and similar issues shown on
 * the "Developer Community" work item form, plus the full text of the newest
 * vs-skill-analysis-*.md attachment (the "View full analysis" content).
 * Read-only; cached results only.
 */
export async function getDevComAi(
  client: AzdoClient,
  args: { id: number }
): Promise<{
  workItemId: number;
  devCommunity: DevComAiResult | null;
  fullAnalysis: { fileName: string; text: string; truncated: boolean } | null;
  notes: string[];
}> {
  if (!args.id || args.id <= 0) {
    throw new Error("A valid work item ID must be provided");
  }

  const notes: string[] = [];
  const item = await client.getWorkItem(args.id);
  const devComId = Number(item.fields["Microsoft.DevDiv.DeveloperCommunityId"]);
  const areaPath = String(item.fields["System.AreaPath"] ?? "");

  let devCommunity: DevComAiResult | null = null;
  if (Number.isFinite(devComId) && devComId > 0) {
    devCommunity = await fetchDevComAi(client, devComId, areaPath);
  } else {
    notes.push("Work item has no Developer Community ID; only the attachment analysis is available.");
  }

  const fullAnalysis = await fetchLatestSkillAnalysis(client, args.id, notes);
  return { workItemId: args.id, devCommunity, fullAnalysis, notes };
}

async function fetchDevComAi(client: AzdoClient, devComId: number, areaPath: string): Promise<DevComAiResult> {
  const result: DevComAiResult = { devComId, errors: [] };
  const area = encodeURIComponent(areaPath);

  const [summary, log, guidance, similar] = await Promise.allSettled([
    client.getDevComJson<SummaryResponse>(`/api/detailsV3/summary?id=${devComId}`),
    client.getDevComJson<LogAnalysisResponse>(
      `/api/detailsV3/logAnalysisSummary?rootPostId=${devComId}&areaPath=${area}`),
    client.getDevComJson<GuidanceResponse>(`/api/detailsV3/aiGuidanceForPost?id=${devComId}`),
    client.getDevComJson<SimilarResponse[]>(`/api/detailsV3/similarIssues?id=${devComId}&take=9`),
  ]);

  const fail = (name: string, r: PromiseRejectedResult) =>
    result.errors.push(`${name}: ${r.reason instanceof Error ? r.reason.message : String(r.reason)}`);

  if (summary.status === "fulfilled") {
    result.summary = { text: clean(summary.value.text), date: summary.value.date, error: summary.value.error };
  } else fail("summary", summary);

  if (log.status === "fulfilled") {
    result.logAnalysis = {
      text: clean(log.value.Text),
      generatedAt: log.value.GeneratedAt,
      sources: (log.value.Sources ?? []).map((s) => ({ fileName: s.FileName, processor: s.ProcessorName })),
      error: log.value.Error,
    };
  } else fail("logAnalysis", log);

  if (guidance.status === "fulfilled") {
    result.aiGuidance = {
      needsMoreInfo: guidance.value.nmi === true,
      askMarkdown: clean(guidance.value.ai_ask_markdown) ?? undefined,
    };
  } else fail("aiGuidance", guidance);

  if (similar.status === "fulfilled") {
    result.similarIssues = similar.value.map((s) => ({
      score: s.searchScoreF,
      title: scrubPii(s.title),
      state: s.state,
      azdoUrl: s.intUrl,
      tags: s.tags ?? [],
    }));
  } else fail("similarIssues", similar);

  return result;
}

async function fetchLatestSkillAnalysis(client: AzdoClient, id: number, notes: string[]) {
  const { attachments } = await client.listAttachments(id);
  // File names embed a sortable UTC timestamp, so the lexically greatest is the newest.
  const latest = attachments
    .filter((a) => /^vs-skill-analysis-.*\.md$/i.test(a.name))
    .sort((a, b) => b.name.localeCompare(a.name))[0];

  if (!latest) {
    notes.push("No vs-skill-analysis-*.md attachment found.");
    return null;
  }

  const dir = path.join(os.tmpdir(), "azdo-triage", String(id));
  const localPath = await client.downloadAttachment(latest.url, dir, latest.name);
  const raw = scrubPii(fs.readFileSync(localPath, "utf8"));
  const truncated = raw.length > MAX_ANALYSIS_CHARS;
  return {
    fileName: latest.name,
    text: truncated ? raw.slice(0, MAX_ANALYSIS_CHARS) : raw,
    truncated,
  };
}
