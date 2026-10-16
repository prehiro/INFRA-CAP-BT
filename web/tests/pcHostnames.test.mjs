import test from 'node:test'
import assert from 'node:assert/strict'
import { nextPcHostname, loadNextPcHostname } from '../app/utils/pcHostnames.ts'

test('requested example: NB123 + NB125 => NB126, never NB124', () => {
  assert.equal(nextPcHostname('Notebook', ['BAL123NB29384', 'BAL125NB29384']), 'BAL126NB29384')
})
test('sequence is numeric, independent for PC/NB/TB, regardless of record order', () => {
  const hosts = ['BAL716TB29384', 'BAL076PC29384', 'BAL123NB29384', 'BAL287PC29384', 'BAL139NB29384']
  assert.equal(nextPcHostname('Desktop', hosts), 'BAL288PC29384')
  assert.equal(nextPcHostname('Notebook', hosts), 'BAL140NB29384')
  assert.equal(nextPcHostname('Tablet', hosts), 'BAL717TB29384')
})
test('empty sequence starts at 001 and all returned hostnames are uppercase', () => {
  assert.equal(nextPcHostname('Desktop', []), 'BAL001PC29384')
  assert.equal(nextPcHostname('notebook', [' bal009nb29384 ']), 'BAL010NB29384')
})
test('unrelated, blank and malformed hostnames do not enter the sequence', () => {
  const hosts = ['', null, 'BAL1111', 'BALFI035M29384', 'XBAL999NB29384', 'BAL999NB293840', 'BALXYZNB29384']
  assert.equal(nextPcHostname('Notebook', hosts), 'BAL001NB29384')
})
test('999 advances without truncation or wrapping, and arbitrary numeric precision is safe', () => {
  assert.equal(nextPcHostname('Tablet', ['BAL999TB29384']), 'BAL1000TB29384')
  assert.equal(nextPcHostname('Notebook', ['BAL9007199254740993NB29384']), 'BAL9007199254740994NB29384')
})
test('an unsupported chassis never invents a hostname or fetches pages', async () => {
  assert.equal(nextPcHostname('Laptop', []), null)
  let calls = 0
  assert.equal(await loadNextPcHostname('Laptop', async () => { calls++; return { items: [], total: 0 } }), null)
  assert.equal(calls, 0)
})
test('highest hostname after the 500-row page cap is included', async () => {
  const records = Array.from({ length: 500 }, (_, i) => ({ id: i + 1, values: { japan_hostname: 'BAL123NB29384' } }))
  const calls = []
  const hostname = await loadNextPcHostname('Notebook', async page => {
    calls.push(page)
    return page === 1 ? { items: records, total: 501 } : { items: [{ id: 501, values: { japan_hostname: 'BAL900NB29384' } }], total: 501 }
  })
  assert.equal(hostname, 'BAL901NB29384')
  assert.deepEqual(calls, [1, 2])
})
test('an empty complete API result starts at 001', async () => {
  assert.equal(await loadNextPcHostname('Tablet', async () => ({ items: [], total: 0 })), 'BAL001TB29384')
})
test('pagination duplicates are deduplicated by id before completeness is accepted', async () => {
  const calls = []
  const hostname = await loadNextPcHostname('Notebook', async page => {
    calls.push(page)
    if (page === 1) return { items: [{ id: 1, values: { japan_hostname: 'BAL123NB29384' } }], total: 2 }
    if (page === 2) return { items: [{ id: 1, values: { japan_hostname: 'BAL123NB29384' } }, { id: 2, values: { japan_hostname: 'BAL125NB29384' } }], total: 2 }
    throw new Error('Unexpected page')
  })
  assert.equal(hostname, 'BAL126NB29384')
  assert.deepEqual(calls, [1, 2])
})
test('incomplete, repeating or failed page reads reject rather than guess a hostname', async () => {
  await assert.rejects(loadNextPcHostname('Notebook', async () => ({ items: [], total: 5 })), /completely/)
  await assert.rejects(loadNextPcHostname('Notebook', async () => ({ items: [{ id: 1, values: {} }], total: 5 })), /completely/)
  await assert.rejects(loadNextPcHostname('Notebook', async () => { throw new Error('API unavailable') }), /API unavailable/)
  await assert.rejects(loadNextPcHostname('Notebook', async () => ({ items: [], total: -1 })), /Invalid/)
})
