<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    data: number[]
    labels: string[]
    label: string
    color?: string
    unit?: string
  }>(),
  {
    color: '#3b82f6',
    unit: '%',
  }
)

const emit = defineEmits<{
  timeRange: [range: string]
}>()

const currentValue = computed(() => {
  if (props.data.length === 0) return 0
  return props.data[props.data.length - 1]
})

const minValue = computed(() => {
  if (props.data.length === 0) return 0
  return Math.min(...props.data)
})

const maxValue = computed(() => {
  if (props.data.length === 0) return 0
  return Math.max(...props.data)
})

const chartHeight = 120
const chartWidth = 600

const points = computed(() => {
  if (props.data.length < 2) return ''
  const max = Math.max(...props.data, 1)
  const stepX = chartWidth / (props.data.length - 1)
  return props.data
    .map((v, i) => {
      const x = i * stepX
      const y = chartHeight - (v / max) * (chartHeight - 10) - 5
      return `${x},${y}`
    })
    .join(' ')
})

const areaPath = computed(() => {
  if (props.data.length < 2) return ''
  const max = Math.max(...props.data, 1)
  const stepX = chartWidth / (props.data.length - 1)
  const linePoints = props.data.map((v, i) => {
    const x = i * stepX
    const y = chartHeight - (v / max) * (chartHeight - 10) - 5
    return `${x},${y}`
  })
  return `M0,${chartHeight} L${linePoints.join(' L')} L${chartWidth},${chartHeight} Z`
})

function formatValue(val: number): string {
  if (props.unit === '%') return `${val.toFixed(1)}%`
  if (props.unit === 'bytes') {
    if (val >= 1073741824) return `${(val / 1073741824).toFixed(1)} GB`
    if (val >= 1048576) return `${(val / 1048576).toFixed(1)} MB`
    if (val >= 1024) return `${(val / 1024).toFixed(1)} KB`
    return `${val} B`
  }
  if (props.unit === 'B/s') {
    if (val >= 1048576) return `${(val / 1048576).toFixed(1)} MB/s`
    if (val >= 1024) return `${(val / 1024).toFixed(1)} KB/s`
    return `${val.toFixed(0)} B/s`
  }
  return `${val.toFixed(1)}${props.unit}`
}

const timeRanges = ['1h', '6h', '24h']
</script>

<template>
  <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-5">
    <div class="mb-4 flex items-start justify-between">
      <div>
        <h3 class="text-sm font-semibold text-[var(--color-text)]">{{ label }}</h3>
        <p class="mt-1 text-2xl font-bold text-[var(--color-text)]">
          {{ formatValue(currentValue) }}
        </p>
      </div>
      <div class="flex gap-1">
        <button
          v-for="range in timeRanges"
          :key="range"
          @click="emit('timeRange', range)"
          class="rounded-md px-2.5 py-1 text-xs font-medium transition-colors bg-[var(--color-bg-tertiary)] text-[var(--color-text-secondary)] hover:text-[var(--color-text)] hover:bg-[var(--color-border)]"
        >
          {{ range }}
        </button>
      </div>
    </div>

    <div class="relative">
      <svg
        :viewBox="`0 0 ${chartWidth} ${chartHeight}`"
        class="h-32 w-full"
        preserveAspectRatio="none"
      >
        <defs>
          <linearGradient :id="`gradient-${label}`" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" :stop-color="color" stop-opacity="0.3" />
            <stop offset="100%" :stop-color="color" stop-opacity="0" />
          </linearGradient>
        </defs>
        <path
          v-if="areaPath"
          :d="areaPath"
          :fill="`url(#gradient-${label})`"
        />
        <polyline
          v-if="points"
          :points="points"
          fill="none"
          :stroke="color"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
        />
      </svg>
    </div>

    <div class="mt-3 flex items-center justify-between border-t border-[var(--color-border)] pt-3 text-xs text-[var(--color-text-secondary)]">
      <span>Min: <span class="font-medium text-[var(--color-text)]">{{ formatValue(minValue) }}</span></span>
      <span>Max: <span class="font-medium text-[var(--color-text)]">{{ formatValue(maxValue) }}</span></span>
    </div>
  </div>
</template>
