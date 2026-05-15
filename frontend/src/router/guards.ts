import type { NavigationGuardNext, RouteLocationNormalized } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

export function authGuard(
  _to: RouteLocationNormalized,
  _from: RouteLocationNormalized,
  next: NavigationGuardNext
) {
  const authStore = useAuthStore()

  if (_to.meta.requiresAuth && !authStore.isAuthenticated) {
    next({ path: '/login', query: { redirect: _to.fullPath } })
  } else if (_to.meta.guest && authStore.isAuthenticated) {
    next({ path: '/' })
  } else {
    next()
  }
}
