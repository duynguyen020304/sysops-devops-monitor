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
      path: '/register',
      name: 'register',
      component: () => import('@/views/auth/RegisterView.vue'),
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
        },
        {
          path: 'repositories',
          name: 'repositories',
          component: () => import('@/views/RepositoriesView.vue'),
        },
        {
          path: 'repositories/:id',
          name: 'repository-detail',
          component: () => import('@/views/RepositoryDetailView.vue'),
        },
        {
          path: 'servers',
          name: 'servers',
          component: () => import('@/views/ServersView.vue'),
        },
        {
          path: 'servers/:id',
          name: 'server-detail',
          component: () => import('@/views/ServerDetailView.vue'),
        },
        {
          path: 'pm2',
          name: 'pm2-processes',
          component: () => import('@/views/PM2ProcessesView.vue'),
        },
        {
          path: 'pm2/:processId',
          name: 'pm2-process-detail',
          component: () => import('@/views/PM2ProcessDetailView.vue'),
        },
        {
          path: 'logs',
          name: 'logs',
          component: () => import('@/views/LogsView.vue'),
        },
        {
          path: 'alerts',
          name: 'alerts',
          component: () => import('@/views/AlertsView.vue'),
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
