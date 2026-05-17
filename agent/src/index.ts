import { config } from './config.js'
import { collectCpu } from './collectors/cpu.js'
import { collectMemory } from './collectors/memory.js'
import { collectDisk } from './collectors/disk.js'
import { collectNetwork } from './collectors/network.js'
import { collectPM2Processes, collectPM2Logs } from './collectors/pm2.js'
import { collectSystemdServices, collectSystemdLogs } from './collectors/systemd.js'
import { HttpReporter } from './reporters/http.js'

const reporter = new HttpReporter()

async function collectAndReport(): Promise<void> {
  try {
    console.log('Collecting metrics...')

    const cpu = await collectCpu()
    const memory = await collectMemory()
    const disk = await collectDisk()
    const network = await collectNetwork()

    await reporter.sendMetrics({ cpu, memory, disk, network })

    const pm2Processes = await collectPM2Processes()
    await reporter.sendPM2Processes(pm2Processes)

    // Collect logs from PM2 processes
    if (pm2Processes.length > 0) {
      const processNames = pm2Processes.map((p) => p.name)
      const logs = await collectPM2Logs(processNames, config.logBatchSize)
      if (logs.length > 0) {
        await reporter.sendLogs(logs)
      }
    }

    if (config.systemdEnabled) {
      const systemdServices = await collectSystemdServices(config.systemdUnits)
      await reporter.sendSystemdServices(systemdServices)
      const units = config.systemdUnits.length > 0 ? config.systemdUnits : systemdServices.map((s) => s.name)
      const systemdLogs = await collectSystemdLogs(units, config.systemdLogBatchSize)
      await reporter.sendSystemdLogs(systemdLogs)
    }

    console.log('Metrics collected and sent')
  } catch (error) {
    console.error('Error collecting metrics:', error)
  }
}

async function sendHeartbeat(): Promise<void> {
  try {
    await reporter.sendHeartbeat(config.serverId)
  } catch (error) {
    console.error('Error sending heartbeat:', error)
  }
}

async function main(): Promise<void> {
  console.log('Monitoring Agent starting...')
  console.log(`API URL: ${config.apiUrl}`)
  console.log(`Server ID: ${config.serverId}`)
  console.log(`Collect interval: ${config.collectIntervalMs}ms`)
  console.log(`Heartbeat interval: ${config.heartbeatIntervalMs}ms`)

  if (!config.serverId) {
    console.warn('WARNING: AGENT_SERVER_ID is not set')
  }
  if (!config.serverToken) {
    console.warn('WARNING: AGENT_SERVER_TOKEN is not set')
  }

  // Initial collection
  await collectAndReport()
  await sendHeartbeat()

  // Periodic metrics collection
  setInterval(() => {
    collectAndReport().catch((err) => {
      console.error('Unhandled error in collect cycle:', err)
    })
  }, config.collectIntervalMs)

  // Periodic heartbeat
  setInterval(() => {
    sendHeartbeat().catch((err) => {
      console.error('Unhandled error in heartbeat:', err)
    })
  }, config.heartbeatIntervalMs)

  console.log('Agent running. Press Ctrl+C to stop.')
}

main().catch((error) => {
  console.error('Fatal error:', error)
  process.exit(1)
})
