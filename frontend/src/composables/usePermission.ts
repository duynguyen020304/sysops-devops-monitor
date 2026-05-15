import { useAuthStore } from '@/stores/auth'

export function usePermission() {
  const auth = useAuthStore()

  return {
    hasPermission: auth.hasPermission,
    hasRole: auth.hasRole,
    hasAnyPermission: auth.hasAnyPermission,
    permissions: auth.permissions,
    roles: auth.roles,
  }
}
