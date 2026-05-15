<script setup lang="ts">
import { useTheme } from '@/composables/useTheme'
import { useMediaQuery } from '@/composables/useMediaQuery'

const emit = defineEmits<{
  'toggle-sidebar': []
}>()

const { theme, toggle: toggleTheme } = useTheme()
const { isDesktop } = useMediaQuery()
</script>

<template>
  <header class="flex h-16 shrink-0 items-center justify-between border-b border-[var(--color-border)] bg-[var(--color-bg)] px-4 lg:px-6">
    <!-- Left: hamburger (mobile) + title -->
    <div class="flex items-center gap-3">
      <button
        v-if="!isDesktop"
        @click="emit('toggle-sidebar')"
        class="rounded-lg p-2 text-[var(--color-text-secondary)] transition-colors hover:bg-[var(--color-bg-tertiary)]"
      >
        <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M4 6h16M4 12h16M4 18h16" />
        </svg>
      </button>
      <h1 class="text-lg font-semibold text-[var(--color-text)]">Dashboard</h1>
    </div>

    <!-- Right: theme toggle -->
    <div class="flex items-center gap-3">
      <button
        @click="toggleTheme"
        class="rounded-lg p-2 text-[var(--color-text-secondary)] transition-colors hover:bg-[var(--color-bg-tertiary)]"
        :title="theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'"
      >
        <!-- Sun icon (shown in dark mode) -->
        <svg v-if="theme === 'dark'" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z" />
        </svg>
        <!-- Moon icon (shown in light mode) -->
        <svg v-else class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
        </svg>
      </button>
    </div>
  </header>
</template>
