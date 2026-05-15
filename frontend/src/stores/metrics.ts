import { defineStore } from 'pinia'
import { ref } from 'vue'
import { metricsApi } from '@/lib/api'
import type { ServerMetricsSummary } from '@/types'

export const useMetricsStore = defineStore('metrics', () => {
  const serverMetrics = ref<Map<string, ServerMetricsSummary>>(new Map())
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetchServerMetrics(serverId: string, from?: string, to?: string) {
    loading.value = true
    error.value = null
    try {
      const { data } = await metricsApi.getServerMetrics(serverId, from, to)
      serverMetrics.value.set(serverId, data)
    } catch (err: unknown) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch metrics'
    } finally {
      loading.value = false
    }
  }

  function getMetricsForServer(serverId: string): ServerMetricsSummary | undefined {
    return serverMetrics.value.get(serverId)
  }

  return {
    serverMetrics,
    loading,
    error,
    fetchServerMetrics,
    getMetricsForServer,
  }
})
