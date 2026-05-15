import { watch, onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import { useLogsStore } from '@/stores/logs'

export function useLogs() {
  const store = useLogsStore()
  const { searchResults, filters, loading } = storeToRefs(store)

  let debounceTimer: ReturnType<typeof setTimeout> | null = null

  function debouncedSearch() {
    if (debounceTimer) clearTimeout(debounceTimer)
    debounceTimer = setTimeout(() => {
      store.searchLogs()
    }, 300)
  }

  watch(
    () => [filters.value.keyword],
    () => {
      debouncedSearch()
    },
  )

  watch(
    () => [filters.value.sourceType, filters.value.severity, filters.value.from, filters.value.to, filters.value.page],
    () => {
      store.searchLogs()
    },
  )

  onMounted(() => {
    store.searchLogs()
  })

  return {
    logs: searchResults,
    filters,
    loading,
    search: () => store.searchLogs(),
    setFilter: store.setFilter,
    clearFilters: store.clearFilters,
    nextPage: store.nextPage,
    prevPage: store.prevPage,
  }
}
