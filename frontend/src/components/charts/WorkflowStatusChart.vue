<script setup lang="ts">
import { computed } from 'vue'
import type { RepositoryStats } from '@/types'

const props = defineProps<{
  stats: RepositoryStats
}>()

const maxValue = computed(() =>
  Math.max(props.stats.successfulRuns, props.stats.failedRuns, props.stats.cancelledRuns, 1)
)

const bars = computed(() => [
  {
    label: 'Success',
    value: props.stats.successfulRuns,
    percent: (props.stats.successfulRuns / maxValue.value) * 100,
    color: 'bg-green-500',
    textColor: 'text-green-400',
  },
  {
    label: 'Failed',
    value: props.stats.failedRuns,
    percent: (props.stats.failedRuns / maxValue.value) * 100,
    color: 'bg-red-500',
    textColor: 'text-red-400',
  },
  {
    label: 'Cancelled',
    value: props.stats.cancelledRuns,
    percent: (props.stats.cancelledRuns / maxValue.value) * 100,
    color: 'bg-yellow-500',
    textColor: 'text-yellow-400',
  },
])
</script>

<template>
  <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-5">
    <h3 class="mb-4 text-sm font-semibold text-[var(--color-text)]">Workflow Outcomes</h3>
    <div class="space-y-4">
      <div v-for="bar in bars" :key="bar.label">
        <div class="mb-1 flex items-center justify-between text-xs">
          <span class="text-[var(--color-text-secondary)]">{{ bar.label }}</span>
          <span :class="['font-medium', bar.textColor]">{{ bar.value }}</span>
        </div>
        <div class="h-3 w-full overflow-hidden rounded-full bg-[var(--color-bg-tertiary)]">
          <div
            :class="[bar.color, 'h-full rounded-full transition-all duration-500']"
            :style="{ width: `${bar.percent}%` }"
          />
        </div>
      </div>
    </div>
    <div class="mt-4 border-t border-[var(--color-border)] pt-3 text-center text-xs text-[var(--color-text-secondary)]">
      Failure rate: <span class="font-medium text-red-400">{{ props.stats.failureRate.toFixed(1) }}%</span>
    </div>
  </div>
</template>
