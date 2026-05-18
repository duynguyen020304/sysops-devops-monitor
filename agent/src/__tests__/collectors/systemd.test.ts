import { beforeEach, describe, expect, it, vi } from 'vitest'

const execMock = vi.fn()
const readFileMock = vi.fn(async () => '{}')
const writeFileMock = vi.fn(async () => undefined)

vi.mock('node:child_process', () => ({ exec: execMock }))
vi.mock('node:fs/promises', () => ({
  mkdir: vi.fn(async () => undefined),
  readFile: readFileMock,
  writeFile: writeFileMock,
}))

function mockExec(handler: (cmd: string) => string): void {
  execMock.mockImplementation((cmd: string, opts: unknown, cb: (err: Error | null, result: { stdout: string }) => void) => {
    cb(null, { stdout: handler(cmd) })
  })
}

describe('systemd collectors', () => {
  beforeEach(() => {
    vi.resetModules()
    execMock.mockReset()
    readFileMock.mockClear()
    writeFileMock.mockClear()
    readFileMock.mockResolvedValue('{}')
  })

  it('discovers active service units', async () => {
    mockExec((cmd) => {
      expect(cmd).toContain('systemctl list-units --type=service --state=active,failed --output=json --no-pager')
      return JSON.stringify([{ unit: 'b.service' }, { unit: 'a.service' }, { unit: 'a.socket' }])
    })

    const { discoverSystemdServiceUnits } = await import('../../collectors/systemd.js')
    await expect(discoverSystemdServiceUnits('active')).resolves.toEqual(['a.service', 'b.service'])
  })

  it('discovers loaded service units with --all', async () => {
    mockExec((cmd) => {
      expect(cmd).toContain('systemctl list-units --type=service --all --output=json --no-pager')
      return JSON.stringify([{ Unit: 'all.service' }])
    })

    const { discoverSystemdServiceUnits } = await import('../../collectors/systemd.js')
    await expect(discoverSystemdServiceUnits('loaded')).resolves.toEqual(['all.service'])
  })

  it('merges discovered and explicit units, then collects service info', async () => {
    mockExec((cmd) => {
      if (cmd.startsWith('systemctl list-units')) return JSON.stringify([{ unit: 'nginx.service' }])
      if (cmd.startsWith('systemctl show')) {
        const name = cmd.includes('ssh.service') ? 'ssh.service' : 'nginx.service'
        return `Id=${name}\nNames=${name}\nLoadState=loaded\nActiveState=active\nSubState=running\nMainPID=123\nMemoryCurrent=456\nCPUUsageNSec=789\nNRestarts=2\n`
      }
      return ''
    })

    const { collectSystemdServices } = await import('../../collectors/systemd.js')
    const services = await collectSystemdServices({ scope: 'active', explicitUnits: ['ssh.service', 'nginx.service'] })
    expect(services.map((s) => s.name)).toEqual(['nginx.service', 'ssh.service'])
    expect(services[0].mainPid).toBe(123)
    expect(execMock).toHaveBeenCalledTimes(3)
  })

  it('explicit mode skips discovery', async () => {
    mockExec((cmd) => {
      expect(cmd).not.toContain('list-units')
      return 'Id=ssh.service\nLoadState=loaded\nActiveState=active\nSubState=running\n'
    })

    const { collectSystemdServices } = await import('../../collectors/systemd.js')
    const services = await collectSystemdServices({ scope: 'explicit', explicitUnits: ['ssh.service'] })
    expect(services[0].name).toBe('ssh.service')
    expect(execMock).toHaveBeenCalledTimes(1)
  })

  it('returns empty on malformed discovery json', async () => {
    mockExec(() => 'not json')
    const { discoverSystemdServiceUnits } = await import('../../collectors/systemd.js')
    await expect(discoverSystemdServiceUnits('active')).resolves.toEqual([])
  })

  it('collects logs and writes cursor state', async () => {
    mockExec(() => JSON.stringify({
      MESSAGE: 'hello',
      PRIORITY: '3',
      __REALTIME_TIMESTAMP: '1700000000000000',
      __CURSOR: 'cur',
      _BOOT_ID: 'boot',
    }) + '\n')

    const { collectSystemdLogs } = await import('../../collectors/systemd.js')
    const logs = await collectSystemdLogs(['ssh.service'], 1)
    expect(logs[0].unitName).toBe('ssh.service')
    expect(logs[0].level).toBe('error')
    expect(writeFileMock).toHaveBeenCalledWith(expect.any(String), JSON.stringify({ 'ssh.service': { cursor: 'cur', timestamp: '2023-11-14T22:13:20.000Z' } }), 'utf-8')
  })
})
