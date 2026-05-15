<script setup lang="ts">
import { ref, computed } from 'vue'
import { AlertSeverity, AlertSourceType } from '@/types'
import type { AlertRule, CreateAlertRuleRequest } from '@/types'

const props = defineProps<{
  initialData?: AlertRule | null
}>()

const emit = defineEmits<{
  submit: [data: CreateAlertRuleRequest]
  cancel: []
}>()

const name = ref(props.initialData?.name ?? '')
const sourceType = ref<AlertSourceType>(props.initialData?.sourceType ?? AlertSourceType.Server)
const conditionType = ref(props.initialData?.conditionType ?? '')
const threshold = ref(props.initialData?.threshold ?? 0)
const timeWindowSeconds = ref(props.initialData?.timeWindowSeconds ?? 300)
const severity = ref<AlertSeverity>(props.initialData?.severity ?? AlertSeverity.Warning)
const isEnabled = ref(props.initialData?.isEnabled ?? true)
const cooldownSeconds = ref(props.initialData?.cooldownSeconds ?? 60)

const conditionOptions = computed(() => {
  switch (sourceType.value) {
    case AlertSourceType.GitHubActions:
      return [
        { value: 'workflow_failure', label: 'Workflow Failure' },
        { value: 'workflow_duration', label: 'Workflow Duration (s)' },
        { value: 'consecutive_failures', label: 'Consecutive Failures' },
      ]
    case AlertSourceType.PM2:
      return [
        { value: 'cpu_usage', label: 'CPU Usage (%)' },
        { value: 'memory_usage', label: 'Memory Usage (MB)' },
        { value: 'restart_count', label: 'Restart Count' },
        { value: 'process_stopped', label: 'Process Stopped' },
      ]
    case AlertSourceType.Server:
      return [
        { value: 'cpu_usage', label: 'CPU Usage (%)' },
        { value: 'memory_usage', label: 'Memory Usage (%)' },
        { value: 'disk_usage', label: 'Disk Usage (%)' },
        { value: 'load_average', label: 'Load Average' },
        { value: 'heartbeat_miss', label: 'Heartbeat Miss (s)' },
      ]
    default:
      return []
  }
})

const isEdit = computed(() => !!props.initialData)

function handleSubmit() {
  if (!name.value || !conditionType.value) return
  emit('submit', {
    name: name.value,
    sourceType: sourceType.value,
    conditionType: conditionType.value,
    threshold: threshold.value,
    timeWindowSeconds: timeWindowSeconds.value,
    severity: severity.value,
    isEnabled: isEnabled.value,
    cooldownSeconds: cooldownSeconds.value,
  })
}
</script>

<template>
  <form @submit.prevent="handleSubmit" class="space-y-4">
    <!-- Name -->
    <div>
      <label class="mb-1 block text-xs font-medium text-[var(--color-text-secondary)]">Rule Name</label>
      <input
        v-model="name"
        type="text"
        required
        placeholder="e.g. High CPU Alert"
        class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-2 text-sm text-[var(--color-text)] placeholder:text-[var(--color-text-secondary)] focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
      />
    </div>

    <!-- Source Type -->
    <div>
      <label class="mb-1 block text-xs font-medium text-[var(--color-text-secondary)]">Source Type</label>
      <select
        v-model="sourceType"
        class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-2 text-sm text-[var(--color-text)] focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
      >
        <option :value="AlertSourceType.GitHubActions">GitHub Actions</option>
        <option :value="AlertSourceType.PM2">PM2</option>
        <option :value="AlertSourceType.Server">Server</option>
      </select>
    </div>

    <!-- Condition Type -->
    <div>
      <label class="mb-1 block text-xs font-medium text-[var(--color-text-secondary)]">Condition</label>
      <select
        v-model="conditionType"
        required
        class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-2 text-sm text-[var(--color-text)] focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
      >
        <option value="" disabled>Select condition</option>
        <option v-for="opt in conditionOptions" :key="opt.value" :value="opt.value">
          {{ opt.label }}
        </option>
      </select>
    </div>

    <!-- Threshold + Time Window row -->
    <div class="grid grid-cols-2 gap-3">
      <div>
        <label class="mb-1 block text-xs font-medium text-[var(--color-text-secondary)]">Threshold</label>
        <input
          v-model.number="threshold"
          type="number"
          min="0"
          step="0.1"
          class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-2 text-sm text-[var(--color-text)] focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
      </div>
      <div>
        <label class="mb-1 block text-xs font-medium text-[var(--color-text-secondary)]">Time Window (s)</label>
        <input
          v-model.number="timeWindowSeconds"
          type="number"
          min="30"
          step="30"
          class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-2 text-sm text-[var(--color-text)] focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
      </div>
    </div>

    <!-- Severity -->
    <div>
      <label class="mb-1 block text-xs font-medium text-[var(--color-text-secondary)]">Severity</label>
      <select
        v-model="severity"
        class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-2 text-sm text-[var(--color-text)] focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
      >
        <option :value="AlertSeverity.Info">Info</option>
        <option :value="AlertSeverity.Warning">Warning</option>
        <option :value="AlertSeverity.Critical">Critical</option>
      </select>
    </div>

    <!-- Cooldown + Enabled row -->
    <div class="grid grid-cols-2 gap-3">
      <div>
        <label class="mb-1 block text-xs font-medium text-[var(--color-text-secondary)]">Cooldown (s)</label>
        <input
          v-model.number="cooldownSeconds"
          type="number"
          min="0"
          step="10"
          class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-2 text-sm text-[var(--color-text)] focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
      </div>
      <div class="flex items-end pb-2">
        <label class="flex cursor-pointer items-center gap-2">
          <button
            type="button"
            role="switch"
            :aria-checked="isEnabled"
            @click="isEnabled = !isEnabled"
            :class="[
              'relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors',
              isEnabled ? 'bg-blue-500' : 'bg-gray-600',
            ]"
          >
            <span
              :class="[
                'pointer-events-none inline-block h-4 w-4 rounded-full bg-white shadow transition-transform',
                isEnabled ? 'translate-x-4' : 'translate-x-0',
              ]"
            />
          </button>
          <span class="text-xs font-medium text-[var(--color-text-secondary)]">Enabled</span>
        </label>
      </div>
    </div>

    <!-- Actions -->
    <div class="flex items-center justify-end gap-2 border-t border-[var(--color-border)] pt-4">
      <button
        type="button"
        @click="emit('cancel')"
        class="rounded-lg px-4 py-2 text-sm font-medium text-[var(--color-text-secondary)] transition-colors hover:bg-[var(--color-bg-tertiary)]"
      >
        Cancel
      </button>
      <button
        type="submit"
        :disabled="!name || !conditionType"
        class="rounded-lg bg-blue-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-600 disabled:opacity-50"
      >
        {{ isEdit ? 'Update Rule' : 'Create Rule' }}
      </button>
    </div>
  </form>
</template>
