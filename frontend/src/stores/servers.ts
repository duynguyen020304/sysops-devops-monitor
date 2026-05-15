import { defineStore } from 'pinia'
import { ref } from 'vue'
import { serversApi } from '@/lib/api'
import type { DeployAgentResponse, Server } from '@/types'

export const useServersStore = defineStore('servers', () => {
  const servers = ref<Server[]>([])
  const selectedServer = ref<Server | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const deploying = ref(false)
  const deployResult = ref<DeployAgentResponse | null>(null)

  async function fetchServers() {
    loading.value = true
    error.value = null
    try {
      const { data } = await serversApi.list()
      servers.value = data
    } catch (err: unknown) {
      error.value =
        err instanceof Error ? err.message : 'Failed to fetch servers'
    } finally {
      loading.value = false
    }
  }

  async function selectServer(id: string) {
    loading.value = true
    error.value = null
    try {
      const { data } = await serversApi.getById(id)
      selectedServer.value = data
    } catch (err: unknown) {
      error.value =
        err instanceof Error ? err.message : 'Failed to fetch server'
    } finally {
      loading.value = false
    }
  }

  async function deleteServer(id: string) {
    loading.value = true
    error.value = null
    try {
      await serversApi.delete(id)
      servers.value = servers.value.filter((s) => s.id !== id)
      if (selectedServer.value?.id === id) {
        selectedServer.value = null
      }
    } catch (err: unknown) {
      error.value =
        err instanceof Error ? err.message : 'Failed to delete server'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function deployAgent(id: string) {
    deploying.value = true
    deployResult.value = null
    error.value = null
    try {
      const { data } = await serversApi.deployAgent(id)
      deployResult.value = data
      return data
    } catch (err: unknown) {
      error.value = err instanceof Error ? err.message : 'Failed to deploy agent'
      throw err
    } finally {
      deploying.value = false
    }
  }

  return {
    servers,
    selectedServer,
    loading,
    error,
    deploying,
    deployResult,
    fetchServers,
    selectServer,
    deleteServer,
    deployAgent,
  }
})
