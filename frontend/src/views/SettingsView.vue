<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useAlertsStore } from '@/stores/alerts'
import type { AlertRule, CreateAlertRuleRequest, UserWithRoles, Role, RoleDetail, Permission, CreateRoleRequest, UpdateRoleRequest } from '@/types'
import AlertRuleForm from '@/components/alerts/AlertRuleForm.vue'
import EmptyState from '@/components/empty/EmptyState.vue'
import api from '@/lib/api'

const auth = useAuthStore()
const alerts = useAlertsStore()
const activeTab = ref<'workspace' | 'users' | 'roles' | 'rules' | 'retention'>('workspace')
const showRuleForm = ref(false)
const editingRule = ref<AlertRule | null>(null)

const isAdmin = computed(() => auth.hasRole('Owner') || auth.hasRole('Admin') || auth.hasRole('Super Admin'))
const canManageUsers = computed(() => auth.hasPermission('manage_users'))

// Users state
const users = ref<UserWithRoles[]>([])
const roles = ref<Role[]>([])
const showUserForm = ref(false)
const editingUser = ref<UserWithRoles | null>(null)
const userForm = ref<CreateUserRequest & { roleIds: string[] }>({
  name: '',
  email: '',
  password: '',
  roleIds: [],
})
const userLoading = ref(false)
const userError = ref('')
const userSuccess = ref('')

// Roles state
const roleDetails = ref<RoleDetail[]>([])
const allPermissions = ref<Permission[]>([])
const showRoleForm = ref(false)
const editingRole = ref<RoleDetail | null>(null)
const roleForm = ref<CreateRoleRequest & { id?: string }>({
  name: '',
  description: null,
  permissionNames: [],
})
const roleLoading = ref(false)
const roleError = ref('')
const roleSuccess = ref('')
const deleteConfirmUserId = ref<string | null>(null)
const deleteConfirmRoleId = ref<string | null>(null)

// Retention settings
const retention = ref({
  githubActions: 30,
  pm2: 14,
  errors: 60,
  alerts: 180,
  metrics: 365,
})

onMounted(() => {
  if (isAdmin.value) {
    alerts.fetchAlertRules()
  }
  if (canManageUsers.value) {
    fetchUsers()
    fetchRoles()
  }
})

async function fetchUsers() {
  try {
    const { data } = await api.get<UserWithRoles[]>('/admin/users')
    users.value = data
  } catch { /* ignore */ }
}

async function fetchRoles() {
  try {
    const { data } = await api.get<Role[]>('/roles')
    roles.value = data
  } catch { /* ignore */ }
}

async function fetchRoleDetails() {
  try {
    const { data } = await api.get<(Role & { createdAt?: string })[]>('/roles')
    roleDetails.value = []
    for (const r of data) {
      try {
        const { data: detail } = await api.get<RoleDetail>(`/roles/${r.id}`)
        roleDetails.value.push(detail)
      } catch { /* skip */ }
    }
  } catch { /* ignore */ }
}

async function fetchAllPermissions() {
  try {
    const { data } = await api.get<Permission[]>('/roles/permissions')
    allPermissions.value = data
  } catch { /* ignore */ }
}

function openCreateUser() {
  editingUser.value = null
  userForm.value = { name: '', email: '', password: '', roleIds: [] }
  userError.value = ''
  userSuccess.value = ''
  showUserForm.value = true
}

function openEditUser(user: UserWithRoles) {
  editingUser.value = user
  userForm.value = {
    name: user.name,
    email: user.email,
    password: '',
    roleIds: [],
  }
  userError.value = ''
  userSuccess.value = ''
  showUserForm.value = true
  // Map role names back to role IDs
  const matchedIds = roles.value
    .filter(r => user.roles.includes(r.name))
    .map(r => r.id)
  userForm.value.roleIds = matchedIds
}

async function handleSaveUser() {
  userError.value = ''
  userSuccess.value = ''
  userLoading.value = true
  try {
    if (editingUser.value) {
      // Update user roles: remove old, add new
      const oldRoleIds = roles.value
        .filter(r => editingUser.value!.roles.includes(r.name))
        .map(r => r.id)
      const newRoleIds = userForm.value.roleIds

      // Remove roles no longer assigned
      for (const oldId of oldRoleIds) {
        if (!newRoleIds.includes(oldId)) {
          await api.delete(`/admin/users/${editingUser.value.id}/roles/${oldId}`)
        }
      }
      // Add new roles
      for (const newId of newRoleIds) {
        if (!oldRoleIds.includes(newId)) {
          await api.post(`/admin/users/${editingUser.value.id}/roles`, { roleId: newId })
        }
      }
      userSuccess.value = 'User updated successfully.'
    } else {
      const body: CreateUserRequest = {
        name: userForm.value.name,
        email: userForm.value.email,
        password: userForm.value.password,
        roleIds: userForm.value.roleIds.length > 0 ? userForm.value.roleIds : undefined,
      }
      await api.post('/admin/users', body)
      userSuccess.value = 'User created successfully.'
    }
    showUserForm.value = false
    editingUser.value = null
    await fetchUsers()
  } catch (e: unknown) {
    userError.value = e instanceof Error ? e.message : 'Failed to save user.'
  } finally {
    userLoading.value = false
  }
}

async function handleDeleteUser(id: string) {
  try {
    await api.delete(`/admin/users/${id}`)
    deleteConfirmUserId.value = null
    await fetchUsers()
  } catch { /* ignore */ }
}

// Role CRUD
function openCreateRole() {
  editingRole.value = null
  roleForm.value = { name: '', description: null, permissionNames: [] }
  roleError.value = ''
  roleSuccess.value = ''
  showRoleForm.value = true
}

function openEditRole(role: RoleDetail) {
  editingRole.value = role
  roleForm.value = {
    id: role.id,
    name: role.name,
    description: role.description,
    permissionNames: [...role.permissions],
  }
  roleError.value = ''
  roleSuccess.value = ''
  showRoleForm.value = true
}

async function handleSaveRole() {
  roleError.value = ''
  roleSuccess.value = ''
  roleLoading.value = true
  try {
    if (editingRole.value) {
      const body: UpdateRoleRequest = {
        name: roleForm.value.name,
        description: roleForm.value.description,
        permissionNames: roleForm.value.permissionNames,
      }
      await api.put(`/roles/${editingRole.value.id}`, body)
      roleSuccess.value = 'Role updated successfully.'
    } else {
      const body: CreateRoleRequest = {
        name: roleForm.value.name,
        description: roleForm.value.description,
        permissionNames: roleForm.value.permissionNames,
      }
      await api.post('/roles', body)
      roleSuccess.value = 'Role created successfully.'
    }
    showRoleForm.value = false
    editingRole.value = null
    await fetchRoles()
    await fetchRoleDetails()
  } catch (e: unknown) {
    roleError.value = e instanceof Error ? e.message : 'Failed to save role.'
  } finally {
    roleLoading.value = false
  }
}

async function handleDeleteRole(id: string) {
  try {
    await api.delete(`/roles/${id}`)
    deleteConfirmRoleId.value = null
    await fetchRoles()
    await fetchRoleDetails()
  } catch { /* ignore */ }
}

function openCreateRule() {
  editingRule.value = null
  showRuleForm.value = true
}

function openEditRule(rule: AlertRule) {
  editingRule.value = rule
  showRuleForm.value = true
}

async function handleRuleSubmit(data: CreateAlertRuleRequest) {
  if (editingRule.value) {
    await alerts.updateRule(editingRule.value.id, data)
  } else {
    await alerts.createRule(data)
  }
  showRuleForm.value = false
  editingRule.value = null
}

async function handleDeleteRule(id: string) {
  await alerts.deleteRule(id)
}

function saveRetention() {
  // TODO: POST retention settings to backend
}

// Track if roles tab was loaded
const rolesTabLoaded = ref(false)
function onRolesTabActivated() {
  if (!rolesTabLoaded.value) {
    fetchRoles()
    fetchRoleDetails()
    fetchAllPermissions()
    rolesTabLoaded.value = true
  }
}

const permissionsByCategory = computed(() => {
  const map = new Map<string, Permission[]>()
  for (const p of allPermissions.value) {
    const cat = p.category ?? 'General'
    if (!map.has(cat)) map.set(cat, [])
    map.get(cat)!.push(p)
  }
  return map
})
</script>

<template>
  <div class="mx-auto max-w-4xl space-y-6 p-4 md:p-6">
    <div>
      <h1 class="text-2xl font-bold text-[var(--color-text)]">Settings</h1>
      <p class="mt-1 text-sm text-[var(--color-text-secondary)]">Manage workspace, alert rules, and retention policies</p>
    </div>

    <!-- Tab bar -->
    <div class="flex gap-1 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-1">
      <button
        v-for="tab in (['workspace', 'users', 'roles', 'rules', 'retention'] as const)"
        :key="tab"
        v-show="tab !== 'roles' || canManageUsers"
        @click="activeTab = tab; tab === 'roles' && onRolesTabActivated()"
        :class="[
          'flex-1 rounded-md px-3 py-2 text-sm font-medium transition-colors capitalize',
          activeTab === tab
            ? 'bg-[var(--color-bg)] text-[var(--color-text)] shadow-sm'
            : 'text-[var(--color-text-secondary)] hover:text-[var(--color-text)]',
        ]"
      >
        {{ tab === 'rules' ? 'Alert Rules' : tab === 'roles' ? 'Roles & Permissions' : tab }}
      </button>
    </div>

    <!-- Workspace tab -->
    <div v-if="activeTab === 'workspace'" class="space-y-4">
      <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-6">
        <h3 class="text-lg font-semibold text-[var(--color-text)]">Current User</h3>
        <div class="mt-4 space-y-3">
          <div class="flex items-center justify-between">
            <span class="text-sm text-[var(--color-text-secondary)]">Name</span>
            <span class="text-sm font-medium text-[var(--color-text)]">{{ auth.user?.name }}</span>
          </div>
          <div class="flex items-center justify-between">
            <span class="text-sm text-[var(--color-text-secondary)]">Email</span>
            <span class="text-sm font-medium text-[var(--color-text)]">{{ auth.user?.email }}</span>
          </div>
          <div class="flex items-center justify-between">
            <span class="text-sm text-[var(--color-text-secondary)]">Role</span>
            <span class="rounded bg-blue-500/20 px-2 py-0.5 text-xs font-semibold uppercase text-blue-400">
              {{ auth.user?.roles?.join(', ') ?? '—' }}
            </span>
          </div>
        </div>
      </div>

      <div v-if="!isAdmin" class="rounded-xl border border-yellow-500/20 bg-yellow-500/5 p-4">
        <p class="text-sm text-yellow-400">Workspace and user management requires Owner or Admin role.</p>
      </div>

      <div v-if="isAdmin" class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-6">
        <h3 class="text-lg font-semibold text-[var(--color-text)]">Agent Token</h3>
        <p class="mt-1 text-sm text-[var(--color-text-secondary)]">Use this token to authenticate VPS monitoring agents.</p>
        <div class="mt-3 flex items-center gap-2">
          <code class="flex-1 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-2 text-sm text-[var(--color-text)]">
            ••••••••••••••••
          </code>
          <button class="rounded-lg border border-[var(--color-border)] px-3 py-2 text-sm text-[var(--color-text)] transition-colors hover:bg-[var(--color-bg-secondary)]">
            Show
          </button>
        </div>
      </div>
    </div>

    <!-- Users tab -->
    <div v-if="activeTab === 'users'">
      <div v-if="!canManageUsers" class="rounded-xl border border-yellow-500/20 bg-yellow-500/5 p-4">
        <p class="text-sm text-yellow-400">User management requires the manage_users permission.</p>
      </div>

      <div v-else class="space-y-4">
        <!-- Success message -->
        <div v-if="userSuccess" class="rounded-lg bg-green-500/10 px-4 py-3 text-sm text-green-400">
          {{ userSuccess }}
        </div>

        <!-- Add user button -->
        <div class="flex justify-end">
          <button
            @click="openCreateUser"
            class="inline-flex items-center gap-2 rounded-lg bg-blue-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-600"
          >
            <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            Add User
          </button>
        </div>

        <!-- Create/Edit user dialog -->
        <div
          v-if="showUserForm"
          class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
          @click.self="showUserForm = false; editingUser = null"
        >
          <div class="w-full max-w-lg rounded-xl border border-[var(--color-border)] bg-[var(--color-bg)] p-6 shadow-2xl">
            <h3 class="mb-4 text-lg font-semibold text-[var(--color-text)]">
              {{ editingUser ? 'Edit User Roles' : 'Create User' }}
            </h3>
            <div v-if="userError" class="mb-4 rounded-lg bg-red-500/10 px-4 py-3 text-sm text-red-400">
              {{ userError }}
            </div>
            <form @submit.prevent="handleSaveUser" class="space-y-4">
              <div v-if="!editingUser">
                <label class="mb-1.5 block text-sm font-medium text-[var(--color-text)]">Name</label>
                <input
                  v-model="userForm.name"
                  type="text"
                  required
                  placeholder="Full name"
                  class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] px-4 py-2.5 text-sm text-[var(--color-text)] placeholder-[var(--color-text-secondary)] outline-none transition-colors focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                />
              </div>
              <div v-if="!editingUser">
                <label class="mb-1.5 block text-sm font-medium text-[var(--color-text)]">Email</label>
                <input
                  v-model="userForm.email"
                  type="email"
                  required
                  placeholder="user@example.com"
                  class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] px-4 py-2.5 text-sm text-[var(--color-text)] placeholder-[var(--color-text-secondary)] outline-none transition-colors focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                />
              </div>
              <div v-if="!editingUser">
                <label class="mb-1.5 block text-sm font-medium text-[var(--color-text)]">Password</label>
                <input
                  v-model="userForm.password"
                  type="password"
                  required
                  minlength="6"
                  placeholder="Minimum 6 characters"
                  class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] px-4 py-2.5 text-sm text-[var(--color-text)] placeholder-[var(--color-text-secondary)] outline-none transition-colors focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                />
              </div>
              <div v-if="editingUser" class="rounded-lg bg-[var(--color-bg-secondary)] p-3">
                <p class="text-sm font-medium text-[var(--color-text)]">{{ editingUser.name }}</p>
                <p class="text-xs text-[var(--color-text-secondary)]">{{ editingUser.email }}</p>
              </div>
              <div>
                <label class="mb-1.5 block text-sm font-medium text-[var(--color-text)]">Roles</label>
                <div class="flex flex-wrap gap-2">
                  <label
                    v-for="role in roles"
                    :key="role.id"
                    class="inline-flex cursor-pointer items-center gap-1.5 rounded-lg border border-[var(--color-border)] px-3 py-1.5 text-sm transition-colors"
                    :class="userForm.roleIds.includes(role.id) ? 'border-blue-500 bg-blue-500/10 text-blue-400' : 'text-[var(--color-text-secondary)] hover:border-[var(--color-text)]'"
                  >
                    <input
                      type="checkbox"
                      :value="role.id"
                      v-model="userForm.roleIds"
                      class="sr-only"
                    />
                    {{ role.name }}
                  </label>
                </div>
                <p class="mt-1 text-xs text-[var(--color-text-secondary)]">Leave unchecked for default Viewer role.</p>
              </div>
              <div class="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  @click="showUserForm = false; editingUser = null"
                  class="rounded-lg border border-[var(--color-border)] px-4 py-2 text-sm font-medium text-[var(--color-text)] transition-colors hover:bg-[var(--color-bg-secondary)]"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  :disabled="userLoading"
                  class="rounded-lg bg-blue-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-600 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <span v-if="userLoading">Saving...</span>
                  <span v-else>{{ editingUser ? 'Update Roles' : 'Create User' }}</span>
                </button>
              </div>
            </form>
          </div>
        </div>

        <!-- Users table -->
        <EmptyState
          v-if="users.length === 0"
          title="No users"
          description="Add users to your workspace"
        />

        <div v-else class="space-y-3">
          <div
            v-for="u in users"
            :key="u.id"
            class="flex flex-col gap-3 rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4 sm:flex-row sm:items-center sm:justify-between"
          >
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-2">
                <h4 class="truncate text-sm font-semibold text-[var(--color-text)]">{{ u.name }}</h4>
                <span
                  :class="[
                    'shrink-0 rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase',
                    u.status === 'Active' ? 'bg-green-500/20 text-green-400' : u.status === 'Suspended' ? 'bg-red-500/20 text-red-400' : 'bg-gray-500/20 text-gray-400',
                  ]"
                >
                  {{ u.status }}
                </span>
              </div>
              <p class="mt-1 text-xs text-[var(--color-text-secondary)]">
                {{ u.email }} &middot; {{ u.roles.join(', ') || 'No roles' }}
              </p>
            </div>
            <div class="flex items-center gap-2">
              <span class="text-xs text-[var(--color-text-secondary)]">
                {{ new Date(u.createdAt).toLocaleDateString() }}
              </span>
              <button
                @click="openEditUser(u)"
                class="rounded-lg px-3 py-1.5 text-xs font-medium text-blue-400 transition-colors hover:bg-blue-500/10"
              >
                Edit
              </button>
              <button
                v-if="u.id !== auth.user?.id"
                @click="deleteConfirmUserId = u.id"
                class="rounded-lg px-3 py-1.5 text-xs font-medium text-red-400 transition-colors hover:bg-red-500/10"
              >
                Delete
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Delete user confirmation -->
    <div
      v-if="deleteConfirmUserId"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      @click.self="deleteConfirmUserId = null"
    >
      <div class="w-full max-w-sm rounded-xl border border-[var(--color-border)] bg-[var(--color-bg)] p-6 shadow-2xl">
        <h3 class="mb-2 text-lg font-semibold text-[var(--color-text)]">Delete User</h3>
        <p class="mb-4 text-sm text-[var(--color-text-secondary)]">Are you sure you want to delete this user? This action cannot be undone.</p>
        <div class="flex justify-end gap-3">
          <button
            @click="deleteConfirmUserId = null"
            class="rounded-lg border border-[var(--color-border)] px-4 py-2 text-sm font-medium text-[var(--color-text)] transition-colors hover:bg-[var(--color-bg-secondary)]"
          >
            Cancel
          </button>
          <button
            @click="handleDeleteUser(deleteConfirmUserId)"
            class="rounded-lg bg-red-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-red-600"
          >
            Delete
          </button>
        </div>
      </div>
    </div>

    <!-- Roles & Permissions tab -->
    <div v-if="activeTab === 'roles'">
      <div v-if="!canManageUsers" class="rounded-xl border border-yellow-500/20 bg-yellow-500/5 p-4">
        <p class="text-sm text-yellow-400">Role management requires the manage_users permission.</p>
      </div>

      <div v-else class="space-y-4">
        <!-- Success message -->
        <div v-if="roleSuccess" class="rounded-lg bg-green-500/10 px-4 py-3 text-sm text-green-400">
          {{ roleSuccess }}
        </div>

        <div class="flex justify-end">
          <button
            @click="openCreateRole"
            class="inline-flex items-center gap-2 rounded-lg bg-blue-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-600"
          >
            <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            Create Role
          </button>
        </div>

        <!-- Create/Edit role dialog -->
        <div
          v-if="showRoleForm"
          class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
          @click.self="showRoleForm = false; editingRole = null"
        >
          <div class="w-full max-w-2xl rounded-xl border border-[var(--color-border)] bg-[var(--color-bg)] p-6 shadow-2xl max-h-[85vh] overflow-y-auto">
            <h3 class="mb-4 text-lg font-semibold text-[var(--color-text)]">
              {{ editingRole ? 'Edit Role' : 'Create Role' }}
            </h3>
            <div v-if="roleError" class="mb-4 rounded-lg bg-red-500/10 px-4 py-3 text-sm text-red-400">
              {{ roleError }}
            </div>
            <form @submit.prevent="handleSaveRole" class="space-y-4">
              <div>
                <label class="mb-1.5 block text-sm font-medium text-[var(--color-text)]">Name</label>
                <input
                  v-model="roleForm.name"
                  type="text"
                  required
                  placeholder="Role name"
                  class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] px-4 py-2.5 text-sm text-[var(--color-text)] placeholder-[var(--color-text-secondary)] outline-none transition-colors focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                />
              </div>
              <div>
                <label class="mb-1.5 block text-sm font-medium text-[var(--color-text)]">Description</label>
                <input
                  v-model="roleForm.description"
                  type="text"
                  placeholder="Optional description"
                  class="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] px-4 py-2.5 text-sm text-[var(--color-text)] placeholder-[var(--color-text-secondary)] outline-none transition-colors focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                />
              </div>
              <div>
                <label class="mb-1.5 block text-sm font-medium text-[var(--color-text)]">Permissions</label>
                <div class="space-y-4 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4">
                  <div v-for="[category, perms] in permissionsByCategory" :key="category">
                    <h5 class="mb-2 text-xs font-semibold uppercase text-[var(--color-text-secondary)]">{{ category }}</h5>
                    <div class="flex flex-wrap gap-2">
                      <label
                        v-for="p in perms"
                        :key="p.name"
                        class="inline-flex cursor-pointer items-center gap-1.5 rounded-lg border px-3 py-1.5 text-xs transition-colors"
                        :class="roleForm.permissionNames.includes(p.name) ? 'border-blue-500 bg-blue-500/10 text-blue-400' : 'border-[var(--color-border)] text-[var(--color-text-secondary)] hover:border-[var(--color-text)]'"
                      >
                        <input
                          type="checkbox"
                          :value="p.name"
                          :checked="roleForm.permissionNames.includes(p.name)"
                          @change="(e: Event) => {
                            const target = e.target as HTMLInputElement
                            if (target.checked) {
                              roleForm.permissionNames.push(p.name)
                            } else {
                              roleForm.permissionNames = roleForm.permissionNames.filter(n => n !== p.name)
                            }
                          }"
                          class="sr-only"
                        />
                        {{ p.name }}
                      </label>
                    </div>
                  </div>
                </div>
              </div>
              <div class="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  @click="showRoleForm = false; editingRole = null"
                  class="rounded-lg border border-[var(--color-border)] px-4 py-2 text-sm font-medium text-[var(--color-text)] transition-colors hover:bg-[var(--color-bg-secondary)]"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  :disabled="roleLoading"
                  class="rounded-lg bg-blue-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-600 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <span v-if="roleLoading">Saving...</span>
                  <span v-else>{{ editingRole ? 'Update Role' : 'Create Role' }}</span>
                </button>
              </div>
            </form>
          </div>
        </div>

        <!-- Role cards -->
        <EmptyState
          v-if="roleDetails.length === 0"
          title="No roles"
          description="Create roles to manage permissions"
        />

        <div v-else class="space-y-3">
          <div
            v-for="role in roleDetails"
            :key="role.id"
            class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-5"
          >
            <div class="flex items-start justify-between">
              <div class="min-w-0 flex-1">
                <div class="flex items-center gap-2">
                  <h4 class="text-sm font-semibold text-[var(--color-text)]">{{ role.name }}</h4>
                  <span v-if="role.isSystem" class="rounded bg-yellow-500/20 px-1.5 py-0.5 text-[10px] font-semibold uppercase text-yellow-400">
                    System
                  </span>
                </div>
                <p v-if="role.description" class="mt-1 text-xs text-[var(--color-text-secondary)]">{{ role.description }}</p>
              </div>
              <div v-if="!role.isSystem" class="flex items-center gap-2">
                <button
                  @click="openEditRole(role)"
                  class="rounded-lg px-3 py-1.5 text-xs font-medium text-blue-400 transition-colors hover:bg-blue-500/10"
                >
                  Edit
                </button>
                <button
                  @click="deleteConfirmRoleId = role.id"
                  class="rounded-lg px-3 py-1.5 text-xs font-medium text-red-400 transition-colors hover:bg-red-500/10"
                >
                  Delete
                </button>
              </div>
              <div v-else>
                <span class="text-xs text-[var(--color-text-secondary)]">🔒 Protected</span>
              </div>
            </div>
            <div class="mt-3 flex flex-wrap gap-1.5">
              <span
                v-for="perm in role.permissions"
                :key="perm"
                class="rounded bg-[var(--color-bg)] px-2 py-0.5 text-[10px] font-medium text-[var(--color-text-secondary)]"
              >
                {{ perm }}
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Delete role confirmation -->
    <div
      v-if="deleteConfirmRoleId"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      @click.self="deleteConfirmRoleId = null"
    >
      <div class="w-full max-w-sm rounded-xl border border-[var(--color-border)] bg-[var(--color-bg)] p-6 shadow-2xl">
        <h3 class="mb-2 text-lg font-semibold text-[var(--color-text)]">Delete Role</h3>
        <p class="mb-4 text-sm text-[var(--color-text-secondary)]">Are you sure you want to delete this role? Users with this role will lose its permissions.</p>
        <div class="flex justify-end gap-3">
          <button
            @click="deleteConfirmRoleId = null"
            class="rounded-lg border border-[var(--color-border)] px-4 py-2 text-sm font-medium text-[var(--color-text)] transition-colors hover:bg-[var(--color-bg-secondary)]"
          >
            Cancel
          </button>
          <button
            @click="handleDeleteRole(deleteConfirmRoleId)"
            class="rounded-lg bg-red-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-red-600"
          >
            Delete
          </button>
        </div>
      </div>
    </div>

    <!-- Alert Rules tab -->
    <div v-if="activeTab === 'rules'">
      <div class="mb-4 flex justify-end">
        <button
          @click="openCreateRule"
          class="inline-flex items-center gap-2 rounded-lg bg-blue-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-600"
        >
          <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 4v16m8-8H4" />
          </svg>
          Add Rule
        </button>
      </div>

      <!-- Rule form dialog -->
      <div
        v-if="showRuleForm"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
        @click.self="showRuleForm = false; editingRule = null"
      >
        <div class="w-full max-w-lg rounded-xl border border-[var(--color-border)] bg-[var(--color-bg)] p-6 shadow-2xl">
          <h3 class="mb-4 text-lg font-semibold text-[var(--color-text)]">
            {{ editingRule ? 'Edit Rule' : 'Create Alert Rule' }}
          </h3>
          <AlertRuleForm
            :initial-data="editingRule"
            @submit="handleRuleSubmit"
            @cancel="showRuleForm = false; editingRule = null"
          />
        </div>
      </div>

      <EmptyState
        v-if="alerts.alertRules.length === 0"
        title="No alert rules"
        description="Create alert rules to monitor your infrastructure"
      />

      <div v-else class="space-y-3">
        <div
          v-for="rule in alerts.alertRules"
          :key="rule.id"
          class="flex flex-col gap-3 rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-4 sm:flex-row sm:items-center sm:justify-between"
        >
          <div class="min-w-0 flex-1">
            <div class="flex items-center gap-2">
              <h4 class="truncate text-sm font-semibold text-[var(--color-text)]">{{ rule.name }}</h4>
              <span
                :class="[
                  'shrink-0 rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase',
                  rule.isEnabled ? 'bg-green-500/20 text-green-400' : 'bg-gray-500/20 text-gray-400',
                ]"
              >
                {{ rule.isEnabled ? 'Enabled' : 'Disabled' }}
              </span>
            </div>
            <p class="mt-1 text-xs text-[var(--color-text-secondary)]">
              {{ rule.sourceType }} &middot; {{ rule.conditionType }} &middot; Threshold: {{ rule.threshold }} &middot; {{ rule.severity }}
            </p>
          </div>
          <div class="flex items-center gap-2">
            <button
              @click="openEditRule(rule)"
              class="rounded-lg px-3 py-1.5 text-xs font-medium text-blue-400 transition-colors hover:bg-blue-500/10"
            >
              Edit
            </button>
            <button
              @click="handleDeleteRule(rule.id)"
              class="rounded-lg px-3 py-1.5 text-xs font-medium text-red-400 transition-colors hover:bg-red-500/10"
            >
              Delete
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Retention tab -->
    <div v-if="activeTab === 'retention'">
      <div v-if="!isAdmin" class="rounded-xl border border-yellow-500/20 bg-yellow-500/5 p-4">
        <p class="text-sm text-yellow-400">Retention settings require Owner or Admin role.</p>
      </div>

      <div v-else class="space-y-4">
        <div class="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-secondary)] p-6">
          <h3 class="text-lg font-semibold text-[var(--color-text)]">Log Retention (days)</h3>
          <p class="mt-1 text-sm text-[var(--color-text-secondary)]">Configure how long different data types are retained.</p>

          <div class="mt-4 space-y-4">
            <div class="flex items-center justify-between">
              <label class="text-sm text-[var(--color-text)]">GitHub Actions Logs</label>
              <input
                v-model.number="retention.githubActions"
                type="number"
                min="1"
                max="365"
                class="w-24 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-1.5 text-sm text-[var(--color-text)] text-right"
              />
            </div>
            <div class="flex items-center justify-between">
              <label class="text-sm text-[var(--color-text)]">PM2 Logs</label>
              <input
                v-model.number="retention.pm2"
                type="number"
                min="1"
                max="90"
                class="w-24 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-1.5 text-sm text-[var(--color-text)] text-right"
              />
            </div>
            <div class="flex items-center justify-between">
              <label class="text-sm text-[var(--color-text)]">Error Logs</label>
              <input
                v-model.number="retention.errors"
                type="number"
                min="1"
                max="365"
                class="w-24 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-1.5 text-sm text-[var(--color-text)] text-right"
              />
            </div>
            <div class="flex items-center justify-between">
              <label class="text-sm text-[var(--color-text)]">Alert History</label>
              <input
                v-model.number="retention.alerts"
                type="number"
                min="1"
                max="730"
                class="w-24 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-1.5 text-sm text-[var(--color-text)] text-right"
              />
            </div>
            <div class="flex items-center justify-between">
              <label class="text-sm text-[var(--color-text)]">Aggregated Metrics</label>
              <input
                v-model.number="retention.metrics"
                type="number"
                min="1"
                max="1825"
                class="w-24 rounded-lg border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-1.5 text-sm text-[var(--color-text)] text-right"
              />
            </div>
          </div>

          <div class="mt-6 flex justify-end">
            <button
              @click="saveRetention"
              class="rounded-lg bg-blue-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-600"
            >
              Save Retention Settings
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
