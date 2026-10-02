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
