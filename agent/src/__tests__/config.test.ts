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

  it('keeps invalid parse as NaN', async () => {
    vi.resetModules()
    process.env.COLLECT_INTERVAL_MS = 'bad'
    const { config } = await import('../config.js')
    expect(Number.isNaN(config.collectIntervalMs)).toBe(true)
  })
})
