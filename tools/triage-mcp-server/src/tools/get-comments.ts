import { AzdoClient } from "../azdo-client";
import { scrubPii } from "../filters/pii-scrubber";

/**
 * MCP tool: get_comments
 *
 * Fetches all comments (internal discussion, user comments, AI summaries)
 * from a work item. PII is scrubbed from comment text.
 */
export async function getComments(
  client: AzdoClient,
  args: { id: number }
): Promise<{
  totalCount: number;
  comments: Array<{
    id: number;
    author: string;
    date: string;
    text: string;
  }>;
}> {
  if (!args.id || args.id <= 0) {
    throw new Error("A valid work item ID must be provided");
  }

  const result = await client.getComments(args.id);

  // Scrub PII from comment text
  return {
    totalCount: result.totalCount,
    comments: result.comments.map((c) => ({
      ...c,
      text: scrubPii(c.text) as string,
    })),
  };
}
