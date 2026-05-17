#!/usr/bin/env node
import { mkdir, readFile, rm } from 'node:fs/promises'
import { spawnSync } from 'node:child_process'
import { atomicSymlink } from './updater/stager.js'

interface PendingUpdate { releaseDir: string; artifactPath: string; assignmentId?: string }

async function extractTarGz(artifactPath: string, releaseDir: string): Promise<void> {
  await rm(releaseDir, { recursive: true, force: true })
  await mkdir(releaseDir, { recursive: true })
  const tar = spawnSync('tar', ['-xzf', artifactPath, '-C', releaseDir], { stdio: 'inherit' })
  if (tar.status !== 0) throw new Error(`tar extraction failed with exit ${tar.status}`)
}

async function main() {
  const markerPath = process.argv[2]
  if (!markerPath) throw new Error('pending update marker path required')
  const pending = JSON.parse(await readFile(markerPath, 'utf-8')) as PendingUpdate
  const currentLink = process.env.AGENT_CURRENT_LINK || '/opt/monitoring-agent/current'
  await extractTarGz(pending.artifactPath, pending.releaseDir)
  await atomicSymlink(pending.releaseDir, currentLink)
  await rm(markerPath, { force: true })

  const pm2Name = process.env.AGENT_PM2_NAME || 'monitoring-agent'
  const restarted = spawnSync('pm2', ['restart', pm2Name, '--update-env'], { stdio: 'inherit' })
  if (restarted.status === 0) return

  spawnSync('pm2', ['delete', pm2Name], { stdio: 'inherit' })
  const fallback = spawnSync('pm2', ['start', `${currentLink}/dist/index.js`, '--name', pm2Name, '--cwd', currentLink, '--update-env'], { stdio: 'inherit' })
  process.exit(fallback.status ?? 1)
}

main().catch((error) => {
  console.error('agent-updater failed:', error)
  process.exit(1)
})
