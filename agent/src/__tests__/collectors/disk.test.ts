import { describe, expect, it, vi } from 'vitest'

vi.mock('node:child_process', () => ({ exec: vi.fn((cmd, cb) => cb(null, { stdout: 'Filesystem 1B-blocks Used Available Use% Mounted on\n/dev/sda1 1000 400 600 40% /\n' })) }))
vi.mock('node:fs/promises', () => ({ readFile: vi.fn(async () => '8 0 sda 0 0 10 0 0 0 20 0 0 0 0 0\n') }))

describe('collectDisk', () => {
  it('returns disk metrics', async () => {
    const { collectDisk } = await import('../../collectors/disk.js')
    const d = await collectDisk()
    expect(d.totalBytes).toBe(1000)
    expect(d.usedBytes).toBe(400)
    expect(d.mountPoints[0].mount).toBe('/')
  })
})
