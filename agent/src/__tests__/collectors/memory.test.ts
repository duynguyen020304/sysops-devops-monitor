import { describe, expect, it, vi } from 'vitest'

vi.mock('node:os', () => ({ default: { totalmem: () => 1000, freemem: () => 250 } }))
vi.mock('node:fs/promises', () => ({ readFile: vi.fn(async () => 'SwapTotal: 10 kB\nSwapFree: 4 kB\n') }))

describe('collectMemory', () => {
  it('returns memory metrics', async () => {
    const { collectMemory } = await import('../../collectors/memory.js')
    const m = await collectMemory()
    expect(m.totalBytes).toBe(1000)
    expect(m.usedBytes).toBe(750)
    expect(m.usagePercent).toBe(75)
    expect(m.swapUsedBytes).toBe(6144)
  })
})
