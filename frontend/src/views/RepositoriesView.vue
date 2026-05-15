<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useRepositoriesStore } from '@/stores/repositories'
import { useMediaQuery } from '@/composables/useMediaQuery'
import EmptyState from '@/components/empty/EmptyState.vue'

const router = useRouter()
const store = useRepositoriesStore()
const { isMobile, isTablet } = useMediaQuery()

const showConnectDialog = ref(false)
const connectForm = ref({ githubToken: '', owner: '', name: '' })
const connectError = ref<string | null>(null)
const connecting = ref(false)

onMounted(() => {
  store.fetchRepositories()
})

function openRepo(id: string) {
  router.push(`/repositories/${id}`)
}

async function handleConnect() {
  connectError.value = null
  connecting.value = true
  try {
    await store.connectRepository(
      connectForm.value.githubToken,
      connectForm.value.owner,
      connectForm.value.name
    )
    showConnectDialog.value = false
    connectForm.value = { githubToken: '', owner: '', name: '' }
  } catch {
    connectError.value = 'Failed to connect repository. Check your credentials.'
  } finally {
    connecting.value = false
  }
}

async function handleDisconnect(id: string, e: Event) {
  e.stopPropagation()
  if (!confirm('Disconnect this repository?')) return
  try {
    await store.disconnectRepository(id)
  } catch {
    // error is set in store
  }
}

function visibilityBadge(visibility: string): string {
  return visibility === 'public'
    ? 'bg-green-500/10 text-green-400 border-green-500/20'
    : 'bg-yellow-500/10 text-yellow-400 border-yellow-500/20'
}
</script>

<template>
  <div>
    <!-- Header -->
    <div class="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h2 class="text-xl font-bold text-[var(--color-text)]">Repositories</h2>
        <p class="mt-1 text-sm text-[var(--color-text-secondary)]">
          Manage connected GitHub repositories
        </p>
      </div>
      <button
        @click="showConnectDialog = true"
        class="inline-flex items-center gap-2 rounded-lg bg-blue-500 px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-blue-600"
      >
        <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 4v16m8-8H4" />
        </svg>
        Connect Repository
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.repositories.length === 0" class="flex items-center justify-center py-20">
      <div class="h-8 w-8 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
    </div>

    <!-- Error -->
    <div
      v-else-if="store.error"
      class="mb-6 rounded-lg border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm text-red-400"
    >
      {{ store.error }}
    </div>

    <!-- Empty state -->
    <div
      v-else-if="store.repositories.length === 0 && !store.loading"
      class="flex flex-col items-center justify-center rounded-xl border border-dashed border-[var(--color-border)] py-20"
    >
      <EmptyState
        title="No repositories connected"
        description="Connect a GitHub repository to start monitoring workflow runs"
        action-label="Connect Repository"
        @action="showConnectDialog = true"
      />
    </div>

    <!-- Repository cards -->
    <div
      v-else
      :class="[
        'grid gap-4',
        isMobile ? 'grid-cols-1' : isTablet ? 'grid-cols-2' : 'grid-cols-3',
      ]"
    >
      <div
        v-for="repo in store.repositories"
        :key="repo.id"
        class="group cursor-pointer rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-5 transition-all hover:border-blue-500/30 hover:shadow-lg"
        @click="openRepo(repo.id)"
      >
        <div class="mb-3 flex items-start justify-between">
          <div class="flex items-center gap-2.5">
            <div class="flex h-9 w-9 items-center justify-center rounded-lg bg-[var(--color-bg-tertiary)]">
              <svg class="h-5 w-5 text-[var(--color-text-secondary)]" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
              </svg>
            </div>
            <div>
              <h3 class="text-sm font-semibold text-[var(--color-text)]">{{ repo.name }}</h3>
              <p class="text-xs text-[var(--color-text-secondary)]">{{ repo.owner }}</p>
            </div>
          </div>
          <button
            @click="handleDisconnect(repo.id, $event)"
            class="rounded-lg p-1.5 text-[var(--color-text-secondary)] opacity-0 transition-all hover:bg-red-500/10 hover:text-red-400 group-hover:opacity-100"
            title="Disconnect"
          >
            <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
            </svg>
          </button>
        </div>

        <div class="space-y-2">
          <div class="flex items-center justify-between text-xs">
            <span class="text-[var(--color-text-secondary)]">Default branch</span>
            <span class="font-mono text-[var(--color-text)]">{{ repo.defaultBranch }}</span>
          </div>
          <div class="flex items-center justify-between text-xs">
            <span class="text-[var(--color-text-secondary)]">Visibility</span>
            <span
              :class="[
                visibilityBadge(repo.visibility),
                'rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase',
              ]"
            >
              {{ repo.visibility }}
            </span>
          </div>
        </div>

        <div class="mt-4 flex items-center justify-end text-xs text-blue-400 opacity-0 transition-opacity group-hover:opacity-100">
          View workflows
          <svg class="ml-1 h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 5l7 7-7 7" />
          </svg>
        </div>
      </div>
    </div>

    <!-- Connect dialog -->
    <Teleport to="body">
      <div
        v-if="showConnectDialog"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4"
        @click.self="showConnectDialog = false"
      >
        <div class="w-full max-w-md rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-6 shadow-2xl">
          <h3 class="mb-1 text-lg font-semibold text-[var(--color-text)]">Connect Repository</h3>
          <p class="mb-5 text-sm text-[var(--color-text-secondary)]">
            Link a GitHub repository to monitor its workflow runs.
          </p>

          <div
            v-if="connectError"
            class="mb-4 rounded-lg border border-red-500/20 bg-red-500/10 px-3 py-2 text-sm text-red-400"
          >
            {{ connectError }}
          </div>

          <form @submit.prevent="handleConnect" class="space-y-4">
            <div>
              <label class="mb-1.5 block text-sm font-medium text-[var(--color-text)]">GitHub Token</label>
              <input
                v-model="connectForm.githubToken"
                type="password"
                required
                placeholder="ghp_xxxxxxxxxxxx"
                class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-2.5 text-sm text-[var(--color-text)] placeholder-[var(--color-text-secondary)] outline-none focus:border-blue-500"
              />
            </div>
            <div>
              <label class="mb-1.5 block text-sm font-medium text-[var(--color-text)]">Owner</label>
              <input
                v-model="connectForm.owner"
                type="text"
                required
                placeholder="e.g. octocat"
                class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-2.5 text-sm text-[var(--color-text)] placeholder-[var(--color-text-secondary)] outline-none focus:border-blue-500"
              />
            </div>
            <div>
              <label class="mb-1.5 block text-sm font-medium text-[var(--color-text)]">Repository Name</label>
              <input
                v-model="connectForm.name"
                type="text"
                required
                placeholder="e.g. hello-world"
                class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-2.5 text-sm text-[var(--color-text)] placeholder-[var(--color-text-secondary)] outline-none focus:border-blue-500"
              />
            </div>

            <div class="flex justify-end gap-3 pt-2">
              <button
                type="button"
                @click="showConnectDialog = false"
                class="rounded-lg border border-[var(--color-border)] px-4 py-2.5 text-sm font-medium text-[var(--color-text)] transition-colors hover:bg-[var(--color-bg-tertiary)]"
              >
                Cancel
              </button>
              <button
                type="submit"
                :disabled="connecting"
                class="inline-flex items-center gap-2 rounded-lg bg-blue-500 px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-blue-600 disabled:opacity-50"
              >
                <div
                  v-if="connecting"
                  class="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent"
                />
                Connect
              </button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>
  </div>
</template>
