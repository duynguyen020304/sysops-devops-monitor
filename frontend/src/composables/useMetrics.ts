import { computed, ref } from 'vue'
import { useMetricsStore } from '@/stores/metrics'

export function useMetrics() {
  const store = useMetricsStore()
  const timeRange = ref('1h')

  function getTimeRange(range: string): { from: string; to: string } {
    const now = new Date()
    let ms: number
    switch (range) {
      case '1h':
        ms = 3600000
        break
      case '6h':
        ms = 21600000
        break
      case '24h':
        ms = 86400000
        break
      case '7d':
        ms = 604800000
        break
      default:
        ms = 3600000
    }
    return {
      from: new Date(now.getTime() - ms).toISOString(),
      to: now.toISOString(),
    }
  }

  async function fetchMetrics(serverId: string, range?: string) {
    const r = range || timeRange.value
    const { from, to } = getTimeRange(r)
    await store.fetchServerMetrics(serverId, from, to)
  }

  function setTimeRange(range: string) {
    timeRange.value = range
  }

  function getMetricsForServer(serverId: string) {
    return computed(() => store.getMetricsForServer(serverId))
  }

  return {
    timeRange,
    loading: computed(() => store.loading),
    fetchMetrics,
    setTimeRange,
    getMetricsForServer,
    getTimeRange,
  }
}
