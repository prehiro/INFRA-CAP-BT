export interface Role {
  id: number
  name: string
  description: string
}

export interface User {
  id: number
  username: string
  email: string
  fullName: string
  isActive: boolean
  createdAt: string
  createdBy: string
  roles: Role[]
}

export interface LoginResponse {
  token: string
  expiresAt: string
  user: User
}

export interface FieldMeta {
  id: number
  name: string
  label: string
  type: string
  isRequired: boolean
  isUnique: boolean
  isSearchable: boolean
  isVisible: boolean
  sortOrder: number
  maxLength: number | null
  defaultValue: string | null
  optionsJson: string | null
  lookupEntityId: number | null
  lookupDisplayField: string | null
}

export interface EntityMeta {
  id: number
  name: string
  slug: string
  description: string
  kind: string
  displayField: string | null
  isSystem: boolean
  isActive: boolean
  sortOrder: number
  recordCount: number
  fields: FieldMeta[]
}

export interface RecordRow {
  id: number
  entityId: number
  entitySlug: string
  createdAt: string
  createdBy: string
  updatedAt: string | null
  updatedBy: string | null
  values: Record<string, any>
  display: string
}

export interface RecordPage {
  page: number
  pageSize: number
  total: number
  items: RecordRow[]
}

export interface AuditActor {
  username: string
  count: number
}

export interface AuditDto {
  id: number
  createdAt: string
  username: string
  userId: number | null
  action: string
  target: string
  targetId: string | null
  summary: string
  success: boolean
  ipAddress: string | null
}

/** Aggregates over the FILTERED set, so the cards describe what is on screen. */
export interface AuditStats {
  total: number
  logins: number
  failed: number
  deletes: number
  actors: AuditActor[]
}

export interface AuditPage {
  page: number
  pageSize: number
  total: number
  items: AuditDto[]
  stats: AuditStats
}

/** Host load reading behind the Dashboard's System Metrics panel. */
export interface MetricsSnapshot {
  cpu: number
  ram: number
  disk: number
  cores: number
  host: string
  os: string
  uptimeSeconds: number
  ramUsedBytes: number
  ramTotalBytes: number
  diskUsedBytes: number
  diskTotalBytes: number
  diskDrive: string
}
