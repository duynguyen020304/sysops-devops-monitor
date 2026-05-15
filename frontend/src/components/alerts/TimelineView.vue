<script setup lang="ts">
import type { TimelineEvent } from '@/types'
import { AlertSeverity } from '@/types'

defineProps<{
  events: TimelineEvent[]
}>()

function eventIcon(type: TimelineEvent['type']): string {
  switch (type) {
    case 'deployment':
      return 'M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12'
    case 'restart':
      return 'M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15'
    case 'error':
      return 'M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z'
    case 'resource_spike':
      return 'M13 17h8m0 0V9m0 8l-8-8-4 4-6-6'
    case 'alert_triggered':
      return 'M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9'
    case 'alert_resolved':
      return 'M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z'
  }
}

function eventIconColor(type: TimelineEvent['type'], severity?: AlertSeverity): string {
  if (severity === AlertSeverity.Critical) return 'text-red-400 bg-red-500/10'
  if (severity === AlertSeverity.Warning) return 'text-yellow-400 bg-yellow-500/10'
  switch (type) {
    case 'deployment':
      return 'text-blue-400 bg-blue-500/10'
    case 'restart':
      return 'text-orange-400 bg-orange-500/10'
    case 'error':
      return 'text-red-400 bg-red-500/10'
    case 'resource_spike':
      return 'text-purple-400 bg-purple-500/10'
    case 'alert_triggered':
      return 'text-red-400 bg-red-500/10'
    case 'alert_resolved':
      return 'text-green-400 bg-green-500/10'
  }
}

function barColor(type: TimelineEvent['type'], severity?: AlertSeverity): string {
  if (severity === AlertSeverity.Critical) return 'bg-red-500/40'
  if (severity === AlertSeverity.Warning) return 'bg-yellow-500/40'
  switch (type) {
    case 'deployment':
      return 'bg-blue-500/40'
    case 'restart':
      return 'bg-orange-500/40'
    case 'error':
      return 'bg-red-500/40'
    case 'resource_spike':
      return 'bg-purple-500/40'
    case 'alert_triggered':
      return 'bg-red-500/40'
    case 'alert_resolved':
      return 'bg-green-500/40'
  }
}

function formatTime(dateStr: string): string {
  try {
    return new Date(dateStr).toLocaleString()
  } catch {
    return dateStr
  }
}
</script>

<template>
  <div class="relative">
    <div v-if="events.length === 0" class="py-8 text-center text-sm text-[var(--color-text-secondary)]">
      No events to display
    </div>

    <div v-else class="relative ml-4 space-y-0">
      <!-- Vertical line -->
      <div class="absolute bottom-0 left-[15px] top-0 w-px bg-[var(--color-border)]" />

      <div
        v-for="event in events"
        :key="event.id"
        class="relative flex gap-4 pb-6"
      >
        <!-- Severity color bar -->
        <div
          :class="['absolute left-0 top-0 h-full w-0.5 rounded-full', barColor(event.type, event.severity)]"
        />

        <!-- Icon -->
        <div
          :class="[
            'relative z-10 flex h-8 w-8 shrink-0 items-center justify-center rounded-full',
            eventIconColor(event.type, event.severity),
          ]"
        >
          <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
            <path stroke-linecap="round" stroke-linejoin="round" :d="eventIcon(event.type)" />
          </svg>
        </div>

        <!-- Content -->
        <div class="min-w-0 flex-1 pt-0.5">
          <p class="text-sm font-medium text-[var(--color-text)]">{{ event.title }}</p>
          <p class="mt-0.5 text-xs text-[var(--color-text-secondary)]">{{ event.description }}</p>
          <p class="mt-1 text-[11px] text-[var(--color-text-secondary)]">
            {{ formatTime(event.timestamp) }}
          </p>
        </div>
      </div>
    </div>
  </div>
</template>
