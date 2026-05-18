import { config } from './config.js'
import { collectCpu } from './collectors/cpu.js'
import { collectMemory } from './collectors/memory.js'
import { collectDisk } from './collectors/disk.js'
import { collectNetwork } from './collectors/network.js'
import { collectPM2Processes, collectPM2Logs } from './collectors/pm2.js'
import { collectSystemdServices, collectSystemdLogs } from './collectors/systemd.js'
import { HttpReporter } from './reporters/http.js'
import { AgentUpdater } from './updater/runner.js'

const reporter = new HttpReporter()
const updater = new AgentUpdater()
const intervals: NodeJS.Timeout[] = []
let shuttingDown = false

async function collectAndReport(): Promise<void> {
  if (shuttingDown) return
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

    console.log('Metrics collected and sent')
  } catch (error) {
    console.error('Error collecting metrics:', error)
  }
}

async function collectAndReportSystemd(reason = 'scheduled'): Promise<void> {
  if (shuttingDown || !config.systemdEnabled) return
  try {
    console.log(`Collecting systemd snapshot (${reason})...`)
    const systemdServices = await collectSystemdServices({
      scope: config.systemdDiscoveryScope,
      explicitUnits: config.systemdUnits,
    })
    await reporter.sendSystemdServices(systemdServices)
    const units = systemdServices.length > 0 ? systemdServices.map((s) => s.name) : config.systemdUnits
    const systemdLogs = await collectSystemdLogs(units, config.systemdLogBatchSize)
    await reporter.sendSystemdLogs(systemdLogs)
  } catch (error) {
    console.error('Error collecting systemd snapshot:', error)
  }
}

async function sendHeartbeat(): Promise<void> {
  if (shuttingDown) return
  try {
    await reporter.sendHeartbeat(config.serverId)
  } catch (error) {
    console.error('Error sending heartbeat:', error)
  }
}

async function pollSystemdRefreshCommand(): Promise<void> {
  if (shuttingDown || !config.systemdEnabled) return
  if (await reporter.shouldRefreshSystemd()) await collectAndReportSystemd('forced')
}

async function main(): Promise<void> {
  console.log('Monitoring Agent starting...')
  console.log(`API URL: ${config.apiUrl}`)
  console.log(`Server ID: ${config.serverId}`)
  console.log(`Collect interval: ${config.collectIntervalMs}ms`)
  console.log(`Heartbeat interval: ${config.heartbeatIntervalMs}ms`)
  console.log(`Systemd collect interval: ${config.systemdCollectIntervalMs}ms`)

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
  intervals.push(setInterval(() => {
    collectAndReport().catch((err) => {
      console.error('Unhandled error in collect cycle:', err)
    })
  }, config.collectIntervalMs))

  // Periodic heartbeat
  intervals.push(setInterval(() => {
    sendHeartbeat().catch((err) => {
      console.error('Unhandled error in heartbeat:', err)
    })
  }, config.heartbeatIntervalMs))

  if (config.systemdEnabled) {
    await collectAndReportSystemd('startup')
    intervals.push(setInterval(() => {
      collectAndReportSystemd('scheduled').catch((err) => {
        console.error('Unhandled error in systemd cycle:', err)
      })
    }, config.systemdCollectIntervalMs))
    intervals.push(setInterval(() => {
      pollSystemdRefreshCommand().catch((err) => {
        console.error('Unhandled error polling systemd refresh command:', err)
      })
    }, config.systemdCommandPollIntervalMs))
  }

  if (config.updateEnabled) {
    await updater.checkOnce()
    intervals.push(setInterval(() => {
      updater.checkOnce().catch((err) => {
        console.error('Unhandled error in updater cycle:', err)
      })
    }, config.updateIntervalMs))
  }

  console.log('Agent running. Press Ctrl+C to stop.')
}

async function shutdown(signal: string): Promise<void> {
  if (shuttingDown) return
  shuttingDown = true
  console.log(`Received ${signal}, shutting down...`)
  for (const interval of intervals) clearInterval(interval)
  await reporter.flushBuffer()
  process.exit(0)
}

process.on('SIGINT', () => void shutdown('SIGINT'))
process.on('SIGTERM', () => void shutdown('SIGTERM'))
process.on('message', (message) => {
  if (message === 'shutdown') void shutdown('PM2 shutdown')
})

main().catch((error) => {
  console.error('Fatal error:', error)
  process.exit(1)
})
