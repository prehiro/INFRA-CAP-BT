/**
 * Auto-capitalises the FIRST character of every free-text box in the app.
 *
 * Implemented as ONE delegated `input` listener on document in the CAPTURE phase rather than
 * as a per-component attribute or a v-model helper, because every text field in this project
 * is a UInput / UTextarea that renders its own <input> underneath. A global directive would
 * still have to be added to each call site, and any new page added later would silently miss
 * the behaviour. Delegation covers the login form, user management, the CCTV entry form and
 * anything written in the future, with no per-component change.
 *
 * Running in the capture phase is deliberate: the first character is fixed BEFORE Vue's own
 * v-model handler reads the value, so the model never sees the lowercase version and no
 * watcher has to fire twice.
 *
 * DELIBERATELY NOT DONE, and these are the two traps:
 *
 * 1. PASSWORD FIELDS ARE SKIPPED. Capitalising a password changes the credential and locks
 *    the user out of their own account. A stored hash was made against the lowercase form,
 *    so "Admin@123" typed into a capitalising box would simply never authenticate.
 *
 * 2. STORED DATA IS NOT REWRITTEN. Only what the user TYPES is touched. Rewriting existing
 *    values too would silently rename accounts: open the Edit form on user "budi", the box
 *    would show "Budi", and saving would change the stored username - after which that user
 *    can no longer sign in with the name they know. Same trap for an existing Section value
 *    that other records reference.
 *
 * ESCAPES: any input can opt out with data-no-capitalize, and the native autocapitalize
 * attribute is honoured when it says "off" or "none".
 */

const SKIP_TYPES = new Set([
  'password',
  'email',
  'url',
  'tel',
  'number',
  'date',
  'time',
  'datetime-local',
  'month',
  'week',
  'search',
  'color',
  'range',
  'file',
  'hidden',
  'checkbox',
  'radio',
])

export default defineNuxtPlugin(() => {
  if (typeof document === 'undefined') return

  document.addEventListener(
    'input',
    (event: Event) => {
      // Ignore keystrokes that are part of an IME composition session (Japanese, Chinese,
      // Indonesian IME candidates): rewriting the value mid-composition cancels the candidate
      // window the user is still choosing from.
      if ((event as InputEvent).isComposing) return

      const el = event.target as HTMLInputElement | HTMLTextAreaElement | null
      if (!el || (el.tagName !== 'INPUT' && el.tagName !== 'TEXTAREA')) return

      const type = (el.getAttribute('type') ?? 'text').toLowerCase()
      if (SKIP_TYPES.has(type)) return
      if (el.hasAttribute('data-no-capitalize')) return

      const ac = (el.getAttribute('autocapitalize') ?? '').toLowerCase()
      if (ac === 'off' || ac === 'none') return

      if (el.readOnly || (el as HTMLInputElement).disabled) return

      const value = el.value
      if (!value) return

      const first = value.charAt(0)
      const upper = first.toUpperCase()
      // Guard against characters whose uppercase is a DIFFERENT length (ß -> SS, ﬁ -> FI).
      // Uppercasing those would change the string length and, with it, the caret position.
      if (upper === first || upper.length !== first.length) return

      el.value = upper + value.slice(1)
      // No caret fix is needed: the substitution is always exactly one character wide, so
      // every offset in the string is unchanged and the cursor stays where the user left it.
    },
    true,
  )
})
