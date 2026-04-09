/**
 * Strips potential PII from work item text fields.
 * Removes email addresses and common PII patterns before
 * passing content to the AI agent.
 */
export function scrubPii(text: unknown): string {
  if (typeof text !== "string") return "";

  let result = text;

  // Remove email addresses
  result = result.replace(
    /[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}/g,
    "[email-removed]"
  );

  // Remove UNC paths that may contain usernames
  result = result.replace(
    /\\\\[a-zA-Z0-9._\-]+\\[a-zA-Z0-9._\-]+/g,
    "[path-removed]"
  );

  // Remove Windows user profile paths
  result = result.replace(
    /[Cc]:\\Users\\[^\s\\]+/g,
    "C:\\Users\\[user-removed]"
  );

  return result;
}

/**
 * Strips HTML tags from work item fields (description/repro steps
 * are often stored as HTML in Azure DevOps).
 */
export function stripHtml(text: unknown): string {
  if (typeof text !== "string") return "";
  return text
    .replace(/<[^>]*>/g, " ")
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/\s+/g, " ")
    .trim();
}
