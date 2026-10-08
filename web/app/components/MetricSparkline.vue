<template>
  <div ref="wrapEl" class="relative w-full select-none" :style="{ height: `${height}px` }">
    <canvas
      ref="canvasEl"
      role="img"
      class="block h-full w-full touch-none"
      :aria-label="`${label} history, latest ${lastValue}%`"
      @pointermove="onPointerMove"
      @pointerleave="onPointerLeave"
    />
    <p
      v-if="!values.length"
      class="pointer-events-none absolute inset-0 grid place-items-center text-[11px] text-muted"
    >
      Waiting for data…
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'

/**
 * Compact line + area chart for a 0-100 metric.
 *
 * DELIBERATELY NOT A CHART LIBRARY: three of these sit on the Dashboard and re-render every
 * five seconds. A canvas draw of 60 points costs well under a millisecond, while pulling in
 * Chart.js/UPlot would add ~200KB to a bundle that is served from an offline IIS host.
 *
 * Two details that are easy to get wrong:
 *  - The bitmap must be sized in DEVICE pixels and the context scaled, or every line is
 *    blurry on a HiDPI screen (the panel is read at a glance, so softness reads as "broken").
 *  - The y-scale adapts to the window but is always LABELLED. A fixed 0-100 scale would flatten
 *    an idle host into a dead straight line, and an unlabelled auto-scale would let the same
 *    shape mean 5% or 95%. Peak x 1.25 rounded up to a readable step keeps the variation
 *    visible while the axis states what it is showing.
 */
const props = withDefaults(defineProps<{
  values: number[]
  color: string
  /** Timestamps matching `values`, used by the hover readout. */
  times?: number[]
  height?: number
  label?: string
}>(), {
  times: () => [],
  height: 72,
  label: 'Metric'
})

// ---- geometry (CSS pixels; the context is scaled by the DPR) ----
const PAD_LEFT = 30
const PAD_RIGHT = 8
const PAD_TOP = 12
const PAD_BOTTOM = 6
const STEP_MS = 420

const STEPS = [5, 10, 20, 25, 50, 75, 100]

const wrapEl = ref<HTMLDivElement | null>(null)
const canvasEl = ref<HTMLCanvasElement | null>(null)
const hoverIndex = ref<number | null>(null)

let ctx: CanvasRenderingContext2D | null = null
let cssW = 0
let cssH = 0
let raf = 0
let resizeObs: ResizeObserver | null = null

/** Values actually on screen — lags `props.values` while a transition is running. */
let shown: number[] = []
let from: number[] = []
let animT0 = 0

const lastValue = computed(() => props.values.length ? props.values[props.values.length - 1].toFixed(1) : '0')

const reduceMotion = () =>
  typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches

/** Ceiling for the y-axis: the window peak rounded up to a readable step. */
function scaleMax(): number {
  const peak = props.values.length ? Math.max(...props.values) : 0
  const target = peak * 1.25
  for (const s of STEPS) if (target <= s) return s
  return 100
}

function xAt(i: number, n: number): number {
  const left = PAD_LEFT
  const right = cssW - PAD_RIGHT
  if (n <= 1) return right
  return left + (i / (n - 1)) * (right - left)
}

function yAt(v: number, max: number): number {
  const top = PAD_TOP
  const bottom = cssH - PAD_BOTTOM
  return bottom - (Math.min(Math.max(v, 0), max) / max) * (bottom - top)
}

/**
 * Fritsch-Butland monotone cubic. A plain polyline looks cheap; a catmull-rom spline overshoots,
 * inventing peaks the host never had and dipping below zero on a flat line. This variant cannot
 * overshoot, so a boring flat metric stays flat.
 */
function smoothPath(pts: Array<[number, number]>): Path2D {
  const p = new Path2D()
  const n = pts.length
  if (!n) return p
  p.moveTo(pts[0][0], pts[0][1])
  if (n === 1) return p
  if (n === 2) {
    p.lineTo(pts[1][0], pts[1][1])
    return p
  }

  const h: number[] = []
  const slope: number[] = []
  for (let i = 0; i < n - 1; i++) {
    h[i] = Math.max(pts[i + 1][0] - pts[i][0], 0.0001)
    slope[i] = (pts[i + 1][1] - pts[i][1]) / h[i]
  }

  const tangent: number[] = [slope[0]]
  for (let i = 1; i < n - 1; i++) {
    const a = slope[i - 1]
    const b = slope[i]
    if (a * b <= 0) {
      tangent[i] = 0 // local extreme: flatten so the curve cannot bulge past it
    } else {
      const w1 = 2 * h[i] + h[i - 1]
      const w2 = h[i] + 2 * h[i - 1]
      tangent[i] = (w1 + w2) / (w1 / a + w2 / b)
    }
  }
  tangent[n - 1] = slope[n - 2]

  for (let i = 0; i < n - 1; i++) {
    const [x0, y0] = pts[i]
    const [x1, y1] = pts[i + 1]
    const third = h[i] / 3
    p.bezierCurveTo(x0 + third, y0 + tangent[i] * third, x1 - third, y1 - tangent[i + 1] * third, x1, y1)
  }
  return p
}

function alpha(hex: string, a: number): string {
  const m = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex.trim())
  if (!m) return hex
  const [r, g, b] = [m[1], m[2], m[3]].map(v => parseInt(v, 16))
  return `rgba(${r}, ${g}, ${b}, ${a})`
}

function draw() {
  if (!ctx) return
  const max = scaleMax()
  ctx.clearRect(0, 0, cssW, cssH)

  // ---- grid + axis labels ----
  ctx.lineWidth = 1
  ctx.font = '10px "Public Sans", ui-sans-serif, system-ui, sans-serif'
  ctx.textAlign = 'right'
  ctx.textBaseline = 'middle'
  // Grid at 0 / half / max, but only the two ENDS are labelled. The scale is fitted to the
  // window peak, so the midpoint is frequently a fraction (a 25 ceiling gives 12.5) and an
  // axis reading "13" next to a percentage looks like a bug. The hover readout carries the
  // exact values instead.
  const levels: Array<{ at: number; label: boolean }> = [
    { at: max, label: true },
    { at: max / 2, label: false },
    { at: 0, label: true }
  ]
  levels.forEach(({ at, label }) => {
    const y = yAt(at, max)
    ctx.strokeStyle = at === 0 ? 'rgba(148, 163, 184, 0.35)' : 'rgba(148, 163, 184, 0.18)'
    ctx.beginPath()
    ctx.moveTo(PAD_LEFT, y)
    ctx.lineTo(cssW - PAD_RIGHT, y)
    ctx.stroke()

    if (label) {
      ctx.fillStyle = 'rgba(148, 163, 184, 0.75)'
      ctx.fillText(`${Math.round(at)}`, PAD_LEFT - 7, y)
    }
  })

  const n = shown.length
  if (n === 0) return

  const pts: Array<[number, number]> = shown.map((v, i) => [xAt(i, n), yAt(v, max)])
  const line = smoothPath(pts)

  // ---- area under the line ----
  if (n > 1) {
    const area = new Path2D(line)
    const baseline = cssH - PAD_BOTTOM
    area.lineTo(pts[n - 1][0], baseline)
    area.lineTo(pts[0][0], baseline)
    area.closePath()
    const grad = ctx.createLinearGradient(0, PAD_TOP, 0, baseline)
    grad.addColorStop(0, alpha(props.color, 0.30))
    grad.addColorStop(1, alpha(props.color, 0))
    ctx.fillStyle = grad
    ctx.fill(area)
  }

  // ---- the line ----
  ctx.strokeStyle = props.color
  ctx.lineWidth = 2
  ctx.lineJoin = 'round'
  ctx.lineCap = 'round'
  ctx.stroke(line)

  // ---- newest point, marked so "now" is unambiguous ----
  const [lx, ly] = pts[n - 1]
  ctx.beginPath()
  ctx.arc(lx, ly, 5, 0, Math.PI * 2)
  ctx.fillStyle = alpha(props.color, 0.20)
  ctx.fill()
  ctx.beginPath()
  ctx.arc(lx, ly, 2.6, 0, Math.PI * 2)
  ctx.fillStyle = props.color
  ctx.fill()

  // ---- hover readout ----
  const hi = hoverIndex.value
  if (hi !== null && hi < n) {
    const [hx, hy] = pts[hi]
    ctx.strokeStyle = 'rgba(148, 163, 184, 0.5)'
    ctx.lineWidth = 1
    ctx.setLineDash([3, 3])
    ctx.beginPath()
    ctx.moveTo(hx, PAD_TOP)
    ctx.lineTo(hx, cssH - PAD_BOTTOM)
    ctx.stroke()
    ctx.setLineDash([])

    ctx.beginPath()
    ctx.arc(hx, hy, 3.4, 0, Math.PI * 2)
    ctx.fillStyle = props.color
    ctx.fill()
    ctx.lineWidth = 2
    ctx.strokeStyle = 'rgba(15, 23, 42, 0.75)'
    ctx.stroke()

    const time = props.times[hi] ? new Date(props.times[hi]).toLocaleTimeString('id-ID', { hour12: false }) : ''
    const text = time ? `${time}  ${shown[hi].toFixed(1)}%` : `${shown[hi].toFixed(1)}%`
    ctx.font = '10px "Public Sans", ui-sans-serif, system-ui, sans-serif'
    const tw = ctx.measureText(text).width
    const bw = tw + 12
    const bh = 18
    const bx = Math.min(Math.max(hx - bw / 2, 2), cssW - bw - 2)
    const by = Math.max(hy - bh - 10, 0)

    ctx.fillStyle = 'rgba(2, 6, 23, 0.88)'
    ctx.beginPath()
    if (typeof ctx.roundRect === 'function') ctx.roundRect(bx, by, bw, bh, 5)
    else ctx.rect(bx, by, bw, bh)
    ctx.fill()

    ctx.fillStyle = '#E2E8F0'
    ctx.textAlign = 'center'
    ctx.textBaseline = 'middle'
    ctx.fillText(text, bx + bw / 2, by + bh / 2 + 0.5)
    ctx.textAlign = 'right'
  }
}

function scheduleDraw() {
  cancelAnimationFrame(raf)
  raf = requestAnimationFrame(draw)
}

/** Right-aligns the two series so a new point enters from the right instead of shifting data. */
function alignTo(targetLength: number, series: number[]): number[] {
  if (series.length === targetLength) return series
  if (series.length > targetLength) return series.slice(series.length - targetLength)
  const pad = new Array(targetLength - series.length).fill(series[0] ?? 0)
  return [...pad, ...series]
}

function transition() {
  const target = props.values
  if (!target.length) {
    shown = []
    from = []
    scheduleDraw()
    return
  }

  if (reduceMotion() || !shown.length) {
    shown = [...target]
    from = []
    scheduleDraw()
    return
  }

  from = alignTo(target.length, shown)
  animT0 = performance.now()
  tick()
}

function tick() {
  const target = props.values
  const span = STEP_MS
  const t = Math.min((performance.now() - animT0) / span, 1)
  const ease = 1 - Math.pow(1 - t, 3)
  shown = target.map((v, i) => {
    const prev = from[i] ?? v
    return prev + (v - prev) * ease
  })
  draw()
  if (t < 1) raf = requestAnimationFrame(tick)
}

function onPointerMove(e: PointerEvent) {
  if (!canvasEl.value || shown.length === 0) return
  const rect = canvasEl.value.getBoundingClientRect()
  const x = e.clientX - rect.left
  const n = shown.length
  const left = PAD_LEFT
  const right = cssW - PAD_RIGHT
  if (x < left - 6 || x > right + 6) { hoverIndex.value = null; scheduleDraw(); return }
  const ratio = n <= 1 ? 1 : (x - left) / Math.max(right - left, 1)
  hoverIndex.value = Math.min(Math.max(Math.round(ratio * (n - 1)), 0), n - 1)
  scheduleDraw()
}

function onPointerLeave() {
  hoverIndex.value = null
  scheduleDraw()
}

function resize() {
  const el = canvasEl.value
  const wrap = wrapEl.value
  if (!el || !wrap) return
  const dpr = Math.min(window.devicePixelRatio || 1, 2)
  cssW = Math.max(wrap.clientWidth, 1)
  cssH = props.height
  el.width = Math.round(cssW * dpr)
  el.height = Math.round(cssH * dpr)
  ctx = el.getContext('2d')
  ctx?.setTransform(dpr, 0, 0, dpr, 0, 0)
  scheduleDraw()
}

watch(() => props.values, transition, { deep: true })
watch(() => props.height, resize)

onMounted(() => {
  resize()
  resizeObs = new ResizeObserver(resize)
  if (wrapEl.value) resizeObs.observe(wrapEl.value)
  if (props.values.length) transition()
})

onBeforeUnmount(() => {
  cancelAnimationFrame(raf)
  resizeObs?.disconnect()
})
</script>
