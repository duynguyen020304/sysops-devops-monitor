#!/usr/bin/env node
import { readFile } from 'node:fs/promises'
import { spawnSync } from 'node:child_process'
import { atomicSymlink } from './updater/stager.js'

interface PendingUpdate { releaseDir: string }

async function main() {
  const markerPath = process.argv[2]
  if (!markerPath) throw new Error('pending update marker path required')
  const pending = JSON.parse(await readFile(markerPath, 'utf-8')) as PendingUpdate
  const currentLink = process.env.AGENT_CURRENT_LINK || '/opt/monitoring-agent/current'
  await atomicSymlink(pending.releaseDir, currentLink)

  const restarted = spawnSync('pm2', ['restart', 'monitoring-agent', '--update-env'], { stdio: 'inherit' })
  if (restarted.status === 0) return

  spawnSync('pm2', ['delete', 'monitoring-agent'], { stdio: 'inherit' })
  const fallback = spawnSync('pm2', ['start', `${currentLink}/dist/index.js`, '--name', 'monitoring-agent'], { stdio: 'inherit' })
  process.exit(fallback.status ?? 1)
}

main().catch((error) => {
  console.error('agent-updater failed:', error)
  process.exit(1)
})
