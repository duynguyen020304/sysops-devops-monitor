import { describe, expect, it, vi } from 'vitest'

vi.mock('node:child_process', () => ({ exec: vi.fn((cmd, opts, cb) => cb(null, { stdout: JSON.stringify([{ pm_id: 1, name: 'app', pid: 123, pm2_env: { status: 'online', pm_uptime: Date.now() - 1000, restart_time: 2, exec_mode: 'fork', node_version: 'v20' }, monit: { cpu: 3, memory: 400 } }]) })) }))
vi.mock('node:fs/promises', () => ({ readFile: vi.fn(async (p: string) => p.includes('error') ? 'err1\n' : 'out1\nout2\n') }))
vi.mock('node:os', () => ({ homedir: () => '/home/test' }))

describe('pm2 collectors', () => {
  it('collects processes', async () => {
    const { collectPM2Processes } = await import('../../collectors/pm2.js')
    const p = await collectPM2Processes()
    expect(p[0].name).toBe('app')
    expect(p[0].restartCount).toBe(2)
  })

  it('collects logs', async () => {
    const { collectPM2Logs } = await import('../../collectors/pm2.js')
    const logs = await collectPM2Logs(['app'], 1)
    expect(logs).toHaveLength(2)
    expect(logs.map((l) => l.logType)).toEqual(['out', 'err'])
  })
})
