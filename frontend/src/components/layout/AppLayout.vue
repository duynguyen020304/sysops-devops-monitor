<script setup lang="ts">
import { ref } from 'vue'
import { RouterView } from 'vue-router'
import { useMediaQuery } from '@/composables/useMediaQuery'
import AppSidebar from './AppSidebar.vue'
import AppHeader from './AppHeader.vue'

const { isMobile } = useMediaQuery()
const sidebarOpen = ref(false)

function toggleSidebar() {
  sidebarOpen.value = !sidebarOpen.value
}

function closeSidebar() {
  sidebarOpen.value = false
}
</script>

<template>
  <div class="flex h-screen overflow-hidden bg-[var(--color-bg)]">
    <!-- Mobile overlay -->
    <div
      v-if="isMobile && sidebarOpen"
      class="fixed inset-0 z-30 bg-black/50 transition-opacity"
      @click="closeSidebar"
    />

    <!-- Sidebar -->
    <aside
      :class="[
        'fixed z-40 flex h-full w-60 flex-col transition-transform duration-200 lg:static lg:translate-x-0',
        isMobile ? (sidebarOpen ? 'translate-x-0' : '-translate-x-full') : 'translate-x-0',
      ]"
    >
      <AppSidebar @navigate="closeSidebar" />
    </aside>

    <!-- Main content -->
    <div class="flex flex-1 flex-col overflow-hidden">
      <AppHeader @toggle-sidebar="toggleSidebar" />

      <main class="flex-1 overflow-y-auto p-4 lg:p-6">
        <RouterView />
      </main>
    </div>
  </div>
</template>
