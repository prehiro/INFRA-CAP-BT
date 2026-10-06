export default defineAppConfig({
  ui: {
    colors: {
      // Green is redefined in main.css (@theme static) with the template's lime ramp,
      // which is the default primary HIRO asked for. Neutral is zinc (HIRO, 2026-10-05:
      // "make accent color green, and neutral color zinc as default theme for all user").
      //
      // MUST stay in step with DEFAULT_PRIMARY / DEFAULT_NEUTRAL in composables/useThemeChoice.ts.
      // This file is what the components read at runtime; that one is what the persistence
      // layer falls back to. They are two sources of truth for one setting, which is the price
      // of appConfig being runtime-only, so they are kept in step by comment and by the
      // DEFAULT_* constants being exported.
      primary: 'green',
      neutral: 'zinc'
    },
    button: {
      slots: {
        base: 'font-medium rounded-md cursor-pointer'
      }
    }
  }
})
