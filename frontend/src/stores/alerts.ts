import { defineStore } from 'pinia'
import { ref, reactive } from 'vue'
import { alertsApi, alertRulesApi } from '@/lib/api'
import type {
  Alert,
  AlertRule,
  AlertListRequest,
  AlertListResult,
  CreateAlertRuleRequest,
} from '@/types'

export const useAlertsStore = defineStore('alerts', () => {
  const alerts = ref<AlertListResult>({
    items: [],
    total: 0,
    page: 1,
    pageSize: 20,
  })
  const alertRules = ref<AlertRule[]>([])
  const selectedAlert = ref<Alert | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const filters = reactive<AlertListRequest>({
    status: undefined,
    severity: undefined,
    sourceType: undefined,
    page: 1,
    pageSize: 20,
  })

  async function fetchAlerts() {
    loading.value = true
    error.value = null
    try {
      const params: AlertListRequest = { ...filters }
      if (!params.status) delete params.status
      if (!params.severity) delete params.severity
      if (!params.sourceType) delete params.sourceType
      const { data } = await alertsApi.list(params)
      alerts.value = data
    } catch (err: unknown) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch alerts'
    } finally {
      loading.value = false
    }
  }

  async function fetchAlertRules() {
    loading.value = true
    error.value = null
    try {
      const { data } = await alertRulesApi.list()
      alertRules.value = data
    } catch (err: unknown) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch alert rules'
    } finally {
      loading.value = false
    }
  }

  async function acknowledgeAlert(id: string) {
    error.value = null
    try {
      const { data } = await alertsApi.acknowledge(id)
      const idx = alerts.value.items.findIndex((a) => a.id === id)
      if (idx !== -1) alerts.value.items[idx] = data
      if (selectedAlert.value?.id === id) selectedAlert.value = data
    } catch (err: unknown) {
      error.value = err instanceof Error ? err.message : 'Failed to acknowledge alert'
      throw err
    }
  }

  async function resolveAlert(id: string) {
    error.value = null
    try {
      const { data } = await alertsApi.resolve(id)
      const idx = alerts.value.items.findIndex((a) => a.id === id)
      if (idx !== -1) alerts.value.items[idx] = data
      if (selectedAlert.value?.id === id) selectedAlert.value = data
    } catch (err: unknown) {
      error.value = err instanceof Error ? err.message : 'Failed to resolve alert'
      throw err
    }
  }

  async function muteAlert(id: string) {
    error.value = null
    try {
      const { data } = await alertsApi.mute(id)
      const idx = alerts.value.items.findIndex((a) => a.id === id)
      if (idx !== -1) alerts.value.items[idx] = data
      if (selectedAlert.value?.id === id) selectedAlert.value = data
    } catch (err: unknown) {
      error.value = err instanceof Error ? err.message : 'Failed to mute alert'
      throw err
    }
  }

  async function createRule(data: CreateAlertRuleRequest) {
    error.value = null
    try {
      const { data: rule } = await alertRulesApi.create(data)
      alertRules.value.push(rule)
      return rule
    } catch (err: unknown) {
      error.value = err instanceof Error ? err.message : 'Failed to create rule'
      throw err
    }
  }

  async function updateRule(id: string, data: Partial<CreateAlertRuleRequest>) {
    error.value = null
    try {
      const { data: rule } = await alertRulesApi.update(id, data)
      const idx = alertRules.value.findIndex((r) => r.id === id)
      if (idx !== -1) alertRules.value[idx] = rule
      return rule
    } catch (err: unknown) {
      error.value = err instanceof Error ? err.message : 'Failed to update rule'
      throw err
    }
  }

  async function deleteRule(id: string) {
    error.value = null
    try {
      await alertRulesApi.delete(id)
      alertRules.value = alertRules.value.filter((r) => r.id !== id)
    } catch (err: unknown) {
      error.value = err instanceof Error ? err.message : 'Failed to delete rule'
      throw err
    }
  }

  function setFilter(key: keyof AlertListRequest, value: string | number | undefined) {
    ;(filters as Record<string, unknown>)[key] = value
    if (key !== 'page') {
      filters.page = 1
    }
  }

  function clearFilters() {
    filters.status = undefined
    filters.severity = undefined
    filters.sourceType = undefined
    filters.page = 1
  }

  function nextPage() {
    const maxPage = Math.ceil(alerts.value.total / alerts.value.pageSize)
    if (filters.page && filters.page < maxPage) {
      filters.page++
    }
  }

  function prevPage() {
    if (filters.page && filters.page > 1) {
      filters.page--
    }
  }

  return {
    alerts,
    alertRules,
    selectedAlert,
    loading,
    error,
    filters,
    fetchAlerts,
    fetchAlertRules,
    acknowledgeAlert,
    resolveAlert,
    muteAlert,
    createRule,
    updateRule,
    deleteRule,
    setFilter,
    clearFilters,
    nextPage,
    prevPage,
  }
})
