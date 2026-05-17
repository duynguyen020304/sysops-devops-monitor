import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'

const envPath = join(process.cwd(), '.env')
if (existsSync(envPath)) {
  for (const line of readFileSync(envPath, 'utf8').split(/\r?\n/)) {
    const match = line.match(/^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)\s*$/)
    if (match && process.env[match[1]] === undefined) process.env[match[1]] = match[2]
  }
}

const apiUrl = process.env.AGENT_API_URL || 'http://localhost:5000'
const normalizedApiUrl = apiUrl.endsWith('/') ? apiUrl.slice(0, -1) : apiUrl
const parsedApiUrl = URL.canParse(normalizedApiUrl) ? new URL(normalizedApiUrl) : null

export const config = {
  apiUrl: parsedApiUrl?.pathname === '/api' ? parsedApiUrl.origin : normalizedApiUrl,
  serverToken: process.env.AGENT_SERVER_TOKEN || '',
  serverId: process.env.AGENT_SERVER_ID || '',
  collectIntervalMs: parseInt(process.env.COLLECT_INTERVAL_MS || '30000', 10), // 30s
  heartbeatIntervalMs: parseInt(process.env.HEARTBEAT_INTERVAL_MS || '60000', 10), // 60s
  logBatchSize: parseInt(process.env.LOG_BATCH_SIZE || '100', 10),
  maxBufferSize: parseInt(process.env.MAX_BUFFER_SIZE || '1000', 10),
  systemdEnabled: (process.env.SYSTEMD_ENABLED || 'false').toLowerCase() === 'true',
  systemdUnits: (process.env.SYSTEMD_UNITS || '').split(',').map((s) => s.trim()).filter(Boolean),
  systemdLogBatchSize: parseInt(process.env.SYSTEMD_LOG_BATCH_SIZE || process.env.LOG_BATCH_SIZE || '100', 10),
}
