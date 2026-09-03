import { useState } from 'react'
import type { FormEvent } from 'react'
import { agentDownloadUrl, isAgentDownloadConfigured } from '../constants'
import { PulseMark } from './PulseMark'

type LoginPanelProps = {
  error: string | null
  isEntraConfigured: boolean
  onSubmit: (input: { email: string; password: string }) => Promise<void>
  onMicrosoftLogin: (mode: LoginMode) => Promise<void>
}

export type LoginMode = 'admin' | 'tl'

export function LoginPanel({ error, isEntraConfigured, onSubmit, onMicrosoftLogin }: LoginPanelProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [mode, setMode] = useState<LoginMode>('admin')

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    try {
      setIsSubmitting(true)
      await onSubmit({ email, password })
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleMicrosoftLogin() {
    try {
      setIsSubmitting(true)
      await onMicrosoftLogin(mode)
    } finally {
      setIsSubmitting(false)
    }
  }

  const isTransformationalLeader = mode === 'tl'

  return (
    <main className="login-redesign-shell">
      <section className="login-redesign-product" aria-label="Vista de producto PulseCheck">
        <div className="login-redesign-brand">
          <PulseMark className="login-redesign-brand__mark" />
          <span>PulseCheck</span>
        </div>

        <div className="login-redesign-scene" aria-hidden="true">
          <div className="login-redesign-mini login-redesign-mini--scale">
            <div className="login-redesign-mini__label">
              <span>Escala</span>
              <span>1/5</span>
            </div>
            <strong>Que tal estuvo tu energia hoy?</strong>
            <div className="login-redesign-scale">
              <span>1</span>
              <span>2</span>
              <span className="login-redesign-scale__active">3</span>
              <span>4</span>
              <span>5</span>
            </div>
          </div>

          <div className="login-redesign-agent">
            <div className="login-redesign-agent__brand">PulseCheck by Solvo</div>
            <div className="login-redesign-agent__title">Check-in operativo</div>
            <div className="login-redesign-agent__meta">Pregunta 2 de 3</div>
            <div className="login-redesign-agent__question">Como esta tu carga de trabajo ahora?</div>
            <div className="login-redesign-choice">🙂 Controlada</div>
            <div className="login-redesign-choice login-redesign-choice--selected">🔥 Alta manejable</div>
            <div className="login-redesign-choice">⚠️ Necesito apoyo</div>
            <div className="login-redesign-choice">💬 Prefiero hablarlo</div>
          </div>

          <div className="login-redesign-mini login-redesign-mini--text">
            <div className="login-redesign-mini__label">
              <span>Respuesta abierta</span>
              <span>3/3</span>
            </div>
            <strong>Que bloqueo deberiamos revisar?</strong>
            <div className="login-redesign-lines">
              <i />
              <i />
              <i />
            </div>
          </div>
        </div>
      </section>

      <section className="login-redesign-access" aria-label="Inicio de sesion administrativo">
        <div className="login-redesign-panel">
          <PulseMark className="login-redesign-mark" />

          <h1>Iniciar sesion</h1>

          <div className="login-redesign-mode" role="tablist" aria-label="Tipo de acceso">
            <button
              type="button"
              role="tab"
              aria-selected={mode === 'admin'}
              className={mode === 'admin' ? 'login-redesign-mode__item login-redesign-mode__item--active' : 'login-redesign-mode__item'}
              disabled={isSubmitting}
              onClick={() => setMode('admin')}
            >
              Admin
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={mode === 'tl'}
              className={mode === 'tl' ? 'login-redesign-mode__item login-redesign-mode__item--active' : 'login-redesign-mode__item'}
              disabled={isSubmitting}
              onClick={() => setMode('tl')}
            >
              Transformational Leader
            </button>
          </div>

          {isEntraConfigured || isTransformationalLeader ? (
            <button
              className="login-redesign-microsoft"
              type="button"
              disabled={isSubmitting || (isTransformationalLeader && !isEntraConfigured)}
              onClick={handleMicrosoftLogin}
            >
              <span className="login-redesign-microsoft__logo" aria-hidden="true">
                <i />
                <i />
                <i />
                <i />
              </span>
              <span>{isSubmitting ? 'Signing in...' : 'Login with Microsoft'}</span>
            </button>
          ) : null}

          {!isEntraConfigured && isTransformationalLeader ? (
            <p className="login-redesign-error">El acceso de Transformational Leader requiere Microsoft Entra configurado.</p>
          ) : null}

          {!isEntraConfigured && !isTransformationalLeader ? (
            <form className="login-redesign-form" onSubmit={handleSubmit}>
              <label>
                <span>Correo</span>
                <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" required placeholder="admin@empresa.com" />
              </label>
              <label>
                <span>Contrasena</span>
                <input value={password} onChange={(event) => setPassword(event.target.value)} type="password" required placeholder="Tu contrasena" />
              </label>
              <button className="login-redesign-submit" type="submit" disabled={isSubmitting}>
                {isSubmitting ? 'Ingresando...' : 'Entrar al panel'}
              </button>
            </form>
          ) : (
            null
          )}

          {error ? <p className="login-redesign-error">{error}</p> : null}

          <div className="login-redesign-download">
            <div>
              <h2>Agente de escritorio</h2>
              <p>Instalador Windows para equipos autorizados. Incluye actualizaciones automaticas.</p>
            </div>
            {isAgentDownloadConfigured ? (
              <a href={agentDownloadUrl}>Descargar agente</a>
            ) : (
              <button type="button" disabled>Instalador pendiente</button>
            )}
          </div>
        </div>
      </section>
    </main>
  )
}
