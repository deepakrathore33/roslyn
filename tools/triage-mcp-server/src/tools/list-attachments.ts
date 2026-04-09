import { AzdoClient } from "../azdo-client";

/**
 * MCP tool: list_attachments
 *
 * Lists all attachments on a work item, categorized by type
 * (image, dump, etl, log, other).
 */
export async function listAttachments(
  client: AzdoClient,
  args: { id: number }
): Promise<ReturnType<AzdoClient["listAttachments"]>> {
  if (!args.id || args.id <= 0) {
    throw new Error("A valid work item ID must be provided");
  }
  return client.listAttachments(args.id);
}
