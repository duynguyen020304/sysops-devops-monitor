import { createHash, verify } from 'node:crypto'
import { readFile } from 'node:fs/promises'

export interface AgentUpdateManifest {
  schemaVersion: number
  version: string
  buildId: string
  gitSha?: string
  createdAt: string
  expiresAt: string
  channel: string
  os?: string
  arch?: string
  nodeRange?: string
  artifactSha256: string
  artifactSize?: number
  minCurrentVersion?: string
  rollbackAllowedFrom?: string[]
}

export function canonicalizeManifest(manifest: AgentUpdateManifest): string {
  return JSON.stringify(sortObject(manifest))
}

function sortObject(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(sortObject)
  if (!value || typeof value !== 'object') return value
  return Object.fromEntries(
    Object.entries(value as Record<string, unknown>)
      .filter(([, entry]) => entry !== undefined)
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([key, entry]) => [key, sortObject(entry)]),
  )
}

export function verifyManifestSignature(
  manifest: AgentUpdateManifest,
  signatureBase64: string,
  publicKeyPem: string,
): boolean {
  try {
    const data = Buffer.from(canonicalizeManifest(manifest))
    const signature = Buffer.from(signatureBase64, 'base64')
    if (verifyAlgorithm(null, data, publicKeyPem, signature)) return true
    return verifyAlgorithm('sha256', data, publicKeyPem, signature)
  } catch {
    return false
  }
}

function verifyAlgorithm(algorithm: string | null, data: Buffer, publicKeyPem: string, signature: Buffer): boolean {
  try {
    return verify(algorithm, data, publicKeyPem, signature)
  } catch {
    return false
  }
}

export async function sha256File(path: string): Promise<string> {
  const data = await readFile(path)
  return createHash('sha256').update(data).digest('hex')
}

export function assertManifestUsable(manifest: AgentUpdateManifest, now = new Date()): void {
  if (manifest.schemaVersion !== 1) throw new Error('Unsupported manifest schemaVersion')
  if (!manifest.version || !manifest.buildId || !manifest.artifactSha256) throw new Error('Manifest missing required fields')
  const expiresAt = Date.parse(manifest.expiresAt)
  if (!Number.isFinite(expiresAt)) throw new Error('Manifest expiresAt invalid')
  if (expiresAt <= now.getTime()) throw new Error('Manifest expired')
}
