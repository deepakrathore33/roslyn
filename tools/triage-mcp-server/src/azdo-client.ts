import fetch from "node-fetch";
import * as fs from "fs";
import * as path from "path";
import {
  AzdoConfig, ALLOWED_FIELDS, WiqlResult, WorkItem,
  WorkItemBatchResult, WorkItemWithRelations, WorkItemRelation,
  FILE_TYPE_MAP, DiagnosticFileType, WorkItemComment,
} from "./types";

/**
 * Lightweight Azure DevOps REST API client.
 * Read-only — only queries and fetches work items.
 */
export class AzdoClient {
  private readonly baseUrl: string;
  private readonly authHeader: string;

  constructor(private readonly config: AzdoConfig) {
    this.baseUrl = `https://dev.azure.com/${config.org}/${config.project}`;
    const token = Buffer.from(`:${config.pat}`).toString("base64");
    this.authHeader = `Basic ${token}`;
  }

  private async request<T>(url: string, options: { method?: string; body?: unknown } = {}): Promise<T> {
    const resp = await fetch(url, {
      method: options.method ?? "GET",
      headers: {
        Authorization: this.authHeader,
        "Content-Type": "application/json",
        Accept: "application/json",
      },
      body: options.body ? JSON.stringify(options.body) : undefined,
    });

    if (!resp.ok) {
      const text = await resp.text();
      throw new Error(`Azure DevOps API error ${resp.status}: ${text}`);
    }
    return resp.json() as Promise<T>;
  }

  /**
   * Run a saved query by ID and return work item references.
   */
  async runSavedQuery(queryId: string): Promise<number[]> {
    const result = await this.request<WiqlResult>(
      `${this.baseUrl}/_apis/wit/wiql/${queryId}?api-version=7.1`
    );
    return result.workItems.map((wi) => wi.id);
  }

  /**
   * Run an ad-hoc WIQL query and return work item IDs.
   */
  async runWiql(query: string): Promise<number[]> {
    const result = await this.request<WiqlResult>(
      `${this.baseUrl}/_apis/wit/wiql?api-version=7.1`,
      { method: "POST", body: { query } }
    );
    return result.workItems.map((wi) => wi.id);
  }

  /**
   * Get a single work item by ID with only allowed fields.
   */
  async getWorkItem(id: number): Promise<WorkItem> {
    const fields = ALLOWED_FIELDS.join(",");
    const result = await this.request<WorkItem>(
      `${this.baseUrl}/_apis/wit/workitems/${id}?fields=${fields}&api-version=7.1`
    );
    return result;
  }

  /**
   * Get multiple work items in a single batch call.
   * Azure DevOps supports up to 200 IDs per batch.
   */
  async getWorkItemsBatch(ids: number[]): Promise<WorkItem[]> {
    if (ids.length === 0) return [];

    // Enforce max batch size
    const batchIds = ids.slice(0, 200);

    const result = await this.request<WorkItemBatchResult>(
      `${this.baseUrl}/_apis/wit/workitemsbatch?api-version=7.1`,
      {
        method: "POST",
        body: {
          ids: batchIds,
          fields: [...ALLOWED_FIELDS],
        },
      }
    );
    return result.value;
  }

  /**
   * Get a work item with its relations (attachments, links).
   * Note: Azure DevOps API does not allow using `fields` and `$expand` together,
   * so we only use `$expand=relations` here and accept all fields in the response.
   */
  async getWorkItemWithRelations(id: number): Promise<WorkItemWithRelations> {
    const result = await this.request<WorkItemWithRelations>(
      `${this.baseUrl}/_apis/wit/workitems/${id}?$expand=relations&api-version=7.1`
    );
    return result;
  }

  /**
   * List all attachments on a work item, categorized by file type.
   */
  async listAttachments(id: number): Promise<{
    attachments: Array<{
      name: string;
      url: string;
      size: number;
      type: DiagnosticFileType;
    }>;
  }> {
    const item = await this.getWorkItemWithRelations(id);
    const relations = item.relations ?? [];

    const attachments = relations
      .filter((r: WorkItemRelation) => r.rel === "AttachedFile")
      .map((r: WorkItemRelation) => {
        const name = r.attributes.name ?? "unknown";
        const ext = path.extname(name).toLowerCase();
        const type: DiagnosticFileType = FILE_TYPE_MAP[ext] ?? "other";
        return {
          name,
          url: r.url,
          size: (r.attributes.resourceSize as number) ?? 0,
          type,
        };
      });

    return { attachments };
  }

  /**
   * Download an attachment by URL and save to local disk.
   * Returns the local file path.
   *
   * The attachment URL from relations is a resource URL like:
   *   https://dev.azure.com/{org}/{guid}/_apis/wit/attachments/{guid}
   * We need to append api-version and use the fileName query param.
   */
  async downloadAttachment(attachmentUrl: string, downloadDir: string, fileName: string): Promise<string> {
    if (!fs.existsSync(downloadDir)) {
      fs.mkdirSync(downloadDir, { recursive: true });
    }

    const filePath = path.join(downloadDir, fileName);

    // Ensure the URL has the api-version parameter
    const separator = attachmentUrl.includes("?") ? "&" : "?";
    const downloadUrl = `${attachmentUrl}${separator}api-version=7.1&fileName=${encodeURIComponent(fileName)}`;

    const resp = await fetch(downloadUrl, {
      headers: {
        Authorization: this.authHeader,
        Accept: "application/octet-stream",
      },
      redirect: "follow",
    });

    if (!resp.ok) {
      const errorText = await resp.text().catch(() => "");
      throw new Error(`Download failed ${resp.status}: ${resp.statusText}. ${errorText}`);
    }

    const buffer = await resp.buffer();
    fs.writeFileSync(filePath, buffer);
    return filePath;
  }

  /**
   * Get all comments on a work item.
   * Returns internal discussion comments, user comments, and AI summaries.
   */
  async getComments(id: number): Promise<{
    totalCount: number;
    comments: Array<{
      id: number;
      author: string;
      date: string;
      text: string;
    }>;
  }> {
    const result = await this.request<{
      totalCount: number;
      comments: WorkItemComment[];
    }>(
      `${this.baseUrl}/_apis/wit/workitems/${id}/comments?api-version=7.1-preview.4&$top=100`
    );

    const comments = result.comments.map((c) => ({
      id: c.id,
      author: c.createdBy.displayName,
      date: c.createdDate,
      // Strip HTML tags and clean up whitespace
      text: c.text
        .replace(/<[^>]*>/g, " ")
        .replace(/&nbsp;/g, " ")
        .replace(/&amp;/g, "&")
        .replace(/&lt;/g, "<")
        .replace(/&gt;/g, ">")
        .replace(/\s+/g, " ")
        .trim(),
    }));

    return { totalCount: result.totalCount, comments };
  }

  /**
   * Get a work item with ALL available data for triage:
   * metadata fields, comments, and developer community info.
   */
  async getWorkItemFull(id: number): Promise<{
    item: WorkItem;
    comments: ReturnType<AzdoClient["getComments"]> extends Promise<infer T> ? T : never;
    devCommunityLink: string | null;
    azdoLink: string;
  }> {
    const [item, comments] = await Promise.all([
      this.getWorkItem(id),
      this.getComments(id),
    ]);

    const devComLink = (item.fields["Microsoft.DevDiv.DeveloperCommunityLink"] as string) ?? null;
    const azdoLink = `https://devdiv.visualstudio.com/DevDiv/_workitems/edit/${id}`;

    return { item, comments, devCommunityLink: devComLink, azdoLink };
  }
}
