export interface User {
  id: string
  name: string
  email: string
  roles: string[]
  permissions: string[]
  workspaceId: string
}

export interface DeployAgentResponse {
  success: boolean
  output: string
  error: string | null
  deployedAt: string
}

export interface AuthResponse {
  user: User
  token: string
  refreshToken: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  name: string
  email: string
  password: string
}

export interface UserWithRoles {
  id: string
  name: string
  email: string
  roles: string[]
  permissions: string[]
  status: string
  createdAt: string
}

export interface Role {
  id: string
  name: string
  description: string | null
  isSystem: boolean
  createdAt?: string
}

export interface RoleDetail {
  id: string
  name: string
  description: string | null
  isSystem: boolean
  permissions: string[]
  createdAt: string
}

export interface Permission {
  id: string
  name: string
  category: string | null
  description: string | null
}

export interface CreateRoleRequest {
  name: string
  description: string | null
  permissionNames: string[]
}

export interface UpdateRoleRequest {
  name?: string | null
  description?: string | null
  permissionNames?: string[] | null
}

export interface CreateUserRequest {
  name: string
  email: string
  password: string
  roleIds?: string[]
}

export interface Repository {
  id: string
  owner: string
  name: string
  fullName: string
  defaultBranch: string
  visibility: string
  createdAt: string
}

export interface WorkflowRun {
  id: string
  workflowName: string
  githubRunId: number
  branch: string
  commitSha: string
  commitMessage: string
  actor: string
  eventType: string
  status: string
  conclusion: string
  startedAt: string | null
  completedAt: string | null
  durationSeconds: number | null
  htmlUrl: string
}

export interface WorkflowLog {
  id: string
  jobName: string
  stepName: string
  timestamp: string
  level: string
  message: string
}

export interface RepositoryStats {
  totalRuns: number
  successfulRuns: number
  failedRuns: number
  cancelledRuns: number
  averageDuration: number
  failureRate: number
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface Server {
  id: string
  hostname: string
  ipAddress: string
  operatingSystem: string
  agentVersion: string
  status: string // Healthy, Warning, Critical, Unknown
  lastHeartbeatAt: string | null
  createdAt: string
  agentBuildId?: string | null
  agentUpdateStatus?: string | null
}

export interface ServerHealth {
  status: string
  lastHeartbeatAt: string | null
  pm2ProcessCount: number
  alertCount: number
}

export interface PM2Process {
  id: string
  pm2Id: number
  name: string
  pid: number
  status: string // online, stopped, errored, launching
  uptimeSeconds: number
  restartCount: number
  cpuUsage: number
  memoryUsage: number
  executionMode: string
  nodeVersion: string
  createdAt: string
}

export interface PM2Log {
  id: string
  streamType: string
  timestamp: string
  level: string
  message: string
}

export interface SystemdService {
  id: string
  serverId: string
  name: string
  displayName: string | null
  loadState: string
  activeState: string
  subState: string
  description: string | null
  fragmentPath: string | null
  mainPid: number | null
  memoryCurrent: number | null
  cpuUsageNSec: number | null
  restartCount: number | null
  updatedAt: string
}

export interface SystemdLog {
  id: string
  timestamp: string
  priority: number | null
  level: string
  message: string
  cursor: string | null
  bootId: string | null
}

export interface AgentUpdateRelease {
  id: string
  version: string
  buildId: string
  channel: string
  gitSha: string | null
  isActive: boolean
  artifactSha256: string
  artifactSize: number
  createdAt: string
}

export interface AgentUpdateAssignment {
  id: string
  serverId: string
  releaseId: string
  version: string
  buildId: string
  status: string
  fromVersion: string | null
  fromBuildId: string | null
  errorMessage: string | null
  createdAt: string
  updatedAt: string
  completedAt: string | null
}

export interface ServerMetric {
  timestamp: string
  cpuUsagePercent: number
  memoryTotalBytes: number
  memoryUsedBytes: number
  memoryUsagePercent: number
  diskTotalBytes: number
  diskUsedBytes: number
  diskUsagePercent: number
  networkRxBytesPerSecond: number
  networkTxBytesPerSecond: number
  loadAverage1m: number
}

export const AlertSeverity = {
  Info: 'info',
  Warning: 'warning',
  Critical: 'critical',
} as const
export type AlertSeverity = (typeof AlertSeverity)[keyof typeof AlertSeverity]

export const AlertStatus = {
  Triggered: 'triggered',
  Acknowledged: 'acknowledged',
  Resolved: 'resolved',
  Muted: 'muted',
} as const
export type AlertStatus = (typeof AlertStatus)[keyof typeof AlertStatus]

export const AlertSourceType = {
  GitHubActions: 'GitHubActions',
  PM2: 'PM2',
  Server: 'Server',
} as const
export type AlertSourceType = (typeof AlertSourceType)[keyof typeof AlertSourceType]

export interface Alert {
  id: string
  sourceType: AlertSourceType
  sourceId: string
  title: string
  description: string
  severity: AlertSeverity
  status: AlertStatus
  triggeredAt: string
  acknowledgedAt: string | null
  resolvedAt: string | null
  assignedUserId: string | null
  ruleId: string | null
}

export interface AlertRule {
  id: string
  name: string
  sourceType: AlertSourceType
  conditionType: string
  threshold: number
  timeWindowSeconds: number
  severity: AlertSeverity
  isEnabled: boolean
  cooldownSeconds: number
}

export interface CreateAlertRuleRequest {
  name: string
  sourceType: AlertSourceType
  conditionType: string
  threshold: number
  timeWindowSeconds: number
  severity: AlertSeverity
  isEnabled: boolean
  cooldownSeconds: number
}

export interface AlertListRequest {
  status?: AlertStatus
  severity?: AlertSeverity
  sourceType?: AlertSourceType
  page?: number
  pageSize?: number
}

export interface AlertListResult {
  items: Alert[]
  total: number
  page: number
  pageSize: number
}

export interface TimelineEvent {
  id: string
  type: 'deployment' | 'restart' | 'error' | 'resource_spike' | 'alert_triggered' | 'alert_resolved'
  title: string
  description: string
  timestamp: string
  severity?: AlertSeverity
}

export interface LogEntry {
  id: string
  sourceType: string
  sourceName: string | null
  timestamp: string
  level: string
  message: string
  sourceId?: string | null
}

export interface LogSearchRequest {
  keyword?: string
  from?: string
  to?: string
  sourceType?: string
  severity?: string
  page?: number
  pageSize?: number
}

export interface LogSearchResult {
  items: LogEntry[]
  total: number
  page: number
  pageSize: number
}


export interface AgentInstallToken {
  id: string
  token: string
  serverName: string
  downloadUrl: string
  pageUrl: string
  plainPassword: string
  expiresAt: string
  usedAt: string | null
  revokedAt: string | null
  status: 'active' | 'used' | 'expired' | 'revoked'
}

export interface AgentInstallTokenList {
  id: string
  serverName: string
  createdAt: string
  expiresAt: string
  usedAt: string | null
  revokedAt: string | null
  status: 'active' | 'used' | 'expired' | 'revoked'
}
export interface MetricTimeSeries {
  timestamp: string
  value: number
}

export interface ServerMetricsSummary {
  cpuUsage: MetricTimeSeries[]
  memoryUsage: MetricTimeSeries[]
  diskUsage: MetricTimeSeries[]
  networkRx: MetricTimeSeries[]
  networkTx: MetricTimeSeries[]
  loadAverage: MetricTimeSeries[]
}

export interface GitHubWorkflow {
  id: string
  name: string
  repository: string
  status: 'success' | 'failure' | 'in_progress' | 'queued'
  branch: string
  commit: string
  startedAt: string
  duration: number
}
