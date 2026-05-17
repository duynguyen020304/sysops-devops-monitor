import { exec } from 'node:child_process'
import { readFile } from 'node:fs/promises'
import { homedir } from 'node:os'
import { join } from 'node:path'
import { promisify } from 'node:util'

const execAsync = promisify(exec)

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

async function readLogTail(filePath: string, maxLines: number): Promise<string[]> {
  try {
    const content = await readFile(filePath, 'utf-8')
    const lines = content.split('\n').filter((l) => l.trim().length > 0)
    return lines.slice(-maxLines)
  } catch {
    return []
  }
}

async function readProcessLogs(processName: string, maxLines: number): Promise<PM2LogEntry[]> {
  const pm2LogDir = join(homedir(), '.pm2', 'logs')
  const logs: PM2LogEntry[] = []

  const outLogPath = join(pm2LogDir, `${processName}-out.log`)
  const errLogPath = join(pm2LogDir, `${processName}-error.log`)

  const [outLines, errLines] = await Promise.all([
    readLogTail(outLogPath, maxLines),
    readLogTail(errLogPath, maxLines),
  ])

  const now = new Date().toISOString()

  for (const line of outLines) {
    logs.push({ processName, logType: 'out', timestamp: now, line })
  }
  for (const line of errLines) {
    logs.push({ processName, logType: 'err', timestamp: now, line })
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

  for (const name of processNames) {
    const logs = await readProcessLogs(name, maxLines)
    allLogs.push(...logs)
  }

  return allLogs
}
