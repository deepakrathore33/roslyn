import { AzdoClient } from "../azdo-client";
import { filterWorkItem } from "../filters/field-allowlist";

/**
 * MCP tool: get_work_item
 *
 * Fetches a single work item by ID, filters to allowed fields only,
 * scrubs PII, and returns safe data.
 */
export async function getWorkItem(
  client: AzdoClient,
  args: { id: number }
): Promise<ReturnType<typeof filterWorkItem>> {
  if (!args.id || args.id <= 0) {
    throw new Error("A valid work item ID must be provided");
  }

  const workItem = await client.getWorkItem(args.id);
  return filterWorkItem(workItem);
}
