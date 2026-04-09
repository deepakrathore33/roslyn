import { AzdoClient } from "../azdo-client";
import { filterWorkItem } from "../filters/field-allowlist";

/**
 * MCP tool: query_work_items
 *
 * Runs the saved untriaged query or a custom WIQL query
 * and returns filtered work item data.
 */
export async function queryWorkItems(
  client: AzdoClient,
  args: { queryId?: string; wiql?: string; maxItems?: number }
): Promise<{ items: ReturnType<typeof filterWorkItem>[]; totalCount: number }> {
  let ids: number[];

  if (args.queryId) {
    ids = await client.runSavedQuery(args.queryId);
  } else if (args.wiql) {
    ids = await client.runWiql(args.wiql);
  } else {
    throw new Error("Either queryId or wiql must be provided");
  }

  const totalCount = ids.length;
  const limit = Math.min(args.maxItems ?? 10, 50); // Cap at 50
  const limitedIds = ids.slice(0, limit);

  if (limitedIds.length === 0) {
    return { items: [], totalCount: 0 };
  }

  const workItems = await client.getWorkItemsBatch(limitedIds);
  const filtered = workItems.map(filterWorkItem);

  return { items: filtered, totalCount };
}
