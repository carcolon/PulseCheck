import { apiBaseUrl, isEntraConfigured } from '../constants'
import type { AdminSession } from '../types'

const returnToStorageKey = 'pulsecheck.entra.returnTo'

export async function startEntraLoginRedirect(returnToPath: string) {
  if (!isEntraConfigured) {
    throw new Error('El acceso corporativo aun no esta configurado para este entorno.')
  }

  setReturnToPath(returnToPath)

  const authorizationUrl = new URL(`${apiBaseUrl}/api/auth/entra/start`)
  authorizationUrl.searchParams.set('redirectOrigin', window.location.origin)
  authorizationUrl.searchParams.set('returnTo', returnToPath)
  window.location.assign(authorizationUrl.toString())
}

export async function completeEntraRedirect() {
  if (!isEntraConfigured) {
    return { session: null as AdminSession | null, returnToPath: null as string | null }
  }

  const params = new URLSearchParams(window.location.search)
  const code = params.get('code')
  const state = params.get('state')
  const error = params.get('error_description') ?? params.get('error')

  if (error) {
    throw new Error(error)
  }

  if (!code || !state) {
    return { session: null as AdminSession | null, returnToPath: null as string | null }
  }

  const response = await fetch(`${apiBaseUrl}/api/auth/entra/callback`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({
      code,
      state,
      redirectUri: `${window.location.origin}/auth/callback`,
    }),
  })

  if (!response.ok) {
    const payload = await safeJson(response)
    throw new Error(payload?.message ?? 'No fue posible completar el inicio de sesion con Microsoft.')
  }

  const session = await response.json() as AdminSession
  const returnToPath = session.returnToPath ?? consumeReturnToPath()
  return { session, returnToPath }
}

export async function getEntraProfilePhotoUrl() {
  return null
}

function setReturnToPath(returnToPath: string) {
  window.sessionStorage.setItem(returnToStorageKey, returnToPath)
  window.localStorage.setItem(returnToStorageKey, returnToPath)
}

function consumeReturnToPath() {
  const returnToPath = window.sessionStorage.getItem(returnToStorageKey)
    ?? window.localStorage.getItem(returnToStorageKey)

  if (returnToPath) {
    window.sessionStorage.removeItem(returnToStorageKey)
    window.localStorage.removeItem(returnToStorageKey)
  }

  return returnToPath
}

async function safeJson(response: Response) {
  try {
    return await response.json()
  } catch {
    return null
  }
}
