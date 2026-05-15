import { describe, expect, it, vi } from 'vitest'
let call = 0
vi.mock('node:fs/promises', () => ({ readFile: vi.fn(async () => {
  call++
  const rx = call === 1 ? 100 : 250
  const tx = call === 1 ? 50 : 125
  return `Inter-| Receive | Transmit\n face |bytes packets errs drop fifo frame compressed multicast|bytes packets errs drop fifo colls carrier compressed\n eth0: ${rx} 2 1 0 0 0 0 0 ${tx} 3 0 1 0 0 0 0\n lo: 999 1 0 0 0 0 0 0 999 1 0 0 0 0 0 0\n`
}) }))

describe('collectNetwork', () => {
  it('parses /proc/net/dev deltas', async () => {
    const { collectNetwork } = await import('../../collectors/network.js')
    const n = await collectNetwork()
    expect(n.rxBytesPerSec).toBe(150)
    expect(n.txBytesPerSec).toBe(75)
    expect(n.errors).toBe(1)
    expect(n.drops).toBe(1)
  })
})
