import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { repositoriesApi } from '@/lib/api'
import type { Repository } from '@/types'

export const useRepositoriesStore = defineStore('repositories', () => {
  const repositories = ref<Repository[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const repositoryCount = computed(() => repositories.value.length)

  async function fetchRepositories() {
    loading.value = true
    error.value = null
    try {
      const { data } = await repositoriesApi.list()
      repositories.value = data
    } catch (err: unknown) {
      error.value =
        err instanceof Error ? err.message : 'Failed to fetch repositories'
    } finally {
      loading.value = false
    }
  }

  async function connectRepository(
    githubToken: string,
    owner: string,
    name: string
  ) {
    loading.value = true
    error.value = null
    try {
      const { data } = await repositoriesApi.connect({ githubToken, owner, name })
      repositories.value.push(data)
      return data
    } catch (err: unknown) {
      error.value =
        err instanceof Error ? err.message : 'Failed to connect repository'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function disconnectRepository(id: string) {
    loading.value = true
    error.value = null
    try {
      await repositoriesApi.disconnect(id)
      repositories.value = repositories.value.filter((r) => r.id !== id)
    } catch (err: unknown) {
      error.value =
        err instanceof Error ? err.message : 'Failed to disconnect repository'
      throw err
    } finally {
      loading.value = false
    }
  }

  return {
    repositories,
    loading,
    error,
    repositoryCount,
    fetchRepositories,
    connectRepository,
    disconnectRepository,
  }
})
