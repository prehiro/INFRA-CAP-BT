import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { stripTypeScriptTypes } from 'node:module'
import vm from 'node:vm'
import { validTheme, presets, extraDefaults, carbonIcons, fontStacks } from '../app/utils/themeStudio.ts'
import { referenceComponents, referenceDocs, referenceIconSets, referenceComponentOverride, referenceCss, referenceCatalog } from '../app/utils/themeReference.ts'

const KEY = 'infra-cap.theme-studio.v1', BASE = `${KEY}.original`
const source = stripTypeScriptTypes(readFileSync(new URL('../app/composables/useThemeStudio.ts', import.meta.url), 'utf8').replace(/^import .*\n/gm, '').replace('export function', 'function').replaceAll('import.meta.client', 'true'))
function harness(storage = new Map(), cookie = '') {
  const ui = { colors: {primary:'violet',neutral:'stone'}, icons:{search:'i-lucide-search',custom:'preserved'}, button:{slots:{base:'original'},defaultVariants:{size:'lg'}}, input:{slots:{base:'keep'}} }
  const originalUi = structuredClone(ui)
  const styles = new Map([['--font-sans', 'Original Font']]), state = new Map()
  const mode = {preference:'system'}
  let currentCookie = cookie, blocked = false
  const document = { documentElement:{style:{getPropertyValue:k=>styles.get(k)||'',getPropertyPriority:()=>'',setProperty:(k,v)=>styles.set(k,v),removeProperty:k=>styles.delete(k)}} }
  Object.defineProperty(document,'cookie',{get:()=>currentCookie,set:value=>{currentCookie=value.includes('max-age=0')?'':value.split(';')[0]}})
  const localStorage = {getItem:k=>storage.get(k)||null,setItem:(k,v)=>{if(blocked)throw Error('denied');storage.set(k,v)},removeItem:k=>storage.delete(k)}
  const ctx = {referenceComponents, referenceDocs, referenceIconSets, referenceComponentOverride, carbonIcons,extraDefaults,fontStacks,validTheme,document,localStorage,console,
    useAppConfig:()=>({ui}),useColorMode:()=>mode,
    useThemeChoice:()=>({setPrimary:p=>{ui.colors.primary=p;document.cookie='infra-cap.theme='+encodeURIComponent(JSON.stringify(ui.colors))},setNeutral:n=>{ui.colors.neutral=n;document.cookie='infra-cap.theme='+encodeURIComponent(JSON.stringify(ui.colors))}}),
    useState:(k,fn)=>{if(!state.has(k))state.set(k,{value:fn()});return state.get(k)},computed:fn=>({get value(){return fn()}})}
  const context = vm.createContext(ctx); vm.runInContext(source+'\nglobalThis.studio=useThemeStudio()',context)
  return {studio:context.studio,ui,originalUi,styles,storage,mode,document,block:()=>{blocked=true}}
}
const plain = x => JSON.parse(JSON.stringify(x))
for (const preset of referenceCatalog.presets) test(`reference ${preset.name}: apply, reload and exact original rollback`,()=>{
  const h=harness(undefined,'infra-cap.theme=existing');h.studio.change(presets[preset.name]);assert.equal(h.studio.current.value.reference,preset.id)
  assert.equal(h.ui.colors.primary,presets[preset.name].primary)
  const again=harness(h.storage);assert.equal(again.studio.current.value.reference,preset.id)
  again.studio.restoreOriginal();assert.deepEqual(plain(again.ui),again.originalUi);assert.equal(again.document.cookie,'infra-cap.theme=existing')
  h.studio.restoreOriginal();assert.deepEqual(plain(h.ui),h.originalUi);assert.equal(h.styles.get('--font-sans'),'Original Font')
})
test('all offline presets are valid; invalid arbitrary values are rejected',()=>{
  for(const value of Object.values(presets))assert.ok(validTheme(value))
  for(const patch of [{font:'https://remote/font'},{radius:'1000'},{size:'huge'},{icons:'remote'},{mode:'invalid'},{variant:'ghost'}])assert.equal(validTheme({...presets.Ocean,...patch}),false)
})
test('initial mounting does not mutate existing theme or create a backup',()=>{
  const h=harness();assert.deepEqual(h.ui,h.originalUi);assert.equal(h.storage.size,0);assert.equal(h.styles.get('--font-sans'),'Original Font')
})
test('first change preserves actual custom original settings, not factory defaults',()=>{
  const h=harness(undefined,'infra-cap.theme=existing');h.studio.change(presets.Ocean)
  const base=JSON.parse(h.storage.get(BASE));assert.equal(base.theme.primary,'violet');assert.equal(base.theme.neutral,'stone');assert.equal(base.theme.mode,'system');assert.equal(base.cookie,'existing')
  const raw=h.storage.get(BASE);h.studio.change(presets.Graphite);assert.equal(h.storage.get(BASE),raw)
})
test('restore returns exact UI overrides, styles, cookie and mode; clears preview storage',()=>{
  const h=harness(undefined,'infra-cap.theme=existing');h.studio.change(presets.Ocean);h.studio.change(presets.Graphite);h.studio.restoreOriginal()
  assert.deepEqual(plain(h.ui),h.originalUi);assert.equal(h.document.cookie,'infra-cap.theme=existing');assert.equal(h.mode.preference,'system');assert.equal(h.styles.get('--font-sans'),'Original Font');assert.equal(h.styles.has('--ui-radius'),false);assert.equal(h.storage.size,0)
})
test('restore removes theme cookie if originally absent',()=>{
  const h=harness();h.studio.change(presets.Ocean);h.studio.restoreOriginal();assert.equal(h.document.cookie,'')
})
test('undo/redo changes current theme but never replaces the recovery snapshot',()=>{
  const h=harness();h.studio.change(presets.Ocean);const base=h.storage.get(BASE);h.studio.change(presets.Graphite);h.studio.undo();assert.equal(h.ui.colors.primary,'sky');h.studio.redo();assert.equal(h.ui.colors.neutral,'neutral');assert.equal(h.storage.get(BASE),base)
})
test('new change after undo clears redo history',()=>{
  const h=harness();h.studio.change(presets.Ocean);h.studio.undo();h.studio.change(presets.Graphite);assert.equal(h.studio.redoStack.value.length,0)
})
test('storage refusal blocks changes before mutating live config',()=>{
  const h=harness();h.block();h.studio.change(presets.Ocean);assert.deepEqual(h.ui,h.originalUi);assert.ok(h.studio.error.value);assert.equal(h.studio.original.value,null)
})
test('reload restores extras and preserves the original snapshot',()=>{
  const a=harness(undefined,'infra-cap.theme=existing');a.studio.change(presets.Graphite);const base=a.storage.get(BASE)
  const b=harness(a.storage);assert.equal(b.styles.get('--font-sans'),fontStacks.mono);assert.equal(b.ui.icons.search,'i-carbon-search');assert.equal(b.storage.get(BASE),base);b.studio.restoreOriginal();assert.equal(b.document.cookie,'infra-cap.theme=existing');assert.equal(b.mode.preference,'system')
})
test('preview without recovery snapshot is not applied; corrupt storage is harmless',()=>{
  const a=harness(new Map([[KEY,JSON.stringify(presets.Ocean)]]));assert.deepEqual(a.ui,a.originalUi)
  const b=harness(new Map([[BASE,'broken json']]));assert.deepEqual(b.ui,b.originalUi);assert.ok(b.studio.error.value)
})
test('Carbon generic icons are all installed locally',()=>{
  const collection=JSON.parse(readFileSync(new URL('../node_modules/@iconify-json/carbon/icons.json',import.meta.url),'utf8'))
  for(const icon of Object.values(carbonIcons))assert.ok(collection.icons[icon.replace('i-carbon-','')]||collection.aliases?.[icon.replace('i-carbon-','')],icon)
})
