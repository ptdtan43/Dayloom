import { useEffect, useState } from 'react'
import { checkHealth, describeHealth, type HealthCheckResult } from './features/health/health'

const statusDotClass: Record<HealthCheckResult['kind'], string> = {
  ok: 'bg-emerald-500',
  degraded: 'bg-amber-500',
  offline: 'bg-rose-500',
}

function App() {
  const [health, setHealth] = useState<HealthCheckResult | null>(null)

  useEffect(() => {
    checkHealth().then(setHealth)
  }, [])

  return (
    <main className="flex min-h-screen items-center justify-center bg-stone-50 px-4 text-stone-800">
      <div className="w-full max-w-sm text-center">
        <h1 className="text-4xl font-semibold tracking-tight">Dayloom</h1>
        <p className="mt-2 text-stone-500">Dệt nên một ngày trọn vẹn.</p>

        <div className="mt-8 flex items-center justify-center gap-2 text-sm">
          {health === null ? (
            <span className="text-stone-400">Đang kiểm tra kết nối…</span>
          ) : (
            <>
              <span className={`size-2.5 rounded-full ${statusDotClass[health.kind]}`} />
              <span>{describeHealth(health)}</span>
            </>
          )}
        </div>
      </div>
    </main>
  )
}

export default App
