export default defineNuxtRouteMiddleware(async (to) => {
  // Public routes
  if (to.path === '/login') return

  const { isAuthenticated, restore } = useAuth()

  if (!isAuthenticated.value) {
    const ok = await restore()
    if (!ok) return navigateTo({ path: '/login', query: { redirect: to.fullPath } })
  }
})
