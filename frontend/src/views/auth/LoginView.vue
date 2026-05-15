<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink } from 'vue-router'
import { useAuth } from '@/composables/useAuth'

const { login } = useAuth()

const email = ref('')
const password = ref('')
const loading = ref(false)
const error = ref('')

async function handleSubmit() {
  error.value = ''
  loading.value = true
  try {
    await login(email.value, password.value)
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Login failed. Please try again.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-[var(--color-bg)] px-4">
    <div class="w-full max-w-md">
      <!-- Logo -->
      <div class="mb-8 text-center">
        <div class="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-xl bg-blue-500">
          <svg class="h-7 w-7 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
          </svg>
        </div>
        <h1 class="text-2xl font-bold text-[var(--color-text)]">Welcome back</h1>
        <p class="mt-1 text-sm text-[var(--color-text-secondary)]">Sign in to your SysMonitor account</p>
      </div>

      <!-- Form -->
      <form @submit.prevent="handleSubmit" class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-6 shadow-lg">
        <div v-if="error" class="mb-4 rounded-lg bg-red-500/10 px-4 py-3 text-sm text-red-400">
          {{ error }}
        </div>

        <div class="space-y-4">
          <div>
            <label for="email" class="mb-1.5 block text-sm font-medium text-[var(--color-text)]">Email</label>
            <input
              id="email"
              v-model="email"
              type="email"
              required
              placeholder="you@example.com"
              class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-4 py-2.5 text-[var(--color-text)] placeholder-[var(--color-text-secondary)] outline-none transition-colors focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
            />
          </div>

          <div>
            <label for="password" class="mb-1.5 block text-sm font-medium text-[var(--color-text)]">Password</label>
            <input
              id="password"
              v-model="password"
              type="password"
              required
              placeholder="Enter your password"
              class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-4 py-2.5 text-[var(--color-text)] placeholder-[var(--color-text-secondary)] outline-none transition-colors focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
            />
          </div>
        </div>

        <button
          type="submit"
          :disabled="loading"
          class="mt-6 w-full rounded-lg bg-blue-500 py-2.5 text-sm font-medium text-white transition-colors hover:bg-blue-600 disabled:cursor-not-allowed disabled:opacity-50"
        >
          <span v-if="loading">Signing in...</span>
          <span v-else>Sign in</span>
        </button>

        <p class="mt-4 text-center text-sm text-[var(--color-text-secondary)]">
          Don't have an account?
          <RouterLink to="/register" class="font-medium text-blue-400 hover:text-blue-300">Create one</RouterLink>
        </p>
      </form>
    </div>
  </div>
</template>
