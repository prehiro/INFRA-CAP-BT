<script setup lang="ts">
import { computed, useId, ref, onMounted, onBeforeUnmount } from 'vue'

// Thin red contour motion; the solid white glyph paints last.
// Local Public Sans 700 glyph outlines retain the app's existing typeface.
// Attribution/licenses: web/THIRD-PARTY-NOTICES.md. No runtime font conversion or network.
const props = withDefaults(defineProps<{ tracking?: number, lineHeight?: number, intro?: boolean }>(), {
  tracking: 0,
  lineHeight: 1.25,
  intro: false
})
// Adapted from the user-supplied faxriddin20 quick-panda-13 chitchat sequence.
// Mount-only, not hover/focus; one accessible static label and a fixed native text footprint.
const label = 'INFRA-BTCAP'
const chatter = ['#', '.', '^{', '-!', '#$_', '№:0', '#{+.', '@}-?', '?{4@%', '=.,^!', '?2@%', ';1}]', '?{%:%', '|{f[4', '{4%0%', "'1_0<", '{0%', "]>'", '4', '2']
const entering = ref(props.intro)
const introText = ref('#' + label.slice(1))
// Fixed character slots: every scramble character is drawn inside the advance width of its
// final letter, so the wordmark never slides left/right while the glyphs change.
const sizeEl = ref<HTMLElement | null>(null)
const slotWidths = ref<number[]>([])
const introChars = computed(() => Array.from(introText.value))
let introTimer: ReturnType<typeof setTimeout> | undefined
let motionQuery: MediaQueryList | undefined
let unmounted = false
// Narrow characters used when a scrambled glyph is wider than its slot, so a glyph
// can never spill over the neighbouring letter.
const narrowFallbacks = ['#', '.', '-', '+', '=', '4', '2']
let charWidths = new Map<string, number>()
function measureSlots() {
  const el = sizeEl.value
  const node = el?.firstChild
  if (!el || !node || node.nodeType !== Node.TEXT_NODE) return
  const previous = el.style.letterSpacing
  el.style.letterSpacing = '0'
  const range = document.createRange()
  const widths: number[] = []
  const content = node.textContent ?? ''
  for (let i = 0; i < content.length; i++) {
    range.setStart(node, i)
    range.setEnd(node, i + 1)
    widths.push(range.getBoundingClientRect().width)
  }
  el.style.letterSpacing = previous
  const spacing = parseFloat(getComputedStyle(el).letterSpacing) || 0
  slotWidths.value = widths.map(width => width + spacing)
  // Glyph widths for the scramble characters, measured in the same typeface.
  const style = getComputedStyle(el)
  const canvas = document.createElement('canvas')
  const context = canvas.getContext('2d')
  if (context) {
    context.font = `${style.fontStyle} ${style.fontWeight} ${style.fontSize} ${style.fontFamily}`
    const candidates = new Set<string>(chatter.join('').split(''))
    narrowFallbacks.forEach(character => candidates.add(character))
    charWidths = new Map(Array.from(candidates, character => [character, context.measureText(character).width]))
  }
}
function slotStyle(index: number) {
  const width = slotWidths.value[index]
  return width ? { width: `${width}px` } : undefined
}
// Keep a scrambled glyph inside its slot: swap it for the widest narrow fallback that fits.
function fitCharacter(character: string, index: number) {
  const limit = slotWidths.value[index]
  if (!limit) return character
  if ((charWidths.get(character) ?? 0) <= limit) return character
  const fitting = narrowFallbacks.find(fallback => (charWidths.get(fallback) ?? 0) <= limit)
  return fitting ?? '.'
}
function fitText(text: string, offset: number) {
  return Array.from(text, (character, index) => fitCharacter(character, offset + index)).join('')
}
function stopIntro() {
  clearTimeout(introTimer)
  entering.value = false
}
function beginCycle() {
  clearTimeout(introTimer)
  if (motionQuery?.matches) { stopIntro(); return }
  entering.value = true
  const start = performance.now()
  function tick() {
    const index = Math.floor((performance.now() - start) / 60)
    if (index >= chatter.length) {
      entering.value = false
      // Run the original contour cycle (including its quiet interval) before scrambling again.
      introTimer = setTimeout(beginCycle, 10000)
      return
    }
    const text = chatter[index]!
    const chars = Array.from(text)
    introText.value = index < 12
      ? fitText(text, 0) + label.slice(chars.length)
      : label.slice(0, -chars.length) + fitText(text, label.length - chars.length)
    introTimer = setTimeout(tick, 60)
  }
  tick()
}
function onMotionChange() { if (motionQuery?.matches) stopIntro(); else beginCycle() }
onMounted(() => {
  if (!props.intro) return
  measureSlots()
  // The bundled font may still be swapping in; re-measure so the slots match the real advance.
  document.fonts?.ready.then(() => { if (!unmounted) measureSlots() })
  motionQuery = window.matchMedia('(prefers-reduced-motion: reduce)')
  motionQuery.addEventListener('change', onMotionChange)
  beginCycle()
})
onBeforeUnmount(() => {
  unmounted = true
  clearTimeout(introTimer)
  motionQuery?.removeEventListener('change', onMotionChange)
})
const units = 2000
const tipGlowId = `brand-tip-${useId()}`
const glyphs = [
  {
    "d": "M162 0V1446H453V0Z",
    "x": 0,
    "y": 0
  },
  {
    "d": "M162 0V1446H451L1111 489V1446H1403V0H1140L453 995V0Z",
    "x": 615,
    "y": 0
  },
  {
    "d": "M162 0V1446H1146L1145 1202H456V839H1053V600H456V0Z",
    "x": 2179,
    "y": 0
  },
  {
    "d": "M162 0V1446H772Q945 1446 1055.0 1391.0Q1165 1336 1217.0 1238.5Q1269 1141 1269 1014Q1269 900 1233.5 821.5Q1198 743 1140.5 691.0Q1083 639 1015 606L1284 0H981L746 557H453V0ZM453 782H720Q837 782 905.5 845.0Q974 908 974 1007Q974 1107 908.0 1165.0Q842 1223 741 1223H453Z",
    "x": 3406,
    "y": 0
  },
  {
    "d": "M55 0 572 1446H881L1398 0H1107L989 335H468L348 0ZM534 590H918L727 1165Z",
    "x": 4812,
    "y": 0
  },
  {
    "d": "M120 408V621H666V409Z",
    "x": 6205,
    "y": 0
  },
  {
    "d": "M162 0V1446H686Q970 1446 1107.5 1348.5Q1245 1251 1245 1067Q1245 959 1190.0 879.5Q1135 800 994 741Q1085 718 1141.0 679.0Q1197 640 1227.0 592.5Q1257 545 1268.0 493.5Q1279 442 1279 392Q1279 192 1143.0 96.0Q1007 0 710 0ZM453 226H716Q848 226 915.5 279.0Q983 332 983 421Q983 485 950.0 528.5Q917 572 860.5 594.0Q804 616 732 616H453ZM453 848H732Q777 848 818.5 858.5Q860 869 892.5 891.0Q925 913 944.0 947.5Q963 982 963 1029Q963 1123 892.5 1171.5Q822 1220 712 1220H453Z",
    "x": 6991,
    "y": 0
  },
  {
    "d": "M475 0V1186H38V1446H1195V1186H768V0Z",
    "x": 8328,
    "y": 0
  },
  {
    "d": "M751 -20Q549 -20 402.0 74.0Q255 168 175.5 338.0Q96 508 96 735Q96 963 175.0 1126.5Q254 1290 401.0 1378.0Q548 1466 751 1466Q914 1466 1041.5 1400.5Q1169 1335 1247.5 1222.0Q1326 1109 1340 967H1034Q1020 1042 983.0 1100.5Q946 1159 886.0 1192.5Q826 1226 742 1226Q641 1226 568.0 1177.0Q495 1128 456.0 1019.0Q417 910 417 731Q417 461 503.5 340.5Q590 220 742 220Q826 220 886.0 258.5Q946 297 983.0 360.5Q1020 424 1034 500H1340Q1327 397 1290.0 303.5Q1253 210 1185.0 137.0Q1117 64 1010.0 22.0Q903 -20 751 -20Z",
    "x": 9554,
    "y": 0
  },
  {
    "d": "M55 0 572 1446H881L1398 0H1107L989 335H468L348 0ZM534 590H918L727 1165Z",
    "x": 10946,
    "y": 0
  },
  {
    "d": "M162 0V1446H694Q852 1446 977.0 1391.5Q1102 1337 1174.5 1231.0Q1247 1125 1247 972Q1247 828 1183.0 726.0Q1119 624 1003.5 570.0Q888 516 732 516H456V0ZM453 742H724Q834 742 904.0 812.5Q974 883 974 987Q974 1095 900.0 1158.5Q826 1222 720 1222H453Z",
    "x": 12399,
    "y": 0
  }
]
const viewHeight = computed(() => units * props.lineHeight)
const viewWidth = computed(() => 13704 + glyphs.length * props.tracking * units)
const baseline = computed(() => (viewHeight.value + 1446) / 2)
const viewBox = computed(() => `0 0 ${viewWidth.value} ${viewHeight.value}`)
</script>

<template>
  <span class="animated-brand-text" :class="{ 'brand-entering': entering, 'brand-login-intro': intro }">
    <span class="sr-only">INFRA-BTCAP</span>
    <!-- Native text reserves the original font/letter-spacing/line-height footprint. -->
    <span ref="sizeEl" class="brand-wordmark-size" aria-hidden="true">INFRA-BTCAP</span>
    <span v-if="entering" class="brand-chitchat" aria-hidden="true"><span
        v-for="(character, index) in introChars" :key="index" class="brand-chitchat-slot"
        :style="slotStyle(index)">{{ character }}</span></span>
    <svg class="brand-wordmark-svg" :viewBox="viewBox" preserveAspectRatio="none" aria-hidden="true">
      <defs>
        <!-- Blur uses font coordinates, not subpixel lengths scaled by the glyph viewBox. -->
        <filter :id="tipGlowId" filterUnits="userSpaceOnUse" primitiveUnits="userSpaceOnUse"
                :x="-units * 0.7" :y="-units * 0.7" :width="units * 2.4" :height="units * 2.4"
                color-interpolation-filters="sRGB">
          <!-- Broaden only the halo emitter, so a wider blur stays visible without a fatter core. -->
          <feMorphology in="SourceGraphic" operator="dilate" :radius="units * 0.04" result="halo-source" />
          <feGaussianBlur in="halo-source" :stdDeviation="units * 0.2" result="soft-tip" />
          <feGaussianBlur in="SourceGraphic" :stdDeviation="units * 0.015" result="tip-core" />
          <feFlood flood-color="#ef4444" flood-opacity="0.55" result="red-light" />
          <feComposite in="red-light" in2="soft-tip" operator="in" result="tip-halo" />
          <feMerge>
            <feMergeNode in="tip-halo" />
            <feMergeNode in="tip-core" />
          </feMerge>
        </filter>
      </defs>
      <g :transform="`translate(0 ${baseline}) scale(1 -1)`">
        <g v-for="(glyph, index) in glyphs" :key="index"
           :transform="`translate(${glyph.x + index * tracking * units} ${glyph.y})`">
          <path class="brand-wordmark-trace" :d="glyph.d" pathLength="360"
                :stroke-width="units * 0.06" />
          <path class="brand-wordmark-tip" :d="glyph.d" pathLength="360"
                :stroke-width="units * 0.075" :filter="`url(#${tipGlowId})`" />
          <path class="brand-wordmark-base" :d="glyph.d" />
        </g>
      </g>
    </svg>

  </span>
</template>

<style scoped>
.animated-brand-text {
  position: relative;
  display: inline-block;
  vertical-align: top;
  white-space: nowrap;
  /* Colour is inherited: the sidebar brand is black on light and white on dark, while the
     login mark keeps its own light text on the dark login background. */
}
.brand-wordmark-size { visibility: hidden; }
.brand-login-intro { color: #fff; }
.brand-chitchat {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: baseline;
  white-space: nowrap;
  pointer-events: none;
}
/* Each character is pinned to its final letter slot, so the wordmark only changes glyphs. */
.brand-chitchat-slot {
  display: inline-block;
  flex: 0 0 auto;
  text-align: left;
  letter-spacing: 0;
}
.brand-entering .brand-wordmark-svg { visibility: hidden; }
.brand-entering .brand-wordmark-trace,
.brand-entering .brand-wordmark-tip { animation: none; }
.brand-wordmark-svg {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  overflow: visible;
  pointer-events: none;
  filter: drop-shadow(0 0 .6px rgb(0 0 0 / .65));
}
/* Paint the opaque glyph above the moving strokes: its white interior is never covered. */
.brand-wordmark-base { fill: currentColor; opacity: 1; }
.brand-wordmark-trace,
.brand-wordmark-tip {
  fill: none;
  stroke-linecap: round;
  stroke-linejoin: round;
  opacity: 0;
  animation: brand-text-contour 10s linear infinite;
}
.brand-wordmark-trace {
  stroke: #ef4444;
  stroke-dasharray: 90 270;
}
.brand-wordmark-tip {
  stroke: #ef4444;
  stroke-dasharray: 6 354;
  animation-name: brand-text-tip;
}

@keyframes brand-text-contour {
  0% { stroke-dashoffset: 360; opacity: 0; }
  3% { opacity: 1; }
  24% { stroke-dashoffset: 0; opacity: 1; }
  28%, 100% { stroke-dashoffset: 0; opacity: 0; }
}
/* Same phase as the body; the short bright segment occupies its leading end (84..90). */
@keyframes brand-text-tip {
  0% { stroke-dashoffset: 276; opacity: 0; }
  3% { opacity: 1; }
  24% { stroke-dashoffset: -84; opacity: 1; }
  28%, 100% { stroke-dashoffset: -84; opacity: 0; }
}
@media (prefers-reduced-motion: reduce), print {
  .brand-chitchat { display: none; }
  .brand-entering .brand-wordmark-svg { visibility: visible; }
  .brand-wordmark-trace,
  .brand-wordmark-tip { animation: none !important; display: none; }
}
</style>
