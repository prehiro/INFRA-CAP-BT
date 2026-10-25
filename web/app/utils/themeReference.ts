// Upstream preset documents are vendored verbatim; see THIRD-PARTY-NOTICES.md.
import catalog from '../assets/theme/reference.json' with { type: 'json' }

export const referenceCatalog = catalog
export const referenceDocs: Record<string, any> = Object.fromEntries(catalog.presets.map(p => [p.id, p.doc]))
export const normalizeStudioIcon = (name: string) => name.replace(/^i-(material-symbols|bi|heroicons|iconoir|ph|ri|tabler|carbon)[-:]/, '$1:')
export const referenceIconSets: Record<string, Record<string, string>> = Object.fromEntries(Object.entries(catalog.icons).map(([set, icons]) => [set, Object.fromEntries(Object.entries(icons).map(([key, name]) => [key, normalizeStudioIcon(name)]))]))
export const bundledFonts = catalog.fonts
export const extraAccents = ['black', 'cobalt', 'clay']
export const extraNeutrals = ['cobalt-gray', 'carbon', 'saturated-mauve', 'parchment']
export const fontKey = (name: string) => name.toLowerCase().replaceAll(' ', '-')
export const referenceComponents = ['button', 'input', 'inputMenu', 'select', 'selectMenu', 'textarea', 'badge', 'card']

// Custom ramps never overwrite Tailwind's built-in ramps.
export const paletteCss = ':root{' + catalog.presets.flatMap(p => Object.entries((p.doc as any).palettes || {}).flatMap(([name, palette]: [string, any]) =>
  Object.entries(palette.shades).map(([shade, color]) => `--color-${name}-${shade}:${color};`)
)).join('') + '--color-black-500:#171717;}'

export function referenceCss(reference = '', primary = ''): string {
  const doc = referenceDocs[reference]
  const global: string[] = []
  const modes: Record<string, Record<string, string>> = { light: {}, dark: {} }
  if (doc) {
    const font = doc.font || {}
    for (const [key, weight] of Object.entries(font.weights || {})) global.push(`--font-weight-${key}:${weight}`)
    for (const kind of ['mono', 'serif']) if (font[kind]) global.push(`--font-${kind}:'Studio ${font[kind]}',${kind === 'mono' ? 'monospace' : 'serif'}`)
    if (doc.fontSize) global.push(`font-size:${doc.fontSize}px`)
    for (const [token, shades] of Object.entries(doc.style?.tokenShades || {}) as [string, any][]) {
      const alias = token.match(/^--ui-(primary|secondary|success|info|warning|error)$/)?.[1] || 'neutral'
      for (const mode of ['light', 'dark']) if (shades[mode] !== undefined) modes[mode]![token] = `var(--ui-color-${alias}-${shades[mode]})`
    }
    for (const mode of ['light', 'dark']) Object.assign(modes[mode]!, doc.tokens?.[mode] || {})
  }
  if (primary === 'black') {
    modes.light!['--ui-primary'] = '#171717'
    modes.dark!['--ui-primary'] = '#fafafa'
  }
  const rules = global.length ? `:root{${global.join(';')}}` : ''
  const body = doc?.font ? [doc.font.lineHeight ? `line-height:${doc.font.lineHeight}` : '', doc.font.letterSpacing !== undefined ? `letter-spacing:${doc.font.letterSpacing}em` : ''].filter(Boolean).join(';') : ''
  return rules + (body ? `body{${body}}` : '') + Object.entries(modes).map(([mode, tokens]) =>
    `${mode === 'light' ? ':root:not(.dark)' : ':root.dark'}{${Object.entries(tokens).map(([k,v]) => `${k}:${v}`).join(';')}}`
  ).join('')
}

export function referenceComponentOverride(reference: string, name: string): any {
  const doc = referenceDocs[reference]
  if (!doc) return {}
  const group = ['button', 'badge'].includes(name) ? 'buttons' : name === 'card' ? 'panels' : 'inputs'
  const defaults = doc.style?.defaults || {}
  const patch = JSON.parse(JSON.stringify(doc.components?.[name] || {}))
  patch.defaultVariants = { ...(defaults.size ? { size: defaults.size } : {}),
    ...(defaults.variants?.[group] ? { variant: defaults.variants[group] } : {}),
    ...(defaults.colors?.[group] ? { color: defaults.colors[group] } : {}) }
  return patch
}
