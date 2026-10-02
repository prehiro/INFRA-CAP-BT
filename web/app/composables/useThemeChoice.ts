/**
 * Persisted theme choices.
 *
 * app.config.ui.colors is only runtime state, so a colour picked from the sidebar menu
 * is lost on reload and the app snaps back to the default. Both the accent and the neutral
 * ramp are therefore mirrored into cookies and re-applied on startup, which is what makes
 * the choice survive a refresh or a restart of the browser.
 *
 * Mirroring rather than replacing: the colour still lives in appConfig (that is what the
 * Nuxt UI components read), and the cookie is only the persistence layer.
 */
export interface ThemeChoice {
  primary: string
  neutral: string
}

const COOKIE = 'infra-cap.theme'
const VALID_PRIMARY = ['green', 'red', 'orange', 'amber', 'yellow', 'lime', 'emerald', 'teal', 'cyan', 'sky', 'blue', 'indigo', 'violet', 'purple', 'fuchsia', 'pink', 'rose']
const VALID_NEUTRAL = ['slate', 'gray', 'zinc', 'neutral', 'stone', 'taupe', 'mauve', 'mist', 'olive']

function isValid(list: string[], v: unknown): v is string {
  return typeof v === 'string' && list.includes(v)
}

export function useThemeChoice() {
  const appConfig = useAppConfig()
  const cookie = useCookie<string | null>(COOKIE, { default: () => null, maxAge: 60 * 60 * 24 * 365 })

  // Applied once per page load, before anything reads a colour.
  function restore() {
    const raw = cookie.value
    if (!raw) return
    try {
      const parsed = JSON.parse(raw) as Partial<ThemeChoice>
      if (isValid(VALID_PRIMARY, parsed.primary)) appConfig.ui.colors.primary = parsed.primary
      if (isValid(VALID_NEUTRAL, parsed.neutral)) appConfig.ui.colors.neutral = parsed.neutral
    } catch {
      // A corrupt cookie must not brick the app; fall back to the defaults silently.
    }
  }

  function persist() {
    cookie.value = JSON.stringify({
      primary: appConfig.ui.colors.primary,
      neutral: appConfig.ui.colors.neutral
    })
  }

  function setPrimary(color: string) {
    if (!isValid(VALID_PRIMARY, color)) return
    appConfig.ui.colors.primary = color
    persist()
  }

  function setNeutral(color: string) {
    if (!isValid(VALID_NEUTRAL, color)) return
    appConfig.ui.colors.neutral = color
    persist()
  }

  return { restore, setPrimary, setNeutral }
}