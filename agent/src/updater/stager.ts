import axios from 'axios'
import { mkdir, readFile, rename, rm, symlink, writeFile } from 'node:fs/promises'
import { createWriteStream } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { pipeline } from 'node:stream/promises'
import { config } from '../config.js'
import { assertManifestUsable, sha256File, verifyManifestSignature } from './manifest.js'
import type { UpdateReleaseOffer } from './client.js'

export interface StageResult { releaseDir: string; markerPath: string; artifactPath: string }

export async function stageUpdate(assignmentId: string, release: UpdateReleaseOffer): Promise<StageResult> {
  assertManifestUsable(release.manifest)
  const publicKey = await readTrustedPublicKey(release.publicKeyId)
  if (!verifyManifestSignature(release.manifest, release.manifestSignature, publicKey)) {
    throw new Error('Manifest signature invalid')
  }

  const workDir = join(config.updateStateDir, 'downloads', assignmentId)
  await mkdir(workDir, { recursive: true })
  const artifactPath = join(workDir, `agent-${release.version}-${release.buildId}.tgz`)
  await downloadFile(release.artifactUrl, artifactPath)

  const actualSha = await sha256File(artifactPath)
  if (actualSha !== release.manifest.artifactSha256) {
    throw new Error(`Artifact sha256 mismatch: expected ${release.manifest.artifactSha256}, got ${actualSha}`)
  }

  const releaseDir = safeReleaseDir(release.version, release.buildId)
  await mkdir(dirname(releaseDir), { recursive: true })
  await mkdir(releaseDir, { recursive: true })
  await writeFile(join(releaseDir, 'manifest.json'), JSON.stringify(release.manifest, null, 2), 'utf-8')

  const markerPath = join(config.updateStateDir, 'pending-update.json')
  await mkdir(dirname(markerPath), { recursive: true })
  await writeFile(markerPath, JSON.stringify({
    assignmentId,
    releaseDir,
    version: release.version,
    buildId: release.buildId,
    artifactPath,
    createdAt: new Date().toISOString(),
  }, null, 2), 'utf-8')

  return { releaseDir, markerPath, artifactPath }
}

async function readTrustedPublicKey(keyId: string): Promise<string> {
  const keyPath = join(config.updateTrustDir, `${keyId}.pem`)
  return readFile(keyPath, 'utf-8')
}

async function downloadFile(url: string, path: string): Promise<void> {
  await mkdir(dirname(path), { recursive: true })
  const { data } = await axios.get(url, {
    responseType: 'stream',
    timeout: 60000,
    headers: {
      'Authorization': `Bearer ${config.serverToken}`,
      'X-Server-Id': config.serverId,
    },
  })
  await pipeline(data, createWriteStream(path, { mode: 0o600 }))
}

function safeReleaseDir(version: string, buildId: string): string {
  const safeId = `${version}-${buildId}`.replace(/[^A-Za-z0-9._-]/g, '_')
  const base = resolve(config.updateReleasesDir)
  const target = resolve(base, safeId)
  if (!target.startsWith(base)) throw new Error('Unsafe release path')
  return target
}

export async function atomicSymlink(target: string, linkPath: string): Promise<void> {
  const tmpLink = `${linkPath}.tmp-${process.pid}`
  await rm(tmpLink, { force: true, recursive: true })
  await symlink(target, tmpLink, 'dir')
  await rename(tmpLink, linkPath)
}
