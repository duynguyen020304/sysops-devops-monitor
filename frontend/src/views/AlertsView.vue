<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useAlertsStore } from '@/stores/alerts'
import { AlertSeverity, AlertStatus, AlertSourceType } from '@/types'
import type { AlertRule, CreateAlertRuleRequest } from '@/types'
import AlertCard from '@/components/alerts/AlertCard.vue'
import AlertRuleForm from '@/components/alerts/AlertRuleForm.vue'
import EmptyState from '@/components/empty/EmptyState.vue'

const store = useAlertsStore()
const activeTab = ref<'active' | 'resolved' | 'rules'>('active')
const showRuleForm = ref(false)
const editingRule = ref<AlertRule | null>(null)

const filteredAlerts = computed(() => store.alerts.items)

async function loadAlerts() {
  if (activeTab.value !== 'rules') {
    store.clearFilters()
    if (activeTab.value === 'resolved') {
      store.setFilter('status', AlertStatus.Resolved)
    }
    await store.fetchAlerts()
  }
}

async function loadRules() {
  if (activeTab.value === 'rules') {
    await store.fetchAlertRules()
  }
}

function switchTab(tab: 'active' | 'resolved' | 'rules') {
  activeTab.value = tab
  if (tab === 'rules') {
    loadRules()
  } else {
    loadAlerts()
  }
}

async function handleAcknowledge(id: string) {
  await store.acknowledgeAlert(id)
}

async function handleResolve(id: string) {
  await store.resolveAlert(id)
}

async function handleMute(id: string) {
  await store.muteAlert(id)
}

async function handleRuleSubmit(data: CreateAlertRuleRequest) {
  if (editingRule.value) {
    await store.updateRule(editingRule.value.id, data)
  } else {
    await store.createRule(data)
  }
  showRuleForm.value = false
  editingRule.value = null
  await store.fetchAlertRules()
}

async function handleDeleteRule(id: string) {
  await store.deleteRule(id)
}

function openCreateRule() {
  editingRule.value = null
  showRuleForm.value = true
}

function openEditRule(rule: AlertRule) {
  editingRule.value = rule
  showRuleForm.value = true
}

const severityFilter = ref<string | undefined>(undefined)
const sourceFilter = ref<string | undefined>(undefined)

function applyFilters() {
  store.clearFilters()
  if (severityFilter.value) store.setFilter('severity', severityFilter.value)
  if (sourceFilter.value) store.setFilter('sourceType', sourceFilter.value)
  if (activeTab.value === 'resolved') store.setFilter('status', AlertStatus.Resolved)
  store.fetchAlerts()
}

onMounted(() => {
  loadAlerts()
})
</script>

<template>
  <div class="mx-auto max-w-6xl space-y-6 p-4 md:p-6">
    <!-- Header -->
    <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 class="text-2xl font-bold text-[var(--color-text)]">Alert Center</h1>
        <p class="mt-1 text-sm text-[var(--color-text-secondary)]">Monitor and manage alerts across your infrastructure</p>
      </div>
      <button
        @click="openCreateRule"
        class="inline-flex items-center gap-2 rounded-lg bg-blue-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-600"
      >
        <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 4v16m8-8H4" />
        </svg>
        Add Rule
      </button>
    </div>

    <!-- Tab bar -->
    <div class="flex gap-1 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-1">
      <button
        v-for="tab in (['active', 'resolved', 'rules'] as const)"
        :key="tab"
        @click="switchTab(tab)"
        :class="[
          'flex-1 rounded-md px-3 py-2 text-sm font-medium transition-colors capitalize',
          activeTab === tab
            ? 'bg-[var(--color-bg)] text-[var(--color-text)] shadow-sm'
            : 'text-[var(--color-text-secondary)] hover:text-[var(--color-text)]',
        ]"
      >
        {{ tab === 'rules' ? 'Alert Rules' : tab }}
      </button>
    </div>

    <!-- Filters (alerts tabs only) -->
    <div v-if="activeTab !== 'rules'" class="flex flex-wrap items-center gap-3">
      <select
        v-model="severityFilter"
        @change="applyFilters"
        class="rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-1.5 text-sm text-[var(--color-text)]"
      >
        <option :value="undefined">All Severities</option>
        <option :value="AlertSeverity.Critical">Critical</option>
        <option :value="AlertSeverity.Warning">Warning</option>
        <option :value="AlertSeverity.Info">Info</option>
      </select>
      <select
        v-model="sourceFilter"
        @change="applyFilters"
        class="rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-1.5 text-sm text-[var(--color-text)]"
      >
        <option :value="undefined">All Sources</option>
        <option :value="AlertSourceType.GitHubActions">GitHub Actions</option>
        <option :value="AlertSourceType.PM2">PM2</option>
        <option :value="AlertSourceType.Server">Server</option>
      </select>
    </div>

    <!-- Active/Resolved alerts -->
    <div v-if="activeTab !== 'rules'">
      <div v-if="store.loading" class="flex items-center justify-center py-12">
        <div class="h-6 w-6 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
      </div>

      <EmptyState
        v-else-if="filteredAlerts.length === 0"
        title="No alerts triggered"
        description="Alerts will appear here when conditions are met"
      />

      <div v-else class="space-y-3">
        <AlertCard
          v-for="alert in filteredAlerts"
          :key="alert.id"
          :alert="alert"
          @acknowledge="handleAcknowledge"
          @resolve="handleResolve"
          @mute="handleMute"
        />
      </div>

      <!-- Pagination -->
      <div
        v-if="store.alerts.total > store.alerts.pageSize"
        class="mt-6 flex items-center justify-between"
      >
        <span class="text-sm text-[var(--color-text-secondary)]">
          {{ (store.alerts.page - 1) * store.alerts.pageSize + 1 }}-{{ Math.min(store.alerts.page * store.alerts.pageSize, store.alerts.total) }} of {{ store.alerts.total }}
        </span>
        <div class="flex gap-2">
          <button
            @click="store.prevPage(); store.fetchAlerts()"
            :disabled="store.alerts.page <= 1"
            class="rounded-lg border border-[var(--color-border)] px-3 py-1.5 text-sm text-[var(--color-text)] transition-colors hover:bg-[var(--color-bg-secondary)] disabled:opacity-50"
          >
            Previous
          </button>
          <button
            @click="store.nextPage(); store.fetchAlerts()"
            :disabled="store.alerts.page * store.alerts.pageSize >= store.alerts.total"
            class="rounded-lg border border-[var(--color-border)] px-3 py-1.5 text-sm text-[var(--color-text)] transition-colors hover:bg-[var(--color-bg-secondary)] disabled:opacity-50"
          >
            Next
          </button>
        </div>
      </div>
    </div>

    <!-- Alert Rules tab -->
    <div v-else>
      <!-- Rule form dialog -->
      <div
        v-if="showRuleForm"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
        @click.self="showRuleForm = false; editingRule = null"
      >
        <div class="w-full max-w-lg rounded-xl border border-[var(--color-border)] bg-[var(--color-bg)] p-6 shadow-2xl">
          <h3 class="mb-4 text-lg font-semibold text-[var(--color-text)]">
            {{ editingRule ? 'Edit Rule' : 'Create Alert Rule' }}
          </h3>
          <AlertRuleForm
            :initial-data="editingRule"
            @submit="handleRuleSubmit"
            @cancel="showRuleForm = false; editingRule = null"
          />
        </div>
      </div>

      <div v-if="store.loading" class="flex items-center justify-center py-12">
        <div class="h-6 w-6 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
      </div>

      <EmptyState
        v-else-if="store.alertRules.length === 0"
        title="No alert rules configured"
        description="Create your first alert rule to start monitoring"
      />

      <div v-else class="space-y-3">
        <div
          v-for="rule in store.alertRules"
          :key="rule.id"
          class="flex flex-col gap-3 rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4 sm:flex-row sm:items-center sm:justify-between"
        >
          <div class="min-w-0 flex-1">
            <div class="flex items-center gap-2">
              <h4 class="truncate text-sm font-semibold text-[var(--color-text)]">{{ rule.name }}</h4>
              <span
                :class="[
                  'shrink-0 rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase',
                  rule.isEnabled ? 'bg-green-500/20 text-green-400' : 'bg-gray-500/20 text-gray-400',
                ]"
              >
                {{ rule.isEnabled ? 'Enabled' : 'Disabled' }}
              </span>
            </div>
            <p class="mt-1 text-xs text-[var(--color-text-secondary)]">
              {{ rule.sourceType }} &middot; {{ rule.conditionType }} &middot; Threshold: {{ rule.threshold }} &middot; {{ rule.severity }}
            </p>
          </div>
          <div class="flex items-center gap-2">
            <button
              @click="openEditRule(rule)"
              class="rounded-lg px-3 py-1.5 text-xs font-medium text-blue-400 transition-colors hover:bg-blue-500/10"
            >
              Edit
            </button>
            <button
              @click="handleDeleteRule(rule.id)"
              class="rounded-lg px-3 py-1.5 text-xs font-medium text-red-400 transition-colors hover:bg-red-500/10"
            >
              Delete
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
