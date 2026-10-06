/**
 * Restore the persisted theme choice on the client, on EVERY page.
 *
 * WHY A PLUGIN AND NOT app.vue / UserMenu (HIRO, 2026-10-05: "make accent color green, and
 * neutral color zinc as default theme for all user").
 *
 * The short version, measured rather than reasoned about: `restore()` was being called from
 * UserMenu's onMounted, then moved to app.vue's, and in BOTH places the stored colour was
 * ignored. The decisive test was setting the cookie to violet/slate - a colour that is NOT the
 * old default and therefore cannot be explained by the migration - reloading, and finding
 * `--ui-primary` still green. So the function was not running, or was running with a detached
 * cookie ref; either way the call site was wrong.
 *
 * `useCookie` resolves its ref against the CURRENT Nuxt instance's payload. A call made from a
 * component's onMounted hook runs with the effect scope of that component, not of the app, and
 * the cookie ref it gets back is not the one the rest of the app reads. That is why `persist()`
 * from the colour menu worked - `setPrimary`/`setNeutral` are called from a click handler in a
 * mounted component and write through the shared ref - while `restore()` did not.
 *
 * A plugin has no such problem: it runs inside the Nuxt instance, once, before the app renders.
 *
 * `.client` so it never runs during SSR, where there is no cookie to read and writing one
 * server-side would emit a Set-Cookie on every response for nothing.
 *
 * NOT awaited: restore() is synchronous and must NOT delay the first paint - the app.config
 * defaults are already correct, so the page renders in the right colours and the stored choice
 * is applied immediately after mount. Blocking here would show a flash of the default theme on
 * every navigation for users who did pick a different colour.
 */
export default defineNuxtPlugin(() => {
  const { restore } = useThemeChoice()

  // Called IMMEDIATELY, not inside onNuxtReady.
  //
  // MEASURED: with `onNuxtReady(() => restore())` the stored colour was never applied - the
  // cookie read violet and --ui-primary still came back green. Calling restore() straight from
  // the plugin body is the form Nuxt's own cookie/theme examples use, and it runs inside the
  // Nuxt instance, which is the whole point of moving off the component.
  //
  // It is safe to run before mount because the app.config defaults are already correct: if
  // there is no stored choice the page simply keeps its defaults, and Nuxt UI reads appConfig
  // reactively, so applying a different colour here updates the running UI.
  restore()
})