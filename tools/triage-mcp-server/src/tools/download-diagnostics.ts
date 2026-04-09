import * as path from "path";
import * as os from "os";
import { AzdoClient } from "../azdo-client";

/**
 * MCP tool: download_diagnostics
 *
 * Downloads ALL diagnostic attachments from a work item at once
 * (images, dumps, ETLs, logs). Returns a summary of downloaded files
 * with their local paths, grouped by type.
 */
export async function downloadDiagnostics(
  client: AzdoClient,
  args: { workItemId: number; types?: string[] }
): Promise<{
  workItemId: number;
  downloadDir: string;
  files: Array<{ name: string; localPath: string; type: string; size: number }>;
  summary: { images: number; dumps: number; etls: number; logs: number; other: number };
}> {
  if (!args.workItemId || args.workItemId <= 0) {
    throw new Error("A valid work item ID must be provided");
  }

  const { attachments } = await client.listAttachments(args.workItemId);

  if (attachments.length === 0) {
    return {
      workItemId: args.workItemId,
      downloadDir: "",
      files: [],
      summary: { images: 0, dumps: 0, etls: 0, logs: 0, other: 0 },
    };
  }

  // Filter by requested types if specified
  const typeFilter = args.types?.length
    ? new Set(args.types)
    : null;

  const toDownload = typeFilter
    ? attachments.filter((a) => typeFilter.has(a.type))
    : attachments;

  const downloadDir = path.join(
    os.tmpdir(),
    "azdo-triage",
    String(args.workItemId)
  );

  const files: Array<{ name: string; localPath: string; type: string; size: number }> = [];

  for (const attachment of toDownload) {
    try {
      const localPath = await client.downloadAttachment(
        attachment.url,
        downloadDir,
        attachment.name
      );
      files.push({
        name: attachment.name,
        localPath,
        type: attachment.type,
        size: attachment.size,
      });
    } catch (err) {
      // Log but continue with other files
      console.error(`Failed to download ${attachment.name}: ${err}`);
    }
  }

  const summary = {
    images: files.filter((f) => f.type === "image").length,
    dumps: files.filter((f) => f.type === "dump").length,
    etls: files.filter((f) => f.type === "etl").length,
    logs: files.filter((f) => f.type === "log").length,
    other: files.filter((f) => f.type === "other").length,
  };

  return { workItemId: args.workItemId, downloadDir, files, summary };
}
