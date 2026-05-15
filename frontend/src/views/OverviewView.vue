<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useRepositoriesStore } from '@/stores/repositories'
import { repositoriesApi } from '@/lib/api'
import { useMediaQuery } from '@/composables/useMediaQuery'
import type { WorkflowRun, RepositoryStats } from '@/types'

const router = useRouter()
const repoStore = useRepositoriesStore()
const { isMobile, isTablet } = useMediaQuery()

const recentFailures = ref<WorkflowRun[]>([])
const repoStats = ref<Map<string, RepositoryStats>>(new Map())
const loading = ref(true)

onMounted(async () => {
  loading.value = true
  await repoStore.fetchRepositories()

  // Fetch stats and recent failures for each repo
  const statsPromises = repoStore.repositories.map(async (repo) => {
    try {
      const [statsRes, workflowsRes] = await Promise.all([
        repositoriesApi.getStats(repo.id),
        repositoriesApi.getWorkflows(repo.id, 1, 50),
      ])
      repoStats.value.set(repo.id, statsRes.data)

      // Collect failures
      const failures = workflowsRes.data.items.filter(
        (w: WorkflowRun) => w.conclusion === 'failure'
      )
      recentFailures.value.push(...failures)
    } catch {
      // skip repos that fail
    }
  })

  await Promise.all(statsPromises)

  // Sort failures by time, take top 5
  recentFailures.value.sort((a, b) => {
    const dateA = new Date(a.completedAt || a.startedAt || 0).getTime()
    const dateB = new Date(b.completedAt || b.startedAt || 0).getTime()
    return dateB - dateA
  })
  recentFailures.value = recentFailures.value.slice(0, 5)

  loading.value = false
})

function getTotalRuns(): number {
  let total = 0
  repoStats.value.forEach((s) => (total += s.totalRuns))
  return total
}

function getTotalFailures(): number {
  let total = 0
  repoStats.value.forEach((s) => (total += s.failedRuns))
  return total
}

function getAverageFailureRate(): number {
  if (repoStats.value.size === 0) return 0
  let total = 0
  repoStats.value.forEach((s) => (total += s.failureRate))
  return total / repoStats.value.size
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

const summaryCards = [
  {
    title: 'Repositories',
    icon: 'M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z',
    color: 'text-blue-400',
    bgColor: 'bg-blue-500/10',
    getValue: () => repoStore.repositoryCount,
    link: '/repositories',
  },
  {
    title: 'Total Runs',
    icon: 'M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z',
    color: 'text-green-400',
    bgColor: 'bg-green-500/10',
    getValue: () => getTotalRuns(),
    link: '/repositories',
  },
  {
    title: 'Failed Runs',
    icon: 'M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z',
    color: 'text-red-400',
    bgColor: 'bg-red-500/10',
    getValue: () => getTotalFailures(),
    link: '/repositories',
  },
  {
    title: 'Failure Rate',
    icon: 'M13 17h8m0 0V9m0 8l-8-8-4 4-6-6',
    color: 'text-orange-400',
    bgColor: 'bg-orange-500/10',
    getValue: () => `${getAverageFailureRate().toFixed(1)}%`,
    link: '/repositories',
  },
]
</script>

<template>
  <div>
    <div class="mb-6">
      <h2 class="text-xl font-bold text-[var(--color-text)]">Overview</h2>
      <p class="mt-1 text-sm text-[var(--color-text-secondary)]">Your infrastructure at a glance</p>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center justify-center py-20">
      <div class="h-8 w-8 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
    </div>

    <template v-else>
      <!-- Summary cards -->
      <div
        :class="[
          'mb-6 grid gap-4',
          isMobile ? 'grid-cols-1' : isTablet ? 'grid-cols-2' : 'grid-cols-4',
        ]"
      >
        <div
          v-for="card in summaryCards"
          :key="card.title"
          class="group cursor-pointer rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-5 transition-all hover:border-blue-500/30 hover:shadow-lg"
          @click="router.push(card.link)"
        >
          <div class="mb-3 flex items-center gap-3">
            <div :class="['flex h-10 w-10 items-center justify-center rounded-lg', card.bgColor]">
              <svg class="h-5 w-5" :class="card.color" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" :d="card.icon" />
              </svg>
            </div>
            <h3 class="text-sm font-medium text-[var(--color-text-secondary)]">{{ card.title }}</h3>
          </div>
          <p class="text-2xl font-bold text-[var(--color-text)]">{{ card.getValue() }}</p>
        </div>
      </div>

      <!-- Two-column layout: repos + failures -->
      <div class="grid gap-6 lg:grid-cols-2">
        <!-- Connected Repositories -->
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)]">
          <div class="flex items-center justify-between border-b border-[var(--color-border)] px-5 py-3">
            <h3 class="text-sm font-semibold text-[var(--color-text)]">Connected Repositories</h3>
            <button
              @click="router.push('/repositories')"
              class="text-xs font-medium text-blue-400 hover:text-blue-300"
            >
              View all
            </button>
          </div>
          <div v-if="repoStore.repositories.length === 0" class="px-5 py-8 text-center text-sm text-[var(--color-text-secondary)]">
            No repositories connected yet.
            <button
              @click="router.push('/repositories')"
              class="ml-1 text-blue-400 hover:underline"
            >
              Connect one now
            </button>
          </div>
          <ul v-else class="divide-y divide-[var(--color-border)]">
            <li
              v-for="repo in repoStore.repositories.slice(0, 5)"
              :key="repo.id"
              class="flex cursor-pointer items-center justify-between px-5 py-3 transition-colors hover:bg-[var(--color-bg-tertiary)]/50"
              @click="router.push(`/repositories/${repo.id}`)"
            >
              <div class="flex items-center gap-3">
                <div class="flex h-8 w-8 items-center justify-center rounded-lg bg-[var(--color-bg-tertiary)]">
                  <svg class="h-4 w-4 text-[var(--color-text-secondary)]" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
                  </svg>
                </div>
                <div>
                  <p class="text-sm font-medium text-[var(--color-text)]">{{ repo.fullName || repo.name }}</p>
                  <p class="text-xs text-[var(--color-text-secondary)]">{{ repo.defaultBranch }}</p>
                </div>
              </div>
              <svg class="h-4 w-4 text-[var(--color-text-secondary)]" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M9 5l7 7-7 7" />
              </svg>
            </li>
          </ul>
        </div>

        <!-- Recent Failures -->
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)]">
          <div class="flex items-center justify-between border-b border-[var(--color-border)] px-5 py-3">
            <h3 class="text-sm font-semibold text-[var(--color-text)]">Recent Failures</h3>
            <button
              @click="router.push('/repositories')"
              class="text-xs font-medium text-blue-400 hover:text-blue-300"
            >
              View all
            </button>
          </div>
          <div v-if="recentFailures.length === 0" class="px-5 py-8 text-center text-sm text-[var(--color-text-secondary)]">
            No recent failures. Everything looks good!
          </div>
          <ul v-else class="divide-y divide-[var(--color-border)]">
            <li
              v-for="failure in recentFailures"
              :key="failure.id"
              class="px-5 py-3"
            >
              <div class="flex items-start justify-between">
                <div>
                  <p class="text-sm font-medium text-[var(--color-text)]">{{ failure.workflowName }}</p>
                  <p class="mt-0.5 text-xs text-[var(--color-text-secondary)]">
                    <span class="rounded bg-[var(--color-bg-tertiary)] px-1.5 py-0.5 font-mono">{{ failure.branch }}</span>
                    <span class="mx-1.5">--</span>
                    {{ failure.actor }}
                  </p>
                </div>
                <span class="text-xs text-[var(--color-text-secondary)]">
                  {{ formatTimeAgo(failure.completedAt) }}
                </span>
              </div>
            </li>
          </ul>
        </div>
      </div>
    </template>
  </div>
</template>
