export const config = {
  apiUrl: process.env.AGENT_API_URL || 'http://localhost:5000',
  serverToken: process.env.AGENT_SERVER_TOKEN || '',
  serverId: process.env.AGENT_SERVER_ID || '',
  collectIntervalMs: parseInt(process.env.COLLECT_INTERVAL_MS || '30000', 10), // 30s
  heartbeatIntervalMs: parseInt(process.env.HEARTBEAT_INTERVAL_MS || '60000', 10), // 60s
  logBatchSize: parseInt(process.env.LOG_BATCH_SIZE || '100', 10),
  maxBufferSize: parseInt(process.env.MAX_BUFFER_SIZE || '1000', 10),
}
