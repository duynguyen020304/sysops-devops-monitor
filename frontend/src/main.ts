import { createApp } from 'vue'
import { createPinia } from 'pinia'
import router from './router'
import { useAuthStore } from './stores/auth'
import { useTheme } from './composables/useTheme'
import App from './App.vue'
import './assets/main.css'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
app.use(router)

// Initialize auth state from localStorage
const authStore = useAuthStore()
authStore.loadFromStorage()

// Initialize theme
const { init: initTheme } = useTheme()
initTheme()

app.mount('#app')
