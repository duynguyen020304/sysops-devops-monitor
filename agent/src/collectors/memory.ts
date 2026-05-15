import os from 'node:os'
import { readFile } from 'node:fs/promises'

export interface MemoryMetrics {
  totalBytes: number
  usedBytes: number
  freeBytes: number
  usagePercent: number
  swapUsedBytes: number
}

async function getSwapUsed(): Promise<number> {
  try {
    const content = await readFile('/proc/meminfo', 'utf-8')
    const lines = content.split('\n')

    let swapTotal = 0
    let swapFree = 0

    for (const line of lines) {
      if (line.startsWith('SwapTotal:')) {
        swapTotal = parseInt(line.split(/\s+/)[1], 10) * 1024 // kB to bytes
      } else if (line.startsWith('SwapFree:')) {
        swapFree = parseInt(line.split(/\s+/)[1], 10) * 1024
      }
    }

    return swapTotal - swapFree
  } catch {
    // Not on Linux or /proc/meminfo not available
    return 0
  }
}

export async function collectMemory(): Promise<MemoryMetrics> {
  const totalBytes = os.totalmem()
  const freeBytes = os.freemem()
  const usedBytes = totalBytes - freeBytes
  const usagePercent = Math.round((usedBytes / totalBytes) * 10000) / 100
  const swapUsedBytes = await getSwapUsed()

  return {
    totalBytes,
    usedBytes,
    freeBytes,
    usagePercent,
    swapUsedBytes,
  }
}
