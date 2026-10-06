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
/**
 * Read the persisted theme choice from the raw cookie.
 *
 * WHY NOT useCookie - MEASURED, and this cost four wrong attempts. `restore()` was called from
 * UserMenu's onMounted, then app.vue's, then a plugin via onNuxtReady, then a plugin called
 * directly. In ALL FOUR the stored colour was ignored: the cookie read violet/slate and
 * `--ui-primary` still came back green after a reload. Calling `t.restore()` from a dynamic
 * import in the browser console did not move the cookie either, which rules out the call site
 * and points at `useCookie` itself - its ref is not the one the app reads and/or its write is
 * not reaching document.cookie from these contexts.
 *
 * So the cookie is read and written directly. That is honest about what this is: a two-key
 * preference stored in a cookie the app already owns, with a shape validated against the same
 * VALID_* lists. It removes a dependency that demonstrably does not work here.
 *
 * The colour still lives in appConfig - that is what the Nuxt UI components read, and Nuxt UI
 * treats appConfig reactively, so assigning to it updates the running UI. The cookie is only
 * the persistence layer.
 */
export interface ThemeChoice {
  primary: string
  neutral: string
}

const COOKIE = 'infra-cap.theme'
const VALID_PRIMARY = ['green', 'red', 'orange', 'amber', 'yellow', 'lime', 'emerald', 'teal', 'cyan', 'sky', 'blue', 'indigo', 'violet', 'purple', 'fuchsia', 'pink', 'rose']
const VALID_NEUTRAL = ['slate', 'gray', 'zinc', 'neutral', 'stone', 'taupe', 'mauve', 'mist', 'olive']

/**
 * The app-wide default (HIRO, 2026-10-05: "make accent color green, and neutral color zinc as
 * default theme for all user"). These MUST stay in step with web/app/app.config.ts - that file
 * is the runtime truth the components read, this one is the persistence layer's truth. They
 * drifted apart once already when the default was slate, which is why they are named constants
 * rather than two inline strings.
 */
export const DEFAULT_PRIMARY = 'green'
export const DEFAULT_NEUTRAL = 'zinc'

/**
 * The default pair that shipped before zinc, i.e. green/slate.
 *
 * WITHOUT THIS MIGRATION the request above would only have half-worked, and the failure would
 * have been invisible on the machine doing the testing: any browser that had EVER opened the
 * app - which is every browser with a real user in it - would have kept its stored slate and
 * never seen the new default. The person testing on a fresh profile would have seen zinc and
 * reported success.
 *
 * The trade-off is explicit and small: a user who deliberately picked exactly green + slate
 * before this change is indistinguishable from one who never picked anything, and their cookie
 * is treated as "no choice made" so they get the new default. The alternative - leaving stale
 * cookies in place - means the default change is invisible to the entire existing user base,
 * which is worse.
 */
const LEGACY_DEFAULTS: ThemeChoice = { primary: 'green', neutral: 'slate' }

function isValid(list: string[], v: unknown): v is string {
  return typeof v === 'string' && list.includes(v)
}

function isLegacyDefault(c: ThemeChoice): boolean {
  return c.primary === LEGACY_DEFAULTS.primary && c.neutral === LEGACY_DEFAULTS.neutral
}

/** The raw cookie, read straight off document. Returns null when absent or unreadable. */
function readRawCookie(): string | null {
  if (import.meta.server) return null
  const match = document.cookie.match(new RegExp(`(?:^|; )${COOKIE}=([^;]*)`))
  return match ? match[1] : null
}

function writeRawCookie(value: string) {
  if (import.meta.server) return
  const oneYear = 60 * 60 * 24 * 365
  document.cookie = `${COOKIE}=${encodeURIComponent(value)}; path=/; max-age=${oneYear}; SameSite=Lax`
}

export function useThemeChoice() {
  const appConfig = useAppConfig()

  /** Start from the app default every time, so a cookie that only half-parses cannot leave one
   *  of the two colours sitting on a stale value. */
  function applyDefaults() {
    appConfig.ui.colors.primary = DEFAULT_PRIMARY
    appConfig.ui.colors.neutral = DEFAULT_NEUTRAL
  }

  function persist(choice: ThemeChoice) {
    writeRawCookie(JSON.stringify(choice))
  }

  // Applied once per page load, before anything reads a colour.
  function restore() {
    const raw = readRawCookie()
    if (!raw) {
      applyDefaults()
      return
    }
    try {
      const parsed = JSON.parse(decodeURIComponent(raw)) as Partial<ThemeChoice>
      const choice: ThemeChoice = {
        primary: isValid(VALID_PRIMARY, parsed.primary) ? parsed.primary : DEFAULT_PRIMARY,
        neutral: isValid(VALID_NEUTRAL, parsed.neutral) ? parsed.neutral : DEFAULT_NEUTRAL
      }
      // A cookie holding the pre-zinc default is not a choice - it is the old default that
      // happens to have been written down. Treat it as absent and rewrite it, so the new
      // default actually reaches browsers that already have one.
      if (isLegacyDefault(choice)) {
        applyDefaults()
        persist({ primary: DEFAULT_PRIMARY, neutral: DEFAULT_NEUTRAL })
        return
      }
      appConfig.ui.colors.primary = choice.primary
      appConfig.ui.colors.neutral = choice.neutral
    } catch {
      // A corrupt cookie must not brick the app; fall back to the defaults silently.
      applyDefaults()
    }
  }

  /**
   * Write the CURRENT pair, read back from appConfig rather than assumed.
   *
   * WHY NOT JUST PERSIST AFTER ASSIGNING: the obvious `appConfig.ui.colors.primary = color;
   * persist()` is wrong, and measurably so. Verified in the browser console: `setPrimary('violet')`
   * wrote `{"primary":"violet","neutral":"stone"}` - the accent was violet but the neutral had
   * already been changed underneath it by app.config's default. `persist()` had no idea what the
   * neutral was supposed to be and simply wrote whatever appConfig held.
   *
   * So `currentChoice()` is the single place that answers "what are the colours right now",
   * and the cookie is always written from that. It reads appConfig and falls back to the
   * exported defaults for any value that is not a valid colour name - which is exactly the
   * case when the composable is called outside a component setup and appConfig is not the live
   * one.
   */
  function currentChoice(): ThemeChoice {
    const p = appConfig.ui.colors.primary
    const n = appConfig.ui.colors.neutral
    return {
      primary: isValid(VALID_PRIMARY, p) ? p : DEFAULT_PRIMARY,
      neutral: isValid(VALID_NEUTRAL, n) ? n : DEFAULT_NEUTRAL
    }
  }

  function setPrimary(color: string) {
    if (!isValid(VALID_PRIMARY, color)) return
    appConfig.ui.colors.primary = color
    persist(currentChoice())
  }

  function setNeutral(color: string) {
    if (!isValid(VALID_NEUTRAL, color)) return
    appConfig.ui.colors.neutral = color
    persist(currentChoice())
  }

  return { restore, setPrimary, setNeutral }
}