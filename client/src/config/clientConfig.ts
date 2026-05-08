export interface ClientConfig {
  preAuth: {
    enabled: boolean
  }
}

const defaultConfig: ClientConfig = {
  preAuth: { enabled: false },
}

let cached: ClientConfig | null = null

function deriveHttpBaseUrl(): string | null {
  const { hostname, host, protocol } = window.location
  if (hostname === 'localhost' || hostname === '127.0.0.1') {
    return null
  }
  return `${protocol}//${host}`
}

export async function getClientConfig(): Promise<ClientConfig> {
  if (cached) {
    return cached
  }

  const baseUrl = deriveHttpBaseUrl()
  if (!baseUrl) {
    cached = defaultConfig
    return cached
  }

  try {
    const res = await fetch(`${baseUrl}/config`)
    if (!res.ok) {
      cached = defaultConfig
      return cached
    }
    const json = await res.json()
    cached = {
      preAuth: {
        enabled: json?.preAuth?.enabled === true,
      },
    }
    return cached
  } catch {
    cached = defaultConfig
    return cached
  }
}

export function deriveAuthBaseUrl(): string {
  const { host, protocol } = window.location
  return `${protocol}//${host}`
}
