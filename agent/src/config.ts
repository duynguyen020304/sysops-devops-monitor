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
const systemdExplicitUnits = process.env.SYSTEMD_EXPLICIT_UNITS || process.env.SYSTEMD_UNITS || ''
const systemdDiscoveryScope = ['active', 'loaded', 'explicit'].includes(process.env.SYSTEMD_DISCOVERY_SCOPE || '')
  ? process.env.SYSTEMD_DISCOVERY_SCOPE as 'active' | 'loaded' | 'explicit'
  : 'active'

const systemdCommandPollIntervalMs = parseInt(process.env.SYSTEMD_COMMAND_POLL_INTERVAL_MS || process.env.HEARTBEAT_INTERVAL_MS || '60000', 10)

export const config = {
  apiUrl: parsedApiUrl?.pathname === '/api' ? parsedApiUrl.origin : normalizedApiUrl,
  serverToken: process.env.AGENT_SERVER_TOKEN || '',
  serverId: process.env.AGENT_SERVER_ID || '',
  collectIntervalMs: parseInt(process.env.COLLECT_INTERVAL_MS || '30000', 10), // 30s
  heartbeatIntervalMs: parseInt(process.env.HEARTBEAT_INTERVAL_MS || '60000', 10), // 60s
  logBatchSize: parseInt(process.env.LOG_BATCH_SIZE || '100', 10),
  maxBufferSize: parseInt(process.env.MAX_BUFFER_SIZE || '1000', 10),
  systemdEnabled: (process.env.SYSTEMD_ENABLED || 'false').toLowerCase() === 'true',
  systemdDiscoveryScope,
  systemdUnits: systemdExplicitUnits.split(',').map((s) => s.trim()).filter(Boolean),
  systemdLogBatchSize: parseInt(process.env.SYSTEMD_LOG_BATCH_SIZE || process.env.LOG_BATCH_SIZE || '100', 10),
  systemdCollectIntervalMs: parseInt(process.env.SYSTEMD_COLLECT_INTERVAL_MS || '3600000', 10),
  systemdCommandPollIntervalMs,
  updateEnabled: (process.env.AGENT_UPDATE_ENABLED || 'false').toLowerCase() === 'true',
  updateIntervalMs: parseInt(process.env.AGENT_UPDATE_INTERVAL_MS || '300000', 10),
  updateStateDir: process.env.AGENT_UPDATE_STATE_DIR || join(process.cwd(), 'agent-data', 'updates'),
  updateReleasesDir: process.env.AGENT_RELEASES_DIR || '/opt/monitoring-agent/releases',
  updateTrustDir: process.env.AGENT_UPDATE_TRUST_DIR || '/etc/monitoring-agent/trusted-keys',
  updateHelperPath: process.env.AGENT_UPDATE_HELPER_PATH || join(process.cwd(), 'dist', 'bin', 'agent-updater.js'),
}
