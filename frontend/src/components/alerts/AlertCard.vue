<script setup lang="ts">
import type { Alert } from '@/types'
import { AlertSeverity, AlertStatus } from '@/types'

defineProps<{
  alert: Alert
}>()

const emit = defineEmits<{
  acknowledge: [id: string]
  resolve: [id: string]
  mute: [id: string]
}>()

function severityColor(severity: AlertSeverity): string {
  switch (severity) {
    case AlertSeverity.Critical:
      return 'text-red-400 bg-red-500/10 border-red-500/20'
    case AlertSeverity.Warning:
      return 'text-yellow-400 bg-yellow-500/10 border-yellow-500/20'
    case AlertSeverity.Info:
      return 'text-blue-400 bg-blue-500/10 border-blue-500/20'
  }
}

function severityIcon(severity: AlertSeverity): string {
  switch (severity) {
    case AlertSeverity.Critical:
      return 'M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z'
    case AlertSeverity.Warning:
      return 'M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z'
    case AlertSeverity.Info:
      return 'M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z'
  }
}

function statusBadge(status: AlertStatus): string {
  switch (status) {
    case AlertStatus.Triggered:
      return 'bg-red-500/20 text-red-400 animate-pulse'
    case AlertStatus.Acknowledged:
      return 'bg-yellow-500/20 text-yellow-400'
    case AlertStatus.Resolved:
      return 'bg-green-500/20 text-green-400'
    case AlertStatus.Muted:
      return 'bg-gray-500/20 text-gray-400'
  }
}

function sourceBadge(sourceType: string): string {
  switch (sourceType) {
    case 'GitHubActions':
      return 'bg-purple-500/20 text-purple-400'
    case 'PM2':
      return 'bg-cyan-500/20 text-cyan-400'
    case 'Server':
      return 'bg-orange-500/20 text-orange-400'
    default:
      return 'bg-gray-500/20 text-gray-400'
  }
}

function formatTimeAgo(dateStr: string): string {
  const date = new Date(dateStr)
  const now = new Date()
  const diff = Math.floor((now.getTime() - date.getTime()) / 1000)
  if (diff < 60) return 'just now'
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`
  if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`
  return `${Math.floor(diff / 86400)}d ago`
}
</script>

<template>
  <div
    class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4 transition-all hover:border-blue-500/20 hover:shadow-md"
  >
    <div class="flex items-start gap-3">
      <!-- Severity icon -->
      <div
        :class="[
          'flex h-9 w-9 shrink-0 items-center justify-center rounded-lg border',
          severityColor(alert.severity),
        ]"
      >
        <svg class="h-4.5 w-4.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
          <path stroke-linecap="round" stroke-linejoin="round" :d="severityIcon(alert.severity)" />
        </svg>
      </div>

      <!-- Content -->
      <div class="min-w-0 flex-1">
        <div class="flex flex-wrap items-center gap-2">
          <h4 class="truncate text-sm font-semibold text-[var(--color-text)]">{{ alert.title }}</h4>
          <span
            :class="[
              'shrink-0 rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase',
              statusBadge(alert.status),
            ]"
          >
            {{ alert.status }}
          </span>
          <span
            :class="[
              'shrink-0 rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase',
              sourceBadge(alert.sourceType),
            ]"
          >
            {{ alert.sourceType }}
          </span>
        </div>

        <p class="mt-1 text-xs text-[var(--color-text-secondary)] line-clamp-2">
          {{ alert.description }}
        </p>

        <div class="mt-2 flex items-center justify-between">
          <span class="text-xs text-[var(--color-text-secondary)]">
            Triggered {{ formatTimeAgo(alert.triggeredAt) }}
          </span>

          <!-- Action buttons -->
          <div class="flex items-center gap-1">
            <button
              v-if="alert.status === AlertStatus.Triggered"
              @click.stop="emit('acknowledge', alert.id)"
              class="rounded-md px-2.5 py-1 text-xs font-medium text-yellow-400 transition-colors hover:bg-yellow-500/10"
            >
              Acknowledge
            </button>
            <button
              v-if="alert.status === AlertStatus.Acknowledged"
              @click.stop="emit('resolve', alert.id)"
              class="rounded-md px-2.5 py-1 text-xs font-medium text-green-400 transition-colors hover:bg-green-500/10"
            >
              Resolve
            </button>
            <button
              v-if="alert.status !== AlertStatus.Muted && alert.status !== AlertStatus.Resolved"
              @click.stop="emit('mute', alert.id)"
              class="rounded-md px-2.5 py-1 text-xs font-medium text-gray-400 transition-colors hover:bg-gray-500/10"
            >
              Mute
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
