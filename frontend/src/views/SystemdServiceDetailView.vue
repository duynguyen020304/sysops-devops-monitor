<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { systemdApi } from '@/lib/api'
import type { SystemdService, SystemdLog } from '@/types'

const route = useRoute()
const router = useRouter()
const serviceId = route.params.serviceId as string
const service = ref<SystemdService | null>(null)
const logs = ref<SystemdLog[]>([])
const loading = ref(true)
const error = ref('')

function formatTime(ts: string): string { return new Date(ts).toLocaleString() }
function formatMemory(bytes: number | null): string {
  if (!bytes) return '-'
  if (bytes >= 1073741824) return `${(bytes / 1073741824).toFixed(1)} GB`
  if (bytes >= 1048576) return `${(bytes / 1048576).toFixed(0)} MB`
  return `${(bytes / 1024).toFixed(0)} KB`
}
function levelColor(level: string): string {
  switch (level.toLowerCase()) {
    case 'error': return 'bg-red-500/20 text-red-400'
    case 'warn': case 'warning': return 'bg-yellow-500/20 text-yellow-400'
    case 'debug': return 'bg-gray-500/20 text-gray-400'
    default: return 'bg-green-500/20 text-green-400'
  }
}

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    const [svcRes, logRes] = await Promise.all([
      systemdApi.getById(serviceId),
      systemdApi.getLogs(serviceId, { limit: 200 }),
    ])
    service.value = svcRes.data
    logs.value = logRes.data
  } catch {
    error.value = 'Failed to load systemd service.'
  } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <div>
    <div v-if="loading" class="flex items-center justify-center py-20">
      <div class="h-8 w-8 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
    </div>
    <div v-else-if="error" class="py-20 text-center text-sm text-red-400">{{ error }}</div>
    <template v-else-if="service">
      <div class="mb-6">
        <div class="flex items-center gap-3">
          <button @click="router.back()" class="rounded-lg p-1.5 text-[var(--color-text-secondary)] hover:bg-[var(--color-bg-tertiary)]">←</button>
          <h2 class="text-xl font-bold text-[var(--color-text)]">{{ service.name }}</h2>
          <span class="rounded px-2 py-1 text-xs font-semibold" :class="service.activeState === 'active' ? 'bg-green-500/20 text-green-400' : 'bg-red-500/20 text-red-400'">{{ service.activeState }}</span>
        </div>
        <p class="mt-1 ml-10 text-sm text-[var(--color-text-secondary)]">{{ service.description || service.displayName }}</p>
      </div>

      <div class="mb-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4"><p class="text-xs text-[var(--color-text-secondary)]">Sub State</p><p class="mt-1 text-xl font-bold text-[var(--color-text)]">{{ service.subState }}</p></div>
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4"><p class="text-xs text-[var(--color-text-secondary)]">Main PID</p><p class="mt-1 text-xl font-bold text-[var(--color-text)]">{{ service.mainPid || '-' }}</p></div>
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4"><p class="text-xs text-[var(--color-text-secondary)]">Restarts</p><p class="mt-1 text-xl font-bold text-[var(--color-text)]">{{ service.restartCount ?? 0 }}</p></div>
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4"><p class="text-xs text-[var(--color-text-secondary)]">Memory</p><p class="mt-1 text-xl font-bold text-[var(--color-text)]">{{ formatMemory(service.memoryCurrent) }}</p></div>
      </div>

      <div class="overflow-hidden rounded-xl border border-[var(--color-border)] bg-[#0d1117]">
        <div class="border-b border-gray-800 px-4 py-2 text-sm font-medium text-gray-300">Recent journal logs</div>
        <div class="h-96 overflow-y-auto font-mono text-xs leading-relaxed">
          <div v-if="logs.length === 0" class="flex h-full items-center justify-center text-gray-500">No logs found</div>
          <div v-for="log in logs" :key="log.id" class="flex gap-3 border-b border-gray-800/50 px-4 py-1 hover:bg-gray-800/30">
            <span class="shrink-0 text-gray-500">{{ formatTime(log.timestamp) }}</span>
            <span :class="[levelColor(log.level), 'shrink-0 rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase']">{{ log.level }}</span>
            <span class="flex-1 break-all text-gray-300">{{ log.message }}</span>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>
