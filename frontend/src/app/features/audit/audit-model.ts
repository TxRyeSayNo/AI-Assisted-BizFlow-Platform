export interface AuditRow {
  auditLogId: string;
  actorType: 'USER' | 'SYSTEM' | 'AI_AGENT';
  actorId: string | null;
  action: string;
  objectType: string | null;
  objectId: string | null;
  before: unknown;
  after: unknown;
  metadata: unknown;
  createdAt: string;
}

export interface AuditPage {
  items: AuditRow[];
  page: number;
  pageSize: number;
  total: number;
}
