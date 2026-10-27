import { bundledFonts, extraAccents, extraNeutrals, fontKey, referenceCatalog, referenceIconSets } from './themeReference.ts'

// Offline theme choices. No remote fonts or icon providers are needed.
export const accents = ['green', 'red', 'orange', 'amber', 'yellow', 'lime', 'emerald', 'teal', 'cyan', 'sky', 'blue', 'indigo', 'violet', 'purple', 'fuchsia', 'pink', 'rose', ...extraAccents]
export const neutrals = ['slate', 'gray', 'zinc', 'neutral', 'stone', 'taupe', 'mauve', 'mist', 'olive', ...extraNeutrals]
export const fonts = {
  ...Object.fromEntries(bundledFonts.map(f => [f.id, f.name])),
  original: 'Original Font', system: 'System Sans', serif: 'System Serif', mono: 'System Mono'
}
/** Randomize across the same offline font choices shown in Theme Studio, excluding
 * the active font so each click actually changes typography. */
export function pickDifferentFont(currentFont: string, random: () => number = Math.random): string {
  const choices = Object.keys(fonts).filter(font => font !== currentFont)
  return choices[Math.floor(random() * choices.length)]!
}
export const fontStacks: Record<string, string> = {
  ...Object.fromEntries(bundledFonts.map(f => [f.id, `"Studio ${f.name}", ${f.category === 'Mono' ? 'monospace' : f.category === 'Serif' ? 'serif' : 'sans-serif'}`])),
  system: 'system-ui, sans-serif', serif: 'Georgia, "Times New Roman", serif', mono: 'Consolas, "Courier New", monospace'
}
export interface StudioTheme {
  primary: string; neutral: string; mode: string; font: string; icons: string
  radius: string; size: string; variant: string; reference?: string
}
export const extraDefaults = { reference: '', font: 'original', icons: 'original', radius: 'original', size: 'original', variant: 'original' }
export function validTheme(value: unknown): value is StudioTheme {
  if (!value || typeof value !== 'object') return false
  const x = value as StudioTheme
  return accents.includes(x.primary) && neutrals.includes(x.neutral)
    && ['light', 'dark', 'system'].includes(x.mode) && Object.keys(fonts).includes(x.font)
    && ['original', 'carbon', ...Object.keys(referenceIconSets)].includes(x.icons)
    && (x.reference === undefined || x.reference === '' || referenceCatalog.presets.some(p => p.id === x.reference))
    && ['original', '0', '0.125', '0.25', '0.375', '0.5', '0.625', '0.75'].includes(x.radius)
    && ['original', 'xs', 'sm', 'md', 'lg', 'xl'].includes(x.size)
    && ['original', 'solid', 'outline', 'soft', 'subtle'].includes(x.variant)
}
export const presets: Record<string, StudioTheme> = {
  ...Object.fromEntries(referenceCatalog.presets.map(p => {
    const d = p.doc as any
    return [p.name, { reference: p.id, primary: d.blackAsPrimary ? 'black' : d.colors?.primary || 'green', neutral: d.colors?.neutral || 'slate', mode: 'dark', font: fontKey(d.font?.sans || 'Public Sans'), icons: d.icons || 'original', radius: String(d.radius ?? 0.25), size: d.style?.defaults?.size || 'md', variant: d.style?.defaults?.variants?.buttons || 'solid' }]
  })),
  'Studio': { reference: '', primary: 'violet', neutral: 'zinc', mode: 'dark', font: 'original', icons: 'original', radius: '0.375', size: 'md', variant: 'soft' },
  'Ocean': { reference: '', primary: 'sky', neutral: 'slate', mode: 'light', font: 'system', icons: 'carbon', radius: '0.5', size: 'md', variant: 'outline' },
  'Graphite': { reference: '', primary: 'green', neutral: 'neutral', mode: 'dark', font: 'mono', icons: 'carbon', radius: '0.125', size: 'sm', variant: 'solid' },
  'Warm Paper': { reference: '', primary: 'amber', neutral: 'stone', mode: 'light', font: 'serif', icons: 'original', radius: '0.25', size: 'md', variant: 'subtle' }
}
// Only Nuxt UI's generic control icons change; branded device/header icons stay intact.
export const carbonIcons = {
  arrowDown: 'i-carbon-arrow-down', arrowLeft: 'i-carbon-arrow-left', arrowRight: 'i-carbon-arrow-right', arrowUp: 'i-carbon-arrow-up',
  check: 'i-carbon-checkmark', chevronDoubleLeft: 'i-carbon-chevron-left', chevronDoubleRight: 'i-carbon-chevron-right',
  chevronDown: 'i-carbon-chevron-down', chevronLeft: 'i-carbon-chevron-left', chevronRight: 'i-carbon-chevron-right', chevronUp: 'i-carbon-chevron-up',
  close: 'i-carbon-close', copy: 'i-carbon-copy', ellipsis: 'i-carbon-overflow-menu-horizontal',
  external: 'i-carbon-launch', eye: 'i-carbon-view', eyeOff: 'i-carbon-view-off', file: 'i-carbon-document',
  folder: 'i-carbon-folder', folderOpen: 'i-carbon-folder-open', loading: 'i-carbon-renew', minus: 'i-carbon-subtract', plus: 'i-carbon-add',
  search: 'i-carbon-search', upload: 'i-carbon-upload', download: 'i-carbon-download'
}
