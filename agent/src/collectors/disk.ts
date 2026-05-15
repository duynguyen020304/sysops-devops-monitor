import { exec } from 'node:child_process'
import { readFile } from 'node:fs/promises'
import { promisify } from 'node:util'

const execAsync = promisify(exec)

export interface MountPoint {
  filesystem: string
  mount: string
  totalBytes: number
  usedBytes: number
  freeBytes: number
  usagePercent: number
}

export interface DiskMetrics {
  totalBytes: number
  usedBytes: number
  freeBytes: number
  usagePercent: number
  readBytesPerSec: number
  writeBytesPerSec: number
  mountPoints: MountPoint[]
}

function parseSize(sizeStr: string): number {
  const units: Record<string, number> = {
    K: 1024,
    M: 1024 ** 2,
    G: 1024 ** 3,
    T: 1024 ** 4,
    P: 1024 ** 5,
  }
  const match = sizeStr.match(/^(\d+(?:\.\d+)?)([KMGTPE])?$/i)
  if (!match) return 0
  const value = parseFloat(match[1])
  const unit = match[2]?.toUpperCase() || ''
  return Math.round(value * (units[unit] || 1))
}

async function getMountPoints(): Promise<MountPoint[]> {
  try {
    const { stdout } = await execAsync(
      'df -B1 --output=source,size,used,avail,pcent,target -x tmpfs -x devtmpfs -x squashfs 2>/dev/null'
    )
    const lines = stdout.trim().split('\n').slice(1) // skip header

    return lines
      .map((line) => {
        const parts = line.trim().split(/\s+/)
        if (parts.length < 6) return null

        const filesystem = parts[0]
        const totalBytes = parseInt(parts[1], 10) || 0
        const usedBytes = parseInt(parts[2], 10) || 0
        const freeBytes = parseInt(parts[3], 10) || 0
        const usagePercent = parseFloat(parts[4].replace('%', '')) || 0
        const mount = parts[5]

        return { filesystem, mount, totalBytes, usedBytes, freeBytes, usagePercent }
      })
      .filter((mp): mp is MountPoint => mp !== null)
  } catch {
    return []
  }
}

interface DiskIoSnapshot {
  readSectors: number
  writeSectors: number
}

async function getDiskIoSnapshot(): Promise<DiskIoSnapshot> {
  try {
    const content = await readFile('/proc/diskstats', 'utf-8')
    let readSectors = 0
    let writeSectors = 0

    for (const line of content.split('\n')) {
      const parts = line.trim().split(/\s+/)
      if (parts.length < 14) continue

      const deviceName = parts[2]
      // Skip partitions (only count whole devices like sda, vda, nvme0n1)
      if (/^(sd|vd|xvd|nvme\d+n\d+)$/.test(deviceName) || /^nvme\d+n\d+$/.test(deviceName)) {
        readSectors += parseInt(parts[5], 10) || 0   // sectors read
        writeSectors += parseInt(parts[9], 10) || 0   // sectors written
      }
    }

    return { readSectors, writeSectors }
  } catch {
    return { readSectors: 0, writeSectors: 0 }
  }
}

export async function collectDisk(): Promise<DiskMetrics> {
  const mountPoints = await getMountPoints()

  // Aggregate totals from mount points (root partition)
  const rootMount = mountPoints.find((mp) => mp.mount === '/') || mountPoints[0]
  const totalBytes = rootMount?.totalBytes || 0
  const usedBytes = rootMount?.usedBytes || 0
  const freeBytes = rootMount?.freeBytes || 0
  const usagePercent = rootMount?.usagePercent || 0

  // Measure I/O rates with 1s gap
  const prev = await getDiskIoSnapshot()
  await new Promise((resolve) => setTimeout(resolve, 1000))
  const curr = await getDiskIoSnapshot()

  // sectors are typically 512 bytes
  const sectorSize = 512
  const readBytesPerSec = (curr.readSectors - prev.readSectors) * sectorSize
  const writeBytesPerSec = (curr.writeSectors - prev.writeSectors) * sectorSize

  return {
    totalBytes,
    usedBytes,
    freeBytes,
    usagePercent,
    readBytesPerSec,
    writeBytesPerSec,
    mountPoints,
  }
}
