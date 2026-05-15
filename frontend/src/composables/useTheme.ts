import { ref, watch } from 'vue'

type Theme = 'dark' | 'light'

const theme = ref<Theme>('dark')

function applyTheme(t: Theme) {
  const html = document.documentElement
  if (t === 'dark') {
    html.classList.add('dark')
  } else {
    html.classList.remove('dark')
  }
}

export function useTheme() {
  function init() {
    const stored = localStorage.getItem('theme') as Theme | null
    theme.value = stored ?? 'dark'
    applyTheme(theme.value)
  }

  function toggle() {
    theme.value = theme.value === 'dark' ? 'light' : 'dark'
    localStorage.setItem('theme', theme.value)
    applyTheme(theme.value)
  }

  function set(t: Theme) {
    theme.value = t
    localStorage.setItem('theme', t)
    applyTheme(t)
  }

  watch(theme, (t) => applyTheme(t))

  return {
    theme,
    init,
    toggle,
    set,
  }
}
