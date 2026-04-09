import * as path from "path";
import * as os from "os";
import { AzdoClient } from "../azdo-client";

/**
 * MCP tool: download_attachment
 *
 * Downloads a specific attachment from a work item to a local temp directory.
 * Returns the local file path for the agent to use with view_image or
 * other analysis tools.
 */
export async function downloadAttachment(
  client: AzdoClient,
  args: { workItemId: number; fileName: string }
): Promise<{ localPath: string; fileName: string; type: string }> {
  if (!args.workItemId || args.workItemId <= 0) {
    throw new Error("A valid work item ID must be provided");
  }
  if (!args.fileName) {
    throw new Error("fileName must be provided");
  }

  // List attachments to find the URL for the requested file
  const { attachments } = await client.listAttachments(args.workItemId);
  const attachment = attachments.find((a) => a.name === args.fileName);

  if (!attachment) {
    const available = attachments.map((a) => a.name).join(", ");
    throw new Error(
      `Attachment "${args.fileName}" not found on work item ${args.workItemId}. Available: ${available}`
    );
  }

  // Download to a temp directory scoped by work item ID
  const downloadDir = path.join(
    os.tmpdir(),
    "azdo-triage",
    String(args.workItemId)
  );

  const localPath = await client.downloadAttachment(
    attachment.url,
    downloadDir,
    args.fileName
  );

  return {
    localPath,
    fileName: args.fileName,
    type: attachment.type,
  };
}
