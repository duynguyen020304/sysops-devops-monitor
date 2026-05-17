import { exec } from 'node:child_process'
import { mkdir, readFile, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { promisify } from 'node:util'

const execAsync = promisify(exec)
const statePath = process.env.SYSTEMD_LOG_STATE_PATH || join(process.cwd(), 'agent-data', 'systemd-log-state.json')

type SystemdLogState = Record<string, { cursor?: string; timestamp?: string }>

export interface SystemdServiceInfo {
  name: string
  displayName?: string
  loadState: string
  activeState: string
  subState: string
  description?: string
  fragmentPath?: string
  mainPid?: number
  memoryCurrent?: number
  cpuUsageNSec?: number
  restartCount?: number
}

export interface SystemdLogEntry {
  unitName: string
  timestamp: string
  priority?: number
  level?: string
  message: string
  rawJson?: string
  cursor?: string
  bootId?: string
}

async function loadState(): Promise<SystemdLogState> {
  try { return JSON.parse(await readFile(statePath, 'utf-8')) as SystemdLogState } catch { return {} }
}

async function saveState(state: SystemdLogState): Promise<void> {
  await mkdir(dirname(statePath), { recursive: true })
  await writeFile(statePath, JSON.stringify(state), 'utf-8')
}

function quote(value: string): string { return `'${value.replace(/'/g, `'\''`)}'` }
function parseNumber(value?: string): number | undefined { const n = Number(value); return Number.isFinite(n) ? n : undefined }

export async function collectSystemdServices(units: string[] = []): Promise<SystemdServiceInfo[]> {
  try {
    const targets = units.length > 0 ? units : []
    if (targets.length === 0) return []

    const services: SystemdServiceInfo[] = []
    for (const unit of targets) {
      const { stdout } = await execAsync(`systemctl show ${quote(unit)} --property=Id,Names,Description,LoadState,ActiveState,SubState,FragmentPath,MainPID,MemoryCurrent,CPUUsageNSec,NRestarts --no-pager`, { timeout: 10000 })
      const props = Object.fromEntries(stdout.split(/\r?\n/).filter(Boolean).map((line) => {
        const idx = line.indexOf('=')
        return idx >= 0 ? [line.slice(0, idx), line.slice(idx + 1)] : [line, '']
      })) as Record<string, string>
      services.push({
        name: props.Id || unit,
        displayName: props.Names || props.Id || unit,
        loadState: props.LoadState || 'unknown',
        activeState: props.ActiveState || 'unknown',
        subState: props.SubState || 'unknown',
        description: props.Description || undefined,
        fragmentPath: props.FragmentPath || undefined,
        mainPid: parseNumber(props.MainPID),
        memoryCurrent: parseNumber(props.MemoryCurrent),
        cpuUsageNSec: parseNumber(props.CPUUsageNSec),
        restartCount: parseNumber(props.NRestarts),
      })
    }
    return services
  } catch {
    return []
  }
}

function priorityToLevel(priority?: number): string {
  if (priority === undefined) return 'info'
  if (priority <= 3) return 'error'
  if (priority === 4) return 'warn'
  if (priority === 7) return 'debug'
  return 'info'
}

function parseJournalLine(unitName: string, line: string): SystemdLogEntry | null {
  try {
    const raw = JSON.parse(line) as Record<string, unknown>
    const micros = typeof raw.__REALTIME_TIMESTAMP === 'string' ? Number(raw.__REALTIME_TIMESTAMP) : undefined
    const timestamp = Number.isFinite(micros) ? new Date((micros as number) / 1000).toISOString() : new Date().toISOString()
    const priority = typeof raw.PRIORITY === 'string' ? Number(raw.PRIORITY) : undefined
    const message = String(raw.MESSAGE || '')
    if (!message) return null
    return {
      unitName,
      timestamp,
      priority: Number.isFinite(priority) ? priority : undefined,
      level: priorityToLevel(Number.isFinite(priority) ? priority : undefined),
      message,
      rawJson: line,
      cursor: typeof raw.__CURSOR === 'string' ? raw.__CURSOR : undefined,
      bootId: typeof raw._BOOT_ID === 'string' ? raw._BOOT_ID : undefined,
    }
  } catch { return null }
}

export async function collectSystemdLogs(units: string[], maxLines = 100): Promise<SystemdLogEntry[]> {
  if (units.length === 0) return []
  const state = await loadState()
  const all: SystemdLogEntry[] = []

  for (const unit of units) {
    const cursor = state[unit]?.cursor
    const since = state[unit]?.timestamp || '5 minutes ago'
    const cursorArg = cursor ? `--after-cursor ${quote(cursor)}` : `--since ${quote(since)}`
    try {
      const { stdout } = await execAsync(`journalctl -u ${quote(unit)} --output=json --no-pager ${cursorArg} -n ${maxLines}`, { timeout: 10000, maxBuffer: 1024 * 1024 })
      const logs = stdout.split(/\r?\n/).filter(Boolean).map((line) => parseJournalLine(unit, line)).filter((x): x is SystemdLogEntry => x !== null)
      all.push(...logs)
      const last = logs.at(-1)
      if (last) state[unit] = { cursor: last.cursor, timestamp: last.timestamp }
    } catch { /* no-op */ }
  }

  await saveState(state)
  return all
}
