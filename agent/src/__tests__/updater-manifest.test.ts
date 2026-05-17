import { generateKeyPairSync, sign } from 'node:crypto'
import { describe, expect, it } from 'vitest'
import { assertManifestUsable, canonicalizeManifest, verifyManifestSignature, type AgentUpdateManifest } from '../updater/manifest.js'

const manifest: AgentUpdateManifest = {
  schemaVersion: 1,
  version: '1.2.3',
  buildId: 'abc',
  createdAt: '2026-01-01T00:00:00.000Z',
  expiresAt: '2999-01-01T00:00:00.000Z',
  channel: 'stable',
  artifactSha256: 'a'.repeat(64),
}

describe('updater manifest', () => {
  it('canonicalizes object keys', () => {
    expect(canonicalizeManifest({ ...manifest, gitSha: 'def' })).toBe(canonicalizeManifest({ gitSha: 'def', ...manifest }))
  })

  it('verifies Ed25519 signatures', () => {
    const { publicKey, privateKey } = generateKeyPairSync('ed25519')
    const signature = sign(null, Buffer.from(canonicalizeManifest(manifest)), privateKey).toString('base64')
    const publicKeyPem = publicKey.export({ format: 'pem', type: 'spki' }).toString()
    expect(verifyManifestSignature(manifest, signature, publicKeyPem)).toBe(true)
    expect(verifyManifestSignature({ ...manifest, buildId: 'tampered' }, signature, publicKeyPem)).toBe(false)
  })

  it('rejects expired manifests', () => {
    expect(() => assertManifestUsable({ ...manifest, expiresAt: '2000-01-01T00:00:00.000Z' })).toThrow('Manifest expired')
  })
})
