<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { serversApi, systemdApi } from '@/lib/api'
import type { Server, SystemdService } from '@/types'

const router = useRouter()

const servers = ref<Server[]>([])
const allServices = ref<(SystemdService & { serverHostname: string; serverId: string })[]>([])
const loading = ref(true)
const refreshing = ref(false)
const refreshMessage = ref('')
const serverFilter = ref('')
const stateFilter = ref('')

async function loadServices(): Promise<void> {
  loading.value = true
  try {
    const { data } = await serversApi.list()
    servers.value = data

    const servicePromises = data.map(async (server) => {
      try {
        const { data: svcs } = await systemdApi.listByServer(server.id)
        return svcs.map((p) => ({
          ...p,
          serverHostname: server.hostname,
          serverId: server.id,
        }))
      } catch {
        return []
      }
    })

    const results = await Promise.all(servicePromises)
    allServices.value = results.flat()
  } catch {
    // handled by UI
  } finally {
    loading.value = false
  }
}

async function forceRefresh(): Promise<void> {
  const targetServers = serverFilter.value ? servers.value.filter((s) => s.id === serverFilter.value) : servers.value
  if (targetServers.length === 0) return
  refreshing.value = true
  refreshMessage.value = ''
  try {
    await Promise.all(targetServers.map((server) => systemdApi.refreshServer(server.id)))
    refreshMessage.value = `Refresh requested for ${targetServers.length} server(s). Agent will snapshot on next poll.`
  } catch {
    refreshMessage.value = 'Refresh request failed.'
  } finally {
    refreshing.value = false
  }
}

onMounted(loadServices)

const filteredServices = computed(() => {
  return allServices.value.filter((p) => {
    if (serverFilter.value && p.serverId !== serverFilter.value) return false
    if (stateFilter.value && p.activeState !== stateFilter.value) return false
    return true
  })
})

const states = computed(() => {
  const set = new Set(allServices.value.map((p) => p.activeState))
  return Array.from(set).sort()
})

function formatMemory(bytes: number): string {
  if (bytes >= 1073741824) return `${(bytes / 1073741824).toFixed(1)} GB`
  if (bytes >= 1048576) return `${(bytes / 1048576).toFixed(0)} MB`
  return `${(bytes / 1024).toFixed(0)} KB`
}
</script>

<template>
  <div>
    <div class="mb-6">
      <h2 class="text-xl font-bold text-[var(--color-text)]">Systemd Services</h2>
      <p class="mt-1 text-sm text-[var(--color-text-secondary)]">
        Monitor systemd services across all connected servers
      </p>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center justify-center py-20">
      <div class="h-8 w-8 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
    </div>

    <template v-else>
      <!-- Filters -->
      <div class="mb-4 flex flex-col gap-3 sm:flex-row">
        <select
          v-model="serverFilter"
          class="rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] px-3 py-2 text-sm text-[var(--color-text)] outline-none focus:border-blue-500"
        >
          <option value="">All servers</option>
          <option v-for="s in servers" :key="s.id" :value="s.id">{{ s.hostname }}</option>
        </select>
        <select
          v-model="stateFilter"
          class="rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] px-3 py-2 text-sm text-[var(--color-text)] outline-none focus:border-blue-500"
        >
          <option value="">All states</option>
          <option v-for="s in states" :key="s" :value="s">{{ s }}</option>
        </select>
        <button
          type="button"
          class="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
          :disabled="refreshing"
          @click="forceRefresh"
        >
          {{ refreshing ? 'Requesting...' : 'Force refresh' }}
        </button>
        <button
          type="button"
          class="rounded-lg border border-[var(--color-border)] px-4 py-2 text-sm text-[var(--color-text)] disabled:opacity-50"
          :disabled="loading"
          @click="loadServices"
        >
          Reload UI
        </button>
      </div>

      <div v-if="refreshMessage" class="mb-4 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] px-4 py-3 text-sm text-[var(--color-text-secondary)]">
        {{ refreshMessage }}
      </div>

      <!-- Empty -->
      <div
        v-if="filteredServices.length === 0"
        class="rounded-xl border border-dashed border-[var(--color-border)] py-12 text-center text-sm text-[var(--color-text-secondary)]"
      >
        No systemd services found.
      </div>

      <!-- Table -->
      <div v-else class="overflow-hidden rounded-xl border border-[var(--color-border)]">
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-[var(--color-border)] bg-[var(--color-bg-tertiary)]">
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">Server</th>
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">Name</th>
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">Main PID</th>
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">Status</th>
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">Sub State</th>
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">Restarts</th>
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">Load</th>
                <th class="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">Memory</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-[var(--color-border)]">
              <tr
                v-for="svc in filteredServices"
                :key="svc.id"
                class="cursor-pointer transition-colors odd:bg-[var(--color-bg-secondary)] even:bg-[var(--color-bg-secondary)]/50 hover:bg-[var(--color-bg-tertiary)]/50"
                @click="router.push(`/systemd/${svc.id}`)"
              >
                <td class="px-4 py-3 text-[var(--color-text-secondary)]">{{ svc.serverHostname }}</td>
                <td class="px-4 py-3 font-medium text-[var(--color-text)]">{{ svc.name }}</td>
                <td class="px-4 py-3 font-mono text-xs text-[var(--color-text-secondary)]">{{ svc.mainPid || '-' }}</td>
                <td class="px-4 py-3 text-[var(--color-text-secondary)]">{{ svc.activeState }}</td>
                <td class="px-4 py-3 text-[var(--color-text-secondary)]">{{ svc.subState }}</td>
                <td class="px-4 py-3 text-[var(--color-text-secondary)]">{{ svc.restartCount ?? 0 }}</td>
                <td class="px-4 py-3 text-[var(--color-text-secondary)]">{{ svc.loadState }}</td>
                <td class="px-4 py-3 text-[var(--color-text-secondary)]">{{ svc.memoryCurrent ? formatMemory(svc.memoryCurrent) : '-' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </template>
  </div>
</template>
