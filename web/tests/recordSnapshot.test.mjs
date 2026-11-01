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
  const calls={updates:0,converts:0,ledgerConverts:0,creates:0,loads:0,toasts:[]}
  const ctx={
    entityId:ref(10),entities:ref([{id:10,slug:'pc_ledger'}]),editingId:ref(1),editTransferStatus:ref(''),editAssetStatus:ref(''),originalAssetStatus:ref(''),hostnamePending:ref(false),formError:ref(''),saving:ref(false),loading:ref(false),showForm:ref(true),rows:ref(rows),form:{status:'Active',sn:'SERIAL-A'},originalSn:ref('SERIAL-A'),
    UNIQUE_FIELDS:['japan_hostname','computer_sn'],uniqueWarned:ref(''),checkUnique:async()=>{},normalizeDeviceIdentity:v=>String(v??'').trim().toUpperCase(),shouldValidateDeviceIdentity:()=>false,findDuplicateDeviceIdentity:async()=>undefined,apiListRecords:async()=>{throw Error('Unexpected identity lookup')},STATUS_OPTIONS:[{value:'Active'},{value:'Inactive'}],isTransferStatus:s=>['Standby','Plan Dispose','Domain PC'].includes(s),cell:(r,k)=>String(r.values[k]??''),factoryPcConflictLabel:()=>'',pcLedgerConflictLabel:()=>'',
    payload:()=>({staff_name:'Typed owner',pic:'Typed owner',status:'Active'}),replaceSavedRecord,
    apiUpdateRecord:async()=>{calls.updates++;return saved},apiConvertFactoryRecord:async()=>{calls.converts++;return {id:99}},apiConvertLedgerRecord:async()=>{calls.ledgerConverts++;return {id:99}},apiCreateRecord:async()=>{calls.creates++},load:async()=>{calls.loads++},toast:{add:toast=>calls.toasts.push(toast)}
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
  if (page === 'pcledger') {
    test('pcledger: duplicate serial on New warns visibly without creating a record',async()=>{
      const {ctx,calls,initial}=context()
      ctx.editingId.value=null
      ctx.uniqueWarned.value='Computer S/N "ABC" is already used by No 7 (Status: Standby).'
      await save(ctx)()
      assert.equal(calls.creates,0);assert.equal(calls.updates,0);assert.equal(calls.loads,0)
      assert.equal(ctx.rows.value,initial);assert.equal(ctx.showForm.value,true)
      assert.match(ctx.formError.value,/Status: Standby/)
      assert.equal(calls.toasts.length,1);assert.equal(calls.toasts[0].color,'error')
      assert.match(calls.toasts[0].description,/Status: Standby/)
    })
    for (const [status, title] of [['Standby','Moved to Standby PC'],['Plan Dispose','Moved to Dispose'],['','Moved to PC Ledger']]) {
      test(`pcledger: ${status || 'clear'} updates the original row without conversion`,async()=>{
        const {ctx,calls,initial}=context()
        ctx.editAssetStatus.value=status
        ctx.originalAssetStatus.value=status ? '' : 'Standby'
        const moved={...initial[0],values:{...initial[0].values,asset_status:status}}
        ctx.apiUpdateRecord=async()=>{calls.updates++;return moved}
        await save(ctx)()
        assert.equal(calls.updates,1);assert.equal(calls.converts,0);assert.equal(ctx.rows.value[0],moved)
        assert.equal(ctx.rows.value[1],initial[1]);assert.equal(calls.toasts[0].title,title)
      })
    }
    test('pcledger: factory conversion removes only the original row after server acknowledgment',async()=>{
      const {ctx,calls,initial}=context();let release
      ctx.editAssetStatus.value='Convert Factory PC'
      ctx.payload=()=>({asset_status:'Convert Factory PC',staff_name:'Typed owner'})
      ctx.apiConvertFactoryRecord=()=>new Promise(resolve=>{release=resolve})
      const work=save(ctx)();for(let i=0;i<10&&!release;i++)await Promise.resolve()
      assert.ok(release);assert.equal(ctx.rows.value,initial);assert.equal(ctx.showForm.value,true)
      release({id:99,values:{status:'Active'}});await work
      assert.deepEqual(ctx.rows.value,[initial[1]]);assert.equal(calls.updates,0);assert.equal(calls.loads,0)
      assert.equal(ctx.showForm.value,false);assert.equal(calls.toasts[0].title,'Moved to Factory PC')
    })
    test('pcledger: failed factory conversion retains the form and original snapshot',async()=>{
      const {ctx,calls,initial}=context()
      ctx.editAssetStatus.value='Convert Factory PC'
      ctx.apiConvertFactoryRecord=async()=>{throw {data:{errors:{remarks:'Too long'}}}}
      await save(ctx)()
      assert.equal(ctx.rows.value,initial);assert.equal(ctx.showForm.value,true)
      assert.equal(ctx.formError.value,'Too long');assert.equal(calls.toasts.length,0)
    })
  }
  if (page === 'factorypc') {
    test('factorypc: Inactive stays in Factory PC and uses the ordinary Edit endpoint',async()=>{
      const {ctx,calls}=context();ctx.form.status='Inactive'
      await save(ctx)()
      assert.equal(calls.updates,1);assert.equal(calls.ledgerConverts,0)
      assert.equal(calls.toasts[0].title,'Record updated')
    })
    for (const [status,title] of [['Standby','Moved to Standby PC'],['Plan Dispose','Moved to Dispose'],['Domain PC','Moved to PC Ledger']]) {
      test(`factorypc: ${status} transfers after acknowledgment, then removes only the source row`,async()=>{
        const {ctx,calls,initial}=context();let release
        ctx.entityId.value=11;ctx.editTransferStatus.value=status
        ctx.payload=()=>({status:'Active',pic:'Edited owner',sn:'SERIAL-A'})
        ctx.apiConvertLedgerRecord=(_entity,_id,values)=>{
          assert.equal(_entity,11);assert.equal(_id,1);assert.equal(values.status,status)
          return new Promise(resolve=>{release=resolve})
        }
        const work=save(ctx)();for(let i=0;i<12&&!release;i++)await Promise.resolve()
        assert.ok(release);assert.equal(ctx.rows.value,initial);assert.equal(ctx.showForm.value,true)
        release({id:99,values:{asset_status:status}});await work
        assert.equal(calls.updates,0);assert.equal(calls.loads,0)
        assert.deepEqual(ctx.rows.value,[initial[1]])
        assert.equal(ctx.showForm.value,false);assert.equal(calls.toasts[0].title,title)
      })
    }
    test('factorypc: failed transfer leaves the original row and dialog intact',async()=>{
      const {ctx,calls,initial}=context();ctx.editTransferStatus.value='Domain PC'
      ctx.apiConvertLedgerRecord=async()=>{throw {data:{errors:{remarks:'Remarks too long'}}}}
      await save(ctx)()
      assert.equal(ctx.rows.value,initial);assert.equal(ctx.showForm.value,true)
      assert.equal(ctx.formError.value,'Remarks too long');assert.equal(calls.toasts.length,0)
    })
    test('factorypc: transfer checks serials in PC Ledger before calling the API',async()=>{
      const {ctx,calls,initial}=context();ctx.editTransferStatus.value='Domain PC'
      ctx.findDuplicateDeviceIdentity=async key=>key==='computer_sn'?{id:55,values:{nomor:'8',asset_status:'Standby'}}:undefined
      ctx.pcLedgerConflictLabel=()=> 'No 8 (Standby PC; Status: Standby)'
      await save(ctx)()
      assert.equal(calls.ledgerConverts,0);assert.equal(calls.updates,0)
      assert.equal(ctx.rows.value,initial);assert.equal(ctx.showForm.value,true)
      assert.match(ctx.formError.value,/Standby PC; Status: Standby/)
      assert.equal(calls.toasts[0].color,'error')
    })
  }
}
test('snapshot replacement does not append unknown IDs or mutate other rows',()=>{
  const rows=original();assert.equal(replaceSavedRecord(rows,{id:99,values:{}}),rows)
  const saved={...rows[1],values:{...rows[1].values,sn:'NEW'}};const next=replaceSavedRecord(rows,saved)
  assert.equal(next[0],rows[0]);assert.equal(next[1],saved);assert.equal(rows[1].values.sn,'SERIAL-B')
})
