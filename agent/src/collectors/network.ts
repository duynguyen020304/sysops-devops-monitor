import { readFile } from 'node:fs/promises'

export interface NetworkMetrics {
  rxBytesPerSec: number
  txBytesPerSec: number
  packetsReceived: number
  packetsSent: number
  errors: number
  drops: number
}

interface NetSnapshot {
  rxBytes: number
  txBytes: number
  rxPackets: number
  txPackets: number
  rxErrors: number
  txErrors: number
  rxDrops: number
  txDrops: number
}

async function getNetSnapshot(): Promise<NetSnapshot> {
  try {
    const content = await readFile('/proc/net/dev', 'utf-8')
    const lines = content.split('\n').slice(2) // skip header lines

    let rxBytes = 0
    let txBytes = 0
    let rxPackets = 0
    let txPackets = 0
    let rxErrors = 0
    let txErrors = 0
    let rxDrops = 0
    let txDrops = 0

    for (const line of lines) {
      const parts = line.trim().split(/[\s:]+/)
      if (parts.length < 17) continue

      const iface = parts[0]
      // Skip loopback
      if (iface === 'lo') continue

      rxBytes += parseInt(parts[1], 10) || 0
      rxPackets += parseInt(parts[2], 10) || 0
      rxErrors += parseInt(parts[3], 10) || 0
      rxDrops += parseInt(parts[4], 10) || 0

      txBytes += parseInt(parts[9], 10) || 0
      txPackets += parseInt(parts[10], 10) || 0
      txErrors += parseInt(parts[11], 10) || 0
      txDrops += parseInt(parts[12], 10) || 0
    }

    return { rxBytes, txBytes, rxPackets, txPackets, rxErrors, txErrors, rxDrops, txDrops }
  } catch {
    return {
      rxBytes: 0, txBytes: 0,
      rxPackets: 0, txPackets: 0,
      rxErrors: 0, txErrors: 0,
      rxDrops: 0, txDrops: 0,
    }
  }
}

export async function collectNetwork(): Promise<NetworkMetrics> {
  const prev = await getNetSnapshot()
  await new Promise((resolve) => setTimeout(resolve, 1000))
  const curr = await getNetSnapshot()

  return {
    rxBytesPerSec: curr.rxBytes - prev.rxBytes,
    txBytesPerSec: curr.txBytes - prev.txBytes,
    packetsReceived: curr.rxPackets,
    packetsSent: curr.txPackets,
    errors: curr.rxErrors + curr.txErrors,
    drops: curr.rxDrops + curr.txDrops,
  }
}
