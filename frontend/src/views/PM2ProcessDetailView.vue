<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { pm2Api } from '@/lib/api'
import type { PM2Process, PM2Log } from '@/types'
import ProcessStatusBadge from '@/components/common/ProcessStatusBadge.vue'

const route = useRoute()
const router = useRouter()
const processId = route.params.processId as string

const process = ref<PM2Process | null>(null)
const stdoutLogs = ref<PM2Log[]>([])
const errorLogs = ref<PM2Log[]>([])
const loading = ref(true)
const activeTab = ref<'stdout' | 'stderr'>('stdout')

onMounted(async () => {
  loading.value = true
  try {
    const [processRes, logsRes] = await Promise.all([
      pm2Api.getById(processId),
      pm2Api.getLogs(processId, 200),
    ])
    process.value = processRes.data
    stdoutLogs.value = logsRes.data.filter((l) => l.streamType === 'stdout')
    errorLogs.value = logsRes.data.filter((l) => l.streamType === 'stderr')
  } catch {
    // handled by UI
  } finally {
    loading.value = false
  }
})

function formatUptime(seconds: number): string {
  if (seconds < 60) return `${seconds}s`
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m`
  if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ${Math.floor((seconds % 3600) / 60)}m`
  return `${Math.floor(seconds / 86400)}d ${Math.floor((seconds % 86400) / 3600)}h`
}

function formatMemory(bytes: number): string {
  if (bytes >= 1073741824) return `${(bytes / 1073741824).toFixed(1)} GB`
  if (bytes >= 1048576) return `${(bytes / 1048576).toFixed(0)} MB`
  return `${(bytes / 1024).toFixed(0)} KB`
}

function formatTime(ts: string): string {
  try {
    return new Date(ts).toLocaleString()
  } catch {
    return ts
  }
}

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

const currentLogs = computed(() =>
  activeTab.value === 'stdout' ? stdoutLogs.value : errorLogs.value
)
</script>

<template>
  <div>
    <!-- Loading -->
    <div v-if="loading" class="flex items-center justify-center py-20">
      <div class="h-8 w-8 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
    </div>

    <template v-else-if="process">
      <!-- Header -->
      <div class="mb-6">
        <div class="flex items-center gap-3">
          <button
            @click="router.back()"
            class="rounded-lg p-1.5 text-[var(--color-text-secondary)] transition-colors hover:bg-[var(--color-bg-tertiary)] hover:text-[var(--color-text)]"
          >
            <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M15 19l-7-7 7-7" />
            </svg>
          </button>
          <h2 class="text-xl font-bold text-[var(--color-text)]">{{ process.name }}</h2>
          <ProcessStatusBadge :status="process.status" />
        </div>
        <p class="mt-1 ml-10 text-sm text-[var(--color-text-secondary)]">
          PID {{ process.pid }} &middot; PM2 ID {{ process.pm2Id }} &middot; {{ process.executionMode }} mode &middot; Node {{ process.nodeVersion }}
        </p>
      </div>

      <!-- Stats cards -->
      <div class="mb-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
          <p class="text-xs font-medium text-[var(--color-text-secondary)]">Uptime</p>
          <p class="mt-1 text-xl font-bold text-[var(--color-text)]">{{ formatUptime(process.uptimeSeconds) }}</p>
        </div>
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
          <p class="text-xs font-medium text-[var(--color-text-secondary)]">Restarts</p>
          <p class="mt-1 text-xl font-bold text-[var(--color-text)]">{{ process.restartCount }}</p>
        </div>
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
          <p class="text-xs font-medium text-[var(--color-text-secondary)]">CPU Usage</p>
          <p class="mt-1 text-xl font-bold text-[var(--color-text)]">{{ process.cpuUsage.toFixed(1) }}%</p>
        </div>
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
          <p class="text-xs font-medium text-[var(--color-text-secondary)]">Memory</p>
          <p class="mt-1 text-xl font-bold text-[var(--color-text)]">{{ formatMemory(process.memoryUsage) }}</p>
        </div>
      </div>

      <!-- Logs tabs -->
      <div class="mb-4 flex gap-1 rounded-lg bg-[var(--color-bg-tertiary)] p-1">
        <button
          @click="activeTab = 'stdout'"
          :class="[
            'rounded-md px-4 py-2 text-sm font-medium transition-colors',
            activeTab === 'stdout'
              ? 'bg-[var(--color-bg-secondary)] text-[var(--color-text)] shadow-sm'
              : 'text-[var(--color-text-secondary)] hover:text-[var(--color-text)]',
          ]"
        >
          Stdout ({{ stdoutLogs.length }})
        </button>
        <button
          @click="activeTab = 'stderr'"
          :class="[
            'rounded-md px-4 py-2 text-sm font-medium transition-colors',
            activeTab === 'stderr'
              ? 'bg-[var(--color-bg-secondary)] text-[var(--color-text)] shadow-sm'
              : 'text-[var(--color-text-secondary)] hover:text-[var(--color-text)]',
          ]"
        >
          Stderr ({{ errorLogs.length }})
        </button>
      </div>

      <!-- Log viewer -->
      <div class="overflow-hidden rounded-xl border border-[var(--color-border)] bg-[#0d1117]">
        <div
          class="h-96 overflow-y-auto font-mono text-xs leading-relaxed"
        >
          <div
            v-if="currentLogs.length === 0"
            class="flex h-full items-center justify-center text-gray-500"
          >
            No {{ activeTab === 'stdout' ? 'stdout' : 'error' }} log entries found
          </div>
          <div
            v-for="log in currentLogs"
            :key="log.id"
            class="flex gap-3 border-b border-gray-800/50 px-4 py-1 hover:bg-gray-800/30"
          >
            <span class="shrink-0 text-gray-500">{{ formatTime(log.timestamp) }}</span>
            <span
              :class="[levelColor(log.level), 'shrink-0 rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase']"
            >
              {{ log.level }}
            </span>
            <span class="flex-1 break-all text-gray-300">{{ log.message }}</span>
          </div>
        </div>
      </div>
    </template>

    <!-- Not found -->
    <div v-else class="flex flex-col items-center justify-center py-20">
      <p class="text-[var(--color-text-secondary)]">Process not found.</p>
      <button @click="router.push('/pm2')" class="mt-3 text-sm text-blue-400 hover:underline">
        Back to PM2 processes
      </button>
    </div>
  </div>
</template>
