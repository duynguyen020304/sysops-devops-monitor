import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '@/lib/api'
import type { User, AuthResponse, LoginRequest } from '@/types'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null)
  const token = ref<string | null>(null)
  const refreshToken = ref<string | null>(null)

  const isAuthenticated = computed(() => !!token.value && !!user.value)
  const permissions = computed(() => user.value?.permissions ?? [])
  const roles = computed(() => user.value?.roles ?? [])

  function hasPermission(permission: string): boolean {
    return permissions.value.includes(permission)
  }

  function hasRole(role: string): boolean {
    return roles.value.includes(role)
  }

  function hasAnyPermission(...perms: string[]): boolean {
    return perms.some(p => permissions.value.includes(p))
  }

  async function login(email: string, password: string) {
    const { data } = await api.post<AuthResponse>('/auth/login', {
      email,
      password,
    } satisfies LoginRequest)
    setUser(data)
  }

  function logout() {
    user.value = null
    token.value = null
    refreshToken.value = null
    localStorage.removeItem('user')
    localStorage.removeItem('token')
    localStorage.removeItem('refreshToken')
  }

  function loadFromStorage() {
    const storedUser = localStorage.getItem('user')
    const storedToken = localStorage.getItem('token')
    const storedRefresh = localStorage.getItem('refreshToken')

    if (storedUser && storedToken) {
      try {
        user.value = JSON.parse(storedUser)
        token.value = storedToken
        refreshToken.value = storedRefresh
      } catch {
        logout()
      }
    }
  }

  function setUser(data: AuthResponse) {
    user.value = data.user
    token.value = data.token
    refreshToken.value = data.refreshToken
    localStorage.setItem('user', JSON.stringify(data.user))
    localStorage.setItem('token', data.token)
    localStorage.setItem('refreshToken', data.refreshToken)
  }

  return {
    user,
    token,
    refreshToken,
    isAuthenticated,
    permissions,
    roles,
    hasPermission,
    hasRole,
    hasAnyPermission,
    login,
    logout,
    loadFromStorage,
  }
})
