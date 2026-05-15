import { describe, expect, it, vi } from 'vitest'

let call = 0
vi.mock('node:os', () => ({ default: {
  cpus: () => {
    call++
    const idle = call <= 2 ? 100 : 150
    const user = call <= 2 ? 100 : 250
    return [{ times: { user, nice: 0, sys: 0, irq: 0, idle } }]
  },
  loadavg: () => [1.11, 2.22, 3.33],
} }))

describe('collectCpu', () => {
  it('returns cpu metrics shape', async () => {
    const { collectCpu } = await import('../../collectors/cpu.js')
    const c = await collectCpu()
    expect(c.usagePercent).toBeGreaterThanOrEqual(0)
    expect(c.usagePercent).toBeLessThanOrEqual(100)
    expect(c.perCoreUsage).toHaveLength(1)
    expect(c.loadAverage1m).toBe(1.11)
  })
})
