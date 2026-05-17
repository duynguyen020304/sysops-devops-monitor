import axios, { AxiosError } from 'axios'
import { mkdir, readFile, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { config } from '../config.js'
import type { CpuMetrics } from '../collectors/cpu.js'
import type { MemoryMetrics } from '../collectors/memory.js'
import type { DiskMetrics } from '../collectors/disk.js'
import type { NetworkMetrics } from '../collectors/network.js'
import type { PM2ProcessInfo, PM2LogEntry } from '../collectors/pm2.js'
import type { SystemdServiceInfo, SystemdLogEntry } from '../collectors/systemd.js'

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
  type: 'metrics' | 'pm2' | 'logs' | 'heartbeat' | 'systemd' | 'systemdLogs'
  payload: unknown
  timestamp: string
}

const MAX_RETRIES = 3
const BASE_DELAY_MS = 1000
const spoolPath = process.env.AGENT_SPOOL_PATH || join(process.cwd(), 'agent-data', 'spool.json')

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

  private async loadSpool(): Promise<BufferedData[]> {
    try { return JSON.parse(await readFile(spoolPath, 'utf-8')) as BufferedData[] } catch { return [] }
  }

  private async saveSpool(items: BufferedData[]): Promise<void> {
    await mkdir(dirname(spoolPath), { recursive: true })
    await writeFile(spoolPath, JSON.stringify(items.slice(-config.maxBufferSize)), 'utf-8')
  }

  private async bufferData(data: BufferedData): Promise<void> {
    const spool = await this.loadSpool()
    spool.push(data)
    await this.saveSpool(spool)
  }

  async flushBuffer(): Promise<void> {
    if (this.isFlushing) return

    this.isFlushing = true
    const toFlush = [...(await this.loadSpool()), ...this.buffer]
    if (toFlush.length === 0) {
      this.isFlushing = false
      return
    }
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
        case 'systemd':
          path = '/api/agent/systemd'
          break
        case 'systemdLogs':
          path = '/api/agent/systemd/logs'
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
        await this.saveSpool(toFlush.slice(toFlush.indexOf(item)))
        break
      }
    }

    if (failedCount > 0) {
      console.warn(`Failed to flush ${failedCount}/${toFlush.length} buffered items`)
    } else {
      await this.saveSpool([])
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
      await this.bufferData({
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
      metrics: {
        cpuUsagePercent: metrics.cpu.usagePercent,
        memoryTotalBytes: metrics.memory.totalBytes,
        memoryUsedBytes: metrics.memory.usedBytes,
        memoryUsagePercent: metrics.memory.usagePercent,
        swapUsedBytes: metrics.memory.swapUsedBytes,
        diskTotalBytes: metrics.disk.totalBytes,
        diskUsedBytes: metrics.disk.usedBytes,
        diskUsagePercent: metrics.disk.usagePercent,
        diskReadBytesPerSecond: metrics.disk.readBytesPerSec,
        diskWriteBytesPerSecond: metrics.disk.writeBytesPerSec,
        networkRxBytesPerSecond: metrics.network.rxBytesPerSec,
        networkTxBytesPerSecond: metrics.network.txBytesPerSec,
        loadAverage1m: metrics.cpu.loadAverage1m,
        loadAverage5m: metrics.cpu.loadAverage5m,
        loadAverage15m: metrics.cpu.loadAverage15m,
      },
    }

    const success = await this.requestWithRetry('post', '/api/agent/metrics', payload)

    if (!success) {
      await this.bufferData({
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
      await this.bufferData({
        type: 'pm2',
        payload,
        timestamp: new Date().toISOString(),
      })
    }
  }

  async sendSystemdServices(services: SystemdServiceInfo[]): Promise<void> {
    if (services.length === 0) return
    const payload = { serverId: config.serverId, collectedAt: new Date().toISOString(), services }
    const success = await this.requestWithRetry('post', '/api/agent/systemd', payload)
    if (!success) await this.bufferData({ type: 'systemd', payload, timestamp: new Date().toISOString() })
  }

  async sendSystemdLogs(logs: SystemdLogEntry[]): Promise<void> {
    if (logs.length === 0) return
    const payload = { serverId: config.serverId, logs }
    const success = await this.requestWithRetry('post', '/api/agent/systemd/logs', payload)
    if (!success) await this.bufferData({ type: 'systemdLogs', payload, timestamp: new Date().toISOString() })
  }

  async sendLogs(logs: LogEntry[]): Promise<void> {
    if (logs.length === 0) return

    const payload = {
      serverId: config.serverId,
      timestamp: new Date().toISOString(),
      logs: logs.map((log) => ({
        processName: log.processName,
        streamType: log.logType === 'err' ? 'stderr' : 'stdout',
        level: log.logType === 'err' ? 'error' : 'info',
        message: log.line,
        timestamp: log.timestamp,
      })),
    }

    const success = await this.requestWithRetry('post', '/api/agent/logs', payload)

    if (!success) {
      await this.bufferData({
        type: 'logs',
        payload,
        timestamp: new Date().toISOString(),
      })
    }
  }
}
