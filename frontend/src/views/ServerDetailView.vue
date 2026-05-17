<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { serversApi, pm2Api, agentUpdateApi } from '@/lib/api'
import type { DeployAgentResponse, Server, ServerHealth, ServerMetric, PM2Process, AgentUpdateRelease, AgentUpdateAssignment } from '@/types'
import MetricChart from '@/components/charts/MetricChart.vue'
import ProcessStatusBadge from '@/components/common/ProcessStatusBadge.vue'

const route = useRoute()
const router = useRouter()
const serverId = route.params.id as string

const server = ref<Server | null>(null)
const health = ref<ServerHealth | null>(null)
const metrics = ref<ServerMetric[]>([])
const processes = ref<PM2Process[]>([])
const loading = ref(true)
const metricsLoading = ref(false)
const activeTab = ref<'metrics' | 'pm2' | 'alerts'>('metrics')
const timeRange = ref('1h')
const deploying = ref(false)
const deployResult = ref<DeployAgentResponse | null>(null)

const releases = ref<AgentUpdateRelease[]>([])
const assignments = ref<AgentUpdateAssignment[]>([])
const updating = ref(false)
const updateError = ref('')

onMounted(async () => {
  loading.value = true
  try {
    const [serverRes, healthRes, processesRes, releasesRes, assignmentsRes] = await Promise.all([
      serversApi.getById(serverId),
      serversApi.getHealth(serverId),
      pm2Api.listByServer(serverId),
      agentUpdateApi.listReleases().catch(() => ({ data: [] as AgentUpdateRelease[] })),
      agentUpdateApi.listAssignments(serverId).catch(() => ({ data: [] as AgentUpdateAssignment[] })),
    ])
    server.value = serverRes.data
    health.value = healthRes.data
    processes.value = processesRes.data
    releases.value = releasesRes.data
    assignments.value = assignmentsRes.data
    await onTimeRangeChange('1h')
  } catch {
    // handled by UI
  } finally {
    loading.value = false
  }
})

async function onTimeRangeChange(range: string) {
  timeRange.value = range
  const now = new Date()
  let from: Date
  switch (range) {
    case '1h':
      from = new Date(now.getTime() - 3600000)
      break
    case '6h':
      from = new Date(now.getTime() - 21600000)
      break
    case '24h':
      from = new Date(now.getTime() - 86400000)
      break
    case '7d':
      from = new Date(now.getTime() - 604800000)
      break
    default:
      from = new Date(now.getTime() - 3600000)
  }
  metricsLoading.value = true
  try {
    const { data } = await serversApi.getMetrics(serverId, from.toISOString(), now.toISOString())
    metrics.value = data
  } catch {
    // keep existing data
  } finally {
    metricsLoading.value = false
  }
}

const orderedMetrics = computed(() => [...metrics.value].sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime()))
const latestMetric = computed(() => orderedMetrics.value.at(-1) ?? null)
const cpuData = computed(() => orderedMetrics.value.map((m) => m.cpuUsagePercent))
const memData = computed(() => orderedMetrics.value.map((m) => m.memoryUsagePercent))
const diskData = computed(() => orderedMetrics.value.map((m) => m.diskUsagePercent))
const networkRxData = computed(() => orderedMetrics.value.map((m) => m.networkRxBytesPerSecond))
const networkTxData = computed(() => orderedMetrics.value.map((m) => m.networkTxBytesPerSecond))
async function deployAgent() {
  deploying.value = true
  deployResult.value = null
  try {
    const { data } = await serversApi.deployAgent(serverId)
    deployResult.value = data
  } catch (err: any) {
    deployResult.value = err?.response?.data ?? { success: false, output: '', error: 'Deploy failed', deployedAt: new Date().toISOString() }
  } finally {
    deploying.value = false
  }
}

const latestRelease = computed(() => releases.value[0] ?? null)
const latestAssignment = computed(() => assignments.value[0] ?? null)
const updateAvailable = computed(() => !!latestRelease.value && (!!server.value?.agentBuildId ? server.value.agentBuildId !== latestRelease.value.buildId : server.value?.agentVersion !== latestRelease.value.version))

async function createRelease() {
  updating.value = true
  updateError.value = ''
  try {
    const { data } = await agentUpdateApi.createCurrentRelease()
    releases.value = [data, ...releases.value.filter((r) => r.id !== data.id)]
  } catch (err: any) {
    updateError.value = err?.response?.data?.message ?? 'Failed to create release.'
  } finally {
    updating.value = false
  }
}

async function updateAgent() {
  if (!latestRelease.value) await createRelease()
  if (!latestRelease.value) return
  updating.value = true
  updateError.value = ''
  try {
    const { data } = await agentUpdateApi.assign(serverId, latestRelease.value.id)
    assignments.value = [data, ...assignments.value]
    if (server.value) server.value.agentUpdateStatus = data.status
  } catch (err: any) {
    updateError.value = err?.response?.data?.message ?? 'Failed to assign update.'
  } finally {
    updating.value = false
  }
}

const loadData = computed(() => orderedMetrics.value.map((m) => m.loadAverage1m))
const metricLabels = computed(() =>
  orderedMetrics.value.map((m) => new Date(m.timestamp).toLocaleString([], timeRange.value === '7d'
    ? { month: 'short', day: 'numeric', hour: '2-digit' }
    : { hour: '2-digit', minute: '2-digit' })),
)

const pm2Totals = computed(() => ({
  total: processes.value.length,
  online: processes.value.filter((p) => p.status === 'Online' || p.status === 'online').length,
  restarts: processes.value.reduce((sum, p) => sum + p.restartCount, 0),
  memory: processes.value.reduce((sum, p) => sum + p.memoryUsage, 0),
}))

function statusColor(status: string): string {
  switch (status) {
    case 'Healthy':
      return 'bg-green-500/20 text-green-400 border-green-500/30'
    case 'Warning':
      return 'bg-yellow-500/20 text-yellow-400 border-yellow-500/30'
    case 'Critical':
      return 'bg-red-500/20 text-red-400 border-red-500/30'
    default:
      return 'bg-gray-500/20 text-gray-400 border-gray-500/30'
  }
}

function statusDot(status: string): string {
  switch (status) {
    case 'Healthy':
      return 'bg-green-400'
    case 'Warning':
      return 'bg-yellow-400'
    case 'Critical':
      return 'bg-red-400'
    default:
      return 'bg-gray-400'
  }
}

function formatTimeAgo(dateStr: string | null): string {
  if (!dateStr) return 'Never'
  const date = new Date(dateStr)
  const now = new Date()
  const diff = Math.floor((now.getTime() - date.getTime()) / 1000)
  if (diff < 60) return 'just now'
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`
  if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`
  return `${Math.floor(diff / 86400)}d ago`
}

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
</script>

<template>
  <div>
    <!-- Loading -->
    <div v-if="loading" class="flex items-center justify-center py-20">
      <div class="h-8 w-8 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
    </div>

    <template v-else-if="server">
      <!-- Header -->
      <div class="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div class="flex items-center gap-3">
            <button
              @click="router.push('/servers')"
              class="rounded-lg p-1.5 text-[var(--color-text-secondary)] transition-colors hover:bg-[var(--color-bg-tertiary)] hover:text-[var(--color-text)]"
            >
              <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M15 19l-7-7 7-7" />
              </svg>
            </button>
            <h2 class="text-xl font-bold text-[var(--color-text)]">{{ server.hostname }}</h2>
            <span
              :class="[
                statusColor(server.status),
                'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium',
              ]"
            >
              <span :class="[statusDot(server.status), 'h-1.5 w-1.5 rounded-full']" />
              {{ server.status }}
            </span>
          </div>
          <p class="mt-1 ml-10 text-sm text-[var(--color-text-secondary)]">
            {{ server.ipAddress }} &middot; {{ server.operatingSystem }} &middot; Agent v{{ server.agentVersion }}
          </p>
        </div>
        <div class="ml-10 flex flex-wrap items-center gap-4 text-xs text-[var(--color-text-secondary)] sm:ml-0">
          <button
            class="rounded-lg bg-blue-500 px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-blue-600 disabled:cursor-not-allowed disabled:opacity-60"
            :disabled="deploying"
            @click="deployAgent"
          >
            {{ deploying ? 'Deploying...' : 'Deploy Agent' }}
          </button>
          <span>Last heartbeat: <span class="font-medium text-[var(--color-text)]">{{ formatTimeAgo(server.lastHeartbeatAt) }}</span></span>
          <span v-if="health">Alerts: <span class="font-medium text-red-400">{{ health.alertCount }}</span></span>
        </div>
      </div>

      <div class="mb-6 rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
        <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 class="text-sm font-semibold text-[var(--color-text)]">Agent Updates</h3>
            <p class="mt-1 text-xs text-[var(--color-text-secondary)]">
              Current: v{{ server.agentVersion }} <span v-if="server.agentBuildId">({{ server.agentBuildId.slice(0, 12) }})</span>
              <span v-if="latestRelease"> · Latest: v{{ latestRelease.version }} ({{ latestRelease.buildId.slice(0, 12) }})</span>
            </p>
            <p v-if="server.agentUpdateStatus" class="mt-1 text-xs text-blue-400">Status: {{ server.agentUpdateStatus }}</p>
            <p v-if="latestAssignment" class="mt-1 text-xs text-[var(--color-text-secondary)]">Last assignment: {{ latestAssignment.status }} · {{ new Date(latestAssignment.updatedAt).toLocaleString() }}</p>
            <p v-if="updateError" class="mt-2 text-xs text-red-400">{{ updateError }}</p>
          </div>
          <div class="flex flex-wrap gap-2">
            <button class="rounded-lg border border-[var(--color-border)] px-3 py-1.5 text-xs font-medium text-[var(--color-text)] hover:bg-[var(--color-bg-tertiary)] disabled:opacity-60" :disabled="updating" @click="createRelease">
              Build Release
            </button>
            <button class="rounded-lg bg-green-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-green-500 disabled:opacity-60" :disabled="updating || !latestRelease" @click="updateAgent">
              {{ updating ? 'Scheduling...' : updateAvailable ? 'Update Agent' : 'Reinstall Latest' }}
            </button>
          </div>
        </div>
      </div>

      <div v-if="deployResult" class="mb-6 rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
        <div class="mb-2 text-sm font-medium" :class="deployResult.success ? 'text-green-400' : 'text-red-400'">
          {{ deployResult.success ? 'Agent deployed' : 'Agent deploy failed' }}
        </div>
        <p v-if="deployResult.error" class="mb-2 text-sm text-red-400">{{ deployResult.error }}</p>
        <details class="text-xs text-[var(--color-text-secondary)]">
          <summary class="cursor-pointer">Deploy output</summary>
          <pre class="mt-2 max-h-64 overflow-auto whitespace-pre-wrap rounded-lg bg-[var(--color-bg-tertiary)] p-3">{{ deployResult.output }}</pre>
        </details>
      </div>

      <!-- Summary cards -->
      <div class="mb-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
          <p class="text-xs text-[var(--color-text-secondary)]">CPU</p>
          <p class="mt-1 text-2xl font-bold text-[var(--color-text)]">{{ latestMetric ? `${latestMetric.cpuUsagePercent.toFixed(1)}%` : '—' }}</p>
        </div>
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
          <p class="text-xs text-[var(--color-text-secondary)]">Memory</p>
          <p class="mt-1 text-2xl font-bold text-[var(--color-text)]">{{ latestMetric ? `${latestMetric.memoryUsagePercent.toFixed(1)}%` : '—' }}</p>
          <p v-if="latestMetric" class="mt-1 text-xs text-[var(--color-text-secondary)]">{{ formatMemory(latestMetric.memoryUsedBytes) }} / {{ formatMemory(latestMetric.memoryTotalBytes) }}</p>
        </div>
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
          <p class="text-xs text-[var(--color-text-secondary)]">Disk</p>
          <p class="mt-1 text-2xl font-bold text-[var(--color-text)]">{{ latestMetric ? `${latestMetric.diskUsagePercent.toFixed(1)}%` : '—' }}</p>
          <p v-if="latestMetric" class="mt-1 text-xs text-[var(--color-text-secondary)]">{{ formatMemory(latestMetric.diskUsedBytes) }} / {{ formatMemory(latestMetric.diskTotalBytes) }}</p>
        </div>
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
          <p class="text-xs text-[var(--color-text-secondary)]">PM2 Apps</p>
          <p class="mt-1 text-2xl font-bold text-[var(--color-text)]">{{ pm2Totals.online }}/{{ pm2Totals.total }}</p>
          <p class="mt-1 text-xs text-[var(--color-text-secondary)]">{{ pm2Totals.restarts }} restarts · {{ formatMemory(pm2Totals.memory) }}</p>
        </div>
      </div>

      <!-- Tabs -->
      <div class="mb-6 flex gap-1 rounded-lg bg-[var(--color-bg-tertiary)] p-1">
        <button
          v-for="tab in ['metrics', 'pm2', 'alerts'] as const"
          :key="tab"
          @click="activeTab = tab"
          :class="[
            'rounded-md px-4 py-2 text-sm font-medium transition-colors',
            activeTab === tab
              ? 'bg-[var(--color-bg-secondary)] text-[var(--color-text)] shadow-sm'
              : 'text-[var(--color-text-secondary)] hover:text-[var(--color-text)]',
          ]"
        >
          {{ tab === 'pm2' ? 'PM2 Processes' : tab.charAt(0).toUpperCase() + tab.slice(1) }}
        </button>
      </div>

      <!-- Metrics Tab -->
      <div v-if="activeTab === 'metrics'">
        <!-- Time range selector -->
        <div class="mb-4 flex items-center gap-2">
          <span class="text-sm text-[var(--color-text-secondary)]">Time range:</span>
          <div class="flex gap-1">
            <button
              v-for="range in ['1h', '24h', '7d']"
              :key="range"
              @click="onTimeRangeChange(range)"
              :class="[
                'rounded-md px-3 py-1.5 text-xs font-medium transition-colors',
                timeRange === range
                  ? 'bg-blue-500 text-white'
                  : 'bg-[var(--color-bg-tertiary)] text-[var(--color-text-secondary)] hover:text-[var(--color-text)] hover:bg-[var(--color-border)]',
              ]"
            >
              {{ range }}
            </button>
          </div>
          <div v-if="metricsLoading" class="ml-2 h-4 w-4 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
        </div>

        <div class="grid gap-4 sm:grid-cols-2">
          <MetricChart
            :data="cpuData"
            :labels="metricLabels"
            label="CPU Usage"
            color="#3b82f6"
            unit="%"
            @time-range="onTimeRangeChange"
          />
          <MetricChart
            :data="memData"
            :labels="metricLabels"
            label="Memory Usage"
            color="#8b5cf6"
            unit="%"
            @time-range="onTimeRangeChange"
          />
          <MetricChart
            :data="diskData"
            :labels="metricLabels"
            label="Disk Usage"
            color="#f59e0b"
            unit="%"
            @time-range="onTimeRangeChange"
          />
          <MetricChart
            :data="networkRxData"
            :labels="metricLabels"
            label="Network RX"
            color="#10b981"
            unit="B/s"
            @time-range="onTimeRangeChange"
          />
          <MetricChart
            :data="networkTxData"
            :labels="metricLabels"
            label="Network TX"
            color="#06b6d4"
            unit="B/s"
            @time-range="onTimeRangeChange"
          />
          <MetricChart
            :data="loadData"
            :labels="metricLabels"
            label="Load Average"
            color="#f97316"
            unit=""
            @time-range="onTimeRangeChange"
          />
        </div>
      </div>

      <!-- PM2 Tab -->
      <div v-else-if="activeTab === 'pm2'">
        <div class="mb-4 grid gap-4 sm:grid-cols-4">
          <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
            <p class="text-xs text-[var(--color-text-secondary)]">Total apps</p>
            <p class="mt-1 text-xl font-bold text-[var(--color-text)]">{{ pm2Totals.total }}</p>
          </div>
          <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
            <p class="text-xs text-[var(--color-text-secondary)]">Online</p>
            <p class="mt-1 text-xl font-bold text-green-400">{{ pm2Totals.online }}</p>
          </div>
          <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
            <p class="text-xs text-[var(--color-text-secondary)]">Restarts</p>
            <p class="mt-1 text-xl font-bold text-[var(--color-text)]">{{ pm2Totals.restarts }}</p>
          </div>
          <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
            <p class="text-xs text-[var(--color-text-secondary)]">Memory</p>
            <p class="mt-1 text-xl font-bold text-[var(--color-text)]">{{ formatMemory(pm2Totals.memory) }}</p>
          </div>
        </div>
        <div v-if="processes.length === 0" class="rounded-xl border border-dashed border-[var(--color-border)] py-12 text-center text-sm text-[var(--color-text-secondary)]">
          No PM2 processes found on this server.
        </div>
        <div v-else class="overflow-hidden rounded-xl border border-[var(--color-border)]">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-[var(--color-border)] bg-[var(--color-bg-tertiary)]">
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
                v-for="proc in processes"
                :key="proc.id"
                class="cursor-pointer transition-colors hover:bg-[var(--color-bg-tertiary)]/50"
                @click="router.push(`/pm2/${proc.id}`)"
              >
                <td class="px-4 py-3 font-medium text-[var(--color-text)]">{{ proc.name }}</td>
                <td class="px-4 py-3 font-mono text-xs text-[var(--color-text-secondary)]">{{ proc.pid }}</td>
                <td class="px-4 py-3"><ProcessStatusBadge :status="proc.status" /></td>
                <td class="px-4 py-3 text-[var(--color-text-secondary)]">{{ formatUptime(proc.uptimeSeconds) }}</td>
                <td class="px-4 py-3 text-[var(--color-text-secondary)]">{{ proc.restartCount }}</td>
                <td class="px-4 py-3 text-[var(--color-text-secondary)]">{{ proc.cpuUsage.toFixed(1) }}%</td>
                <td class="px-4 py-3 text-[var(--color-text-secondary)]">{{ formatMemory(proc.memoryUsage) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Alerts Tab -->
      <div v-else-if="activeTab === 'alerts'">
        <div class="rounded-xl border border-dashed border-[var(--color-border)] py-12 text-center text-sm text-[var(--color-text-secondary)]">
          <div class="mb-3 flex justify-center">
            <svg class="h-10 w-10 text-[var(--color-text-secondary)]" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
            </svg>
          </div>
          No alerts for this server.
        </div>
      </div>
    </template>

    <!-- Not found -->
    <div v-else class="flex flex-col items-center justify-center py-20">
      <p class="text-[var(--color-text-secondary)]">Server not found.</p>
      <button @click="router.push('/servers')" class="mt-3 text-sm text-blue-400 hover:underline">
        Back to servers
      </button>
    </div>
  </div>
</template>
