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
}>(), {
  modelValue: null,
  label: '',
  height: 90,
  disabled: false
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
  if (model) {
    // Redraw an existing signature so editing a row shows the stored ink.
    const img = new Image()
    img.onload = () => c.drawImage(img, 0, 0, width, height)
    img.src = model
  }
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
  // A fresh stroke discards whatever was loaded before, otherwise editing a signed
  // row would keep the old signature underneath the new one.
  c.clearRect(0, 0, canvasEl.value!.width, canvasEl.value!.height)
  hasInk.value = false
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
  emit('update:modelValue', canvasEl.value?.toDataURL('image/png') ?? null)
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
  <div>
    <p v-if="label" class="mb-1 text-xs font-medium text-muted">{{ label }}</p>
    <div class="relative inline-block">
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
        class="absolute right-1.5 top-1.5 inline-flex size-7 items-center justify-center rounded-lg
               border border-error/40 bg-elevated text-error shadow-sm transition-colors
               hover:bg-error hover:text-white
               focus:outline-none focus-visible:outline-2 focus-visible:outline-offset-2
               focus-visible:outline-error"
        title="Clear signature"
        aria-label="Clear signature"
        @click="clear"
      >
        <UIcon name="i-lucide-eraser" class="size-4" />
      </button>
    </div>
  </div>
</template>