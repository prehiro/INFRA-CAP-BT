<script setup lang="ts">
import { accents, neutrals, fonts, presets, pickDifferentFont } from '~/utils/themeStudio'
import { referenceCatalog, referenceCss, referenceIconSets } from '~/utils/themeReference'

const open = ref(false)
const { current, original, error, undoStack, redoStack, change, restoreOriginal, undo, redo } = useThemeStudio()
const config = useAppConfig()
const playingIcon = ref('')
let iconTimer: ReturnType<typeof setTimeout> | undefined
onBeforeUnmount(() => clearTimeout(iconTimer))
function perform(name: string, action: () => void) {
  action()
  clearTimeout(iconTimer)
  playingIcon.value = ''
  nextTick(() => {
    playingIcon.value = name
    iconTimer = setTimeout(() => { playingIcon.value = '' }, 720)
  })
}
const selectedPreset = computed(() => referenceCatalog.presets.find(p => p.id === current.value.reference)?.name || '')
useHead(() => ({ style: [{ key: 'theme-studio-reference', innerHTML: referenceCss(current.value.reference, current.value.primary) }] }))
const titleCase = (value: string) => value.charAt(0).toUpperCase() + value.slice(1)
function preset(event: Event) {
  const name = (event.target as HTMLSelectElement).value
  if (presets[name]) change({ ...presets[name], ...(presets[name].reference ? { mode: current.value.mode } : {}) })
}
function choose(key: string, event: Event) {
  change({ [key]: (event.target as HTMLSelectElement).value })
}
function randomize() {
  const pick = <T,>(values: T[]) => values[Math.floor(Math.random() * values.length)]!
  change({ primary: pick(accents), neutral: pick(neutrals), font: pickDifferentFont(current.value.font), radius: pick(['0.125', '0.25', '0.375', '0.5']), variant: pick(['solid', 'soft', 'outline', 'subtle']) })
}
</script>

<template>
  <USlideover v-model:open="open" title="Accent Picker" description="Customize your workspace. Changes stay in this browser."
    side="right" :modal="false" :overlay="false"
    :ui="{ content: 'theme-studio-panel fixed inset-y-3 right-3 w-[340px] max-w-[calc(100vw-32px)] rounded-xl ring ring-default shadow-2xl', header: 'p-5 pr-12', body: 'p-5 space-y-5', footer: 'p-4 border-t border-default' }">
    <button type="button" class="theme-studio-trigger" aria-label="Open theme selector" title="Theme Studio" :aria-expanded="open">
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M12 3a9 9 0 1 0 0 18h1a2 2 0 0 0 1.4-3.4 1.4 1.4 0 0 1 1-2.4H17a4 4 0 0 0 4-4c0-4.4-4-8.2-9-8.2Z"/><circle cx="7.5" cy="10" r="1"/><circle cx="11" cy="6.8" r="1"/><circle cx="16" cy="8" r="1"/></svg>
      <span>Accent</span>
    </button>
    <template #body>
      <div class="studio-toolbar">
        <button type="button" :disabled="!undoStack.length" @click="perform('undo', undo)">
          <svg class="studio-icon" data-motion="undo" :class="{ 'is-playing': playingIcon === 'undo' }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m9 14-5-5 5-5M4 9h10a6 6 0 0 1 0 12h-2" /></svg>Undo
        </button>
        <button type="button" :disabled="!redoStack.length" @click="perform('redo', redo)">
          <svg class="studio-icon" data-motion="redo" :class="{ 'is-playing': playingIcon === 'redo' }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m15 14 5-5-5-5M20 9H10a6 6 0 0 0 0 12h2" /></svg>Redo
        </button>
        <button type="button" @click="perform('randomize', randomize)">
          <svg class="studio-icon" data-motion="randomize" :class="{ 'is-playing': playingIcon === 'randomize' }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 7h3c4 0 7 10 11 10h4m-4-3 4 3-4 3M3 17h3c1.5 0 2.5-1 3.5-2.5M15 9c.8-1.3 1.5-2 2.5-2H21m-4-3 4 3-4 3" /></svg>Randomize
        </button>
      </div>
      <section>
        <label for="studio-preset">Preset</label>
        <select id="studio-preset" :value="selectedPreset" @change="preset">
          <option value="" disabled>Choose a starting point</option>
          <optgroup label="Vue Presets"><option v-for="p in referenceCatalog.presets" :key="p.id" :value="p.name">{{ p.name }}</option></optgroup>
          <optgroup label="Local Presets"><option v-for="name in ['Studio', 'Ocean', 'Graphite', 'Warm Paper']" :key="name" :value="name">{{ name }}</option></optgroup>
        </select>
      </section>
      <section>
        <div class="studio-label">Appearance</div>
        <div class="studio-modes" role="group" aria-label="Appearance">
          <button type="button" :aria-pressed="current.mode === 'light'" @click="perform('light', () => change({ mode: 'light' }))">
            <svg class="studio-icon" data-motion="light" :class="{ 'is-playing': playingIcon === 'light' }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" aria-hidden="true"><circle cx="12" cy="12" r="4"/><path d="M12 2v2m0 16v2M4.93 4.93l1.42 1.42m11.3 11.3 1.42 1.42M2 12h2m16 0h2M4.93 19.07l1.42-1.42m11.3-11.3 1.42-1.42"/></svg>Light
          </button>
          <button type="button" :aria-pressed="current.mode === 'dark'" @click="perform('dark', () => change({ mode: 'dark' }))">
            <svg class="studio-icon" data-motion="dark" :class="{ 'is-playing': playingIcon === 'dark' }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M20.8 13A9 9 0 0 1 11 3.2 9 9 0 1 0 20.8 13Z"/></svg>Dark
          </button>
          <button type="button" :aria-pressed="current.mode === 'system'" @click="perform('system', () => change({ mode: 'system' }))">
            <svg class="studio-icon" data-motion="system" :class="{ 'is-playing': playingIcon === 'system' }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="3" y="4" width="18" height="14" rx="2"/><path d="M8 21h8m-4-3v3M7 8h10"/></svg>System
          </button>
        </div>
      </section>
      <section v-for="group in [{ key: 'primary', label: 'Accent Color', values: accents }, { key: 'neutral', label: 'Neutral Color', values: neutrals }]" :key="group.key">
        <div class="studio-label flex justify-between"><span>{{ group.label }}</span><span class="text-muted font-normal">{{ titleCase(group.key === 'primary' ? current.primary : current.neutral) }}</span></div>
        <div class="studio-swatches" role="group" :aria-label="group.label">
          <button v-for="color in group.values" :key="color" type="button" :aria-label="`${group.label}: ${titleCase(color)}`" :title="titleCase(color)" :aria-pressed="(group.key === 'primary' ? current.primary : current.neutral) === color" @click="change({ [group.key]: color })">
            <span :style="{ background: `var(--color-${color === 'neutral' ? 'old-neutral' : color}-500)` }" />
            <svg v-if="(group.key === 'primary' ? current.primary : current.neutral) === color" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="white" stroke-width="3" aria-hidden="true"><path d="m5 12 4 4L19 6" /></svg>
          </button>
        </div>
      </section>
      <div class="studio-grid">
        <section>
          <label for="studio-font">Font · Offline</label>
          <select id="studio-font" :value="current.font" @change="choose('font', $event)"><option v-for="(label, value) in fonts" :key="value" :value="value">{{ label }}</option></select>
        </section>
        <section>
          <label for="studio-icons">Icon Set</label>
          <select id="studio-icons" :value="current.icons" @change="choose('icons', $event)"><option value="original">Lucide / Original</option><option value="carbon">Carbon</option><option v-for="(_, name) in referenceIconSets" :key="name" :value="name">{{ titleCase(name) }}</option></select>
        </section>
        <section>
          <label for="studio-radius">Radius</label>
          <select id="studio-radius" :value="current.radius" @change="choose('radius', $event)"><option value="original">Original</option><option v-for="r in ['0', '0.125', '0.25', '0.375', '0.5', '0.625', '0.75']" :key="r" :value="r">{{ r }} rem</option></select>
        </section>
        <section>
          <label for="studio-size">Default Size</label>
          <select id="studio-size" :value="current.size" @change="choose('size', $event)"><option value="original">Original</option><option v-for="size in ['xs', 'sm', 'md', 'lg', 'xl']" :key="size" :value="size">{{ size.toUpperCase() }}</option></select>
        </section>
      </div>
      <section>
        <label for="studio-variant">Button / Badge Variant</label>
        <select id="studio-variant" :value="current.variant" @change="choose('variant', $event)"><option value="original">Original</option><option v-for="variant in ['solid', 'outline', 'soft', 'subtle']" :key="variant" :value="variant">{{ titleCase(variant) }}</option></select>
        <p class="studio-note">Defaults affect controls without an explicit size or variant. Custom device icons stay unchanged. All fonts and icons work offline.</p>
      </section>
      <section class="studio-preview">
        <div class="studio-label">Live Preview</div>
        <p class="text-sm mb-3">Make this workspace yours.</p>
        <div class="flex items-center flex-wrap gap-2"><UButton data-theme-preview :icon="config.ui.icons.search">Button</UButton><UBadge>Badge</UBadge></div>
        <UInput class="mt-3 w-full" placeholder="Input preview" aria-label="Theme input preview" />
      </section>
      <p v-if="error" role="alert" class="text-xs text-error">{{ error }}</p>
    </template>
    <template #footer>
      <div class="w-full space-y-2">
        <button type="button" class="studio-restore" :disabled="!original" @click="perform('restore', restoreOriginal)">
          <svg class="studio-icon" data-motion="restore" :class="{ 'is-playing': playingIcon === 'restore' }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 11a9 9 0 1 1 2.6 6.4M3 4v7h7m2-4v5l3 2"/></svg>Restore Original
        </button>
        <p class="studio-note text-center">{{ original ? 'Restores your settings from before the first change, even after a refresh.' : 'Your original settings are active. A backup is saved before your first change.' }}</p>
      </div>
    </template>
  </USlideover>
</template>

<style scoped>
.theme-studio-trigger { position: fixed; right: 0; top: calc(var(--ui-header-height, 4rem) / 2); transform: translateY(-50%); z-index: 35; display: flex; align-items: center; gap: 8px; padding: 12px 10px; border: 1px solid var(--ui-border-accented); border-right: 0; border-radius: 12px 0 0 12px; color: var(--ui-primary); background: var(--ui-bg); box-shadow: 0 4px 18px #0002; cursor: pointer; font-size: 12px; font-weight: 600; transition: padding 180ms ease, background 180ms ease; }
.theme-studio-trigger:hover { padding-right: 16px; background: var(--ui-bg-elevated); }
.theme-studio-trigger[aria-expanded='true'] { visibility: hidden; }
section label, .studio-label { display: block; font-size: 12px; font-weight: 600; margin-bottom: 9px; color: var(--ui-text-highlighted); }
select { width: 100%; min-width: 0; border: 1px solid var(--ui-border-accented); border-radius: 7px; padding: 9px 8px; background: var(--ui-bg); color: var(--ui-text); font-family: 'Public Sans', sans-serif; font-size: 12px; }
.studio-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px 12px; }
.studio-toolbar, .studio-modes { display: flex; gap: 4px; padding: 4px; background: var(--ui-bg-elevated); border: 1px solid var(--ui-border); border-radius: 8px; }
.studio-toolbar button, .studio-modes button { flex: 1; display: inline-flex; align-items: center; justify-content: center; gap: 5px; white-space: nowrap; padding: 7px 4px; font-size: 11px; border-radius: 5px; cursor: pointer; }
.studio-icon { width: 15px; height: 15px; flex: none; }
.studio-icon.is-playing[data-motion='undo'] { animation: studio-undo 620ms ease-out both; }
.studio-icon.is-playing[data-motion='redo'] { animation: studio-redo 620ms ease-out both; }
.studio-icon.is-playing[data-motion='randomize'] { animation: studio-random 680ms ease-out both; }
.studio-icon.is-playing[data-motion='light'] { animation: studio-sun 680ms ease-out both; }
.studio-icon.is-playing[data-motion='dark'] { animation: studio-moon 600ms ease-out both; }
.studio-icon.is-playing[data-motion='system'] { animation: studio-system 600ms ease-out both; }
.studio-icon.is-playing[data-motion='restore'] { animation: studio-restore 680ms ease-out both; }
@keyframes studio-undo { 40% { transform: rotate(-25deg) translateX(-2px); } 72% { transform: rotate(7deg); } }
@keyframes studio-redo { 40% { transform: rotate(25deg) translateX(2px); } 72% { transform: rotate(-7deg); } }
@keyframes studio-random { 30% { transform: rotate(-17deg) scale(1.12); } 65% { transform: rotate(17deg) scale(1.08); } }
@keyframes studio-sun { 60% { transform: rotate(90deg) scale(1.12); } }
@keyframes studio-moon { 38% { transform: rotate(-18deg); } 68% { transform: rotate(9deg); } }
@keyframes studio-system { 42% { transform: scale(1.14); } 70% { transform: scale(.95); } }
@keyframes studio-restore { 65% { transform: rotate(-300deg); } 100% { transform: rotate(-360deg); } }
.studio-toolbar button:hover:not(:disabled), .studio-modes button:hover { background: var(--ui-bg-accented); }
.studio-modes button[aria-pressed='true'] { background: var(--ui-bg); color: var(--ui-primary); box-shadow: 0 1px 3px #0002; }
.studio-swatches { display: grid; grid-template-columns: repeat(9, 1fr); gap: 4px; }
.studio-swatches button { position: relative; height: 28px; border: 2px solid transparent; border-radius: 50%; display: grid; place-items: center; cursor: pointer; }
.studio-swatches button > span { height: 20px; width: 20px; border-radius: 50%; }
.studio-swatches button > svg { position: absolute; filter: drop-shadow(0 1px 2px #0008); }
.studio-swatches button[aria-pressed='true'] { border-color: var(--ui-primary); }
.studio-note { font-size: 10px; line-height: 1.6; color: var(--ui-text-muted); margin-top: 7px; }
.studio-label.flex { display: flex; }
.studio-preview { font-family: var(--font-sans); border: 1px solid var(--ui-border); background: var(--ui-bg-elevated); padding: 14px; border-radius: 10px; }
.studio-restore { width: 100%; display: inline-flex; align-items: center; justify-content: center; gap: 7px; border: 1px solid var(--ui-border-accented); border-radius: 8px; padding: 10px; font-size: 12px; font-weight: 600; cursor: pointer; color: var(--ui-text-highlighted); }
.studio-restore:hover:not(:disabled) { border-color: var(--ui-primary); color: var(--ui-primary); }
button:disabled { opacity: .4; cursor: default; }
button:focus-visible, select:focus-visible { outline: 2px solid var(--ui-primary); outline-offset: 3px; }
@media (prefers-reduced-motion: reduce) { .theme-studio-trigger { transition: none; } .studio-icon.is-playing[data-motion] { animation: none; } }
@media print { .theme-studio-trigger { display: none !important; } }
</style>
<style>
/* Only this opt-in panel; do not disable animations on existing app dialogs. */
.theme-studio-panel { font-family: 'Public Sans', sans-serif; }
@media (prefers-reduced-motion: reduce) { .theme-studio-panel { animation-duration: 0s !important; transition-duration: 0s !important; } }
@media print { .theme-studio-panel { display: none !important; } }
</style>
