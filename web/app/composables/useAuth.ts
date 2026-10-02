export function useAuth() {
  const token = useToken()
  const user = useUser()

  const isAuthenticated = computed(() => !!token.value)
  const isAdmin = computed(() => user.value?.roles?.some(r => r.name === 'Admin') ?? false)
  const roleNames = computed(() => user.value?.roles?.map(r => r.name) ?? [])

  async function login(username: string, password: string) {
    const res = await apiLogin(username, password)
    token.value = res.token
    user.value = res.user
    writeStoredToken(res.token)
  }

  function logout() {
    token.value = null
    user.value = null
    writeStoredToken(null)
    // Deliberately no useRoute() here: this composable is also used from route
    // middleware, where useRoute() is invalid (Nuxt NUXT_E2005).
    return navigateTo('/login')
  }

  /** Restore the session after a page reload in SPA mode. */
  async function restore() {
    if (token.value && user.value) return true
    const stored = readStoredToken()
    if (!stored) return false
    token.value = stored
    try {
      user.value = await apiMe()
      return true
    } catch {
      token.value = null
      writeStoredToken(null)
      return false
    }
  }

  return { token, user, isAuthenticated, isAdmin, roleNames, login, logout, restore }
}
