<script setup lang="ts">
/**
 * SuggestInput - a text input with a ranked, keyboard- and mouse-driven suggestion list.
 *
 * WHY IT EXISTS: three views of the same fact (a person's name, email and GID) used to be typed by
 * hand three times on the PC Ledger form, and the chassis model is whatever the last machine was.
 * This turns those into pickers fed by real data - the imported Global ID list, or the models the
 * register has already seen.
 *
 * DESIGN DECISIONS WORTH KEEPING (each one earned):
 *
 * 1. THE DROPDOWN IS TELEPORTED TO <body> AND POSITIONED `fixed`. The form lives inside the dialog's
 *    scrolling body (`flex-1 overflow-y-auto`), and an absolutely positioned list inside it is
 *    CLIPPED at the body's edge - suggestions would vanish exactly when the field sits near the
 *    bottom of the form. Teleporting escapes that clipping. The price is that a fixed panel cannot
 *    follow a moving input, hence: any scroll OUTSIDE the list closes it.
 *
 * 2. SCROLLING INSIDE THE LIST MUST NOT CLOSE IT. That was a real defect: the scroll listener used
 *    the capture phase, so the list's own scroll looked like a page scroll and shut the dropdown
 *    before it could be scrolled - HIRO: "belum bisa di scroll jika banyak preview dropdown list".
 *    The listener now ignores events whose target is inside the list, and the list is
 *    `overscroll-contain` so reaching its end does not drag the dialog behind it.
 *
 * 3. BOTH MOUSE AND KEYBOARD PICK. The rows carry `@mousedown.prevent` (so the input keeps focus and
 *    no blur-close races the click) AND `@click` (the actual pick). HIRO: "smart suggestion tidak
 *    bisa di click, hanya bisa enter. saya mau bisa keduanya."
 *
 * 4. MATCHING IS RANKED, NOT JUST FILTERED. A prefix hit on the label or value outranks a hit inside
 *    the secondary line; exact match wins outright. Typing "li" in the name field must surface
 *    "LI LI OH" above someone whose email merely contains "li".
 */
export type Suggestion = {
  /** What is written into the input when this row is picked. */
  value: string
  /** The bold first line. */
  label: string
  /** The dimmed second line: email, GID, employee number, "used 3 times"... */
  meta?: string
  /** When present, picking also fills sibling form fields (the parent handles that). */
  fill?: Record<string, any>
  /** Shown in the quiet "filled from" trace under the input, e.g. the GID. */
  hint?: string
  /** Extra strings this row should also be matched against. */
  keys?: string[]
}

const props = withDefaults(defineProps<{
  modelValue: string
  suggestions: Suggestion[]
  placeholder?: string
  /** The dropdown's header line; omit to hide it. */
  header?: string
  /** Values already in the form, used to lift the candidate that agrees with them. */
  context?: string[]
}>(), { placeholder: '', header: '', context: () => [] })

const emit = defineEmits<{
  (e: 'update:modelValue', v: string): void
  (e: 'fill', s: Suggestion): void
}>()

const inputEl = ref<any>(null)
const rootEl = ref<HTMLElement | null>(null)
const panelEl = ref<HTMLElement | null>(null)
/** Where the panel is teleported. Resolved per open: the dialog that owns the input, or <body>. */
const panelTarget = ref<HTMLElement>(document.body)
const open = ref(false)
const active = ref(0)
const filledFrom = ref('')
const dropStyle = ref<Record<string, string>>({})

const text = (v: unknown) => String(v ?? '').trim()

function domInput(): HTMLInputElement | null {
  const el: any = inputEl.value
  if (!el) return null
  return (el.$el as HTMLInputElement) ?? (el as HTMLInputElement)
}

function rank(s: Suggestion, q: string): number {
  const label = text(s.label).toLowerCase()
  const value = text(s.value).toLowerCase()
  const meta = text(s.meta).toLowerCase()
  const keys = (s.keys ?? []).map((k) => text(k).toLowerCase())
  let score = 0
  if (label === q || value === q) score += 200
  if (label.startsWith(q) || value.startsWith(q)) score += 100
  if (keys.some((k) => k.startsWith(q))) score += 60
  if (label.includes(q) || value.includes(q)) score += 30
  if (meta.includes(q) || keys.some((k) => k.includes(q))) score += 10
  if (!score) return 0
  // A candidate that agrees with something already in the form wins ties: typing an email after
  // picking a name should offer that same person rather than an arbitrary one.
  const ctx = props.context.map((c) => text(c).toLowerCase()).filter(Boolean)
  if (ctx.length && ctx.some((c) => c === label || c === value || keys.includes(c))) score += 40
  return score
}

const matches = computed<Suggestion[]>(() => {
  const q = text(props.modelValue).toLowerCase()
  if (!q) return []
  const seen = new Set<string>()
  return props.suggestions
    .map((s) => ({ s, score: rank(s, q) }))
    .filter((m) => m.score > 0 && !seen.has(text(m.s.value)) && seen.add(text(m.s.value)))
    .sort((a, b) => b.score - a.score || text(a.s.value).localeCompare(text(b.s.value), undefined, { numeric: true }))
    .slice(0, 8)
    .map((m) => m.s)
})

/** Split a string around the typed text so the hit can be marked inside it. */
function parts(value: unknown): { before: string; hit: string; after: string } {
  const full = text(value)
  const q = text(props.modelValue)
  if (!q) return { before: full, hit: '', after: '' }
  const i = full.toLowerCase().indexOf(q.toLowerCase())
  if (i < 0) return { before: full, hit: '', after: '' }
  return { before: full.slice(0, i), hit: full.slice(i, i + q.length), after: full.slice(i + q.length) }
}

async function place() {
  const el = domInput()
  if (!el) return
  const r = el.getBoundingClientRect()

  // WHERE THE PANEL LIVES IS PART OF THE FIX, not an implementation detail. A panel teleported to
  // <body> is a DOM stranger to the dialog, so Reka reads a click on it as a click OUTSIDE the dialog
  // and dismisses the whole form. Measured: the click landed inside the list (elementFromPoint
  // returned a node with closest('[data-suggest-list]') true) and the dialog closed anyway - HIRO:
  // "ketika saya click suggestion dropdown form modal langsung tertutup". Teleporting into the
  // dialog's own [role=dialog] element makes the panel an insider, so no dismissal fires. That element
  // is also the only box that clips it, hence the bounds keeping below.
  const dlg = el.closest('[role="dialog"]') as HTMLElement | null
  panelTarget.value = dlg ?? document.body

  const width = Math.max(260, Math.round(r.width))
  // Keep the list on screen: if the field is near the right edge, pull the list left instead of
  // letting it run off the viewport.
  const left = Math.min(Math.round(r.left), Math.max(8, window.innerWidth - width - 8))
  let top = Math.round(r.bottom + 4)

  // The dialog is translated (-50% -50%), which makes it the containing block for a fixed child, so
  // top/left would be measured from the dialog rather than the viewport. Instead of assuming which it
  // is, put the panel at 0,0, read where it actually landed and offset by the difference - exact
  // either way, and it survives any future change to the dialog's transform.
  const base: Record<string, string> = {
    position: 'fixed', top: '0px', left: '0px', width: `${width}px`, zIndex: '20',
    // Reka's portal wrapper sets pointer-events: none and re-enables it only on the dialog content;
    // a teleported node inherits none and every mouse click on it is swallowed (Enter still worked
    // because the keyboard goes to the input inside the dialog).
    pointerEvents: 'auto'
  }
  dropStyle.value = base
  await nextTick()
  const panel = panelEl.value
  if (!panel) return
  const origin = panel.getBoundingClientRect()
  // Keep the whole list inside the dialog: flip above the field when there is no room below, and
  // clamp the height when neither side fits.
  const limitBottom = (dlg ? dlg.getBoundingClientRect().bottom : window.innerHeight) - 6
  const limitTop = (dlg ? dlg.getBoundingClientRect().top : 0) + 6
  let maxHeight = ''
  if (top + panel.offsetHeight > limitBottom) {
    const above = Math.round(r.top - 4 - panel.offsetHeight)
    if (above >= limitTop) top = above
    else maxHeight = `${Math.max(140, limitBottom - top)}px`
  }
  dropStyle.value = {
    ...base,
    top: `${Math.round(top - origin.top)}px`,
    left: `${Math.round(left - origin.left)}px`,
    ...(maxHeight ? { maxHeight } : {})
  }
}

function show() {
  if (!matches.value.length) { open.value = false; return }
  open.value = true
  if (active.value >= matches.value.length) active.value = 0
  void place()
}

function onInput(e: Event) {
  filledFrom.value = ''
  emit('update:modelValue', (e.target as HTMLInputElement).value)
  // The parent owns the value, so `matches` cannot see the new text until the prop comes back
  // around: calling show() synchronously matched an empty list and the dropdown never opened.
  nextTick(show)
}

// When the value changes from outside - a pick in a sibling field filling this one - keep an open
// list in step rather than showing stale suggestions.
watch(() => props.modelValue, () => { if (open.value) show() })

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
    if (!open.value) { show(); if (!open.value) return }
    e.preventDefault()
    const n = matches.value.length
    active.value = (active.value + (e.key === 'ArrowDown' ? 1 : n - 1)) % n
    return
  }
  if (e.key === 'Enter' && open.value && matches.value[active.value]) {
    e.preventDefault()
    pick(matches.value[active.value]!)
    return
  }
  if (e.key === 'Escape' && open.value) {
    e.preventDefault()
    open.value = false
    return
  }
  if (e.key === 'Tab') open.value = false
}

function pick(s: Suggestion) {
  emit('update:modelValue', text(s.value))
  emit('fill', s)
  filledFrom.value = s.hint ?? text(s.value)
  open.value = false
}

function dismiss() {
  // A short delay so a click on a suggestion still lands before the blur closes the list.
  window.setTimeout(() => { open.value = false }, 150)
}

function onDocPointer(e: MouseEvent) {
  const t = e.target as HTMLElement
  if (rootEl.value?.contains(t)) return
  if (t.closest?.('[data-suggest-list]')) return
  open.value = false
}

function onAnyScroll(e: Event) {
  // Scrolls INSIDE the list are the list being used - they must not close it. Anything else means
  // the input has moved under a fixed panel, and a dropdown that cannot follow must close.
  const t = e.target as HTMLElement | null
  if (t?.closest?.('[data-suggest-list]')) return
  open.value = false
}

onMounted(() => {
  document.addEventListener('mousedown', onDocPointer)
  window.addEventListener('scroll', onAnyScroll, true)
  window.addEventListener('resize', () => { open.value = false })
})
onBeforeUnmount(() => {
  document.removeEventListener('mousedown', onDocPointer)
  window.removeEventListener('scroll', onAnyScroll, true)
})
</script>

<template>
  <div ref="rootEl" class="relative">
    <UInput
      ref="inputEl"
      :model-value="modelValue"
      :placeholder="placeholder"
      autocomplete="off"
      class="w-full"
      :ui="{ base: 'h-9' }"
      @input="onInput"
      @focus="show"
      @blur="dismiss"
      @keydown="onKeydown"
    />

    <p v-if="filledFrom" class="mt-1 flex items-center gap-1 text-[10px] text-primary">
      <UIcon name="i-lucide-sparkles" class="size-3" />
      Filled from {{ filledFrom }}
    </p>

    <Teleport :to="panelTarget">
      <div
        v-if="open && matches.length"
        ref="panelEl"
        data-suggest-list
        :style="dropStyle"
        class="max-h-72 overflow-y-auto overscroll-contain rounded-lg border border-default bg-elevated shadow-lg"
      >
        <p
          v-if="header"
          class="sticky top-0 z-10 flex items-center gap-1.5 border-b border-default bg-elevated px-3 py-1.5 text-[10px] uppercase tracking-wide text-muted"
        >
          <UIcon name="i-lucide-sparkles" class="size-3" />
          {{ header }}
        </p>
        <button
          v-for="(s, i) in matches"
          :key="s.value"
          type="button"
          class="flex w-full items-start gap-2 px-3 py-2 text-left text-xs transition-colors"
          :class="i === active ? 'bg-primary/10' : 'hover:bg-elevated/60'"
          @mouseenter="active = i"
          @mousedown.prevent="pick(s)"
          @click="pick(s)"
        >
          <span class="mt-0.5 grid size-5 shrink-0 place-items-center rounded bg-primary/10 text-primary">
            <UIcon name="i-lucide-corner-down-left" class="size-3" />
          </span>
          <span class="min-w-0 flex-1">
            <span class="block truncate font-medium">
              <template v-if="parts(s.label).hit">
                <span class="text-dimmed">{{ parts(s.label).before }}</span><mark class="bg-primary/20 text-primary">{{ parts(s.label).hit }}</mark><span class="text-dimmed">{{ parts(s.label).after }}</span>
              </template>
              <span v-else>{{ s.label || '-' }}</span>
            </span>
            <span v-if="s.meta" class="mt-0.5 block truncate text-[11px] text-muted">{{ s.meta }}</span>
          </span>
          <span class="mt-0.5 shrink-0 text-[10px] text-dimmed">{{ i === active ? '↵' : '' }}</span>
        </button>
      </div>
    </Teleport>
  </div>
</template>
