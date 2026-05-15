<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useAlertsStore } from '@/stores/alerts'
import type { AlertRule, CreateAlertRuleRequest } from '@/types'
import AlertRuleForm from '@/components/alerts/AlertRuleForm.vue'
import EmptyState from '@/components/empty/EmptyState.vue'

const auth = useAuthStore()
const alerts = useAlertsStore()
const activeTab = ref<'workspace' | 'rules' | 'retention'>('workspace')
const showRuleForm = ref(false)
const editingRule = ref<AlertRule | null>(null)

const isAdmin = computed(() => auth.hasRole('Owner') || auth.hasRole('Admin') || auth.hasRole('Super Admin'))

// Retention settings
const retention = ref({
  githubActions: 30,
  pm2: 14,
  errors: 60,
  alerts: 180,
  metrics: 365,
})

onMounted(() => {
  if (isAdmin.value) {
    alerts.fetchAlertRules()
  }
})

function openCreateRule() {
  editingRule.value = null
  showRuleForm.value = true
}

function openEditRule(rule: AlertRule) {
  editingRule.value = rule
  showRuleForm.value = true
}

async function handleRuleSubmit(data: CreateAlertRuleRequest) {
  if (editingRule.value) {
    await alerts.updateRule(editingRule.value.id, data)
  } else {
    await alerts.createRule(data)
  }
  showRuleForm.value = false
  editingRule.value = null
}

async function handleDeleteRule(id: string) {
  await alerts.deleteRule(id)
}

function saveRetention() {
  // TODO: POST retention settings to backend
}
</script>

<template>
  <div class="mx-auto max-w-4xl space-y-6 p-4 md:p-6">
    <div>
      <h1 class="text-2xl font-bold text-[var(--color-text)]">Settings</h1>
      <p class="mt-1 text-sm text-[var(--color-text-secondary)]">Manage workspace, alert rules, and retention policies</p>
    </div>

    <!-- Tab bar -->
    <div class="flex gap-1 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-1">
      <button
        v-for="tab in (['workspace', 'rules', 'retention'] as const)"
        :key="tab"
        @click="activeTab = tab"
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

    <!-- Workspace tab -->
    <div v-if="activeTab === 'workspace'" class="space-y-4">
      <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-6">
        <h3 class="text-lg font-semibold text-[var(--color-text)]">Current User</h3>
        <div class="mt-4 space-y-3">
          <div class="flex items-center justify-between">
            <span class="text-sm text-[var(--color-text-secondary)]">Name</span>
            <span class="text-sm font-medium text-[var(--color-text)]">{{ auth.user?.name }}</span>
          </div>
          <div class="flex items-center justify-between">
            <span class="text-sm text-[var(--color-text-secondary)]">Email</span>
            <span class="text-sm font-medium text-[var(--color-text)]">{{ auth.user?.email }}</span>
          </div>
          <div class="flex items-center justify-between">
            <span class="text-sm text-[var(--color-text-secondary)]">Role</span>
            <span class="rounded bg-blue-500/20 px-2 py-0.5 text-xs font-semibold uppercase text-blue-400">
              {{ auth.user?.roles?.join(', ') ?? '—' }}
            </span>
          </div>
        </div>
      </div>

      <div v-if="!isAdmin" class="rounded-xl border border-yellow-500/20 bg-yellow-500/5 p-4">
        <p class="text-sm text-yellow-400">Workspace and user management requires Owner or Admin role.</p>
      </div>

      <div v-if="isAdmin" class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-6">
        <h3 class="text-lg font-semibold text-[var(--color-text)]">Agent Token</h3>
        <p class="mt-1 text-sm text-[var(--color-text-secondary)]">Use this token to authenticate VPS monitoring agents.</p>
        <div class="mt-3 flex items-center gap-2">
          <code class="flex-1 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-2 text-sm text-[var(--color-text)]">
            ••••••••••••••••
          </code>
          <button class="rounded-lg border border-[var(--color-border)] px-3 py-2 text-sm text-[var(--color-text)] transition-colors hover:bg-[var(--color-bg-secondary)]">
            Show
          </button>
        </div>
      </div>
    </div>

    <!-- Alert Rules tab -->
    <div v-if="activeTab === 'rules'">
      <div class="mb-4 flex justify-end">
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

      <EmptyState
        v-if="alerts.alertRules.length === 0"
        title="No alert rules"
        description="Create alert rules to monitor your infrastructure"
      />

      <div v-else class="space-y-3">
        <div
          v-for="rule in alerts.alertRules"
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

    <!-- Retention tab -->
    <div v-if="activeTab === 'retention'">
      <div v-if="!isAdmin" class="rounded-xl border border-yellow-500/20 bg-yellow-500/5 p-4">
        <p class="text-sm text-yellow-400">Retention settings require Owner or Admin role.</p>
      </div>

      <div v-else class="space-y-4">
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-6">
          <h3 class="text-lg font-semibold text-[var(--color-text)]">Log Retention (days)</h3>
          <p class="mt-1 text-sm text-[var(--color-text-secondary)]">Configure how long different data types are retained.</p>

          <div class="mt-4 space-y-4">
            <div class="flex items-center justify-between">
              <label class="text-sm text-[var(--color-text)]">GitHub Actions Logs</label>
              <input
                v-model.number="retention.githubActions"
                type="number"
                min="1"
                max="365"
                class="w-24 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-1.5 text-sm text-[var(--color-text)] text-right"
              />
            </div>
            <div class="flex items-center justify-between">
              <label class="text-sm text-[var(--color-text)]">PM2 Logs</label>
              <input
                v-model.number="retention.pm2"
                type="number"
                min="1"
                max="90"
                class="w-24 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-1.5 text-sm text-[var(--color-text)] text-right"
              />
            </div>
            <div class="flex items-center justify-between">
              <label class="text-sm text-[var(--color-text)]">Error Logs</label>
              <input
                v-model.number="retention.errors"
                type="number"
                min="1"
                max="365"
                class="w-24 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-1.5 text-sm text-[var(--color-text)] text-right"
              />
            </div>
            <div class="flex items-center justify-between">
              <label class="text-sm text-[var(--color-text)]">Alert History</label>
              <input
                v-model.number="retention.alerts"
                type="number"
                min="1"
                max="730"
                class="w-24 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-1.5 text-sm text-[var(--color-text)] text-right"
              />
            </div>
            <div class="flex items-center justify-between">
              <label class="text-sm text-[var(--color-text)]">Aggregated Metrics</label>
              <input
                v-model.number="retention.metrics"
                type="number"
                min="1"
                max="1825"
                class="w-24 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-1.5 text-sm text-[var(--color-text)] text-right"
              />
            </div>
          </div>

          <div class="mt-6 flex justify-end">
            <button
              @click="saveRetention"
              class="rounded-lg bg-blue-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-600"
            >
              Save Retention Settings
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
