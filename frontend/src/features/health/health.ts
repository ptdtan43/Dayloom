export type HealthResponse = {
  status: 'ok' | 'degraded'
  database: 'ok' | 'unreachable'
}

export type HealthCheckResult =
  | { kind: 'ok' }
  | { kind: 'degraded' }
  | { kind: 'offline' }

export async function checkHealth(): Promise<HealthCheckResult> {
  try {
    const response = await fetch('/api/health')
    // 503 still carries a body: the API is up but the database is not.
    const body = (await response.json()) as HealthResponse
    return body.status === 'ok' ? { kind: 'ok' } : { kind: 'degraded' }
  } catch {
    return { kind: 'offline' }
  }
}

export function describeHealth(result: HealthCheckResult): string {
  switch (result.kind) {
    case 'ok':
      return 'Hệ thống hoạt động bình thường'
    case 'degraded':
      return 'Máy chủ đang chạy nhưng không kết nối được cơ sở dữ liệu'
    case 'offline':
      return 'Không kết nối được máy chủ'
  }
}
