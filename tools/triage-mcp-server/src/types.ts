export interface AzdoConfig {
  pat?: string;
  org: string;
  project: string;
  queryId?: string;
  /** If true, use `az cli` to get bearer tokens instead of PAT */
  useAzCli?: boolean;
}

export interface WorkItemReference {
  id: number;
  url: string;
}

export interface WorkItemField {
  [key: string]: unknown;
}

export interface WorkItem {
  id: number;
  fields: WorkItemField;
}

export interface WiqlResult {
  workItems: WorkItemReference[];
}

export interface WorkItemBatchRequest {
  ids: number[];
  fields: string[];
}

export interface WorkItemBatchResult {
  count: number;
  value: WorkItem[];
}

/** The safe fields we expose to the agent — no customer diagnostic data */
export const ALLOWED_FIELDS = [
  "System.Id",
  "System.Title",
  "System.Description",
  "System.State",
  "System.AreaPath",
  "System.Tags",
  "System.WorkItemType",
  "System.CreatedDate",
  "System.ChangedDate",
  "Microsoft.VSTS.TCM.ReproSteps",
  "Microsoft.VSTS.Common.Priority",
  "Microsoft.VSTS.Common.Severity",
  "Microsoft.DevDiv.DeveloperCommunityId",
  "Microsoft.DevDiv.DeveloperCommunityLink",
  "Microsoft.DevDiv.DeveloperCommunityComment",
  "Microsoft.DevDiv.TrackingId",
  "Microsoft.DevDiv.Source",
  "Microsoft.DevDiv.Product",
  "Microsoft.DevDiv.ProductVersion",
  "Microsoft.DevDiv.OS",
  "Microsoft.DevDiv.Votes",
  "Microsoft.DevDiv.Score",
] as const;

/** Comment from Azure DevOps Comments API */
export interface WorkItemComment {
  id: number;
  text: string;
  createdBy: { displayName: string };
  createdDate: string;
}

/** Attachment metadata returned by Azure DevOps API */
export interface AttachmentReference {
  id: string;
  url: string;
  name: string;
  size?: number;
}

/** Relation on a work item (attachments, links, etc.) */
export interface WorkItemRelation {
  rel: string;
  url: string;
  attributes: {
    name?: string;
    resourceSize?: number;
    [key: string]: unknown;
  };
}

/** Work item with relations (attachments) */
export interface WorkItemWithRelations extends WorkItem {
  relations?: WorkItemRelation[];
}

/** Supported diagnostic file types */
export type DiagnosticFileType = "image" | "dump" | "etl" | "log" | "other";

/** File extension to diagnostic type mapping */
export const FILE_TYPE_MAP: Record<string, DiagnosticFileType> = {
  ".png": "image",
  ".jpg": "image",
  ".jpeg": "image",
  ".gif": "image",
  ".bmp": "image",
  ".webp": "image",
  ".dmp": "dump",
  ".mdmp": "dump",
  ".hdmp": "dump",
  ".etl": "etl",
  ".log": "log",
  ".txt": "log",
  ".csv": "log",
  ".xml": "log",
  ".json": "log",
};
