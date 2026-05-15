import axios, { AxiosError } from 'axios'
import { config } from '../config.js'
import type { CpuMetrics } from '../collectors/cpu.js'
import type { MemoryMetrics } from '../collectors/memory.js'
import type { DiskMetrics } from '../collectors/disk.js'
import type { NetworkMetrics } from '../collectors/network.js'
import type { PM2ProcessInfo, PM2LogEntry } from '../collectors/pm2.js'

export interface SystemMetrics {
  cpu: CpuMetrics
  memory: MemoryMetrics
  disk: DiskMetrics
  network: NetworkMetrics
}

export interface LogEntry {
  processName: string
  logType: 'out' | 'err'
  timestamp: string
  line: string
}

interface BufferedData {
  type: 'metrics' | 'pm2' | 'logs' | 'heartbeat'
  payload: unknown
  timestamp: string
}

const MAX_RETRIES = 3
const BASE_DELAY_MS = 1000

export class HttpReporter {
  private buffer: BufferedData[] = []
  private isFlushing = false

  private getHeaders() {
    return {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${config.serverToken}`,
      'X-Server-Id': config.serverId,
    }
  }

  private async requestWithRetry(
    method: 'post' | 'get',
    path: string,
    data?: unknown,
  ): Promise<boolean> {
    const url = `${config.apiUrl}${path}`

    for (let attempt = 0; attempt < MAX_RETRIES; attempt++) {
      try {
        if (method === 'post') {
          await axios.post(url, data, {
            headers: this.getHeaders(),
            timeout: 10000,
          })
        } else {
          await axios.get(url, {
            headers: this.getHeaders(),
            timeout: 10000,
          })
        }
        return true
      } catch (error) {
        const axiosError = error as AxiosError
        const isLastAttempt = attempt === MAX_RETRIES - 1

        if (isLastAttempt) {
          console.error(
            `Request failed after ${MAX_RETRIES} attempts: ${method.toUpperCase()} ${path}`,
            axiosError.message,
          )
          return false
        }

        // Exponential backoff
        const delay = BASE_DELAY_MS * Math.pow(2, attempt)
        await new Promise((resolve) => setTimeout(resolve, delay))
      }
    }

    return false
  }

  private bufferData(data: BufferedData): void {
    if (this.buffer.length >= config.maxBufferSize) {
      // Drop oldest entries
      this.buffer = this.buffer.slice(-Math.floor(config.maxBufferSize / 2))
    }
    this.buffer.push(data)
  }

  async flushBuffer(): Promise<void> {
    if (this.isFlushing || this.buffer.length === 0) return

    this.isFlushing = true
    const toFlush = [...this.buffer]
    this.buffer = []

    let failedCount = 0

    for (const item of toFlush) {
      let path: string
      switch (item.type) {
        case 'metrics':
          path = '/api/agent/metrics'
          break
        case 'pm2':
          path = '/api/agent/pm2'
          break
        case 'logs':
          path = '/api/agent/logs'
          break
        case 'heartbeat':
          path = '/api/agent/heartbeat'
          break
        default:
          continue
      }

      const success = await this.requestWithRetry('post', path, item.payload)
      if (!success) {
        failedCount++
      }
    }

    if (failedCount > 0) {
      console.warn(`Failed to flush ${failedCount}/${toFlush.length} buffered items`)
    } else {
      console.log(`Flushed ${toFlush.length} buffered items`)
    }

    this.isFlushing = false
  }

  async sendHeartbeat(serverId: string): Promise<void> {
    const success = await this.requestWithRetry('post', '/api/agent/heartbeat', {
      serverId,
      timestamp: new Date().toISOString(),
    })

    if (!success) {
      this.bufferData({
        type: 'heartbeat',
        payload: { serverId, timestamp: new Date().toISOString() },
        timestamp: new Date().toISOString(),
      })
    } else {
      // Try flushing buffer on successful heartbeat
      await this.flushBuffer()
    }
  }

  async sendMetrics(metrics: SystemMetrics): Promise<void> {
    const payload = {
      serverId: config.serverId,
      timestamp: new Date().toISOString(),
      ...metrics,
    }

    const success = await this.requestWithRetry('post', '/api/agent/metrics', payload)

    if (!success) {
      this.bufferData({
        type: 'metrics',
        payload,
        timestamp: new Date().toISOString(),
      })
    }
  }

  async sendPM2Processes(processes: PM2ProcessInfo[]): Promise<void> {
    if (processes.length === 0) return

    const payload = {
      serverId: config.serverId,
      timestamp: new Date().toISOString(),
      processes,
    }

    const success = await this.requestWithRetry('post', '/api/agent/pm2', payload)

    if (!success) {
      this.bufferData({
        type: 'pm2',
        payload,
        timestamp: new Date().toISOString(),
      })
    }
  }

  async sendLogs(logs: LogEntry[]): Promise<void> {
    if (logs.length === 0) return

    const payload = {
      serverId: config.serverId,
      timestamp: new Date().toISOString(),
      logs,
    }

    const success = await this.requestWithRetry('post', '/api/agent/logs', payload)

    if (!success) {
      this.bufferData({
        type: 'logs',
        payload,
        timestamp: new Date().toISOString(),
      })
    }
  }
}
