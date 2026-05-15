import { defineStore } from 'pinia'
import { ref } from 'vue'
import { pm2Api } from '@/lib/api'
import type { PM2Process, PM2Log } from '@/types'

export const usePm2Store = defineStore('pm2', () => {
  const processes = ref<PM2Process[]>([])
  const selectedProcess = ref<PM2Process | null>(null)
  const logs = ref<PM2Log[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetchProcesses(serverId: string) {
    loading.value = true
    error.value = null
    try {
      const { data } = await pm2Api.listByServer(serverId)
      processes.value = data
    } catch (err: unknown) {
      error.value =
        err instanceof Error ? err.message : 'Failed to fetch PM2 processes'
    } finally {
      loading.value = false
    }
  }

  async function selectProcess(processId: string) {
    loading.value = true
    error.value = null
    try {
      const { data } = await pm2Api.getById(processId)
      selectedProcess.value = data
    } catch (err: unknown) {
      error.value =
        err instanceof Error ? err.message : 'Failed to fetch PM2 process'
    } finally {
      loading.value = false
    }
  }

  async function fetchLogs(processId: string, limit = 100) {
    loading.value = true
    error.value = null
    try {
      const { data } = await pm2Api.getLogs(processId, limit)
      logs.value = data
    } catch (err: unknown) {
      error.value =
        err instanceof Error ? err.message : 'Failed to fetch PM2 logs'
    } finally {
      loading.value = false
    }
  }

  return {
    processes,
    selectedProcess,
    logs,
    loading,
    error,
    fetchProcesses,
    selectProcess,
    fetchLogs,
  }
})
