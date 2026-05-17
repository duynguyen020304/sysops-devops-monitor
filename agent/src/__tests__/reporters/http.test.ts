import axios from 'axios'
import { describe, expect, it, vi, beforeEach } from 'vitest'

vi.mock('axios', () => ({ default: { post: vi.fn(), get: vi.fn() } }))
vi.mock('node:fs/promises', () => ({
  readFile: vi.fn(async () => '[]'),
  mkdir: vi.fn(async () => undefined),
  writeFile: vi.fn(async () => undefined),
}))
vi.mock('../../config.js', () => ({ config: { apiUrl: 'http://api', serverToken: 'tok', serverId: 'srv', maxBufferSize: 2 } }))

const post = vi.mocked(axios.post)

describe('HttpReporter', () => {
  beforeEach(() => post.mockReset())

  it('sends metrics to endpoint with headers', async () => {
    post.mockResolvedValue({})
    const { HttpReporter } = await import('../../reporters/http.js')
    await new HttpReporter().sendMetrics({ cpu: {}, memory: {}, disk: {}, network: {} } as any)
    expect(post).toHaveBeenCalledWith('http://api/api/agent/metrics', expect.objectContaining({ serverId: 'srv' }), expect.objectContaining({ headers: expect.objectContaining({ Authorization: 'Bearer tok' }) }))
  })

  it('sends heartbeat, pm2, logs', async () => {
    post.mockResolvedValue({})
    const { HttpReporter } = await import('../../reporters/http.js')
    const r = new HttpReporter()
    await r.sendHeartbeat('srv')
    await r.sendPM2Processes([{ name: 'app' }] as any)
    await r.sendLogs([{ line: 'x' }] as any)
    expect(post.mock.calls.map((c) => c[0])).toEqual(expect.arrayContaining(['http://api/api/agent/heartbeat', 'http://api/api/agent/pm2', 'http://api/api/agent/logs']))
  })

  it('retries failed request', async () => {
    post.mockRejectedValueOnce(new Error('x')).mockResolvedValueOnce({})
    vi.useFakeTimers()
    const { HttpReporter } = await import('../../reporters/http.js')
    const p = new HttpReporter().sendMetrics({ cpu: {}, memory: {}, disk: {}, network: {} } as any)
    await vi.runAllTimersAsync()
    await p
    vi.useRealTimers()
    expect(post).toHaveBeenCalledTimes(2)
  })

  it('spools buffered data to disk', async () => {
    const fs = await import('node:fs/promises')
    const { HttpReporter } = await import('../../reporters/http.js')
    const r: any = new HttpReporter()
    await r.bufferData({ type: 'logs', payload: 1, timestamp: '1' })
    expect(vi.mocked(fs.writeFile)).toHaveBeenCalled()
  })
})
