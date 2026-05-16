<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { agentInstallApi } from '@/lib/api'
import type { AgentInstallToken, AgentInstallTokenList } from '@/types'

const serverName = ref('')
const plainPassword = ref('')
const showPassword = ref(false)
const loading = ref(false)
const generatedToken = ref<AgentInstallToken | null>(null)
const tokens = ref<AgentInstallTokenList[]>([])
const error = ref('')
const copiedCurl = ref(false)
const copiedPage = ref(false)
const copiedPw = ref(false)

let refreshTimer: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  loadTokens()
  refreshTimer = setInterval(loadTokens, 30000)
})

onUnmounted(() => {
  if (refreshTimer) clearInterval(refreshTimer)
})

async function loadTokens() {
  try {
    const { data } = await agentInstallApi.listTokens()
    tokens.value = data
  } catch { /* ignore */ }
}

async function generate() {
  error.value = ''
  if (!serverName.value.trim()) { error.value = 'Server name is required'; return }

  loading.value = true
  try {
    const { data } = await agentInstallApi.generateToken({
      serverName: serverName.value.trim(),
    })
    generatedToken.value = data
    plainPassword.value = data.plainPassword
    showPassword.value = false
    serverName.value = ''
    await loadTokens()
  } catch (e: any) {
    error.value = e.response?.data?.message || 'Failed to generate token'
  } finally {
    loading.value = false
  }
}

async function revokeToken(id: string) {
  try {
    await agentInstallApi.revokeToken(id)
    await loadTokens()
    if (generatedToken.value?.id === id) generatedToken.value = null
  } catch { /* ignore */ }
}

function copyText(text: string, type: 'curl' | 'page') {
  navigator.clipboard.writeText(text)
  if (type === 'curl') {
    copiedCurl.value = true
    setTimeout(() => { copiedCurl.value = false }, 2000)
  } else {
    copiedPage.value = true
    setTimeout(() => { copiedPage.value = false }, 2000)
  }
}

function timeLeft(expiresAt: string): string {
  const diff = new Date(expiresAt).getTime() - Date.now()
  if (diff <= 0) return 'Expired'
  const mins = Math.floor(diff / 60000)
  const secs = Math.floor((diff % 60000) / 1000)
  return `${mins}m ${secs}s left`
}

function statusColor(status: string): string {
  switch (status) {
    case 'active': return 'text-green-400'
    case 'used': return 'text-blue-400'
    case 'expired': return 'text-yellow-400'
    case 'revoked': return 'text-red-400'
    default: return 'text-gray-400'
  }
}

function statusIcon(status: string): string {
  switch (status) {
    case 'active': return '●'
    case 'used': return '✓'
    case 'expired': return '⏰'
    case 'revoked': return '✗'
    default: return '?'
  }
}
</script>

<template>
  <div class="space-y-6">
    <h1 class="text-2xl font-bold text-[var(--color-text)]">Deploy Monitoring Agent</h1>

    <!-- Generate Form -->
    <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-6">
      <h2 class="mb-4 text-lg font-semibold text-[var(--color-text)]">Generate Install Link</h2>
      <div>
        <label class="mb-1 block text-sm text-[var(--color-text-secondary)]">Server Name</label>
        <input
          v-model="serverName"
          type="text"
          placeholder="e.g. prod-web-01"
          class="w-full max-w-md rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-4 py-2.5 text-[var(--color-text)] placeholder-[var(--color-text-secondary)] focus:border-blue-500 focus:outline-none"
        />
        <p class="mt-2 text-xs text-[var(--color-text-secondary)]">Backend generates a random password and shows it once after link creation.</p>
      </div>
      <div v-if="error" class="mt-2 text-sm text-red-400">{{ error }}</div>
      <div class="mt-4 flex flex-wrap gap-2">
        <button
          @click="generate"
          :disabled="loading"
          class="rounded-lg bg-blue-600 px-6 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-blue-700 disabled:opacity-50"
        >
          {{ loading ? 'Generating...' : 'Generate Link' }}
        </button>

      </div>
    </div>

    <!-- Generated Link -->
    <div v-if="generatedToken" class="rounded-xl border border-green-500/30 bg-green-500/5 p-6">
      <h2 class="mb-4 text-lg font-semibold text-green-400">Install Link Generated</h2>

      <div class="mb-4 rounded-lg border border-yellow-500/30 bg-yellow-500/10 p-3">
        <p class="mb-1 text-sm font-medium text-yellow-300">Download Password</p>
        <div class="flex items-center gap-2">
          <code class="flex-1 overflow-x-auto rounded bg-[var(--color-bg)] px-3 py-2 font-mono text-sm text-yellow-100">
            {{ showPassword ? plainPassword : '•'.repeat(plainPassword.length) }}
          </code>
          <button
            @click="showPassword = !showPassword"
            type="button"
            class="rounded bg-[var(--color-bg)] px-3 py-2 text-xs text-[var(--color-text-secondary)] hover:text-white"
          >
            {{ showPassword ? 'Hide' : 'View' }}
          </button>
          <button
            @click="navigator.clipboard.writeText(plainPassword); copiedPw = true; setTimeout(() => copiedPw = false, 2000)"
            type="button"
            class="rounded bg-[var(--color-bg)] px-3 py-2 text-xs text-[var(--color-text-secondary)] hover:text-white"
          >
            {{ copiedPw ? '✓ Copied' : '📋 Copy Password' }}
          </button>
        </div>
      </div>

      <!-- Method A: curl -->
      <div class="mb-4">
        <p class="mb-1 text-sm font-medium text-[var(--color-text-secondary)]">Method A: curl (direct VPS install)</p>
        <div class="relative">
          <pre class="overflow-x-auto rounded-lg bg-[var(--color-bg)] p-3 font-mono text-sm text-green-300">curl -fsSL "{{ generatedToken.downloadUrl }}" | sudo bash</pre>
          <button
            @click="copyText(`curl -fsSL '${generatedToken.downloadUrl}' | sudo bash`, 'curl')"
            class="absolute right-2 top-2 rounded bg-[var(--color-bg-secondary)] px-2 py-1 text-xs text-[var(--color-text-secondary)] hover:text-white"
          >
            {{ copiedCurl ? '✓ Copied' : '📋 Copy' }}
          </button>
        </div>
      </div>

      <!-- Method B: browser -->
      <div>
        <p class="mb-1 text-sm font-medium text-[var(--color-text-secondary)]">Method B: Browser download page</p>
        <div class="relative">
          <pre class="overflow-x-auto rounded-lg bg-[var(--color-bg)] p-3 font-mono text-sm text-blue-300">{{ generatedToken.pageUrl }}</pre>
          <button
            @click="copyText(generatedToken.pageUrl, 'page')"
            class="absolute right-2 top-2 rounded bg-[var(--color-bg-secondary)] px-2 py-1 text-xs text-[var(--color-text-secondary)] hover:text-white"
          >
            {{ copiedPage ? '✓ Copied' : '📋 Copy' }}
          </button>
        </div>
        <p class="mt-1 text-xs text-[var(--color-text-secondary)]">Share this URL + the password with the recipient</p>
      </div>

      <div class="mt-3 text-sm text-yellow-400">⏱ Expires in 1 hour</div>
    </div>

    <!-- Token List -->
    <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-6">
      <h2 class="mb-4 text-lg font-semibold text-[var(--color-text)]">Install Links</h2>
      <div v-if="tokens.length === 0" class="text-sm text-[var(--color-text-secondary)]">No install links generated yet.</div>
      <div v-else class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="border-b border-[var(--color-border)] text-left text-[var(--color-text-secondary)]">
              <th class="pb-2 pr-4">Server</th>
              <th class="pb-2 pr-4">Created</th>
              <th class="pb-2 pr-4">Expires</th>
              <th class="pb-2 pr-4">Status</th>
              <th class="pb-2">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="t in tokens"
              :key="t.id"
              class="border-b border-[var(--color-border)]/50"
              :class="{ 'opacity-50': t.status === 'expired' || t.status === 'revoked' }"
            >
              <td class="py-3 pr-4 font-medium text-[var(--color-text)]">{{ t.serverName }}</td>
              <td class="py-3 pr-4 text-[var(--color-text-secondary)]">{{ new Date(t.createdAt).toLocaleString() }}</td>
              <td class="py-3 pr-4 text-[var(--color-text-secondary)]">
                <span v-if="t.status === 'active'">{{ timeLeft(t.expiresAt) }}</span>
                <span v-else>—</span>
              </td>
              <td class="py-3 pr-4">
                <span :class="statusColor(t.status)">{{ statusIcon(t.status) }} {{ t.status }}</span>
              </td>
              <td class="py-3">
                <button
                  v-if="t.status === 'active'"
                  @click="revokeToken(t.id)"
                  class="rounded px-3 py-1 text-xs text-red-400 transition-colors hover:bg-red-500/10"
                >
                  Revoke
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
