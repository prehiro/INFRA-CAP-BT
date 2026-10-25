import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createHash } from 'node:crypto'
import { stripTypeScriptTypes } from 'node:module'
import vm from 'node:vm'
import { referenceCatalog, referenceDocs, referenceCss, referenceComponentOverride, referenceIconSets, extraAccents, extraNeutrals } from '../app/utils/themeReference.ts'
import { presets, validTheme, fonts } from '../app/utils/themeStudio.ts'

const manifest = JSON.parse(readFileSync(new URL('../public/fonts/studio/manifest.json', import.meta.url),'utf8'))
test('reference catalog has all 12 named presets and all 24 curated font families',()=>{
  assert.deepEqual(referenceCatalog.presets.map(p=>p.name),['Default','Mono','Cobalt','Sky','Mint','Iris','Crimson','Coral','Sunset','Carbon','Bubblegum','Parchment'])
  assert.equal(referenceCatalog.fonts.length,24);assert.equal(manifest.fonts.length,24)
  for(const p of referenceCatalog.presets){assert.ok(validTheme(presets[p.name]));assert.equal(presets[p.name].reference,p.id)}
  for(const f of referenceCatalog.fonts)assert.equal(fonts[f.id],f.name)
})
test('all bundled fonts have valid WOFF2 bytes, pinned hashes, license and regular Latin face',()=>{
  for(const font of manifest.fonts){
    assert.ok(font.version);assert.ok(readFileSync(new URL(`../public/fonts/studio/licenses/${font.id}.txt`,import.meta.url),'utf8').length>500)
    assert.ok(font.faces.some(f=>f.weight===400&&f.style==='normal'&&f.subset==='latin'))
    for(const f of font.faces){const bytes=readFileSync(new URL('../public/fonts/studio/'+f.file,import.meta.url));assert.equal(bytes.subarray(0,4).toString(),'wOF2');assert.equal(createHash('sha256').update(bytes).digest('hex'),f.sha256)}
  }
  const css=readFileSync(new URL('../public/fonts/studio/fonts.css',import.meta.url),'utf8')
  assert.doesNotMatch(css,/https?:|@import/);assert.equal((css.match(/@font-face/g)||[]).length,manifest.fonts.flatMap(f=>f.faces).length)
})
test('every reference generic icon resolves in installed offline collections',()=>{
  const collections={}
  for(const icons of Object.values(referenceIconSets))for(const value of Object.values(icons)){
    const [prefix,name]=value.split(':');assert.ok(prefix&&name)
    collections[prefix] ||= JSON.parse(readFileSync(new URL(`../node_modules/@iconify-json/${prefix}/icons.json`,import.meta.url),'utf8'))
    assert.ok(collections[prefix].icons[name]||collections[prefix].aliases?.[name],value)
  }
})
test('reference-specific colors, typography and surfaces are compiled, not only renamed presets',()=>{
  assert.match(referenceCss('mono','black'),/--ui-primary:#171717/);assert.match(referenceCss('mono','black'),/--ui-primary:#fafafa/)
  assert.match(referenceCss('cobalt'),/font-size:15px/);assert.match(referenceCss('cobalt'),/--ui-border:var\(--ui-color-neutral-400\)/)
  assert.match(referenceCss('mint'),/--font-weight-normal:500/);assert.match(referenceComponentOverride('mint','input').slots.base,/rounded-full/)
  assert.equal(referenceComponentOverride('iris','input').defaultVariants.variant,'subtle')
  assert.equal(referenceComponentOverride('coral','input').defaultVariants.color,'neutral')
  assert.match(referenceCss('parchment'),/Studio Source Serif 4/)
  assert.match(referenceComponentOverride('sunset','button').compoundVariants[0].class,/shadow-primary/)
  assert.doesNotMatch(referenceCss(''),/font-size|font-weight|--ui-bg:/)
  assert.equal(referenceDocs.cobalt.palettes.cobalt.shades['500'],'oklch(57.8% 0.228 260.025)')
})
const cookieSource=stripTypeScriptTypes(readFileSync(new URL('../app/composables/useThemeChoice.ts',import.meta.url),'utf8').replace(/^import .*\n/gm,'').replaceAll('export ','').replaceAll('import.meta.server','false'))
function preference(cookie=''){
 const config={ui:{colors:{primary:'green',neutral:'zinc'}}};let raw=cookie
 const document={};Object.defineProperty(document,'cookie',{get:()=>raw,set:v=>{raw=v.split(';')[0]}})
 const ctx=vm.createContext({extraAccents,extraNeutrals,document,useAppConfig:()=>config});vm.runInContext(cookieSource+';globalThis.api=useThemeChoice()',ctx)
 return {api:ctx.api,config,get cookie(){return raw}}
}
test('explicit Default preset green/slate survives restore; old legacy cookie still migrates',()=>{
 const a=preference();a.api.setNeutral('slate');assert.match(decodeURIComponent(a.cookie),/"version":2/)
 const b=preference(a.cookie);b.api.restore();assert.equal(b.config.ui.colors.neutral,'slate')
 const old=preference('infra-cap.theme='+encodeURIComponent(JSON.stringify({primary:'green',neutral:'slate'})));old.api.restore();assert.equal(old.config.ui.colors.neutral,'zinc')
})
test('custom reference palettes survive preference persistence',()=>{
 for(const [primary,neutral] of [['black','neutral'],['cobalt','cobalt-gray'],['clay','parchment'],['pink','saturated-mauve']]){
  const a=preference();a.api.setPrimary(primary);a.api.setNeutral(neutral);const b=preference(a.cookie);b.api.restore();assert.equal(b.config.ui.colors.primary,primary);assert.equal(b.config.ui.colors.neutral,neutral)
 }
})
