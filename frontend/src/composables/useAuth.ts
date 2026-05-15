import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

export function useAuth() {
  const authStore = useAuthStore()
  const router = useRouter()

  const isAuthenticated = computed(() => authStore.isAuthenticated)
  const user = computed(() => authStore.user)

  async function login(email: string, password: string) {
    await authStore.login(email, password)
    router.push('/')
  }

  async function register(name: string, email: string, password: string) {
    await authStore.register(name, email, password)
    router.push('/')
  }

  function logout() {
    authStore.logout()
    router.push('/login')
  }

  return {
    isAuthenticated,
    user,
    login,
    register,
    logout,
  }
}
