<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { serversApi, pm2Api, systemdApi } from '@/lib/api'
import type { Server, PM2Process, SystemdService } from '@/types'
import ProcessStatusBadge from '@/components/common/ProcessStatusBadge.vue'

type RuntimeKind = 'pm2' | 'systemd'
type RuntimeProcess = {
  id: string
  kind: RuntimeKind
  serverHostname: string
  serverId: string
  name: string
  description?: string | null
  pid: number | null
  status: string
  subStatus?: string | null
  uptimeSeconds?: number | null
  restartCount: number | null
  cpuUsage?: number | null
  memoryUsage?: number | null
}

const router = useRouter()

const servers = ref<Server[]>([])
const allProcesses = ref<RuntimeProcess[]>([])
const loading = ref(true)
const serverFilter = ref('')
const typeFilter = ref<RuntimeKind | ''>('')
const statusFilter = ref('')
const search = ref('')

function mapPm2(p: PM2Process & { serverHostname: string; serverId: string }): RuntimeProcess {
  return {
    id: p.id,
    kind: 'pm2',
    serverHostname: p.serverHostname,
    serverId: p.serverId,
    name: p.name,
    pid: p.pid,
    status: p.status,
    uptimeSeconds: p.uptimeSeconds,
    restartCount: p.restartCount,
    cpuUsage: p.cpuUsage,
    memoryUsage: p.memoryUsage,
  }
}

function mapSystemd(s: SystemdService & { serverHostname: string; serverId: string }): RuntimeProcess {
  return {
    id: s.id,
    kind: 'systemd',
    serverHostname: s.serverHostname,
    serverId: s.serverId,
    name: s.name,
    description: s.description || s.displayName,
    pid: s.mainPid,
    status: s.activeState,
    subStatus: s.subState,
    restartCount: s.restartCount,
    memoryUsage: s.memoryCurrent,
  }
}

onMounted(async () => {
  loading.value = true
  try {
    const { data } = await serversApi.list()
    servers.value = data

    const processPromises = data.map(async (server) => {
      const [pm2, systemd] = await Promise.all([
        pm2Api.listByServer(server.id).then((r) => r.data.map((p) => mapPm2({ ...p, serverHostname: server.hostname, serverId: server.id }))).catch(() => []),
        systemdApi.listByServer(server.id).then((r) => r.data.map((s) => mapSystemd({ ...s, serverHostname: server.hostname, serverId: server.id }))).catch(() => []),
      ])
      return [...pm2, ...systemd]
    })

    const results = await Promise.all(processPromises)
    allProcesses.value = results.flat()
  } finally {
    loading.value = false
  }
})

const filteredProcesses = computed(() => {
  const q = search.value.trim().toLowerCase()
  return allProcesses.value.filter((p) => {
    if (serverFilter.value && p.serverId !== serverFilter.value) return false
    if (typeFilter.value && p.kind !== typeFilter.value) return false
    if (statusFilter.value && p.status !== statusFilter.value) return false
    if (q && !`${p.name} ${p.description ?? ''} ${p.serverHostname} ${p.status} ${p.subStatus ?? ''}`.toLowerCase().includes(q)) return false
    return true
  })
})

const statuses = computed(() => {
  const set = new Set(allProcesses.value.map((p) => p.status))
  return Array.from(set).sort()
})

function formatUptime(seconds?: number | null): string {
  if (seconds == null) return '-'
  if (seconds < 60) return `${seconds}s`
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m`
  if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ${Math.floor((seconds % 3600) / 60)}m`
  return `${Math.floor(seconds / 86400)}d ${Math.floor((seconds % 86400) / 3600)}h`
}

function formatMemory(bytes?: number | null): string {
  if (!bytes) return '-'
  if (bytes >= 1073741824) return `${(bytes / 1073741824).toFixed(1)} GB`
  if (bytes >= 1048576) return `${(bytes / 1048576).toFixed(0)} MB`
  return `${(bytes / 1024).toFixed(0)} KB`
}
</script>

<template>
  <div>
    <div class="mb-6">
      <h2 class="text-xl font-bold text-[var(--color-text)]">Processes</h2>
      <p class="mt-1 text-sm text-[var(--color-text-secondary)]">
        Monitor PM2 processes and systemd services across all connected servers
      </p>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center justify-center py-20">
      <div class="h-8 w-8 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
    </div>

    <template v-else>
      <!-- Filters -->
      <div class="mb-4 flex flex-col gap-3 lg:flex-row">
        <input
          v-model="search"
          type="search"
          placeholder="Search name, description, server, status..."
          class="min-w-0 flex-1 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] px-3 py-2 text-sm text-[var(--color-text)] outline-none focus:border-blue-500"
        />
        <select
          v-model="serverFilter"
          class="rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] px-3 py-2 text-sm text-[var(--color-text)] outline-none focus:border-blue-500"
        >
          <option value="">All servers</option>
          <option v-for="s in servers" :key="s.id" :value="s.id">{{ s.hostname }}</option>
        </select>
        <select
          v-model="typeFilter"
          class="rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] px-3 py-2 text-sm text-[var(--color-text)] outline-none focus:border-blue-500"
        >
          <option value="">All types</option>
          <option value="pm2">PM2</option>
          <option value="systemd">systemd</option>
        </select>
        <select
          v-model="statusFilter"
          class="rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] px-3 py-2 text-sm text-[var(--color-text)] outline-none focus:border-blue-500"
        >
          <option value="">All statuses</option>
          <option v-for="s in statuses" :key="s" :value="s">{{ s }}</option>
        </select>
      </div>

      <!-- Empty -->
      <div
        v-if="filteredProcesses.length === 0"
        class="rounded-xl border border-dashed border-[var(--color-border)] py-12 text-center text-sm text-[var(--color-text-secondary)]"
      >
        No processes found.
      </div>

      <!-- Table -->
      <div v-else class="overflow-hidden rounded-xl border border-[var(--color-border)]">
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-[var(--color-border)] bg-[var(--color-bg-tertiary)]">
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">Server</th>
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">Type</th>
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">Name</th>
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">PID</th>
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">Status</th>
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">Uptime</th>
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">Restarts</th>
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">CPU</th>
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">Memory</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-[var(--color-border)]">
              <tr
                v-for="proc in filteredProcesses"
                :key="proc.id"
                class="cursor-pointer transition-colors odd:bg-[var(--color-bg-secondary)] even:bg-[var(--color-bg-secondary)]/50 hover:bg-[var(--color-bg-tertiary)]/50"
                @click="router.push(`/pm2/${proc.kind}/${proc.id}`)"
              >
                <td class="px-4 py-3 text-[var(--color-text-secondary)]">{{ proc.serverHostname }}</td>
                <td class="px-4 py-3"><span class="rounded bg-blue-500/20 px-2 py-1 text-xs font-semibold uppercase text-blue-400">{{ proc.kind }}</span></td>
                <td class="px-4 py-3 font-medium text-[var(--color-text)]">
                  <div>{{ proc.name }}</div>
                  <div v-if="proc.description" class="mt-0.5 max-w-xs truncate text-xs font-normal text-[var(--color-text-secondary)]">{{ proc.description }}</div>
                </td>
                <td class="px-4 py-3 font-mono text-xs text-[var(--color-text-secondary)]">{{ proc.pid || '-' }}</td>
                <td class="px-4 py-3">
                  <ProcessStatusBadge v-if="proc.kind === 'pm2'" :status="proc.status" />
                  <span v-else class="rounded px-2 py-1 text-xs font-semibold" :class="proc.status === 'active' ? 'bg-green-500/20 text-green-400' : 'bg-red-500/20 text-red-400'">{{ proc.status }}<span v-if="proc.subStatus">/{{ proc.subStatus }}</span></span>
                </td>
                <td class="px-4 py-3 text-[var(--color-text-secondary)]">{{ formatUptime(proc.uptimeSeconds) }}</td>
                <td class="px-4 py-3 text-[var(--color-text-secondary)]">{{ proc.restartCount ?? 0 }}</td>
                <td class="px-4 py-3 text-[var(--color-text-secondary)]">{{ proc.cpuUsage == null ? '-' : `${proc.cpuUsage.toFixed(1)}%` }}</td>
                <td class="px-4 py-3 text-[var(--color-text-secondary)]">{{ formatMemory(proc.memoryUsage) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </template>
  </div>
</template>
