import { spawn } from 'node:child_process'
import { config } from '../config.js'
import { AgentUpdateClient } from './client.js'
import { stageUpdate } from './stager.js'

const AGENT_VERSION = process.env.AGENT_VERSION || '1.0.0'
const AGENT_BUILD_ID = process.env.AGENT_BUILD_ID || 'dev'

export class AgentUpdater {
  private client = new AgentUpdateClient()
  private running = false

  async checkOnce(): Promise<void> {
    if (!config.updateEnabled || this.running) return
    this.running = true
    try {
      const offer = await this.client.check(AGENT_VERSION, AGENT_BUILD_ID)
      if (offer.action !== 'update' || !offer.assignmentId || !offer.release) return
      await this.client.reportEvent({ assignmentId: offer.assignmentId, eventType: 'Downloading' })
      const staged = await stageUpdate(offer.assignmentId, offer.release)
      await this.client.reportEvent({ assignmentId: offer.assignmentId, eventType: 'Verified', metadata: { ...staged } })
      spawnUpdater(staged.markerPath)
      await this.client.reportEvent({ assignmentId: offer.assignmentId, eventType: 'Restarting' })
      process.kill(process.pid, 'SIGTERM')
    } catch (error) {
      console.error('Agent update check failed:', error)
      const message = error instanceof Error ? error.message : String(error)
      try {
        await this.client.reportEvent({ eventType: 'Failed', message })
      } catch {
        // best-effort only
      }
    } finally {
      this.running = false
    }
  }
}

function spawnUpdater(markerPath: string): void {
  const child = spawn(process.execPath, [config.updateHelperPath, markerPath], {
    detached: true,
    stdio: 'ignore',
    env: process.env,
  })
  child.unref()
}
