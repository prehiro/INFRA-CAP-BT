<script setup lang="ts">
/**
 * Signature pad: draws with mouse, touch or pen onto a canvas and exposes the result
 * as a PNG data-URL suitable for storing in a dynamic Text field.
 *
 * Two details that matter on the real hardware:
 *  - pointer events cover mouse, finger and stylus in one path, so no separate touch
 *    handlers (touch-action has to be none or the browser scrolls instead of drawing);
 *  - the canvas is scaled by devicePixelRatio, otherwise the stroke is blurry on any
 *    screen with a HiDPI ratio - which includes most office laptops.
 */
const props = withDefaults(defineProps<{
  modelValue?: string | null
  label?: string
  height?: number
  disabled?: boolean
  /** Red outline when a mandatory signature has not been drawn yet. */
  invalid?: boolean
  /**
   * Fill the whole width of the parent instead of shrink-wrapping.
   *
   * The wrapper is `inline-block` by default, which makes the element shrink-to-fit its
   * content - and a canvas has no intrinsic width, so "fit the content" resolves to roughly
   * the width of the "Sign here" placeholder. Measured at 124px inside a grid column that
   * was actually ~250px wide, which is why the pad felt cramped even though the column had
   * room to spare. Opt-in rather than changed globally: the CCTV dialog deliberately puts
   * two pads side by side and relies on the current sizing, so flipping the default there
   * would change a page HIRO did not ask me to touch.
   */
  fullWidth?: boolean
}>(), {
  modelValue: null,
  label: '',
  height: 90,
  disabled: false,
  invalid: false,
  fullWidth: false
})

const emit = defineEmits<{ 'update:modelValue': [string | null] }>()

const canvasEl = ref<HTMLCanvasElement | null>(null)
const drawing = ref(false)
const hasInk = ref(!!props.modelValue)
const dpr = ref(1)

// A transparent drawing surface: exported PNGs composite cleanly onto the printed row.
const INK = '#000000'

function ctx(): CanvasRenderingContext2D | null {
  return canvasEl.value?.getContext('2d') ?? null
}

function paint(model?: string | null) {
  const c = ctx()
  if (!c || !canvasEl.value) return
  const { width, height } = canvasEl.value
  c.clearRect(0, 0, width, height)
  if (!model) {
    return
  }
  // Redraw an existing signature so editing a row shows the stored ink.
  //
  // Two corrections, both forced by exportDataUrl cropping the stored PNG:
  //
  // 1. AT NATURAL SIZE, NOT STRETCHED TO THE PAD. The old drawImage(img, 0, 0, width,
  //    height) was correct only while the stored image WAS the pad. Exports are now
  //    narrower than the pad, so that would squash the ink across the full width on edit -
  //    a signature drawn at 55% of the pad would come back at 100%, wider than anything the
  //    user could have drawn.
  //
  // 2. DIVIDED BY dpr, or the ink renders dpr TIMES TOO BIG. naturalWidth is in DEVICE
  //    pixels, but resize() already did g.scale(dpr, dpr), so the context works in CSS
  //    pixels. Drawing naturalWidth straight into it doubles the signature on every HiDPI
  //    screen. This is invisible at dpr=1, which is exactly why it survived my earlier
  //    testing, and it was HIRO's 1920 laptop that exposed it: a 201px-wide signature was
  //    measured drawing 392 device px, filling half the pad.
  //
  // CENTRED HORIZONTALLY. The crop threw away where in the pad the ink sat, so there is no
  // offset left to restore - 0 would just hug the left edge and look like it slid off.
  // Vertical placement IS still real (the export does not crop height), so y stays 0.
  const img = new Image()
  img.onload = () => {
    const w = img.naturalWidth / dpr.value
    const h = img.naturalHeight / dpr.value
    c.drawImage(img, (width / dpr.value - w) / 2, 0, w, h)
  }
  img.src = model
}

function resize() {
  const c = canvasEl.value
  if (!c) return
  const rect = c.getBoundingClientRect()
  if (!rect.width) return
  dpr.value = window.devicePixelRatio || 1
  c.width = Math.round(rect.width * dpr.value)
  c.height = Math.round(rect.height * dpr.value)
  const g = c.getContext('2d')
  if (g) g.scale(dpr.value, dpr.value)
  paint(props.modelValue)
}

function pos(e: PointerEvent) {
  const rect = canvasEl.value!.getBoundingClientRect()
  return { x: e.clientX - rect.left, y: e.clientY - rect.top }
}

/**
 * Trim transparent margin from the LEFT and RIGHT of the exported PNG.
 *
 * WHY: the pad is 422x130 in the CCTV dialog but a signature is rarely the full width of
 * it, so the stored PNG carries a wide empty margin. The table preview scales that whole
 * margin into a 132px chip with object-contain, which shrinks the INK to fit - measured
 * before this: ink 60px of a 38px-tall box. Cropping the margin means the ink occupies the
 * full height of the preview box instead.
 *
 * LEFT/RIGHT ONLY, as asked. Vertical margin is deliberately left alone: the pad is short
 * and mostly used, and trimming the height would change the aspect ratio for no gain here.
 *
 * Falls back to the full canvas when there is nothing worth trimming (no ink, an ink
 * narrower than one stroke, or a stray dot), so a signature can never be cropped away.
 *
 * device pixels, not CSS: the canvas backing store is scaled by devicePixelRatio, so every
 * bound below is in the canvas's own pixel space and needs no conversion except the pad.
 */
const CROP_PAD_CSS = 3
const MIN_CROP_W = 16

function inkBounds() {
  const c = canvasEl.value
  const g = c?.getContext('2d')
  if (!c || !g) return null
  const { width, height } = c
  // Alpha is the whole test: the surface is transparent until ink is drawn, so a pixel
  // with any alpha IS ink and the colour itself is irrelevant.
  const { data } = g.getImageData(0, 0, width, height)
  let minX = width
  let maxX = -1
  for (let y = 0; y < height; y++) {
    const row = y * width * 4
    for (let x = 0; x < width; x++) {
      if (data[row + x * 4 + 3] > 10) {
        if (x < minX) minX = x
        if (x > maxX) maxX = x
      }
    }
  }
  return maxX < 0 ? null : { minX, maxX, width, height }
}

function exportDataUrl(): string | null {
  const c = canvasEl.value
  if (!c) return null
  const b = inkBounds()
  if (!b) return c.toDataURL('image/png')
  const pad = Math.max(1, Math.round(CROP_PAD_CSS * dpr.value))
  const x0 = Math.max(0, b.minX - pad)
  const x1 = Math.min(b.width, b.maxX + 1 + pad)
  const w = x1 - x0
  // Nothing to gain (already flush) or too small to be a signature: keep the whole pad.
  if (w >= b.width || w < MIN_CROP_W) return c.toDataURL('image/png')
  const out = document.createElement('canvas')
  out.width = w
  out.height = b.height
  // A FRESH context, so the pad's own dpr transform does not scale this copy as well.
  out.getContext('2d')?.drawImage(c, x0, 0, w, b.height, 0, 0, w, b.height)
  return out.toDataURL('image/png')
}

function start(e: PointerEvent) {
  if (props.disabled) return
  e.preventDefault()
  canvasEl.value?.setPointerCapture(e.pointerId)
  const c = ctx()
  if (!c) return
  const { x, y } = pos(e)
  c.beginPath()
  c.moveTo(x, y)
  // Round cap/join so fast strokes do not look like a chain of dots.
  c.lineCap = 'round'
  c.lineJoin = 'round'
  c.strokeStyle = INK
  c.lineWidth = 1.6
  drawing.value = true
  // Strokes ACCUMULATE (HIRO, 2026-10-04): signing is naturally done in several passes -
  // a name, then a date, then a flourish - and this used to erase everything on every
  // pointerdown, so the second stroke silently destroyed the first. Clearing is now the
  // job of the explicit Clear button, which is the only way to start over.
  // Note this also fixes editing an existing signed row: the stored signature is redrawn by
  // paint() and further strokes now sit alongside it instead of replacing it, which is what
  // you want when someone adds a date under a name.
  //
  // hasInk is deliberately NOT set here: it still flips in move() on the first real pixel,
  // so the "Sign here" placeholder and the Clear button only appear once something was
  // actually drawn, and a stray click that moves nothing does not emit an unchanged PNG.
}

function move(e: PointerEvent) {
  if (!drawing.value || props.disabled) return
  e.preventDefault()
  const c = ctx()
  if (!c) return
  const { x, y } = pos(e)
  c.lineTo(x, y)
  c.stroke()
  hasInk.value = true
}

function end() {
  if (!drawing.value) return
  drawing.value = false
  if (!hasInk.value) return
  // exportDataUrl(), not the raw canvas: the stored PNG is cropped to the ink so the table
  // preview is not scaled down by empty margin. The pad on screen keeps its full width.
  emit('update:modelValue', exportDataUrl())
}

function clear() {
  const c = ctx()
  if (!c || !canvasEl.value) return
  c.clearRect(0, 0, canvasEl.value.width, canvasEl.value.height)
  hasInk.value = false
  emit('update:modelValue', null)
}

onMounted(() => {
  resize()
  window.addEventListener('resize', resize)
})
onBeforeUnmount(() => window.removeEventListener('resize', resize))
</script>

<template>
  <!-- `w-full` on the ROOT as well as the wrapper: the root is a plain block, so the wrapper
       inside it is only as wide as the root allows. Without it, `full-width` alone would
       have no extra room to grow into. -->
  <div :class="fullWidth ? 'w-full' : ''">
    <p v-if="label" class="mb-1 text-xs font-medium text-muted">{{ label }}</p>
    <!-- ONE `:class` binding only. Vue's template compiler rejects a second `:class`
         attribute on the same element outright ("Duplicate attribute" from
         plugin:vite:vue), and it took down the whole page with a 500 rather than
         something local - the HMR error overlay hid it. All the conditional classes
         are therefore folded into a single expression. -->
    <div
      class="relative"
      :class="[
        fullWidth ? 'block w-full' : 'inline-block',
        invalid ? 'rounded ring-2 ring-error' : ''
      ]"
    >
      <canvas
        ref="canvasEl"
        class="block w-full cursor-crosshair rounded border border-default bg-white touch-none dark:bg-white"
        :class="disabled ? 'opacity-60' : ''"
        :style="{ height: height + 'px' }"
        @pointerdown="start"
        @pointermove="move"
        @pointerup="end"
        @pointerleave="end"
        @pointercancel="end"
      />
      <p
        v-if="!hasInk"
        class="pointer-events-none absolute inset-0 flex items-center justify-center text-xs text-muted"
      >
        Sign here
      </p>
      <!-- Clear control, ICON ONLY (HIRO removed the label).
           It started as a 14px icon in bg-white/90 sitting on a WHITE canvas, which made it
           effectively invisible - the control matched its own background exactly. It is now a
           larger icon on a solid, theme-aware chip: `bg-elevated` + `border-default` read in
           both light and dark, the error border signals "destructive" without needing a word,
           and size-4 makes it comfortably clickable. Hidden entirely when there is no ink, so
           it never clutters an empty pad. -->
      <button
        v-if="!disabled && modelValue"
        type="button"
        class="absolute right-1 top-1 inline-flex size-6 items-center justify-center rounded-md
               border border-error/40 bg-elevated text-error shadow-sm transition-colors
               hover:bg-error hover:text-white
               focus:outline-none focus-visible:outline-2 focus-visible:outline-offset-2
               focus-visible:outline-error"
        title="Clear signature"
        aria-label="Clear signature"
        @click="clear"
      >
        <UIcon name="i-lucide-eraser" class="size-3.5" />
      </button>
    </div>
  </div>
</template>