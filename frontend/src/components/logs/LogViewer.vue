<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue'

interface LogLine {
  id: string
  timestamp: string
  level: string
  source?: string
  message: string
  sourceType?: string
  sourceName?: string | null
}

const props = withDefaults(
  defineProps<{
    logs: LogLine[]
    showSourceLink?: boolean
  }>(),
  {
    showSourceLink: false,
  },
)

const emit = defineEmits<{
  'source-click': [entry: LogLine]
}>()

const searchQuery = ref('')
const autoScroll = ref(true)
const logContainer = ref<HTMLElement | null>(null)

const filteredLogs = computed(() => {
  if (!searchQuery.value.trim()) return props.logs
  const q = searchQuery.value.toLowerCase()
  return props.logs.filter(
    (log) =>
      log.message.toLowerCase().includes(q) ||
      (log.source && log.source.toLowerCase().includes(q)),
  )
})

function levelColor(level: string): string {
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

function formatTime(ts: string): string {
  try {
    return new Date(ts).toLocaleTimeString()
  } catch {
    return ts
  }
}

function handleSourceClick(entry: LogLine) {
  if (props.showSourceLink && entry.sourceName) {
    emit('source-click', entry)
  }
}

watch(
  () => props.logs.length,
  async () => {
    if (autoScroll.value && logContainer.value) {
      await nextTick()
      logContainer.value.scrollTop = logContainer.value.scrollHeight
    }
  },
)
</script>

<template>
  <div class="flex flex-1 flex-col overflow-hidden rounded-xl border border-[var(--color-border)] bg-[#0d1117]">
    <!-- Toolbar -->
    <div class="flex items-center gap-3 border-b border-gray-700 px-4 py-2.5">
      <div class="relative flex-1">
        <svg
          class="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-500"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          stroke-width="2"
        >
          <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
        <input
          v-model="searchQuery"
          type="text"
          placeholder="Filter logs..."
          class="w-full rounded-lg border border-gray-700 bg-gray-900 py-1.5 pl-9 pr-3 text-sm text-gray-300 placeholder-gray-500 outline-none focus:border-blue-500"
        />
      </div>
      <label class="flex items-center gap-2 text-xs text-gray-400 select-none">
        <input
          v-model="autoScroll"
          type="checkbox"
          class="h-3.5 w-3.5 rounded border-gray-600 bg-gray-800 text-blue-500 focus:ring-blue-500"
        />
        Auto-scroll
      </label>
      <span class="text-xs text-gray-500">{{ filteredLogs.length }} lines</span>
    </div>

    <!-- Log lines -->
    <div
      ref="logContainer"
      class="flex-1 min-h-0 overflow-y-auto font-mono text-xs leading-relaxed"
    >
      <div
        v-if="filteredLogs.length === 0"
        class="flex h-full items-center justify-center text-gray-500"
      >
        No log entries found
      </div>
      <div
        v-for="log in filteredLogs"
        :key="log.id"
        class="flex gap-3 border-b border-gray-800/50 px-4 py-1 transition-colors hover:bg-gray-800/30"
        :class="{ 'cursor-pointer': showSourceLink && log.sourceName }"
        @click="handleSourceClick(log)"
      >
        <span class="shrink-0 text-gray-500">{{ formatTime(log.timestamp) }}</span>
        <span
          :class="[levelColor(log.level), 'shrink-0 rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase']"
        >
          {{ log.level }}
        </span>
        <span v-if="log.source" class="text-gray-500">{{ log.source }}</span>
        <span class="flex-1 break-all text-gray-300">{{ log.message }}</span>
      </div>
    </div>
  </div>
</template>
