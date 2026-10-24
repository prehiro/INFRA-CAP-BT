import test from 'node:test'
import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { replaceSavedRecord } from '../app/utils/recordSnapshot.ts'

// Explicit unit fixtures: these tests do not claim to persist any API/database record.
const original = () => [
  { id:1, values:{nomor:'1',staff_name:'Old owner',pic:'Old owner',sn:'SERIAL-A'}, updatedAt:null },
  { id:2, values:{nomor:'2',staff_name:'Other owner',pic:'Other owner',sn:'SERIAL-B'}, updatedAt:null }
]
const ref = value => ({value})
function context() {
  const rows=original(), saved={...rows[0],values:{...rows[0].values,staff_name:'Canonical owner',pic:'Canonical owner'},updatedAt:'2026-01-01T00:00:00Z'}
  const calls={updates:0,creates:0,loads:0,toasts:[]}
  const ctx={
    entityId:ref(10),editingId:ref(1),hostnamePending:ref(false),formError:ref(''),saving:ref(false),loading:ref(false),showForm:ref(true),rows:ref(rows),form:{status:'Active',sn:'SERIAL-A'},originalSn:ref('SERIAL-A'),
    UNIQUE_FIELDS:['japan_hostname','computer_sn'],uniqueWarned:ref(''),checkUnique:async()=>{},normalizeDeviceIdentity:v=>String(v??'').trim().toUpperCase(),shouldValidateDeviceIdentity:()=>false,findDuplicateDeviceIdentity:async()=>undefined,apiListRecords:async()=>{throw Error('Unexpected identity lookup')},STATUS_OPTIONS:[{value:'Active'},{value:'Inactive'}],cell:(r,k)=>String(r.values[k]??''),
    payload:()=>({staff_name:'Typed owner',pic:'Typed owner'}),replaceSavedRecord,
    apiUpdateRecord:async()=>{calls.updates++;return saved},apiCreateRecord:async()=>{calls.creates++},load:async()=>{calls.loads++},toast:{add:toast=>calls.toasts.push(toast)}
  }
  return {ctx,calls,initial:rows,saved}
}
for (const page of ['pcledger','factorypc']) {
  const source=await readFile(new URL(`../app/pages/${page}.vue`,import.meta.url),'utf8')
  const start=source.indexOf('async function save() {'), end=source.indexOf('/* ---------------- delete',start)
  const handler=source.slice(start,end).replaceAll('catch (e: any)','catch (e)').replaceAll('entityId.value!','entityId.value')
  function save(ctx) { return new Function(...Object.keys(ctx),handler+'\nreturn save')(...Object.values(ctx)) }
  test(`${page}: successful Edit applies only canonical response row, without load or table spinner`,async()=>{
    const {ctx,calls,initial,saved}=context();await save(ctx)()
    assert.equal(calls.updates,1);assert.equal(calls.loads,0);assert.equal(calls.creates,0)
    assert.notEqual(ctx.rows.value,initial);assert.equal(ctx.rows.value[0],saved);assert.equal(ctx.rows.value[1],initial[1]);assert.equal(ctx.rows.value.length,initial.length)
    assert.equal(initial[0].values.staff_name,'Old owner');assert.equal(ctx.rows.value[0].updatedAt,saved.updatedAt)
    assert.equal(ctx.loading.value,false);assert.equal(ctx.showForm.value,false);assert.equal(ctx.saving.value,false);assert.equal(calls.toasts[0].title,'Record updated')
  })
  test(`${page}: pending Edit keeps modal/table/cache until server acknowledgment`,async()=>{
    const {ctx,calls,initial,saved}=context();let release
    ctx.apiUpdateRecord=()=>new Promise(resolve=>{release=resolve})
    const work=save(ctx)();for(let i=0;i<10&&!release;i++)await Promise.resolve()
    assert.ok(release);assert.equal(ctx.rows.value,initial);assert.equal(ctx.loading.value,false);assert.equal(ctx.showForm.value,true);assert.equal(ctx.saving.value,true)
    release(saved);await work;assert.equal(ctx.rows.value[0],saved);assert.equal(calls.loads,0)
  })
  test(`${page}: failed Edit retains form/cache and does not announce success`,async()=>{
    const {ctx,calls,initial}=context();ctx.apiUpdateRecord=async()=>{throw {data:{errors:{sn:'Serial already used'}}}}
    await save(ctx)();assert.equal(ctx.rows.value,initial);assert.equal(ctx.showForm.value,true);assert.equal(ctx.saving.value,false);assert.equal(ctx.loading.value,false)
    assert.equal(ctx.formError.value,'Serial already used');assert.equal(calls.loads,0);assert.equal(calls.toasts.length,0)
  })
  test(`${page}: New keeps the original create-and-reload path`,async()=>{
    const {ctx,calls}=context();ctx.editingId.value=null;await save(ctx)()
    assert.equal(calls.creates,1);assert.equal(calls.updates,0);assert.equal(calls.loads,1);assert.equal(calls.toasts[0].title,'Record created')
  })
}
test('snapshot replacement does not append unknown IDs or mutate other rows',()=>{
  const rows=original();assert.equal(replaceSavedRecord(rows,{id:99,values:{}}),rows)
  const saved={...rows[1],values:{...rows[1].values,sn:'NEW'}};const next=replaceSavedRecord(rows,saved)
  assert.equal(next[0],rows[0]);assert.equal(next[1],saved);assert.equal(rows[1].values.sn,'SERIAL-B')
})
