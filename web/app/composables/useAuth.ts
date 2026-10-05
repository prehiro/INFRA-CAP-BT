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

  /**
   * Re-read the signed-in user from the server.
   *
   * WHY THIS EXISTS: `useUser()` is a global useState populated once at login, and the
   * Dashboard's WelcomeBanner, the sidebar UserMenu and the avatar initials all read from it.
   * Editing your OWN account on /users updated the database but left that state untouched, so
   * a renamed user kept seeing the old name everywhere until a full browser refresh re-ran
   * `restore()`. Callers that mutate the current user must call this afterwards.
   *
   * Returns true when the refresh succeeded. It deliberately does NOT log out or clear the
   * token on failure: a transient network error must not sign the user out of a session that
   * is still valid, and the next refresh will pick the change up.
   */
  async function refreshUser(): Promise<boolean> {
    if (!token.value) return false
    try {
      user.value = await apiMe()
      return true
    } catch {
      return false
    }
  }

  return { token, user, isAuthenticated, isAdmin, roleNames, login, logout, restore, refreshUser }
}
