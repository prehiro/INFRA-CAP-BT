import { computed, ref, watch, nextTick, onMounted, onBeforeUnmount, type Ref } from 'vue'
import { registerOffsets, registerIndexAt } from '../utils/registerWindow'

/** Variable-height viewport rendering for signature/wrapped-text registers. UI-only: the complete
 * source remains authoritative for filtering, counts, exports and writes. No API requests here. */
export function useRegisterTable<T extends { id: number }>(source: Ref<T[]>, options: { estimate?: number; duration?: number } = {}) {
  const tableEl = ref<HTMLTableElement | null>(null)
  const estimate = options.estimate ?? 71
  const duration = options.duration ?? 220
  const top = ref(0)
  const height = ref(600)
  const headerHeight = ref(40)
  const printing = ref(false)
  const revision = ref(0)
  const sizes = new Map<number, number>()
  const offsets = computed(() => {
    void revision.value
    return registerOffsets(source.value, sizes, estimate)
  })
  function indexAt(y: number) {
    return registerIndexAt(offsets.value, y)
  }
  const windowed = computed(() => !printing.value && source.value.length > 80)
  const start = computed(() => windowed.value ? Math.max(0, Math.min(source.value.length - 1, indexAt(Math.max(0, top.value - headerHeight.value))) - 6) : 0)
  const end = computed(() => windowed.value ? Math.min(source.value.length, indexAt(top.value + height.value) + 7) : source.value.length)
  const renderedRows = computed(() => source.value.slice(start.value, end.value))
  const spacerTop = computed(() => offsets.value[start.value] ?? 0)
  const spacerBottom = computed(() => Math.max(0, offsets.value[source.value.length]! - offsets.value[end.value]!))
  let scrolling = false
  let scrollFrame = 0
  let measureFrame = 0
  let moveFrame = 0
  let observer: ResizeObserver | undefined
  let observed: Element[] = []
  const scroller = () => tableEl.value?.closest('.logbook-scroll') as HTMLElement | null
  const dataRows = () => [...(tableEl.value?.querySelectorAll<HTMLElement>('tbody tr[data-id]') ?? [])]
  function measure() {
    if (printing.value) return
    const scroll = scroller()
    if (!scroll) return
    height.value = scroll.clientHeight
    headerHeight.value = tableEl.value?.tHead?.offsetHeight ?? 40
    const anchor = indexAt(Math.max(0, scroll.scrollTop - headerHeight.value))
    const before = offsets.value[anchor] ?? 0
    const measured = dataRows().map(el => [Number(el.dataset.id), el.offsetHeight] as const)
    let changed = false
    for (const [id, value] of measured) {
      if (value > 0 && sizes.get(id) !== value) { sizes.set(id, value); changed = true }
    }
    if (changed) {
      revision.value++
      const delta = (offsets.value[anchor] ?? before) - before
      if (scroll.scrollTop > 0 && delta) scroll.scrollTop += delta
    }
    top.value = scroll.scrollTop
  }
  function scheduleMeasure() {
    cancelAnimationFrame(measureFrame)
    measureFrame = requestAnimationFrame(measure)
  }
  async function observeRows() {
    await nextTick()
    if (!tableEl.value) return
    observed.forEach(el => observer?.unobserve(el))
    observed = dataRows()
    observed.forEach(el => observer?.observe(el))
    measure()
  }
  function onScroll(event: Event) {
    const el = event.currentTarget as HTMLElement
    if (top.value === el.scrollTop) return
    scrolling = true
    top.value = el.scrollTop
    height.value = el.clientHeight
    cancelAnimationFrame(scrollFrame)
    scrollFrame = requestAnimationFrame(() => { scrolling = false })
  }
  const animations = new Map<Element, Animation[]>()
  const reduced = () => printing.value || window.matchMedia('(prefers-reduced-motion: reduce), print').matches
  function cancelRow(el: Element) {
    animations.get(el)?.forEach(a => a.cancel())
    animations.delete(el)
  }
  function enterRow(el: Element, done: () => void) {
    if (!el.hasAttribute('data-id') || scrolling || reduced()) { done(); return }
    const fade = el.animate([{ opacity: 0 }, { opacity: 1 }], { duration, easing: 'cubic-bezier(.22,1,.36,1)' })
    const move = el.animate([{ transform: 'translateY(-8px)' }, { transform: 'translateY(0)' }], { duration, easing: 'cubic-bezier(.22,1,.36,1)' })
    animations.set(el, [fade, move])
    move.onfinish = () => { animations.delete(el); done() }
  }
  let before = new Map<number, number>()
  watch(source, () => {
    scrolling = false
    before = new Map(dataRows().map(el => [Number(el.dataset.id), el.offsetTop]))
    const scroll = scroller()
    const maxTop = Math.max(0, offsets.value[source.value.length]! + headerHeight.value - height.value)
    if (scroll && scroll.scrollTop > maxTop) { scroll.scrollTop = maxTop; top.value = maxTop }
    cancelAnimationFrame(moveFrame)
  })
  watch(source, () => {
    const old = before
    if (!old.size || reduced()) return
    let tries = 0
    const pin = () => {
      const rows = dataRows().filter(el => old.has(Number(el.dataset.id)))
      const moves = rows.map(el => [el, old.get(Number(el.dataset.id))! - el.offsetTop] as const)
      // Read all layout first; never alternate layout reads and transform writes per row.
      moves.forEach(([el, dy]) => { el.style.transform = Math.abs(dy) > .5 ? `translateY(${dy}px)` : '' })
      if (moves.some(([,dy]) => Math.abs(dy) > .5) || ++tries > 2) {
        moves.forEach(([el,dy]) => {
          el.style.transform = ''
          if (Math.abs(dy) < .5) return
          cancelRow(el)
          const move = el.animate([{ transform: `translateY(${dy}px)` }, { transform: 'translateY(0)' }], { duration, easing: 'cubic-bezier(.22,1,.36,1)' })
          animations.set(el, [move]);move.onfinish = () => animations.delete(el)
        })
      } else moveFrame = requestAnimationFrame(pin)
    }
    moveFrame = requestAnimationFrame(pin)
  }, { flush: 'post' })
  watch([tableEl, renderedRows], observeRows, { flush: 'post' })
  let printMedia: MediaQueryList | undefined
  const beforePrint = () => { printing.value = true; animations.forEach((_a,el) => cancelRow(el)) }
  const afterPrint = () => { printing.value = false }
  const printChange = (event: MediaQueryListEvent) => { event.matches ? beforePrint() : afterPrint() }
  onMounted(() => {
    observer = new ResizeObserver(scheduleMeasure)
    printMedia = window.matchMedia('print')
    printing.value = printMedia.matches
    printMedia.addEventListener('change', printChange)
    window.addEventListener('beforeprint', beforePrint)
    window.addEventListener('afterprint', afterPrint)
    window.addEventListener('resize', scheduleMeasure)
    void observeRows()
  })
  onBeforeUnmount(() => {
    observer?.disconnect()
    cancelAnimationFrame(scrollFrame);cancelAnimationFrame(measureFrame);cancelAnimationFrame(moveFrame)
    animations.forEach((_a,el) => cancelRow(el))
    printMedia?.removeEventListener('change', printChange)
    window.removeEventListener('beforeprint', beforePrint);window.removeEventListener('afterprint', afterPrint)
    window.removeEventListener('resize', scheduleMeasure)
  })
  return { tableEl, renderedRows, windowStart: start, spacerTop, spacerBottom, onScroll, enterRow, cancelRow }
}
