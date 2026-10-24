import test from 'node:test'
import assert from 'node:assert/strict'
import { normalizeDeviceIdentity, shouldValidateDeviceIdentity, findDuplicateDeviceIdentity } from '../app/utils/deviceIdentity.ts'

const row=(id,key,value)=>({id,values:{[key]:value}})
for (const key of ['japan_hostname','computer_sn','sn']) {
  test(`${key}: user-only edit skips unchanged and case/space-equivalent identities`,()=>{
    assert.equal(shouldValidateDeviceIdentity('  serial-a ', 'SERIAL-A', true),false)
    assert.equal(shouldValidateDeviceIdentity('SERIAL-A', 'SERIAL-B', true),true)
    assert.equal(shouldValidateDeviceIdentity('SERIAL-A', 'SERIAL-A', false),true)
    assert.equal(shouldValidateDeviceIdentity('', 'SERIAL-A', true),false)
  })
  test(`${key}: detects another record, not the current ID`,async()=>{
    const duplicate=await findDuplicateDeviceIdentity(key,' SERIAL-A ',7,[row(7,key,'serial-a'),row(8,key,'serial-a')],async()=>{throw Error('Unexpected request')})
    assert.equal(duplicate.id,8)
    const self=await findDuplicateDeviceIdentity(key,'SERIAL-A',7,[],async()=>({total:1,items:[row('7',key,'serial-a')]}))
    assert.equal(self,undefined)
  })
  test(`${key}: finds exact normalized collision beyond page500 without substring search`,async()=>{
    const calls=[]
    const duplicate=await findDuplicateDeviceIdentity(key,'TARGET',null,[],async page=>{
      calls.push(page)
      return page===1?{total:501,items:Array.from({length:500},(_,i)=>row(i+1,key,'TARGET-SUFFIX-'+i))}:{total:501,items:[row(501,key,' target ')]}
    })
    assert.equal(duplicate.id,501);assert.deepEqual(calls,[1,2])
  })
}
test('snapshot normalization handles nulls without treating blanks as device identities',()=>{
  assert.equal(normalizeDeviceIdentity(null),'')
  assert.equal(normalizeDeviceIdentity(' nb123 '),'NB123')
})
test('failed/incomplete checks do not report a fabricated successful validation',async()=>{
  await assert.rejects(findDuplicateDeviceIdentity('sn','X',null,[],async()=>{throw Error('offline')}),/offline/)
  await assert.rejects(findDuplicateDeviceIdentity('sn','X',null,[],async()=>({total:2,items:[]})),/Incomplete/)
})
