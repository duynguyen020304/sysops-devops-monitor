import axios from 'axios'
import { config } from '../config.js'
import type { AgentUpdateManifest } from './manifest.js'

export interface UpdateReleaseOffer {
  releaseId: string
  version: string
  buildId: string
  manifest: AgentUpdateManifest
  manifestSignature: string
  publicKeyId: string
  artifactUrl: string
  rollback?: boolean
}

export interface UpdateCheckResponse {
  assignmentId?: string
  action: 'none' | 'update'
  release?: UpdateReleaseOffer
  retryAfterSeconds?: number
}

export interface UpdateEventPayload {
  assignmentId?: string
  eventType: string
  message?: string
  metadata?: Record<string, unknown>
}

export class AgentUpdateClient {
  private headers() {
    return {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${config.serverToken}`,
      'X-Server-Id': config.serverId,
    }
  }

  async check(currentVersion: string, buildId: string): Promise<UpdateCheckResponse> {
    const { data } = await axios.post<UpdateCheckResponse>(`${config.apiUrl}/api/agent/update/check`, {
      currentVersion,
      buildId,
      capabilities: {
        updaterProtocol: 1,
        platform: process.platform,
        arch: process.arch,
        node: process.version,
      },
    }, { headers: this.headers(), timeout: 10000 })
    return data
  }

  async reportEvent(payload: UpdateEventPayload): Promise<void> {
    await axios.post(`${config.apiUrl}/api/agent/update/events`, {
      serverId: config.serverId,
      timestamp: new Date().toISOString(),
      ...payload,
    }, { headers: this.headers(), timeout: 10000 })
  }
}
