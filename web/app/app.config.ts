export default defineAppConfig({
  ui: {
    colors: {
      // Green is redefined in main.css (@theme static) with the template's lime ramp,
      // which is the default primary HIRO asked for.
      primary: 'green',
      neutral: 'slate'
    },
    button: {
      slots: {
        base: 'font-medium rounded-md cursor-pointer'
      }
    }
  }
})
