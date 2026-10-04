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
    },
    // The dashboard panel's body is its own `flex-1 overflow-y-auto` scroll container, and
    // it is the one that flickers. The vendor slot carries no data-slot attribute, so it
    // cannot be targeted from page CSS without coupling to its class string; overriding the
    // theme here is the framework-supported way, and it fixes EVERY panel at once rather
    // than one page at a time.
    //
    // scrollbar-gutter: stable reserves this container's scrollbar permanently. Without it,
    // changing the filter makes the panel content cross the scroll threshold, the panel's
    // 15px scrollbar appears or disappears, the body gets 15px wider or narrower, and the
    // whole page content slides left and right. It reproduced once the table passed about
    // nine rows and the table itself became scrollable, because that is when the panel's
    // content height starts hovering around its own client height.
    dashboardPanel: {
      slots: {
        // BOTH parts are needed, and the distinction matters:
        //   scrollbar-gutter-stable  reserves the 15px permanently, so the panel body never
        //     changes width and the page content cannot slide sideways. This alone does NOT
        //     stop the scrollbar from blinking - it only stops it from MOVING anything.
        //   overflow-y-scroll (not auto) renders the track permanently, so the scrollbar
        //     cannot appear and vanish either.
        // Only one of the two was needed to stop the layout shift; HIRO then reported the
        // scrollbar still blinking, so the track itself had to be made permanent too.
        body: 'flex flex-col gap-4 sm:gap-6 flex-1 overflow-y-scroll p-4 sm:p-6 scrollbar-gutter-stable'
      }
    }
  }
})