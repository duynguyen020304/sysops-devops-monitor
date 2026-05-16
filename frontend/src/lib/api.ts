import axios from 'axios'
import { useAuthStore } from '@/stores/auth'
import router from '@/router'
import type {
  Repository,
  WorkflowRun,
  WorkflowLog,
  RepositoryStats,
  PagedResult,
  Server,
  ServerHealth,
  ServerMetric,
  PM2Process,
  PM2Log,
  LogSearchRequest,
  LogSearchResult,
  ServerMetricsSummary,
  Alert,
  AlertRule,
  AlertListRequest,
  AlertListResult,
  CreateAlertRuleRequest,
  DeployAgentResponse,
  AgentInstallToken,
  AgentInstallTokenList,
} from '@/types'

const api = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
  },
})

api.interceptors.request.use((config) => {
  const authStore = useAuthStore()
  if (authStore.token) {
    config.headers.Authorization = `Bearer ${authStore.token}`
  }
  return config
})

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config
    const authStore = useAuthStore()

    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true

      if (authStore.refreshToken) {
        try {
          const { data } = await axios.post('/api/auth/refresh', {
            refreshToken: authStore.refreshToken,
          })
          authStore.token = data.token
          authStore.refreshToken = data.refreshToken
          localStorage.setItem('token', data.token)
          localStorage.setItem('refreshToken', data.refreshToken)
          originalRequest.headers.Authorization = `Bearer ${data.token}`
          return api(originalRequest)
        } catch {
          authStore.logout()
          router.push('/login')
        }
      } else {
        authStore.logout()
        router.push('/login')
      }
    }

    return Promise.reject(error)
  }
)

export const repositoriesApi = {
  list: () => api.get<Repository[]>('/repositories'),
  connect: (data: { githubToken: string; owner: string; name: string }) =>
    api.post<Repository>('/repositories', data),
  disconnect: (id: string) => api.delete(`/repositories/${id}`),
  getById: (id: string) => api.get<Repository>(`/repositories/${id}`),
  getWorkflows: (id: string, page = 1, pageSize = 20) =>
    api.get<PagedResult<WorkflowRun>>(`/repositories/${id}/workflows`, {
      params: { page, pageSize },
    }),
  getWorkflowDetail: (repoId: string, runId: string) =>
    api.get<WorkflowRun>(`/repositories/${repoId}/workflows/${runId}`),
  getWorkflowLogs: (repoId: string, runId: number) =>
    api.get<PagedResult<WorkflowLog>>(`/repositories/${repoId}/workflows/${runId}/logs`),
  getStats: (id: string) =>
    api.get<RepositoryStats>(`/repositories/${id}/stats`),
}

export const serversApi = {
  list: () => api.get<Server[]>('/servers'),
  getById: (id: string) => api.get<Server>(`/servers/${id}`),
  delete: (id: string) => api.delete(`/servers/${id}`),
  getMetrics: (id: string, from?: string, to?: string) =>
    api.get<ServerMetric[]>(`/servers/${id}/metrics`, { params: { from, to } }),
  getHealth: (id: string) => api.get<ServerHealth>(`/servers/${id}/health`),
  deployAgent: (id: string) =>
    api.post<DeployAgentResponse>(`/servers/${id}/deploy-agent`),
}

export const pm2Api = {
  listByServer: (serverId: string) =>
    api.get<PM2Process[]>(`/servers/${serverId}/pm2`),
  getById: (processId: string) => api.get<PM2Process>(`/pm2/${processId}`),
  getLogs: (processId: string, limit = 100) =>
    api.get<PM2Log[]>(`/pm2/${processId}/logs`, { params: { limit } }),
}

export const logsApi = {
  search: (params: LogSearchRequest) =>
    api.get<LogSearchResult>('/logs/search', { params }),
}

export const metricsApi = {
  getServerMetrics: (serverId: string, from?: string, to?: string) =>
    api.get<ServerMetricsSummary>(`/servers/${serverId}/metrics/summary`, { params: { from, to } }),
}

export const alertsApi = {
  list: (params: AlertListRequest) =>
    api.get<AlertListResult>('/alerts', { params }),
  getById: (id: string) =>
    api.get<Alert>(`/alerts/${id}`),
  acknowledge: (id: string) =>
    api.post<Alert>(`/alerts/${id}/acknowledge`),
  resolve: (id: string) =>
    api.post<Alert>(`/alerts/${id}/resolve`),
  mute: (id: string) =>
    api.post<Alert>(`/alerts/${id}/mute`),
}

export const alertRulesApi = {
  list: () =>
    api.get<AlertRule[]>('/alerts/rules'),
  create: (data: CreateAlertRuleRequest) =>
    api.post<AlertRule>('/alerts/rules', data),
  update: (id: string, data: Partial<CreateAlertRuleRequest>) =>
    api.put<AlertRule>(`/alerts/rules/${id}`, data),
  delete: (id: string) =>
    api.delete(`/alerts/rules/${id}`),
}

export const agentInstallApi = {
  listTokens: () => api.get<AgentInstallTokenList[]>('/agent-install/tokens'),
  generateToken: (data: { serverName: string }) =>
    api.post<AgentInstallToken>('/agent-install/tokens', data),
  revokeToken: (id: string) =>
    api.post(`/agent-install/tokens/${id}/revoke`),
}

export default api
