<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useLogs } from '@/composables/useLogs'
import LogSearchBar from '@/components/logs/LogSearchBar.vue'

const router = useRouter()
const { logs, filters, loading, setFilter, clearFilters, nextPage, prevPage } = useLogs()

const totalPages = () => Math.ceil(logs.value.total / logs.value.pageSize)

function onKeywordChange(val: string) {
  setFilter('keyword', val)
}

function onSourceTypeChange(val: string | undefined) {
  setFilter('sourceType', val)
}

function onSeverityChange(val: string | undefined) {
  setFilter('severity', val)
}

function onTimeRange(range: string) {
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
      ms = 86400000
  }
  setFilter('from', new Date(now.getTime() - ms).toISOString())
  setFilter('to', now.toISOString())
}

function sourceBadgeClass(sourceType: string): string {
  if (sourceType === 'workflow') return 'bg-blue-500/20 text-blue-400'
  if (sourceType === 'pm2') return 'bg-purple-500/20 text-purple-400'
  if (sourceType === 'systemd') return 'bg-orange-500/20 text-orange-400'
  return 'bg-gray-500/20 text-gray-400'
}

function levelBadgeClass(level: string): string {
  switch (level.toLowerCase()) {
    case 'error':
      return 'bg-red-500/20 text-red-400'
    case 'warning':
    case 'warn':
      return 'bg-yellow-500/20 text-yellow-400'
    case 'info':
      return 'bg-green-500/20 text-green-400'
    default:
      return 'bg-gray-500/20 text-gray-400'
  }
}

function formatTimestamp(ts: string): string {
  try {
    return new Date(ts).toLocaleString()
  } catch {
    return ts
  }
}

function handleLogClick(entry: { sourceType: string; sourceName: string | null; sourceId?: string | null }) {
  if (entry.sourceType === 'workflow' && entry.sourceName) {
    router.push(`/repositories/${entry.sourceName}`)
  } else if (entry.sourceType === 'pm2' && entry.sourceId) {
    router.push(`/pm2/${entry.sourceId}`)
  } else if (entry.sourceType === 'systemd' && entry.sourceId) {
    router.push(`/pm2/systemd/${entry.sourceId}`)
  }
}
</script>

<template>
  <div>
    <div class="mb-6 flex items-center justify-between">
      <div>
        <h2 class="text-xl font-bold text-[var(--color-text)]">Logs</h2>
        <p class="mt-1 text-sm text-[var(--color-text-secondary)]">Search across workflow, PM2, and systemd logs</p>
      </div>
      <span class="text-sm text-[var(--color-text-secondary)]">
        {{ logs.total.toLocaleString() }} results
      </span>
    </div>

    <!-- Search bar -->
    <div class="mb-6">
      <LogSearchBar
        :keyword="filters.keyword"
        :source-type="filters.sourceType"
        :severity="filters.severity"
        @update:keyword="onKeywordChange"
        @update:source-type="onSourceTypeChange"
        @update:severity="onSeverityChange"
        @time-range="onTimeRange"
      />
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center justify-center py-20">
      <div class="h-8 w-8 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
    </div>

    <!-- Results -->
    <template v-else>
      <div
        v-if="logs.items.length === 0"
        class="flex flex-col items-center justify-center rounded-xl border border-dashed border-[var(--color-border)] py-20"
      >
        <svg class="mb-3 h-10 w-10 text-[var(--color-text-secondary)]" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
          <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
        <p class="text-sm text-[var(--color-text-secondary)]">No logs found</p>
        <button
          v-if="filters.keyword || filters.sourceType || filters.severity"
          @click="clearFilters"
          class="mt-2 text-xs text-blue-400 hover:underline"
        >
          Clear filters
        </button>
      </div>

      <div
        v-else
        class="overflow-hidden rounded-xl border border-[var(--color-border)] bg-[#0d1117]"
      >
        <!-- Log entries -->
        <div class="max-h-[60vh] overflow-y-auto font-mono text-xs leading-relaxed">
          <div
            v-for="entry in logs.items"
            :key="entry.id"
            class="flex flex-wrap items-start gap-x-3 gap-y-1 border-b border-gray-800/50 px-4 py-2 transition-colors hover:bg-gray-800/30"
            :class="{ 'cursor-pointer': entry.sourceName }"
            @click="handleLogClick(entry)"
          >
            <span class="shrink-0 text-gray-500">{{ formatTimestamp(entry.timestamp) }}</span>
            <span
              :class="[sourceBadgeClass(entry.sourceType), 'shrink-0 rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase']"
            >
              {{ entry.sourceType }}
            </span>
            <span
              :class="[levelBadgeClass(entry.level), 'shrink-0 rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase']"
            >
              {{ entry.level }}
            </span>
            <span v-if="entry.sourceName" class="shrink-0 text-gray-400">{{ entry.sourceName }}</span>
            <span class="min-w-0 flex-1 break-all text-gray-300">{{ entry.message }}</span>
          </div>
        </div>

        <!-- Pagination -->
        <div class="flex items-center justify-between border-t border-gray-700 px-4 py-3">
          <button
            @click="prevPage"
            :disabled="!filters.page || filters.page <= 1"
            class="rounded-lg px-3 py-1.5 text-xs font-medium text-gray-400 transition-colors hover:bg-gray-800 hover:text-white disabled:opacity-40 disabled:hover:bg-transparent disabled:hover:text-gray-400"
          >
            Previous
          </button>
          <div class="flex items-center gap-1">
            <span class="text-xs text-gray-500">
              Page {{ filters.page || 1 }} of {{ totalPages() || 1 }}
            </span>
          </div>
          <button
            @click="nextPage"
            :disabled="(filters.page || 1) >= totalPages()"
            class="rounded-lg px-3 py-1.5 text-xs font-medium text-gray-400 transition-colors hover:bg-gray-800 hover:text-white disabled:opacity-40 disabled:hover:bg-transparent disabled:hover:text-gray-400"
          >
            Next
          </button>
        </div>
      </div>
    </template>
  </div>
</template>
