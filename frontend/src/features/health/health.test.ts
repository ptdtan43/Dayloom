import { afterEach, describe, expect, it, vi } from 'vitest'
import { checkHealth, describeHealth } from './health'

function mockFetch(implementation: () => Promise<Response>) {
  vi.stubGlobal('fetch', vi.fn(implementation))
}

describe('checkHealth', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reports ok when the API and database are up', async () => {
    mockFetch(async () => Response.json({ status: 'ok', database: 'ok' }))

    expect(await checkHealth()).toEqual({ kind: 'ok' })
  })

  it('reports degraded when the API answers 503', async () => {
    mockFetch(async () => Response.json({ status: 'degraded', database: 'unreachable' }, { status: 503 }))

    expect(await checkHealth()).toEqual({ kind: 'degraded' })
  })

  it('reports offline when the request fails', async () => {
    mockFetch(async () => {
      throw new TypeError('Failed to fetch')
    })

    expect(await checkHealth()).toEqual({ kind: 'offline' })
  })
})

describe('describeHealth', () => {
  it('returns a Vietnamese message for every state', () => {
    expect(describeHealth({ kind: 'ok' })).toBe('Hệ thống hoạt động bình thường')
    expect(describeHealth({ kind: 'offline' })).toBe('Không kết nối được máy chủ')
  })
})
