import { createRouter, createWebHistory } from 'vue-router'
import { authGuard } from './guards'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/auth/LoginView.vue'),
      meta: { guest: true },
    },
    {
      path: '/',
      component: () => import('@/components/layout/AppLayout.vue'),
      meta: { requiresAuth: true },
      children: [
        {
          path: '',
          name: 'overview',
          component: () => import('@/views/OverviewView.vue'),
          meta: { requiredPermission: 'view_dashboards' },
        },
        {
          path: 'repositories',
          name: 'repositories',
          component: () => import('@/views/RepositoriesView.vue'),
          meta: { requiredPermission: 'connect_repos' },
        },
        {
          path: 'repositories/:id',
          name: 'repository-detail',
          component: () => import('@/views/RepositoryDetailView.vue'),
          meta: { requiredPermission: 'connect_repos' },
        },
        {
          path: 'servers',
          name: 'servers',
          component: () => import('@/views/ServersView.vue'),
          meta: { requiredPermission: 'view_servers' },
        },
        {
          path: 'servers/:id',
          name: 'server-detail',
          component: () => import('@/views/ServerDetailView.vue'),
          meta: { requiredPermission: 'view_servers' },
        },
        {
          path: 'deploy',
          name: 'deploy',
          component: () => import('@/views/DeployView.vue'),
          meta: { requiredPermission: 'deploy_agents' },
        },
        {
          path: 'pm2',
          name: 'pm2-processes',
          component: () => import('@/views/PM2ProcessesView.vue'),
          meta: { requiredPermission: 'view_pm2_logs' },
        },
        {
          path: 'pm2/:processId',
          name: 'pm2-process-detail',
          component: () => import('@/views/PM2ProcessDetailView.vue'),
          meta: { requiredPermission: 'view_pm2_logs' },
        },
        {
          path: 'systemd',
          name: 'systemd-services',
          component: () => import('@/views/SystemdServicesView.vue'),
          meta: { requiredPermission: 'view_pm2_logs' },
        },
        {
          path: 'systemd/:serviceId',
          name: 'systemd-service-detail',
          component: () => import('@/views/SystemdServiceDetailView.vue'),
          meta: { requiredPermission: 'view_pm2_logs' },
        },
        {
          path: 'logs',
          name: 'logs',
          component: () => import('@/views/LogsView.vue'),
          meta: { requiredPermission: 'view_audit_logs' },
        },
        {
          path: 'alerts',
          name: 'alerts',
          component: () => import('@/views/AlertsView.vue'),
          meta: { requiredPermission: 'view_dashboards' },
        },
        {
          path: 'settings',
          name: 'settings',
          component: () => import('@/views/SettingsView.vue'),
        },
      ],
    },
  ],
})

router.beforeEach(authGuard)

export default router
