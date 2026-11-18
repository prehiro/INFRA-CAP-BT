/**
 * Favicon follows the accent colour.
 *
 * The tab icon is an SVG file, and a file in `public/` cannot know what colour the user picked in
 * the Theme Studio, so it ships with the default accent baked in. This keeps a `data:` URL version
 * of the SAME glyph in the document head, rebuilt with the current `--ui-primary` so switching the
 * accent repaints the tab icon too.
 *
 * WHY useHead AND NOT link.href = ... : the first attempt mutated the <link> directly and the head
 * manager (unhead) put the static href straight back on its next patch, so the tab stayed green
 * whatever was picked. Declaring the link through unhead with a reactive href - and the same `key`
 * the nuxt.config link uses, so it replaces rather than duplicates - makes the colour a rendered
 * value instead of a mutation that gets overwritten.
 *
 * Why read `--ui-primary` instead of `appConfig.ui.colors.primary`: appConfig holds a palette NAME
 * ("violet"), not a colour. Nuxt UI resolves that name into CSS variables, so the computed variable
 * is the only place the actual value exists - the same reason the 3D scene resolves it for his halo.
 *
 * Why the canvas: the variable is an `oklch(...)` string, and browsers do not hand back an RGB triple
 * for it. Canvas `fillStyle` accepts oklch and `getImageData` gives the bytes, which is the cheap,
 * dependency-free way to convert. If the browser rejects the value the fillStyle stays #000000, and
 * bailing out there leaves the static green favicon in place rather than painting the tab black.
 */
export default defineNuxtPlugin(() => {
  if (import.meta.server) return

  // setTimeout, not requestAnimationFrame: rAF never fires in a background tab, and a favicon that
  // only updates while the tab is in front is a bug that hides itself from whoever tests it.
  const later = (fn: () => void) => setTimeout(fn, 0)

  const appConfig = useAppConfig()
  const base = useRuntimeConfig().app.baseURL || '/'
  const FALLBACK = `${base}favicon.svg`
  const FILL_TOKEN = /fill="#[0-9a-f]{3,8}"/i

  // Starts on the static file so the tab is never left without an icon while the SVG is fetched.
  const href = ref(FALLBACK)
  useHead({
    link: [{ key: 'favicon', rel: 'icon', type: 'image/svg+xml', href }]
  })

  let glyph: string | null = null
  let glyphPromise: Promise<void> | null = null

  /** Current accent as #rrggbb, or null when the value cannot be resolved. */
  function accentHex(): string | null {
    const raw = getComputedStyle(document.documentElement).getPropertyValue('--ui-primary').trim()
    if (!raw) return null
    const canvas = document.createElement('canvas')
    canvas.width = canvas.height = 1
    const ctx = canvas.getContext('2d', { willReadFrequently: true })
    if (!ctx) return null
    ctx.fillStyle = '#000000'
    ctx.fillStyle = raw
    // Unsupported value leaves the probe at the sentinel; treat that as "cannot resolve".
    if (ctx.fillStyle === '#000000' && !/^#0{3,8}$|black/i.test(raw)) return null
    ctx.clearRect(0, 0, 1, 1)
    ctx.fillRect(0, 0, 1, 1)
    const [r, g, b] = ctx.getImageData(0, 0, 1, 1).data
    return '#' + [r, g, b].map((v) => v.toString(16).padStart(2, '0')).join('')
  }

  /** One fetch of the shipped glyph; the accent is substituted into that markup. */
  function loadGlyph() {
    if (glyphPromise) return glyphPromise
    glyphPromise = $fetch<string>(FALLBACK, { responseType: 'text' })
      .then((svg) => { glyph = svg })
      .catch(() => { /* keep the static file */ })
    return glyphPromise
  }

  async function apply() {
    await loadGlyph()
    if (!glyph) return
    const hex = accentHex()
    if (!hex) return
    href.value = 'data:image/svg+xml;charset=utf-8,'
      + encodeURIComponent(glyph.replace(FILL_TOKEN, `fill="${hex}"`))
  }

  // appConfig is what both the Theme Studio and the cookie restore assign to, so watching it covers
  // every path a colour change can take. The frame delay lets Nuxt UI write the CSS variables first.
  watch(
    () => [appConfig.ui.colors.primary, appConfig.ui.colors.neutral],
    () => { nextTick(() => later(apply)) },
    { immediate: true }
  )

  // Light and dark can resolve the accent token differently, so repaint when the mode class flips.
  observeColorMode(apply)

  function observeColorMode(run: () => void) {
    if (typeof MutationObserver === 'undefined') return
    new MutationObserver(run).observe(document.documentElement, { attributes: true, attributeFilter: ['class'] })
  }
})
