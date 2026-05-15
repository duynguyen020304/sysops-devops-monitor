import { defineStore } from 'pinia'
import { ref, reactive } from 'vue'
import { logsApi } from '@/lib/api'
import type { LogSearchRequest, LogSearchResult } from '@/types'

export const useLogsStore = defineStore('logs', () => {
  const searchResults = ref<LogSearchResult>({
    items: [],
    total: 0,
    page: 1,
    pageSize: 50,
  })
  const filters = reactive<LogSearchRequest>({
    keyword: '',
    sourceType: undefined,
    severity: undefined,
    from: undefined,
    to: undefined,
    page: 1,
    pageSize: 50,
  })
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function searchLogs() {
    loading.value = true
    error.value = null
    try {
      const params: LogSearchRequest = { ...filters }
      if (!params.keyword) delete params.keyword
      if (!params.sourceType) delete params.sourceType
      if (!params.severity) delete params.severity
      if (!params.from) delete params.from
      if (!params.to) delete params.to
      const { data } = await logsApi.search(params)
      searchResults.value = data
    } catch (err: unknown) {
      error.value = err instanceof Error ? err.message : 'Failed to search logs'
    } finally {
      loading.value = false
    }
  }

  function setFilter(key: keyof LogSearchRequest, value: string | number | undefined) {
    ;(filters as Record<string, unknown>)[key] = value
    if (key !== 'page') {
      filters.page = 1
    }
  }

  function clearFilters() {
    filters.keyword = ''
    filters.sourceType = undefined
    filters.severity = undefined
    filters.from = undefined
    filters.to = undefined
    filters.page = 1
  }

  function nextPage() {
    const maxPage = Math.ceil(searchResults.value.total / searchResults.value.pageSize)
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
    searchResults,
    filters,
    loading,
    error,
    searchLogs,
    setFilter,
    clearFilters,
    nextPage,
    prevPage,
  }
})
