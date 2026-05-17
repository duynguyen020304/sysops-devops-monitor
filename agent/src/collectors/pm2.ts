import { exec } from 'node:child_process'
import { mkdir, readFile, stat, writeFile } from 'node:fs/promises'
import { homedir } from 'node:os'
import { dirname, join } from 'node:path'
import { promisify } from 'node:util'

const execAsync = promisify(exec)
const statePath = process.env.PM2_LOG_STATE_PATH || join(process.cwd(), 'agent-data', 'pm2-log-state.json')

interface LogFileState { size: number }
type LogState = Record<string, LogFileState>

async function loadState(): Promise<LogState> {
  try { return JSON.parse(await readFile(statePath, 'utf-8')) as LogState } catch { return {} }
}

async function saveState(state: LogState): Promise<void> {
  await mkdir(dirname(statePath), { recursive: true })
  await writeFile(statePath, JSON.stringify(state), 'utf-8')
}

export function parseLogTimestamp(line: string): string | null {
  const iso = line.match(/^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z?)/)
  if (iso) { const d = new Date(iso[1]); if (!Number.isNaN(d.getTime())) return d.toISOString() }
  const bracket = line.match(/^\[(\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2})\]/)
  if (bracket) { const d = new Date(bracket[1].replace(' ', 'T') + 'Z'); if (!Number.isNaN(d.getTime())) return d.toISOString() }
  return null
}

export interface PM2ProcessInfo {
  pm2Id: number
  name: string
  pid: number
  status: string
  uptimeSeconds: number
  restartCount: number
  cpuUsage: number
  memoryUsage: number
  executionMode: string
  nodeVersion: string
}

export interface PM2LogEntry {
  processName: string
  logType: 'out' | 'err'
  timestamp: string
  line: string
}

interface PM2RawProcess {
  pm_id: number
  name: string
  pid: number
  pm2_env: {
    status: string
    pm_uptime: number
    restart_time: number
    exec_mode: string
    node_version?: string
  }
  monit: {
    cpu: number
    memory: number
  }
}

async function readNewLogLines(filePath: string, maxLines: number, state: LogState): Promise<string[]> {
  try {
    const info = await stat(filePath)
    const prevSize = state[filePath]?.size ?? 0
    const rotated = info.size < prevSize
    const start = rotated ? Math.max(0, info.size - 128 * 1024) : prevSize
    if (info.size === prevSize && !rotated) return []

    const content = await readFile(filePath, 'utf-8')
    state[filePath] = { size: info.size }
    const slice = content.slice(start)
    const lines = slice.split('\n').filter((l) => l.trim().length > 0)
    return lines.slice(-maxLines)
  } catch {
    return []
  }
}

async function readProcessLogs(processName: string, maxLines: number, state: LogState): Promise<PM2LogEntry[]> {
  const pm2LogDir = join(homedir(), '.pm2', 'logs')
  const logs: PM2LogEntry[] = []

  const outLogPath = join(pm2LogDir, `${processName}-out.log`)
  const errLogPath = join(pm2LogDir, `${processName}-error.log`)

  const [outLines, errLines] = await Promise.all([
    readNewLogLines(outLogPath, maxLines, state),
    readNewLogLines(errLogPath, maxLines, state),
  ])

  const now = new Date().toISOString()

  for (const line of outLines) {
    logs.push({ processName, logType: 'out', timestamp: parseLogTimestamp(line) ?? now, line })
  }
  for (const line of errLines) {
    logs.push({ processName, logType: 'err', timestamp: parseLogTimestamp(line) ?? now, line })
  }

  return logs
}

export async function collectPM2Processes(): Promise<PM2ProcessInfo[]> {
  try {
    const pm2Command = process.env.PM2_BIN || 'pm2'
    const { stdout } = await execAsync(`"${pm2Command}" jlist`, { timeout: 10000 })
    const processes: PM2RawProcess[] = JSON.parse(stdout)

    return processes.map((proc) => ({
      pm2Id: proc.pm_id,
      name: proc.name,
      pid: proc.pid,
      status: proc.pm2_env.status,
      uptimeSeconds: Math.floor((Date.now() - proc.pm2_env.pm_uptime) / 1000),
      restartCount: proc.pm2_env.restart_time,
      cpuUsage: proc.monit.cpu,
      memoryUsage: proc.monit.memory,
      executionMode: proc.pm2_env.exec_mode,
      nodeVersion: proc.pm2_env.node_version || '',
    }))
  } catch {
    // PM2 not installed or not running
    return []
  }
}

export async function collectPM2Logs(processNames: string[], maxLines: number = 100): Promise<PM2LogEntry[]> {
  const allLogs: PM2LogEntry[] = []
  const state = await loadState()

  for (const name of processNames) {
    const logs = await readProcessLogs(name, maxLines, state)
    allLogs.push(...logs)
  }

  await saveState(state)
  return allLogs
}
