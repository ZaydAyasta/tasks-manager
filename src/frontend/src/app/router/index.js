import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth.store'
import AppLayout from '../layouts/AppLayout.vue'
import LoginView from '../../modules/auth/LoginView.vue'
import ProjectsView from '../../modules/projects/ProjectsView.vue'
import ProjectBoardView from '../../modules/projects/ProjectBoardView.vue'
import TaskDetailView from '../../modules/tasks/TaskDetailView.vue'
import MyWorkView from '../../modules/work/MyWorkView.vue'
import BasicStateView from '../../shared/components/BasicStateView.vue'

export const routes = [
  { path: '/login', component: LoginView, meta: { guest: true } },
  { path: '/', component: AppLayout, meta: { requiresAuth: true }, children: [
    { path: '', redirect: '/projects' }, { path: 'projects', component: ProjectsView, meta: { requiresAuth: true } },
    { path: 'projects/:projectId', component: ProjectBoardView, props: true, meta: { requiresAuth: true } },
    { path: 'tasks/:taskId', component: TaskDetailView, props: true, meta: { requiresAuth: true } },
    { path: 'my-work', component: MyWorkView, meta: { requiresAuth: true } },
    { path: '403', component: BasicStateView, props: { kind: 'forbidden' } }
  ] },
  { path: '/:pathMatch(.*)*', component: BasicStateView, props: { kind: 'notFound' } }
]
export const resolveNavigation = (to, auth) => {
  if (to.meta.requiresAuth && !auth.isAuthenticated) return { path: '/login', query: { redirect: to.fullPath } }
  if (to.meta.roles && !to.meta.roles.includes(auth.user?.role)) return '/403'
  if (to.meta.guest && auth.isAuthenticated) return auth.isAdmin ? '/projects' : '/my-work'
}
const router = createRouter({ history: createWebHistory(), routes })
router.beforeEach(async to => {
  const auth = useAuthStore()
  if (!auth.accessToken && localStorage.getItem('nakama.access-token')) await auth.restoreSession()
  return resolveNavigation(to, auth)
})
export default router
