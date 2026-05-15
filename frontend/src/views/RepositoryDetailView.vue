<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { repositoriesApi } from '@/lib/api'
import type { Repository, WorkflowRun, WorkflowLog, RepositoryStats, PagedResult } from '@/types'
import WorkflowStatusChart from '@/components/charts/WorkflowStatusChart.vue'
import LogViewer from '@/components/logs/LogViewer.vue'

const route = useRoute()
const router = useRouter()
const repoId = computed(() => route.params.id as string)

const repo = ref<Repository | null>(null)
const workflows = ref<WorkflowRun[]>([])
const stats = ref<RepositoryStats | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const currentPage = ref(1)
const totalPages = ref(1)
const totalRuns = ref(0)
const pageSize = 20

// Log viewer state
const showLogs = ref(false)
const logsLoading = ref(false)
const selectedRun = ref<WorkflowRun | null>(null)
const logs = ref<WorkflowLog[]>([])

// Time range filter
const timeRange = ref('all')

async function loadRepoData() {
  loading.value = true
  error.value = null
  try {
    const [repoRes, statsRes, workflowsRes] = await Promise.all([
      repositoriesApi.getById(repoId.value),
      repositoriesApi.getStats(repoId.value),
      repositoriesApi.getWorkflows(repoId.value, currentPage.value, pageSize),
    ])
    repo.value = repoRes.data
    stats.value = statsRes.data
    const paged: PagedResult<WorkflowRun> = workflowsRes.data
    workflows.value = paged.items
    totalPages.value = Math.ceil(paged.total / paged.pageSize)
    totalRuns.value = paged.total
  } catch {
    error.value = 'Failed to load repository data'
  } finally {
    loading.value = false
  }
}

async function loadPage(page: number) {
  currentPage.value = page
  try {
    const { data } = await repositoriesApi.getWorkflows(repoId.value, page, pageSize)
    workflows.value = data.items
    totalPages.value = Math.ceil(data.total / data.pageSize)
    totalRuns.value = data.total
  } catch {
    // keep existing data
  }
}

async function viewLogs(run: WorkflowRun) {
  selectedRun.value = run
  showLogs.value = true
  logsLoading.value = true
  try {
    const { data } = await repositoriesApi.getWorkflowLogs(repoId.value, run.id)
    logs.value = data
  } catch {
    logs.value = []
  } finally {
    logsLoading.value = false
  }
}

function closeLogs() {
  showLogs.value = false
  selectedRun.value = null
  logs.value = []
}

function statusBadgeClass(conclusion: string): string {
  switch (conclusion) {
    case 'success':
      return 'bg-green-500/10 text-green-400 border-green-500/20'
    case 'failure':
      return 'bg-red-500/10 text-red-400 border-red-500/20'
    case 'cancelled':
      return 'bg-yellow-500/10 text-yellow-400 border-yellow-500/20'
    case 'in_progress':
    case 'queued':
      return 'bg-blue-500/10 text-blue-400 border-blue-500/20'
    default:
      return 'bg-gray-500/10 text-gray-400 border-gray-500/20'
  }
}

function statusLabel(conclusion: string): string {
  switch (conclusion) {
    case 'success':
      return 'Success'
    case 'failure':
      return 'Failed'
    case 'cancelled':
      return 'Cancelled'
    case 'in_progress':
      return 'In Progress'
    case 'queued':
      return 'Queued'
    default:
      return conclusion || 'Unknown'
  }
}

function formatDuration(seconds: number | null): string {
  if (seconds === null || seconds === undefined) return '--'
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return m > 0 ? `${m}m ${s}s` : `${s}s`
}

function formatTimeAgo(dateStr: string | null): string {
  if (!dateStr) return '--'
  const date = new Date(dateStr)
  const now = new Date()
  const diff = Math.floor((now.getTime() - date.getTime()) / 1000)
  if (diff < 60) return 'just now'
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`
  if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`
  return `${Math.floor(diff / 86400)}d ago`
}

function truncateSha(sha: string): string {
  return sha?.slice(0, 7) || '--'
}

onMounted(loadRepoData)
</script>

<template>
  <div>
    <!-- Back button + header -->
    <div class="mb-6">
      <button
        @click="router.push('/repositories')"
        class="mb-4 inline-flex items-center gap-1.5 text-sm text-[var(--color-text-secondary)] transition-colors hover:text-[var(--color-text)]"
      >
        <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M15 19l-7-7 7-7" />
        </svg>
        Back to Repositories
      </button>

      <!-- Loading -->
      <div v-if="loading" class="flex items-center justify-center py-20">
        <div class="h-8 w-8 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
      </div>

      <!-- Error -->
      <div
        v-else-if="error"
        class="rounded-lg border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm text-red-400"
      >
        {{ error }}
      </div>

      <template v-else-if="repo">
        <!-- Repo info -->
        <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 class="text-xl font-bold text-[var(--color-text)]">{{ repo.name }}</h2>
            <div class="mt-1 flex flex-wrap items-center gap-3 text-sm text-[var(--color-text-secondary)]">
              <span class="flex items-center gap-1.5">
                <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                </svg>
                {{ repo.owner }}
              </span>
              <span class="flex items-center gap-1.5">
                <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z" />
                </svg>
                {{ repo.defaultBranch }}
              </span>
              <span
                :class="[
                  repo.visibility === 'public'
                    ? 'bg-green-500/10 text-green-400'
                    : 'bg-yellow-500/10 text-yellow-400',
                  'rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase',
                ]"
              >
                {{ repo.visibility }}
              </span>
            </div>
          </div>

          <!-- Time range filter -->
          <select
            v-model="timeRange"
            class="rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] px-3 py-2 text-sm text-[var(--color-text)] outline-none focus:border-blue-500"
          >
            <option value="all">All time</option>
            <option value="24h">Last 24 hours</option>
            <option value="7d">Last 7 days</option>
            <option value="30d">Last 30 days</option>
          </select>
        </div>

        <!-- Stats cards -->
        <div v-if="stats" class="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
          <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
            <p class="text-xs text-[var(--color-text-secondary)]">Total Runs</p>
            <p class="mt-1 text-2xl font-bold text-[var(--color-text)]">{{ stats.totalRuns }}</p>
          </div>
          <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
            <p class="text-xs text-[var(--color-text-secondary)]">Successful</p>
            <p class="mt-1 text-2xl font-bold text-green-400">{{ stats.successfulRuns }}</p>
          </div>
          <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
            <p class="text-xs text-[var(--color-text-secondary)]">Failed</p>
            <p class="mt-1 text-2xl font-bold text-red-400">{{ stats.failedRuns }}</p>
          </div>
          <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
            <p class="text-xs text-[var(--color-text-secondary)]">Failure Rate</p>
            <p class="mt-1 text-2xl font-bold text-orange-400">{{ (stats.failureRate * 100).toFixed(1) }}%</p>
          </div>
        </div>

        <!-- Chart + workflows -->
        <div class="mt-6 grid gap-6 lg:grid-cols-[320px_1fr]">
          <!-- Chart -->
          <WorkflowStatusChart v-if="stats" :stats="stats" />

          <!-- Workflow runs table -->
          <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)]">
            <div class="flex items-center justify-between border-b border-[var(--color-border)] px-5 py-3">
              <h3 class="text-sm font-semibold text-[var(--color-text)]">
                Workflow Runs
                <span class="ml-1 text-xs font-normal text-[var(--color-text-secondary)]">
                  ({{ totalRuns }} total)
                </span>
              </h3>
            </div>

            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead>
                  <tr class="border-b border-[var(--color-border)] text-left text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                    <th class="px-5 py-3">Workflow</th>
                    <th class="px-5 py-3">Branch</th>
                    <th class="px-5 py-3">Commit</th>
                    <th class="px-5 py-3">Status</th>
                    <th class="px-5 py-3">Actor</th>
                    <th class="px-5 py-3">Duration</th>
                    <th class="px-5 py-3">Time</th>
                    <th class="px-5 py-3"></th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-if="workflows.length === 0">
                    <td colspan="8" class="px-5 py-12 text-center text-[var(--color-text-secondary)]">
                      No workflow runs found
                    </td>
                  </tr>
                  <tr
                    v-for="run in workflows"
                    :key="run.id"
                    class="border-b border-[var(--color-border)] transition-colors hover:bg-[var(--color-bg-tertiary)]/50 cursor-pointer"
                    @click="viewLogs(run)"
                  >
                    <td class="px-5 py-3 font-medium text-[var(--color-text)]">
                      {{ run.workflowName }}
                    </td>
                    <td class="px-5 py-3">
                      <span class="rounded bg-[var(--color-bg-tertiary)] px-2 py-0.5 font-mono text-xs text-[var(--color-text)]">
                        {{ run.branch }}
                      </span>
                    </td>
                    <td class="px-5 py-3">
                      <a
                        :href="run.htmlUrl"
                        target="_blank"
                        rel="noopener"
                        class="font-mono text-xs text-blue-400 hover:underline"
                        @click.stop
                      >
                        {{ truncateSha(run.commitSha) }}
                      </a>
                    </td>
                    <td class="px-5 py-3">
                      <span
                        :class="[
                          statusBadgeClass(run.conclusion),
                          'inline-flex items-center rounded-full border px-2 py-0.5 text-[11px] font-semibold',
                        ]"
                      >
                        {{ statusLabel(run.conclusion) }}
                      </span>
                    </td>
                    <td class="px-5 py-3 text-[var(--color-text-secondary)]">{{ run.actor }}</td>
                    <td class="px-5 py-3 font-mono text-xs text-[var(--color-text-secondary)]">
                      {{ formatDuration(run.durationSeconds) }}
                    </td>
                    <td class="px-5 py-3 text-xs text-[var(--color-text-secondary)]">
                      {{ formatTimeAgo(run.completedAt || run.startedAt) }}
                    </td>
                    <td class="px-5 py-3">
                      <button
                        class="rounded p-1 text-[var(--color-text-secondary)] transition-colors hover:bg-[var(--color-bg-tertiary)] hover:text-[var(--color-text)]"
                        title="View logs"
                      >
                        <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                          <path stroke-linecap="round" stroke-linejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                        </svg>
                      </button>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- Pagination -->
            <div
              v-if="totalPages > 1"
              class="flex items-center justify-between border-t border-[var(--color-border)] px-5 py-3"
            >
              <span class="text-xs text-[var(--color-text-secondary)]">
                Page {{ currentPage }} of {{ totalPages }}
              </span>
              <div class="flex gap-2">
                <button
                  :disabled="currentPage <= 1"
                  @click="loadPage(currentPage - 1)"
                  class="rounded-lg border border-[var(--color-border)] px-3 py-1.5 text-xs font-medium text-[var(--color-text)] transition-colors hover:bg-[var(--color-bg-tertiary)] disabled:opacity-40"
                >
                  Previous
                </button>
                <button
                  :disabled="currentPage >= totalPages"
                  @click="loadPage(currentPage + 1)"
                  class="rounded-lg border border-[var(--color-border)] px-3 py-1.5 text-xs font-medium text-[var(--color-text)] transition-colors hover:bg-[var(--color-bg-tertiary)] disabled:opacity-40"
                >
                  Next
                </button>
              </div>
            </div>
          </div>
        </div>
      </template>
    </div>

    <!-- Log viewer slide-over panel -->
    <Teleport to="body">
      <div v-if="showLogs" class="fixed inset-0 z-50 flex justify-end bg-black/50" @click.self="closeLogs">
        <div class="flex h-full w-full max-w-3xl flex-col bg-[var(--color-bg)] shadow-2xl">
          <div class="flex items-center justify-between border-b border-[var(--color-border)] px-5 py-4">
            <div>
              <h3 class="text-sm font-semibold text-[var(--color-text)]">
                {{ selectedRun?.workflowName }}
              </h3>
              <p class="text-xs text-[var(--color-text-secondary)]">
                {{ selectedRun?.branch }} -- {{ truncateSha(selectedRun?.commitSha || '') }}
              </p>
            </div>
            <button
              @click="closeLogs"
              class="rounded-lg p-2 text-[var(--color-text-secondary)] transition-colors hover:bg-[var(--color-bg-tertiary)]"
            >
              <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <div v-if="logsLoading" class="flex flex-1 items-center justify-center">
            <div class="h-8 w-8 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
          </div>

          <div v-else class="flex-1 overflow-hidden p-4">
            <LogViewer :logs="logs" />
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
