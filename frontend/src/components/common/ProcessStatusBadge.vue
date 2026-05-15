<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  status: string
}>()

const badgeClass = computed(() => {
  switch (props.status.toLowerCase()) {
    case 'online':
      return 'bg-green-500/20 text-green-400 border-green-500/30'
    case 'stopped':
      return 'bg-red-500/20 text-red-400 border-red-500/30'
    case 'errored':
      return 'bg-red-500/20 text-red-400 border-red-500/30'
    case 'launching':
      return 'bg-yellow-500/20 text-yellow-400 border-yellow-500/30'
    default:
      return 'bg-gray-500/20 text-gray-400 border-gray-500/30'
  }
})

const dotClass = computed(() => {
  switch (props.status.toLowerCase()) {
    case 'online':
      return 'bg-green-400'
    case 'stopped':
    case 'errored':
      return 'bg-red-400'
    case 'launching':
      return 'bg-yellow-400 animate-pulse'
    default:
      return 'bg-gray-400'
  }
})
</script>

<template>
  <span
    :class="[
      badgeClass,
      'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium',
    ]"
  >
    <span :class="[dotClass, 'h-1.5 w-1.5 rounded-full']" />
    {{ status }}
  </span>
</template>
