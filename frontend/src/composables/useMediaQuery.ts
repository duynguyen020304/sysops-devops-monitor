import { ref, onMounted, onUnmounted } from 'vue'

export function useMediaQuery() {
  const isMobile = ref(false)
  const isTablet = ref(false)
  const isDesktop = ref(true)

  const mobileQuery = window.matchMedia('(max-width: 767px)')
  const tabletQuery = window.matchMedia('(min-width: 768px) and (max-width: 1023px)')
  const desktopQuery = window.matchMedia('(min-width: 1024px)')

  function update() {
    isMobile.value = mobileQuery.matches
    isTablet.value = tabletQuery.matches
    isDesktop.value = desktopQuery.matches
  }

  onMounted(() => {
    update()
    mobileQuery.addEventListener('change', update)
    tabletQuery.addEventListener('change', update)
    desktopQuery.addEventListener('change', update)
  })

  onUnmounted(() => {
    mobileQuery.removeEventListener('change', update)
    tabletQuery.removeEventListener('change', update)
    desktopQuery.removeEventListener('change', update)
  })

  return { isMobile, isTablet, isDesktop }
}
