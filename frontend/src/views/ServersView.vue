<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useServersStore } from '@/stores/servers'

const router = useRouter()
const store = useServersStore()

onMounted(() => {
  store.fetchServers()
})

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

async function deployAgent(serverId: string, event: MouseEvent) {
  event.stopPropagation()
  try {
    await store.deployAgent(serverId)
  } catch {
    // shown via store.error
  }
}

async function cleanupStaleServers() {
  try {
    await store.cleanupStaleServers()
  } catch {
    // shown via store.error
  }
}
</script>

<template>
  <div>
    <div class="mb-6 flex flex-wrap items-start justify-between gap-3">
      <div>
        <h2 class="text-xl font-bold text-[var(--color-text)]">Servers</h2>
        <p class="mt-1 text-sm text-[var(--color-text-secondary)]">
          Monitor connected servers and their health
        </p>
        <p v-if="store.cleanupMessage" class="mt-2 text-sm text-green-400">{{ store.cleanupMessage }}</p>
        <p v-if="store.error" class="mt-2 text-sm text-red-400">{{ store.error }}</p>
      </div>
      <button
        class="rounded-lg border border-[var(--color-border)] px-4 py-2 text-sm text-[var(--color-text-secondary)] transition-colors hover:bg-[var(--color-bg-tertiary)] disabled:opacity-50"
        :disabled="store.cleaning"
        @click="cleanupStaleServers"
      >
        {{ store.cleaning ? 'Cleaning...' : 'Cleanup stale duplicates' }}
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="h-8 w-8 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
    </div>

    <!-- Empty state -->
    <div
      v-else-if="store.servers.length === 0"
      class="flex flex-col items-center justify-center rounded-xl border border-dashed border-[var(--color-border)] py-16"
    >
      <div class="mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-[var(--color-bg-tertiary)]">
        <svg class="h-8 w-8 text-[var(--color-text-secondary)]" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
          <path stroke-linecap="round" stroke-linejoin="round" d="M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01" />
        </svg>
      </div>
      <h3 class="mb-1 text-lg font-medium text-[var(--color-text)]">No servers connected</h3>
      <p class="mb-4 max-w-md text-center text-sm text-[var(--color-text-secondary)]">
        Install the monitoring agent on your servers to start tracking health and performance metrics.
      </p>
      <div class="rounded-lg bg-[var(--color-bg-tertiary)] px-4 py-3 text-sm text-[var(--color-text-secondary)]">
        <code class="font-mono text-xs">curl -sSL https://agent.sysmonitor.dev/install.sh | bash</code>
      </div>
    </div>

    <!-- Server grid -->
    <div v-else class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <div
        v-for="server in store.servers"
        :key="server.id"
        class="group cursor-pointer rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-5 transition-all hover:border-blue-500/30 hover:shadow-lg"
        @click="router.push(`/servers/${server.id}`)"
      >
        <div class="mb-3 flex items-start justify-between">
          <div class="flex items-center gap-3">
            <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-500/10">
              <svg class="h-5 w-5 text-blue-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01" />
              </svg>
            </div>
            <div>
              <h3 class="text-sm font-semibold text-[var(--color-text)]">{{ server.hostname }}</h3>
              <p class="text-xs text-[var(--color-text-secondary)]">{{ server.ipAddress }}</p>
            </div>
          </div>
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

        <div class="space-y-2 text-xs text-[var(--color-text-secondary)]">
          <div class="flex items-center justify-between">
            <span>OS</span>
            <span class="font-medium text-[var(--color-text)]">{{ server.operatingSystem }}</span>
          </div>
          <div class="flex items-center justify-between">
            <span>Agent</span>
            <span class="font-medium text-[var(--color-text)]">v{{ server.agentVersion }}</span>
          </div>
          <div class="flex items-center justify-between">
            <span>Last heartbeat</span>
            <span class="font-medium text-[var(--color-text)]">{{ formatTimeAgo(server.lastHeartbeatAt) }}</span>
          </div>
        </div>
        <div class="mt-4 flex items-center gap-2 border-t border-[var(--color-border)] pt-4">
          <button
            class="rounded-lg bg-blue-500 px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-blue-600 disabled:cursor-not-allowed disabled:opacity-60"
            :disabled="store.deploying"
            @click="deployAgent(server.id, $event)"
          >
            {{ store.deploying ? 'Deploying...' : 'Deploy Agent' }}
          </button>
          <span v-if="store.deployResult" class="text-xs" :class="store.deployResult.success ? 'text-green-400' : 'text-red-400'">
            {{ store.deployResult.success ? 'Deploy ok' : 'Deploy failed' }}
          </span>
        </div>
      </div>
    </div>
  </div>
</template>
