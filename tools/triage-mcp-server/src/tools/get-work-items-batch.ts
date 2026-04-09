import { AzdoClient } from "../azdo-client";
import { filterWorkItem } from "../filters/field-allowlist";

/**
 * MCP tool: get_work_items_batch
 *
 * Fetches multiple work items by ID in a single API call.
 * Filters all items to allowed fields and scrubs PII.
 */
export async function getWorkItemsBatch(
  client: AzdoClient,
  args: { ids: number[] }
): Promise<ReturnType<typeof filterWorkItem>[]> {
  if (!args.ids || args.ids.length === 0) {
    throw new Error("At least one work item ID must be provided");
  }

  // Cap at 50 items per request to the agent
  const limitedIds = args.ids.slice(0, 50);
  const workItems = await client.getWorkItemsBatch(limitedIds);
  return workItems.map(filterWorkItem);
}
