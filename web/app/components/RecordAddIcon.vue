<script setup lang="ts">
const props = withDefaults(defineProps<{ name?: string; size?: 'sm' | 'md' | 'lg'; randomHalo?: boolean }>(), {
  name: 'i-carbon-data-add', size: 'sm', randomHalo: false
})

// Only opted-in header rings change targets; the glyph's sway and inner SVG motions stay untouched.
const haloScale = ref(1)
const haloOpacity = ref(0.4)
const haloDuration = ref(1000)
let haloTimer: ReturnType<typeof setTimeout> | undefined
let motionQuery: MediaQueryList | undefined
let expanded = false
function stopHalo() { clearTimeout(haloTimer); haloTimer = undefined }
function stepHalo() {
  if (!props.randomHalo || motionQuery?.matches) return
  expanded = !expanded
  haloScale.value = expanded ? 1.16 + Math.random() * 0.19 : 0.83 + Math.random() * 0.15
  haloOpacity.value = expanded ? 0.12 + Math.random() * 0.12 : 0.3 + Math.random() * 0.18
  haloDuration.value = 850 + Math.round(Math.random() * 700)
  haloTimer = setTimeout(stepHalo, haloDuration.value + 80)
}
function onMotionChange() {
  stopHalo()
  if (!motionQuery?.matches) stepHalo()
}
onMounted(() => {
  if (!props.randomHalo) return
  motionQuery = window.matchMedia('(prefers-reduced-motion: reduce)')
  motionQuery.addEventListener('change', onMotionChange)
  onMotionChange()
})
onBeforeUnmount(() => {
  stopHalo()
  motionQuery?.removeEventListener('change', onMotionChange)
})
</script>

<template>
  <!-- Explicit opt-in for header icons; preserve the existing plate and crisp glyph size. -->
  <span class="pc-add-mark relative inline-grid shrink-0 place-items-center" :class="size === 'lg' ? 'size-7' : size === 'md' ? 'size-6' : 'size-5'" aria-hidden="true">
    <span class="pc-add-halo pointer-events-none absolute -inset-1 rounded-md"
      :class="{ 'pc-add-halo--random': randomHalo }"
      :style="randomHalo ? { transform: `scale(${haloScale})`, opacity: haloOpacity, transitionDuration: `${haloDuration}ms` } : undefined" />
    <slot>
      <ChannelAddIcon v-if="name === 'channel-add'" class="pc-add-glyph" :class="size === 'lg' ? 'size-7' : 'size-5'" />
      <UIcon v-else :name="name" class="pc-add-glyph" :class="size === 'lg' ? 'size-7' : 'size-5'" />
    </slot>
  </span>
</template>

<style scoped>
.pc-add-glyph,
:slotted(.pc-add-tilt) {
  animation: pc-add-sway 3.6s ease-in-out infinite;
  transform-origin: 50% 60%;
}

.pc-add-halo {
  border: 1px solid color-mix(in oklab, var(--ui-primary) 45%, transparent);
  animation: pc-add-halo 3.6s ease-in-out infinite;
}
.pc-add-halo--random {
  animation: none;
  transition-property: transform, opacity;
  transition-timing-function: ease-in-out;
}

@keyframes pc-add-sway {
  0%, 100% { transform: translateY(0) rotate(-4deg); }
  50% { transform: translateY(-1.5px) rotate(5deg); }
}

@keyframes pc-add-halo {
  0%, 100% { transform: scale(1); opacity: 0.4; }
  50% { transform: scale(1.25); opacity: 0.08; }
}

/* Hovering the plate plays the Sign In arrow motion on the fluent glyph. */
.pc-add-mark:hover :deep(.channel-add-icon) { animation: record-add-forward 650ms ease-in-out; }

@media (prefers-reduced-motion: reduce) {
  .pc-add-glyph,
  :slotted(.pc-add-tilt),
  .pc-add-halo { animation: none; }
  .pc-add-halo { opacity: 0.18 !important; transform: none !important; transition: none !important; }
}
</style>
