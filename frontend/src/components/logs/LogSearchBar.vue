<script setup lang="ts">
import { ref, watch } from 'vue'

const props = defineProps<{
  keyword?: string
  sourceType?: string
  severity?: string
}>()

const emit = defineEmits<{
  'update:keyword': [value: string]
  'update:sourceType': [value: string | undefined]
  'update:severity': [value: string | undefined]
  'time-range': [range: string]
}>()

const localKeyword = ref(props.keyword || '')

let debounceTimer: ReturnType<typeof setTimeout> | null = null

watch(localKeyword, (val) => {
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    emit('update:keyword', val)
  }, 300)
})

watch(
  () => props.keyword,
  (val) => {
    if (val !== localKeyword.value) {
      localKeyword.value = val || ''
    }
  },
)

const sourceTypes = [
  { label: 'All Sources', value: '' },
  { label: 'Workflows', value: 'workflow' },
  { label: 'PM2', value: 'pm2' },
]

const severities = [
  { label: 'All Levels', value: '' },
  { label: 'Error', value: 'error' },
  { label: 'Warning', value: 'warning' },
  { label: 'Info', value: 'info' },
]

const timeRanges = ['1h', '6h', '24h', '7d']
const activeTimeRange = ref('24h')

function onSourceTypeChange(e: Event) {
  const val = (e.target as HTMLSelectElement).value
  emit('update:sourceType', val || undefined)
}

function onSeverityChange(e: Event) {
  const val = (e.target as HTMLSelectElement).value
  emit('update:severity', val || undefined)
}

function onTimeRange(range: string) {
  activeTimeRange.value = range
  emit('time-range', range)
}
</script>

<template>
  <div class="flex flex-col gap-3 sm:flex-row sm:items-center">
    <!-- Search input -->
    <div class="relative flex-1">
      <svg
        class="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--color-text-secondary)]"
        fill="none"
        viewBox="0 0 24 24"
        stroke="currentColor"
        stroke-width="2"
      >
        <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
      </svg>
      <input
        v-model="localKeyword"
        type="text"
        placeholder="Search logs..."
        class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] py-2 pl-9 pr-3 text-sm text-[var(--color-text)] placeholder-[var(--color-text-secondary)] outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
      />
    </div>

    <!-- Source type filter -->
    <select
      :value="sourceType || ''"
      @change="onSourceTypeChange"
      class="rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] px-3 py-2 text-sm text-[var(--color-text)] outline-none focus:border-blue-500"
    >
      <option v-for="st in sourceTypes" :key="st.value" :value="st.value">{{ st.label }}</option>
    </select>

    <!-- Severity filter -->
    <select
      :value="severity || ''"
      @change="onSeverityChange"
      class="rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] px-3 py-2 text-sm text-[var(--color-text)] outline-none focus:border-blue-500"
    >
      <option v-for="s in severities" :key="s.value" :value="s.value">{{ s.label }}</option>
    </select>

    <!-- Time range quick picks -->
    <div class="flex gap-1">
      <button
        v-for="range in timeRanges"
        :key="range"
        @click="onTimeRange(range)"
        :class="[
          'rounded-md px-3 py-2 text-xs font-medium transition-colors',
          activeTimeRange === range
            ? 'bg-blue-500 text-white'
            : 'bg-[var(--color-bg-tertiary)] text-[var(--color-text-secondary)] hover:text-[var(--color-text)] hover:bg-[var(--color-border)]',
        ]"
      >
        {{ range }}
      </button>
    </div>
  </div>
</template>
