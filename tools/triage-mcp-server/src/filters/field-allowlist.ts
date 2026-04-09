import { ALLOWED_FIELDS, WorkItem, WorkItemField } from "../types";
import { scrubPii, stripHtml } from "./pii-scrubber";

/** Text fields that should be PII-scrubbed and HTML-stripped */
const TEXT_FIELDS = new Set([
  "System.Title",
  "System.Description",
  "Microsoft.VSTS.TCM.ReproSteps",
]);

/**
 * Filters a work item to only include allowed fields,
 * strips HTML, and scrubs PII from text fields.
 */
export function filterWorkItem(item: WorkItem): WorkItem {
  const filtered: WorkItemField = {};
  const allowedSet = new Set<string>(ALLOWED_FIELDS);

  for (const [key, value] of Object.entries(item.fields)) {
    if (!allowedSet.has(key)) continue;

    if (TEXT_FIELDS.has(key)) {
      filtered[key] = scrubPii(stripHtml(value));
    } else {
      filtered[key] = value;
    }
  }

  return { id: item.id, fields: filtered };
}
