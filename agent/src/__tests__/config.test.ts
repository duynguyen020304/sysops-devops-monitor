import { describe, expect, it, vi } from 'vitest'

describe('config', () => {
  it('loads env vars', async () => {
    vi.resetModules()
    process.env.AGENT_API_URL = 'http://api'
    process.env.AGENT_SERVER_TOKEN = 'tok'
    process.env.AGENT_SERVER_ID = 'srv'
    process.env.COLLECT_INTERVAL_MS = '123'
    const { config } = await import('../config.js')
    expect(config.apiUrl).toBe('http://api')
    expect(config.serverToken).toBe('tok')
    expect(config.serverId).toBe('srv')
    expect(config.collectIntervalMs).toBe(123)
  })

  it('uses defaults', async () => {
    vi.resetModules()
    delete process.env.AGENT_API_URL
    delete process.env.COLLECT_INTERVAL_MS
    const { config } = await import('../config.js')
    expect(config.apiUrl).toBe('http://localhost:5000')
    expect(config.collectIntervalMs).toBe(30000)
  })

  it('loads systemd discovery scope and explicit units', async () => {
    vi.resetModules()
    process.env.SYSTEMD_DISCOVERY_SCOPE = 'loaded'
    process.env.SYSTEMD_EXPLICIT_UNITS = 'nginx.service, ssh.service'
    process.env.SYSTEMD_UNITS = 'legacy.service'
    const { config } = await import('../config.js')
    expect(config.systemdDiscoveryScope).toBe('loaded')
    expect(config.systemdUnits).toEqual(['nginx.service', 'ssh.service'])
  })

  it('defaults invalid systemd discovery scope to active and supports legacy units', async () => {
    vi.resetModules()
    delete process.env.SYSTEMD_EXPLICIT_UNITS
    process.env.SYSTEMD_DISCOVERY_SCOPE = 'bad'
    process.env.SYSTEMD_UNITS = 'legacy.service'
    const { config } = await import('../config.js')
    expect(config.systemdDiscoveryScope).toBe('active')
    expect(config.systemdUnits).toEqual(['legacy.service'])
  })

  it('defaults systemd collection and command polling intervals', async () => {
    vi.resetModules()
    delete process.env.SYSTEMD_COLLECT_INTERVAL_MS
    delete process.env.SYSTEMD_COMMAND_POLL_INTERVAL_MS
    delete process.env.HEARTBEAT_INTERVAL_MS
    const { config } = await import('../config.js')
    expect(config.systemdCollectIntervalMs).toBe(3600000)
    expect(config.systemdCommandPollIntervalMs).toBe(60000)
  })

  it('keeps invalid parse as NaN', async () => {
    vi.resetModules()
    process.env.COLLECT_INTERVAL_MS = 'bad'
    const { config } = await import('../config.js')
    expect(Number.isNaN(config.collectIntervalMs)).toBe(true)
  })
})
