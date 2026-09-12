import { useEffect, useRef, useState } from 'react'
import { apiBaseUrl, csrfHeaderName, isEntraConfigured } from '../constants'
import { completeEntraRedirect, getEntraProfilePhotoUrl, startEntraLoginRedirect } from '../lib/entra'
import type { AdminSession } from '../types'

type LoginInput = {
  email: string
  password: string
}

const sessionTouchThrottleMs = 60_000
const adminSessionStorageKey = 'pulsecheck.admin.session'

export function useAdminSession() {
  const [session, setSession] = useState<AdminSession | null>(null)
  const [profilePhotoUrl, setProfilePhotoUrl] = useState<string | null>(null)
  const [isRestoring, setIsRestoring] = useState(true)
  const [authError, setAuthError] = useState<string | null>(null)
  const lastSessionTouchAtRef = useRef(0)
  const sessionTouchInFlightRef = useRef(false)

  useEffect(() => {
    void initializeSession()
  }, [])

  useEffect(() => {
    if (!session || !isEntraConfigured) {
      setProfilePhotoUrl(null)
      return undefined
    }

    let isMounted = true
    let objectUrl: string | null = null

    void getEntraProfilePhotoUrl().then((photoUrl) => {
      if (!isMounted) {
        if (photoUrl) URL.revokeObjectURL(photoUrl)
        return
      }

      objectUrl = photoUrl
      setProfilePhotoUrl(photoUrl)
    })

    return () => {
      isMounted = false
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [session])

  useEffect(() => {
    if (!session) {
      lastSessionTouchAtRef.current = 0
      sessionTouchInFlightRef.current = false
      return undefined
    }

    const touchSessionFromActivity = () => {
      void touchSession()
    }

    const touchOnVisible = () => {
      if (document.visibilityState === 'visible') {
        void touchSession()
      }
    }

    const windowEvents = [
      'beforeinput',
      'click',
      'dblclick',
      'focus',
      'input',
      'keydown',
      'keyup',
      'mousedown',
      'mousemove',
      'mouseup',
      'pageshow',
      'pointerdown',
      'pointermove',
      'pointerup',
      'resize',
      'scroll',
      'touchmove',
      'touchstart',
      'wheel',
    ]

    document.addEventListener('visibilitychange', touchOnVisible, { passive: true })
    document.addEventListener('selectionchange', touchSessionFromActivity)
    windowEvents.forEach((eventName) => {
      window.addEventListener(eventName, touchSessionFromActivity, { passive: true, capture: true })
    })

    return () => {
      document.removeEventListener('visibilitychange', touchOnVisible)
      document.removeEventListener('selectionchange', touchSessionFromActivity)
      windowEvents.forEach((eventName) => {
        window.removeEventListener(eventName, touchSessionFromActivity, { capture: true })
      })
    }
  }, [session])

  async function initializeSession() {
    try {
      setAuthError(null)

      if (isEntraConfigured) {
        const { session: entraSession, returnToPath } = await completeEntraRedirect()
        if (entraSession) {
          saveStoredSession(entraSession)
          setSession(entraSession)

          if (returnToPath) {
            window.location.replace(returnToPath)
            return
          }
        }
      }

      await restoreSession()
    } catch (error) {
      setSession(null)
      setAuthError(error instanceof Error ? error.message : 'No fue posible restaurar la sesion.')
    } finally {
      setIsRestoring(false)
    }
  }

  async function restoreSession() {
    try {
      setAuthError(null)
      const storedSession = readStoredSession()
      const headers = new Headers()
      if (storedSession?.token) {
        headers.set('Authorization', `Bearer ${storedSession.token}`)
      }

      const response = await fetch(`${apiBaseUrl}/api/auth/session`, {
        headers,
        credentials: 'include',
        cache: 'no-store',
      })

      if (!response.ok) {
        if (response.status === 401) {
          clearStoredSession()
        }
        return
      }

      const restoredSession = await response.json() as AdminSession
      saveStoredSession(restoredSession)
      setSession(restoredSession)
    } catch (error) {
      setSession(null)
      setAuthError(error instanceof Error ? error.message : 'No fue posible restaurar la sesion.')
    } finally {
      setIsRestoring(false)
    }
  }

  async function touchSession() {
    if (!session) {
      return
    }

    const now = Date.now()
    if (sessionTouchInFlightRef.current || now - lastSessionTouchAtRef.current < sessionTouchThrottleMs) {
      return
    }

    lastSessionTouchAtRef.current = now
    sessionTouchInFlightRef.current = true

    try {
      const headers = new Headers()
      if (session.token) {
        headers.set('Authorization', `Bearer ${session.token}`)
      }

      const response = await fetch(`${apiBaseUrl}/api/auth/session`, {
        headers,
        credentials: 'include',
        cache: 'no-store',
      })

      if (response.status === 401) {
        clearStoredSession()
        setSession(null)
        return
      }

      if (response.ok) {
        const touchedSession = await response.json() as AdminSession
        saveStoredSession(touchedSession)
        setSession(touchedSession)
      }
    } catch {
      // The next user activity will retry the idle-session touch.
    } finally {
      sessionTouchInFlightRef.current = false
    }
  }

  async function login(input: LoginInput) {
    setAuthError(null)
    const response = await fetch(`${apiBaseUrl}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify(input),
    })

    if (!response.ok) {
      const payload = await safeJson(response)
      throw new Error(payload?.message ?? 'Credenciales invalidas.')
    }

    const createdSession = await response.json() as AdminSession
    saveStoredSession(createdSession)
    setSession(createdSession)
  }

  async function loginWithMicrosoft(returnToPath: string) {
    setAuthError(null)
    await startEntraLoginRedirect(returnToPath)
  }

  async function logout() {
    const headers = new Headers({ [csrfHeaderName]: session?.csrfToken ?? '' })
    if (session?.token) {
      headers.set('Authorization', `Bearer ${session.token}`)
    }

    await fetch(`${apiBaseUrl}/api/auth/logout`, {
      method: 'POST',
      headers,
      credentials: 'include',
    }).catch(() => undefined)

    setProfilePhotoUrl(null)
    clearStoredSession()
    setSession(null)
  }

  return {
    session,
    profilePhotoUrl,
    isRestoring,
    authError,
    setAuthError,
    isEntraConfigured,
    login,
    loginWithMicrosoft,
    logout,
  }
}

async function safeJson(response: Response) {
  try {
    return await response.json()
  } catch {
    return null
  }
}

function saveStoredSession(session: AdminSession) {
  if (!session.token) {
    return
  }

  window.sessionStorage.setItem(adminSessionStorageKey, JSON.stringify(session))
}

function readStoredSession(): AdminSession | null {
  const rawSession = window.sessionStorage.getItem(adminSessionStorageKey)
  if (!rawSession) {
    return null
  }

  try {
    const parsedSession = JSON.parse(rawSession) as AdminSession
    if (!parsedSession.token || new Date(parsedSession.expiresAtUtc).getTime() <= Date.now()) {
      clearStoredSession()
      return null
    }

    return parsedSession
  } catch {
    clearStoredSession()
    return null
  }
}

function clearStoredSession() {
  window.sessionStorage.removeItem(adminSessionStorageKey)
}
