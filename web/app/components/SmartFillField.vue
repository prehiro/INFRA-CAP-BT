<script setup lang="ts">
/**
 * SmartFillField - a text input that suggests entries from the imported GID list and fills its
 * siblings when one is picked.
 *
 * WHY THIS EXISTS: on the PC Ledger form, Staff Name, Email Address and GID are three views of one
 * person, and they were typed by hand three times. Every typo in any of them silently breaks the
 * link between a machine and its owner. This field turns the imported Global ID list into a picker:
 * type any part of a name, an email, a GID or an employee number, pick the person once, and all
 * three fields are filled from the same source.
 *
 * DESIGN DECISIONS WORTH KEEPING:
 *
 * 1. THE DROPDOWN IS TELEPORTED TO <body> AND POSITIONED `fixed`. The form lives inside the
 *    dialog's scrolling body (`flex-1 overflow-y-auto`), and an absolutely positioned list inside
 *    it is CLIPPED at the body's edge - the suggestions would be cut off exactly when the field is
 *    near the bottom of the form. Teleporting escapes that; `fixed` + the input's own rect keeps it
 *    visually attached. In exchange it must be closed on scroll, because the input can move under a
 *    fixed panel.
 *
 * 2. MATCHING IS RANKED, NOT JUST FILTERED. A prefix hit on the field being typed in outranks a hit
 *    on one of the other three; a substring hit ranks lower still. Without this, typing "li" in the
 *    name field surfaces someone whose email merely contains "li" above someone called "LI LI OH".
 *
 * 3. CROSS-FIELD BOOST. If the form already holds a name (or email, or GID) and it agrees with a
 *    candidate, that candidate is lifted. Typing an email after picking a name therefore suggests
 *    that same person's address instead of an arbitrary one.
 *
 * 4. PICKING FILLS, TYPING NEVER DOES. Nothing is written into the sibling fields on keystrokes -
 *    only on an explicit pick - so the field can never quietly overwrite something typed by hand.
 *    The `filled-from` hint under the input records where the values came from and disappears as
 *    soon as the user edits the field again.
 */
type GidRow = {
  gid?: string
  name?: string
  email?: string
  employee_no?: string
}

const props = defineProps<{
  modelValue: string
  fieldKey: 'staff_name' | 'email' | 'gid'
  candidates: GidRow[]
  /** The record being edited, so the other two fields can boost a candidate. */
  form: Record<string, any>
  placeholder?: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', v: string): void
  (e: 'fill', row: GidRow): void
}>()

/** Which column of the GID list this input mirrors. */
const OWN_FIELD: Record<string, keyof GidRow> = {
  staff_name: 'name',
  email: 'email',
  gid: 'gid'
}

/** The other two form fields this input agrees with. */
const SIBLINGS: Record<string, string[]> = {
  staff_name: ['email', 'gid'],
  email: ['staff_name', 'gid'],
  gid: ['staff_name', 'email']
}

const inputEl = ref<any>(null)
const rootEl = ref<HTMLElement | null>(null)
const open = ref(false)
const active = ref(0)
const filledFrom = ref('')
const dropStyle = ref<Record<string, string>>({})

function domInput(): HTMLInputElement | null {
  const el: any = inputEl.value
  if (!el) return null
  return (el.$el as HTMLInputElement) ?? (el as HTMLInputElement)
}

const text = (v: unknown) => String(v ?? '').trim()

/** Every place a candidate can be found, and how strongly a hit counts. */
function rank(row: GidRow, q: string): number {
  const own = text(row[OWN_FIELD[props.fieldKey]]).toLowerCase()
  const others = [row.gid, row.name, row.email, row.employee_no]
    .map((v) => text(v).toLowerCase())
    .filter(Boolean)
  let score = 0
  if (own === q) score += 200
  if (own.startsWith(q)) score += 100
  if (others.some((v) => v.startsWith(q))) score += 60
  if (own.includes(q)) score += 30
  if (others.some((v) => v.includes(q))) score += 10
  if (!score) return 0
  // Cross-field boost: a candidate that agrees with what the form already holds wins ties.
  for (const key of SIBLINGS[props.fieldKey]) {
    const typed = text(props.form?.[key]).toLowerCase()
    if (typed && others.concat(own).some((v) => v === typed)) score += 40
  }
  return score
}

const matches = computed<GidRow[]>(() => {
  const q = text(props.modelValue).toLowerCase()
  if (!q) return []
  const seen = new Set<string>()
  return props.candidates
    .map((row) => ({ row, score: rank(row, q) }))
    .filter((m) => m.score > 0 && !seen.has(text(m.row.gid)) && seen.add(text(m.row.gid)))
    .sort((a, b) => b.score - a.score || text(a.row.gid).localeCompare(text(b.row.gid), undefined, { numeric: true }))
    .slice(0, 8)
    .map((m) => m.row)
})

/** Split a label around the typed text so the hit can be marked inside it. */
function parts(value: unknown): { before: string; hit: string; after: string } {
  const full = text(value)
  const q = text(props.modelValue)
  if (!q) return { before: full, hit: '', after: '' }
  const i = full.toLowerCase().indexOf(q.toLowerCase())
  if (i < 0) return { before: full, hit: '', after: '' }
  return { before: full.slice(0, i), hit: full.slice(i, i + q.length), after: full.slice(i + q.length) }
}

function place() {
  const el = domInput()
  if (!el) return
  const r = el.getBoundingClientRect()
  dropStyle.value = {
    position: 'fixed',
    top: `${Math.round(r.bottom + 4)}px`,
    left: `${Math.round(r.left)}px`,
    width: `${Math.round(r.width)}px`,
    zIndex: '60'
  }
}

function show() {
  if (!matches.value.length) return
  place()
  open.value = true
  active.value = 0
}

function onInput(e: Event) {
  filledFrom.value = ''
  emit('update:modelValue', (e.target as HTMLInputElement).value)
  // The parent owns the value, so `matches` cannot see the new text until the prop has come back
  // around. Calling show() synchronously here matched an EMPTY list and the dropdown never opened
  // (measured in the browser: typing produced no suggestions at all). One tick is enough.
  nextTick(show)
}

// When the value changes from outside - a pick in a sibling field filling this one - keep an already
// open list in step rather than showing stale suggestions.
watch(() => props.modelValue, () => { if (open.value) show() })

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
    if (!open.value) { show(); return }
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

function pick(row: GidRow) {
  const own = text(row[OWN_FIELD[props.fieldKey]])
  emit('update:modelValue', own)
  emit('fill', row)
  filledFrom.value = text(row.gid)
  open.value = false
}

function dismiss() {
  // A short delay so a click on a suggestion lands before the blur closes the list.
  window.setTimeout(() => { open.value = false }, 120)
}

function onDocPointer(e: MouseEvent) {
  const t = e.target as HTMLElement
  if (rootEl.value?.contains(t)) return
  if (t.closest?.('[data-smart-fill]')) return
  open.value = false
}

onMounted(() => {
  document.addEventListener('mousedown', onDocPointer)
  // The input can move under a fixed panel, so any scroll closes the list rather than letting it
  // drift away from the field it belongs to.
  window.addEventListener('scroll', () => { open.value = false }, true)
  window.addEventListener('resize', () => { open.value = false })
})
onBeforeUnmount(() => {
  document.removeEventListener('mousedown', onDocPointer)
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
      Filled from GID list ({{ filledFrom }})
    </p>

    <Teleport to="body">
      <div
        v-if="open && matches.length"
        data-smart-fill
        :style="dropStyle"
        class="max-h-64 overflow-y-auto rounded-lg border border-default bg-elevated shadow-lg"
      >
        <p class="flex items-center gap-1.5 border-b border-default px-3 py-1.5 text-[10px] uppercase tracking-wide text-muted">
          <UIcon name="i-lucide-sparkles" class="size-3" />
          GID list - picking fills name, email and GID
        </p>
        <button
          v-for="(row, i) in matches"
          :key="row.gid"
          type="button"
          class="flex w-full items-start gap-2 px-3 py-2 text-left text-xs transition-colors"
          :class="i === active ? 'bg-primary/10' : 'hover:bg-elevated/60'"
          @mouseenter="active = i"
          @mousedown.prevent="pick(row)"
        >
          <span class="mt-0.5 grid size-5 shrink-0 place-items-center rounded bg-primary/10 text-primary">
            <UIcon name="i-lucide-user-round" class="size-3" />
          </span>
          <span class="min-w-0 flex-1">
            <span class="block truncate font-medium text-default">
              <span v-if="parts(row.name).hit" class="text-dimmed">{{ parts(row.name).before }}</span><mark class="bg-primary/20 text-primary">{{ parts(row.name).hit }}</mark><span v-if="parts(row.name).hit" class="text-dimmed">{{ parts(row.name).after }}</span>
              <span v-if="!parts(row.name).hit">{{ row.name || '-' }}</span>
            </span>
            <span class="mt-0.5 block truncate text-[11px] text-muted">
              <template v-if="row.email">
                <span v-if="parts(row.email).hit">{{ parts(row.email).before }}</span><mark v-if="parts(row.email).hit" class="bg-primary/20 text-primary">{{ parts(row.email).hit }}</mark><span v-if="parts(row.email).hit">{{ parts(row.email).after }}</span><span v-else>{{ row.email }}</span>
              </template>
              <template v-else>-</template>
              <span class="mx-1 text-dimmed">|</span>
              <span class="tabular-nums">
                <span v-if="parts(row.gid).hit">{{ parts(row.gid).before }}</span><mark v-if="parts(row.gid).hit" class="bg-primary/20 text-primary">{{ parts(row.gid).hit }}</mark><span v-if="parts(row.gid).hit">{{ parts(row.gid).after }}</span><span v-else>{{ row.gid }}</span>
              </span>
              <span v-if="row.employee_no" class="mx-1 text-dimmed">|</span>
              <span v-if="row.employee_no" class="tabular-nums">{{ row.employee_no }}</span>
            </span>
          </span>
          <span class="mt-0.5 shrink-0 text-[10px] text-dimmed">↵</span>
        </button>
      </div>
    </Teleport>
  </div>
</template>
