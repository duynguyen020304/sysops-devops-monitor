import os from 'node:os'

export interface CpuMetrics {
  usagePercent: number
  perCoreUsage: number[]
  loadAverage1m: number
  loadAverage5m: number
  loadAverage15m: number
}

interface CpuSnapshot {
  idle: number
  total: number
}

function getCpuSnapshot(): CpuSnapshot {
  const cpus = os.cpus()
  let idle = 0
  let total = 0
  for (const cpu of cpus) {
    const { user, nice, sys, irq } = cpu.times
    idle += cpu.times.idle
    total += user + nice + sys + irq + cpu.times.idle
  }
  return { idle, total }
}

function getPerCoreSnapshot(): CpuSnapshot[] {
  return os.cpus().map((cpu) => {
    const { user, nice, sys, irq } = cpu.times
    return {
      idle: cpu.times.idle,
      total: user + nice + sys + irq + cpu.times.idle,
    }
  })
}

function calculateUsage(prev: CpuSnapshot, curr: CpuSnapshot): number {
  const idleDiff = curr.idle - prev.idle
  const totalDiff = curr.total - prev.total
  if (totalDiff === 0) return 0
  return Math.round(((totalDiff - idleDiff) / totalDiff) * 10000) / 100
}

export async function collectCpu(): Promise<CpuMetrics> {
  const prevOverall = getCpuSnapshot()
  const prevPerCore = getPerCoreSnapshot()

  // Wait 100ms to measure delta
  await new Promise((resolve) => setTimeout(resolve, 100))

  const currOverall = getCpuSnapshot()
  const currPerCore = getPerCoreSnapshot()

  const usagePercent = calculateUsage(prevOverall, currOverall)
  const perCoreUsage = prevPerCore.map((prev, i) => calculateUsage(prev, currPerCore[i]))

  const loadAvg = os.loadavg()

  return {
    usagePercent,
    perCoreUsage,
    loadAverage1m: Math.round(loadAvg[0] * 100) / 100,
    loadAverage5m: Math.round(loadAvg[1] * 100) / 100,
    loadAverage15m: Math.round(loadAvg[2] * 100) / 100,
  }
}
