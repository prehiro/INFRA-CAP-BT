import test from 'node:test'
import assert from 'node:assert/strict'
import { registerOffsets, registerIndexAt } from '../app/utils/registerWindow.ts'

test('empty and one-row viewports have bounded indices', () => {
  assert.deepEqual(registerOffsets([], new Map(), 71), [0])
  for (const y of [-10, 0, 100]) assert.equal(registerIndexAt([0], y), 0)
  assert.equal(registerIndexAt([0, 71], -5), 0)
  assert.equal(registerIndexAt([0, 71], 70.9), 0)
  assert.equal(registerIndexAt([0, 71], 71), 1)
  assert.equal(registerIndexAt([0, 71], 1000), 1)
})
test('variable signature/wrapped rows use measured heights, unseen rows use estimates', () => {
  assert.deepEqual(registerOffsets([{id: 1}, {id: 2}, {id: 3}], new Map([[1, 461], [3, 581]]), 71), [0, 461, 532, 1113])
  assert.equal(registerIndexAt([0, 461, 532, 1113], 460), 0)
  assert.equal(registerIndexAt([0, 461, 532, 1113], 461), 1)
  assert.equal(registerIndexAt([0, 461, 532, 1113], 532), 2)
})
test('filtering and reordering retain measurements by record ID rather than old row position', () => {
  const sizes = new Map([[4, 200], [9, 80], [12, 400]])
  assert.deepEqual(registerOffsets([{id: 12}, {id: 4}], sizes, 71), [0, 400, 600])
  assert.deepEqual(registerOffsets([{id: 9}], sizes, 71), [0, 80])
})
test('fractional pixel boundaries remain precise', () => {
  const offsets = registerOffsets([{id: 1}, {id: 2}], new Map([[1, 71.5], [2, 80.25]]), 71)
  assert.deepEqual(offsets, [0, 71.5, 151.75])
  assert.equal(registerIndexAt(offsets, 71.49), 0)
  assert.equal(registerIndexAt(offsets, 71.5), 1)
})
test('500 variable-height rows agree with a linear oracle across the complete scroll extent', () => {
  const rows = Array.from({length: 500}, (_,i) => ({id: i}))
  const sizes = new Map(rows.map(r => [r.id, r.id % 7 ? 71 : 581]))
  const offsets = registerOffsets(rows, sizes, 71)
  for (let y = -1; y <= offsets.at(-1) + 100; y += 17) {
    const expected = Math.max(0, offsets.findLastIndex(value => value <= y))
    assert.equal(registerIndexAt(offsets, y), expected)
  }
})
