import { useEffect, useState } from 'react'
import { AdminShell } from './components/AdminShell'
import { LoginPanel } from './components/LoginPanel'
import type { LoginMode } from './components/LoginPanel'
import { PulseLoader } from './components/PulseLoader'
import { tabCopy } from './constants'
import { useAdminSession } from './hooks/useAdminSession'
import { isTlSession, TransformationalLeaderDashboard } from './views/TransformationalLeaderDashboard'
import type { Tab } from './types'
import { canAccessTab, getAllowedTabs } from './utils/adminRoles'

const validTabs: Tab[] = ['overview', 'campaigns', 'agents', 'activity', 'responses', 'admins', 'lt', 'settings']

export default function App() {
  const {
    session,
    profilePhotoUrl,
    isRestoring,
    authError,
    isEntraConfigured,
    login,
    loginWithMicrosoft,
    logout,
  } = useAdminSession()
  const [location, setLocation] = useState(() => getCurrentLocation())
  const [routeTransition, setRouteTransition] = useState<{ title: string; caption: string } | null>(null)

  useEffect(() => {
    function handleLocationChange() {
      setLocation(getCurrentLocation())
    }

    window.addEventListener('popstate', handleLocationChange)
    window.addEventListener('pulsecheck:navigate', handleLocationChange)

    return () => {
      window.removeEventListener('popstate', handleLocationChange)
      window.removeEventListener('pulsecheck:navigate', handleLocationChange)
    }
  }, [])

  useEffect(() => {
    if (!routeTransition) return
    const timerId = window.setTimeout(() => setRouteTransition(null), 280)
    return () => window.clearTimeout(timerId)
  }, [location.hash, location.pathname, location.search, routeTransition])

  function startRouteTransition(title: string, caption: string) {
    setRouteTransition({ title, caption })
  }

  function navigate(path: string, options?: { replace?: boolean }) {
    navigateTo(path, options)
  }

  if (isRestoring) {
    return <PulseLoader title="Restaurando sesion" caption="Validando tu acceso administrativo" />
  }

  const routeElement = renderRoute({
    location,
    session,
    profilePhotoUrl,
    authError,
    isEntraConfigured,
    login,
    loginWithMicrosoft,
    logout,
    onRouteStart: startRouteTransition,
    navigate,
  })

  return (
    <>
      {routeElement}
      {routeTransition ? (
        <div className="pulse-loader-overlay" aria-live="polite" aria-busy="true">
          <PulseLoader title={routeTransition.title} caption={routeTransition.caption} fullScreen={false} />
        </div>
      ) : null}
    </>
  )
}

function LoginPage({
  error,
  isEntraConfigured,
  onLogin,
  onMicrosoftLogin,
  onRouteStart,
  navigate,
  location,
}: {
  error: string | null
  isEntraConfigured: boolean
  onLogin: (input: { email: string; password: string }) => Promise<void>
  onMicrosoftLogin: (returnToPath: string) => Promise<void>
  onRouteStart: (title: string, caption: string) => void
  navigate: (path: string, options?: { replace?: boolean }) => void
  location: AppLocation
}) {
  const targetPath = extractTargetPath(location.search)

  return (
    <LoginPanel
      error={error}
      isEntraConfigured={isEntraConfigured}
      onMicrosoftLogin={async (mode: LoginMode) => {
        const returnToPath = mode === 'tl' ? '/tl' : targetPath
        onRouteStart('Conectando con Microsoft', 'Iniciando autenticacion corporativa')
        await onMicrosoftLogin(returnToPath)
      }}
      onSubmit={async (input) => {
        onRouteStart('Validando credenciales', 'Preparando tu panel administrativo')
        await onLogin(input)
        navigate(targetPath, { replace: true })
      }}
    />
  )
}

function AuthCallbackPage({
  session,
  error,
  navigate,
}: {
  session: ReturnType<typeof useAdminSession>['session']
  error: string | null
  navigate: (path: string, options?: { replace?: boolean }) => void
}) {
  useEffect(() => {
    if (session) {
      navigate(session.returnToPath || (isTlSession(session) ? '/tl' : '/admin/overview'), { replace: true })
      return
    }

    if (error) {
      navigate('/login', { replace: true })
    }
  }, [error, navigate, session])

  if (session) {
    return <PulseLoader title="Abriendo panel" caption="Preparando tu sesion" />
  }

  if (error) {
    return <PulseLoader title="No se pudo validar el acceso" caption="Volviendo al inicio de sesion" />
  }

  return <PulseLoader title="Validando acceso corporativo" caption="Conectando con Microsoft Entra ID" />
}

function AdminPage({
  sessionTokenView,
  profilePhotoUrl,
  onLogout,
  onRouteStart,
  tab,
  navigate,
}: {
  sessionTokenView: NonNullable<ReturnType<typeof useAdminSession>['session']>
  profilePhotoUrl: string | null
  onLogout: () => Promise<void>
  onRouteStart: (title: string, caption: string) => void
  tab: string | undefined
  navigate: (path: string, options?: { replace?: boolean }) => void
}) {
  const parsedTab = parseTab(tab)
  const activeTab = canAccessTab(sessionTokenView.user.role, parsedTab)
    ? parsedTab
    : getAllowedTabs(sessionTokenView.user.role)[0] ?? 'overview'

  useEffect(() => {
    if (tab !== activeTab) {
      navigate(`/admin/${activeTab}`, { replace: true })
    }
  }, [activeTab, navigate, tab])

  return (
    <AdminShell
      session={sessionTokenView}
      profilePhotoUrl={profilePhotoUrl}
      activeTab={activeTab}
      onTabChange={(nextTab) => {
        if (nextTab === activeTab) return
        if (!canAccessTab(sessionTokenView.user.role, nextTab)) return
        onRouteStart('Cambiando de vista', `Abriendo ${tabCopy[nextTab].title.toLowerCase()}`)
        navigate(`/admin/${nextTab}`)
      }}
      onLogout={async () => {
        onRouteStart('Cerrando sesion', 'Volviendo al acceso principal')
        await onLogout()
        navigate('/login', { replace: true })
      }}
    />
  )
}

function parseTab(tab: string | undefined): Tab {
  if (!tab) return 'overview'
  return validTabs.includes(tab as Tab) ? (tab as Tab) : 'overview'
}

function extractTargetPath(search: string): string {
  const from = new URLSearchParams(search).get('from')
  if (from && from.startsWith('/admin/')) {
    return from
  }

  if (from === '/tl') {
    return from
  }

  return '/admin/overview'
}

type AppLocation = {
  pathname: string
  search: string
  hash: string
}

type RenderRouteArgs = {
  location: AppLocation
  session: ReturnType<typeof useAdminSession>['session']
  profilePhotoUrl: string | null
  authError: string | null
  isEntraConfigured: boolean
  login: (input: { email: string; password: string }) => Promise<void>
  loginWithMicrosoft: (returnToPath: string) => Promise<void>
  logout: () => Promise<void>
  onRouteStart: (title: string, caption: string) => void
  navigate: (path: string, options?: { replace?: boolean }) => void
}

function renderRoute({
  location,
  session,
  profilePhotoUrl,
  authError,
  isEntraConfigured,
  login,
  loginWithMicrosoft,
  logout,
  onRouteStart,
  navigate,
}: RenderRouteArgs) {
  const path = normalizePath(location.pathname)

  if (path === '/' || path === '/login') {
    if (session) {
      return (
        <RedirectTo
          path={isTlSession(session) ? '/tl' : '/admin/overview'}
          navigate={navigate}
          title="Abriendo panel"
          caption="Preparando tu sesion"
        />
      )
    }

    return (
      <LoginPage
        error={authError}
        isEntraConfigured={isEntraConfigured}
        onLogin={login}
        onMicrosoftLogin={loginWithMicrosoft}
        onRouteStart={onRouteStart}
        navigate={navigate}
        location={location}
      />
    )
  }

  if (path === '/auth/callback') {
    return <AuthCallbackPage session={session} error={authError} navigate={navigate} />
  }

  if (path === '/admin') {
    if (!session) {
      return (
        <RedirectTo
          path={buildLoginRedirect(location)}
          navigate={navigate}
          title="Validando sesion"
          caption="Redirigiendo al inicio de sesion"
        />
      )
    }

    return (
      <RedirectTo
        path={isTlSession(session) ? '/tl' : '/admin/overview'}
        navigate={navigate}
        title="Abriendo panel"
        caption="Preparando tu sesion"
      />
    )
  }

  if (path === '/tl') {
    if (!session) {
      return (
        <RedirectTo
          path={buildLoginRedirect(location)}
          navigate={navigate}
          title="Validando sesion"
          caption="Redirigiendo al inicio de sesion"
        />
      )
    }

    if (!isTlSession(session)) {
      return (
        <RedirectTo
          path="/admin/overview"
          navigate={navigate}
          title="Validando acceso"
          caption="Abriendo panel administrativo"
        />
      )
    }

    return (
      <TransformationalLeaderDashboard
        session={session}
        onLogout={async () => {
          onRouteStart('Cerrando sesion', 'Volviendo al acceso principal')
          await logout()
          navigate('/login', { replace: true })
        }}
      />
    )
  }

  const adminTab = getAdminTab(path)
  if (adminTab) {
    if (!session) {
      return (
        <RedirectTo
          path={buildLoginRedirect(location)}
          navigate={navigate}
          title="Validando sesion"
          caption="Redirigiendo al inicio de sesion"
        />
      )
    }

    if (isTlSession(session)) {
      return (
        <RedirectTo
          path="/tl"
          navigate={navigate}
          title="Validando acceso"
          caption="Abriendo vista TL"
        />
      )
    }

    return (
      <AdminPage
        sessionTokenView={session}
        profilePhotoUrl={profilePhotoUrl}
        onLogout={logout}
        onRouteStart={onRouteStart}
        tab={adminTab}
        navigate={navigate}
      />
    )
  }

  return <RedirectTo path="/" navigate={navigate} title="Redirigiendo" caption="Volviendo al inicio" />
}

function RedirectTo({
  path,
  navigate,
  title,
  caption,
}: {
  path: string
  navigate: (path: string, options?: { replace?: boolean }) => void
  title: string
  caption: string
}) {
  useEffect(() => {
    navigate(path, { replace: true })
  }, [navigate, path])

  return <PulseLoader title={title} caption={caption} />
}

function getCurrentLocation(): AppLocation {
  return {
    pathname: window.location.pathname,
    search: window.location.search,
    hash: window.location.hash,
  }
}

function navigateTo(path: string, options?: { replace?: boolean }) {
  const target = new URL(path, window.location.origin)
  const current = `${window.location.pathname}${window.location.search}${window.location.hash}`
  const next = `${target.pathname}${target.search}${target.hash}`

  if (next === current) return

  if (options?.replace) {
    window.history.replaceState(null, '', next)
  } else {
    window.history.pushState(null, '', next)
  }

  window.dispatchEvent(new Event('pulsecheck:navigate'))
}

function normalizePath(pathname: string): string {
  if (!pathname || pathname === '/') return '/'
  return pathname.replace(/\/+$/, '')
}

function getAdminTab(pathname: string): string | undefined {
  const match = /^\/admin\/([^/]+)$/.exec(pathname)
  return match?.[1]
}

function buildLoginRedirect(location: AppLocation): string {
  const from = `${location.pathname}${location.search}${location.hash}`
  return `/login?from=${encodeURIComponent(from)}`
}
