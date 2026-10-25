import { referenceComponents, referenceDocs, referenceIconSets, referenceComponentOverride } from '~/utils/themeReference'
import { carbonIcons, extraDefaults, fontStacks, validTheme, type StudioTheme } from '~/utils/themeStudio'

const KEY = 'infra-cap.theme-studio.v1'
const BASE = `${KEY}.original`
const components = referenceComponents
const clone = <T>(v: T): T => JSON.parse(JSON.stringify(v))

export function useThemeStudio() {
  const config = useAppConfig()
  const mode = useColorMode()
  const choice = useThemeChoice()
  const extras = useState('studio-extras', () => ({ ...extraDefaults }))
  const undoStack = useState<StudioTheme[]>('studio-undo', () => [])
  const redoStack = useState<StudioTheme[]>('studio-redo', () => [])
  const original = useState<{ theme: StudioTheme; cookie: string | null } | null>('studio-original', () => null)
  const error = useState('studio-error', () => '')
  const ready = useState('studio-ready', () => false)
  const baseline = useState<any>('studio-ui-baseline', () => null)
  const current = computed<StudioTheme>(() => ({
    primary: config.ui.colors.primary, neutral: config.ui.colors.neutral,
    mode: mode.preference, ...extras.value
  }))

  function apply(theme: StudioTheme) {
    if (!validTheme(theme)) return
    choice.setPrimary(theme.primary)
    choice.setNeutral(theme.neutral)
    mode.preference = theme.mode
    extras.value = { reference: theme.reference || '', font: theme.font, icons: theme.icons, radius: theme.radius, size: theme.size, variant: theme.variant }
    const style = document.documentElement.style
    for (const [key, value] of Object.entries({ '--font-sans': fontStacks[theme.font], '--ui-radius': theme.radius === 'original' ? undefined : `${theme.radius}rem` })) {
      const old = baseline.value.styles[key]
      if (value) style.setProperty(key, value)
      else if (old.value) style.setProperty(key, old.value, old.priority)
      else style.removeProperty(key)
    }
    const ui = config.ui as any
    ui.icons = { ...clone(baseline.value.icons), ...(theme.icons === 'carbon' ? carbonIcons : referenceIconSets[theme.icons] || {}) }
    const doc = referenceDocs[theme.reference || '']
    for (const key of ['secondary', 'success', 'info', 'warning', 'error']) {
      const value = doc?.colors?.[key] ?? baseline.value.colors[key]
      if (value === undefined) delete ui.colors[key]
      else ui.colors[key] = value
    }
    for (const name of components) {
      const old = baseline.value.components[name]
      if (old === null) delete ui[name]
      else ui[name] = clone(old)
      const refPatch = referenceComponentOverride(theme.reference || '', name)
      if (Object.keys(refPatch).length) {
        ui[name] ??= {}
        if (refPatch.slots) {
          ui[name].slots = { ...ui[name].slots }
          for (const [key, value] of Object.entries(refPatch.slots)) ui[name].slots[key] = `${ui[name].slots[key] || ''} ${value}`.trim()
        }
        if (refPatch.compoundVariants) ui[name].compoundVariants = [...(ui[name].compoundVariants || []), ...clone(refPatch.compoundVariants)]
      }
      const overrides: Record<string, string> = { ...refPatch.defaultVariants }
      if (theme.size !== 'original') overrides.size = theme.size
      // Variant choices are button/badge defaults; input variants have a different contract.
      if (theme.variant !== 'original' && ['button', 'badge'].includes(name)) overrides.variant = theme.variant
      if (Object.keys(overrides).length) {
        ui[name] ??= {}
        ui[name].defaultVariants = { ...ui[name].defaultVariants, ...overrides }
      }
    }
  }
  function save(theme: StudioTheme) {
    localStorage.setItem(KEY, JSON.stringify(theme))
    apply(theme)
  }
  function change(patch: Partial<StudioTheme>) {
    const next = { ...current.value, ...patch }
    if (!validTheme(next) || JSON.stringify(next) === JSON.stringify(current.value)) return
    try {
      // Durable write MUST precede the first mutation. Never overwrite this rollback point.
      if (!original.value) {
        const snapshot = { theme: clone(current.value), cookie: document.cookie.match(/(?:^|; )infra-cap\.theme=([^;]*)/)?.[1] ?? null }
        localStorage.setItem(BASE, JSON.stringify(snapshot))
        original.value = snapshot
      }
      const previous = clone(current.value)
      save(next)
      undoStack.value = [...undoStack.value.slice(-49), previous]
      redoStack.value = []
      error.value = ''
    } catch { error.value = 'Browser storage is unavailable. Theme changes could not be saved.' }
  }
  function travel(from: StudioTheme[], to: StudioTheme[]) {
    const target = from.at(-1)
    if (!target) return
    try { const previous = clone(current.value); save(target); from.pop(); to.push(previous); error.value = '' }
    catch { error.value = 'Could not save this theme change.' }
  }
  function restoreOriginal() {
    if (!original.value) return
    try {
      const snapshot = original.value
      // Leave the recovery snapshot intact until applying and clearing the preview succeeded.
      apply(snapshot.theme)
      document.cookie = snapshot.cookie === null
        ? 'infra-cap.theme=; path=/; max-age=0; SameSite=Lax'
        : `infra-cap.theme=${snapshot.cookie}; path=/; max-age=31536000; SameSite=Lax`
      localStorage.removeItem(KEY)
      localStorage.removeItem(BASE)
      original.value = null
      undoStack.value = []; redoStack.value = []; error.value = ''
    } catch { error.value = 'Restore could not finish. Your original settings are still backed up.' }
  }
  function init() {
    if (ready.value) return
    const ui = config.ui as any
    baseline.value = {
      icons: clone(ui.icons || {}),
      colors: clone(ui.colors),
      components: Object.fromEntries(components.map(name => [name, ui[name] ? clone(ui[name]) : null])),
      styles: Object.fromEntries(['--font-sans', '--ui-radius'].map(key => [key, {
        value: document.documentElement.style.getPropertyValue(key), priority: document.documentElement.style.getPropertyPriority(key)
      }]))
    }
    ready.value = true
    try {
      const savedOriginal = JSON.parse(localStorage.getItem(BASE) || 'null')
      if (savedOriginal && validTheme(savedOriginal.theme) && (savedOriginal.cookie === null || typeof savedOriginal.cookie === 'string')) original.value = savedOriginal
      const saved = JSON.parse(localStorage.getItem(KEY) || 'null')
      // Never load a preview without its rollback snapshot.
      if (original.value && validTheme(saved)) apply({ ...saved, primary: config.ui.colors.primary, neutral: config.ui.colors.neutral, mode: mode.preference })
    } catch { error.value = 'Stored preview could not be read; existing theme settings were kept.' }
  }
  if (import.meta.client) init()
  return { current, original, error, undoStack, redoStack, change, restoreOriginal,
    undo: () => travel(undoStack.value, redoStack.value), redo: () => travel(redoStack.value, undoStack.value) }
}
