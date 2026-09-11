import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth.store'
import AppLayout from '../layouts/AppLayout.vue'
import LoginView from '../../modules/auth/LoginView.vue'
import ProjectsView from '../../modules/projects/ProjectsView.vue'
import ProjectBoardView from '../../modules/projects/ProjectBoardView.vue'
import TaskDetailView from '../../modules/tasks/TaskDetailView.vue'
import MyWorkView from '../../modules/work/MyWorkView.vue'
import NotificationsView from '../../modules/notifications/NotificationsView.vue'
import DashboardView from '../../modules/dashboard/DashboardView.vue'
import BasicStateView from '../../shared/components/BasicStateView.vue'

export const routes = [
  { path: '/login', component: LoginView, meta: { guest: true } },
  {
    path: '/',
    component: AppLayout,
    meta: { requiresAuth: true },
    children: [
    { path: 'dashboard', component: DashboardView, meta: { requiresAuth: true, roles: ['Admin'] } },
    { path: 'projects', component: ProjectsView, meta: { requiresAuth: true } },
    { path: 'projects/:projectId', component: ProjectBoardView, props: true, meta: { requiresAuth: true } },
    { path: 'tasks/:taskId', component: TaskDetailView, props: true, meta: { requiresAuth: true } },
    { path: 'my-work', component: MyWorkView, meta: { requiresAuth: true } },
    { path: 'notifications', component: NotificationsView, meta: { requiresAuth: true } },
    { path: '403', component: BasicStateView, props: { kind: 'forbidden' } }
    ]
  },
  { path: '/:pathMatch(.*)*', component: BasicStateView, props: { kind: 'notFound' } }
]
export const resolveNavigation = (to, auth) => {
  if (to.meta.requiresAuth && !auth.isAuthenticated) return { path: '/login', query: { redirect: to.fullPath } }
  if (to.path === '/' && auth.isAuthenticated) return auth.isAdmin ? '/dashboard' : '/my-work'
  if (to.meta.roles && !to.meta.roles.includes(auth.user?.role)) return '/403'
  if (to.meta.guest && auth.isAuthenticated) return auth.isAdmin ? '/dashboard' : '/my-work'
}
export const restoreSessionOrRedirect = async (to, auth) => {
  if (!auth.sessionChecked) {
    try {
      await auth.restoreSession()
    } catch {
      if (to.path === '/login') return
      return { path: '/login', query: { redirect: to.fullPath, error: 'unavailable' } }
    }
  }

  return resolveNavigation(to, auth)
}
const router = createRouter({ history: createWebHistory(), routes })
router.beforeEach(to => restoreSessionOrRedirect(to, useAuthStore()))
export default router
