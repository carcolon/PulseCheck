import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useEffect, useMemo, useRef, useState } from 'react'
import { CampaignOptionRow, formatCampaignWeeks, MultiSelectColumn, QuestionAnswers, ResultsModal, WeekMultiSelect } from '../components/tl/TlDashboardComponents'
import { PrimaryButton, SoftButton } from '../components/ui'
import { apiBaseUrl } from '../constants'
import type { AdminSession, TlDashboard, TlExportJob, TlSession } from '../types'

type TlDashboardRequest = {
  weekIds: string[]
  campaignIds: string[]
  answerFilters: { questionId: string; values: string[] }[]
}

type TransformationalLeaderDashboardProps = {
  session: TlSession
  onLogout: () => Promise<void>
}

export function isTlSession(session: AdminSession | null): session is TlSession {
  return session?.user.role === 'TransformationalLeader' && Boolean(session.solvoId && (session.operation || session.operations?.length))
}

export function TransformationalLeaderDashboard({ session, onLogout }: TransformationalLeaderDashboardProps) {
  const sessionStartedAtRef = useRef(Date.now())
  const [dashboard, setDashboard] = useState<TlDashboard | null>(null)
  const [modalDashboard, setModalDashboard] = useState<TlDashboard | null>(null)
  const [selectedWeeks, setSelectedWeeks] = useState<string[]>([])
  const [selectedCampaigns, setSelectedCampaigns] = useState<string[]>([])
  const [selectedAnswers, setSelectedAnswers] = useState<Record<string, string[]>>({})
  const [campaignSearch, setCampaignSearch] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isViewing, setIsViewing] = useState(false)
  const [isCreatingExport, setIsCreatingExport] = useState(false)
  const [exportJobs, setExportJobs] = useState<TlExportJob[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    void loadDashboard({ weekIds: [], campaignIds: [], answerFilters: [] }, { initial: true })
    void loadExportJobs()
  }, [session.token])

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/tl-notifications`, {
        accessTokenFactory: () => session.token,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('tlExportUpdated', (job: TlExportJob) => {
      setExportJobs((current) => upsertExportJob(current, job))
    })

    connection.start().catch(() => {
      void loadExportJobs()
    })

    return () => {
      connection.stop().catch(() => undefined)
    }
  }, [session.token])

  useEffect(() => {
    if (!exportJobs.some((job) => job.status === 'Pending' || job.status === 'Processing')) {
      return
    }

    const timer = window.setInterval(() => {
      void loadExportJobs()
    }, 8000)

    return () => window.clearInterval(timer)
  }, [exportJobs, session.token])

  const availableCampaigns = useMemo(() => {
    if (!dashboard) return []
    const search = campaignSearch.trim().toLowerCase()
    const weekSet = new Set(selectedWeeks)
    return dashboard.campaigns
      .filter((campaign) => selectedWeeks.length === 0 || campaign.weekIds.some((weekId) => weekSet.has(weekId)))
      .filter((campaign) => !search || campaign.name.toLowerCase().includes(search))
  }, [campaignSearch, dashboard, selectedWeeks])

  const selectedCampaignOptions = useMemo(() => {
    const campaignSet = new Set(selectedCampaigns)
    return selectedCampaigns.length === 0
      ? availableCampaigns
      : availableCampaigns.filter((campaign) => campaignSet.has(campaign.id))
  }, [availableCampaigns, selectedCampaigns])

  const sessionExportJobs = useMemo(() => {
    const sessionStartedAt = sessionStartedAtRef.current - 60000
    return exportJobs.filter((job) => {
      const createdAt = new Date(job.createdAtUtc).getTime()
      return Number.isNaN(createdAt) || createdAt >= sessionStartedAt
    })
  }, [exportJobs])
  const runningExportJob = sessionExportJobs.find((job) => isRunningExportStatus(job.status))
  const visibleExportJob = runningExportJob ?? sessionExportJobs.find((job) => job.status === 'Completed' || job.status === 'Failed')
  const exportInProgress = isCreatingExport || Boolean(runningExportJob)

  async function loadDashboard(filters: TlDashboardRequest, options?: { initial?: boolean }) {
    try {
      setError(null)
      if (options?.initial) {
        setIsLoading(true)
      } else {
        setIsViewing(true)
      }

      const response = await fetch(`${apiBaseUrl}/api/tl/dashboard`, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${session.token}`,
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify(filters),
      })

      if (!response.ok) {
        const payload = await safeJson(response)
        throw new Error(payload?.message ?? 'No fue posible cargar la vista de Transformational Leader.')
      }

      const payload = await response.json() as TlDashboard
      if (options?.initial) {
        setDashboard(payload)
      } else {
        setModalDashboard(payload)
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No fue posible cargar la vista de Transformational Leader.')
    } finally {
      setIsLoading(false)
      setIsViewing(false)
    }
  }

  async function loadExportJobs() {
    try {
      const response = await fetch(`${apiBaseUrl}/api/tl/exports`, {
        headers: { Authorization: `Bearer ${session.token}` },
        credentials: 'include',
      })

      if (response.ok) {
        setExportJobs(await response.json() as TlExportJob[])
      }
    } catch {
      // Export notifications are best-effort; polling will retry while the view is open.
    }
  }

  async function createExport() {
    try {
      setError(null)
      setIsCreatingExport(true)
      const response = await fetch(`${apiBaseUrl}/api/tl/exports`, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${session.token}`,
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify(buildRequest(selectedWeeks, selectedCampaigns, selectedAnswers)),
      })

      if (!response.ok) {
        const payload = await safeJson(response)
        throw new Error(payload?.message ?? 'No fue posible iniciar el export.')
      }

      const job = await response.json() as TlExportJob
      setExportJobs((current) => upsertExportJob(current, job))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No fue posible iniciar el export.')
    } finally {
      setIsCreatingExport(false)
    }
  }

  async function dismissExport(jobId: string) {
    setExportJobs((current) => current.filter((job) => job.id !== jobId))
    await fetch(`${apiBaseUrl}/api/tl/exports/${jobId}/dismiss`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${session.token}` },
      credentials: 'include',
    }).catch(() => undefined)
  }

  async function downloadExport(job: TlExportJob) {
    const response = await fetch(`${apiBaseUrl}/api/tl/exports/${job.id}/download`, {
      headers: { Authorization: `Bearer ${session.token}` },
      credentials: 'include',
    })

    if (!response.ok) {
      setError('No fue posible descargar el export. Intenta generarlo de nuevo.')
      return
    }

    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = job.fileName || `tl-responses-${new Date().toISOString().slice(0, 10)}.xlsx`
    link.click()
    URL.revokeObjectURL(url)
    setExportJobs((current) => current.filter((item) => item.id !== job.id))
  }

  function handleView() {
    void loadDashboard(buildRequest(selectedWeeks, selectedCampaigns, selectedAnswers), { initial: false })
  }

  if (isLoading) {
    return (
      <main className="tl-shell">
        <div className="tl-loading">Cargando vista TL...</div>
      </main>
    )
  }

  return (
    <main className="tl-shell">
      <header className="tl-topbar">
        <div>
          <p>PulseCheck TL</p>
          <h1>Welcome {dashboard?.displayName || session.user.displayName}</h1>
        </div>
        <div className="tl-topbar__session">
          <span>{dashboard?.operation || formatSessionOperations(session)}</span>
          <SoftButton type="button" onClick={() => void onLogout()}>Salir</SoftButton>
        </div>
      </header>

      {error ? <div className="tl-error">{error}</div> : null}
      {visibleExportJob ? (
        <TlExportToast
          job={visibleExportJob}
          onDownload={() => void downloadExport(visibleExportJob)}
          onDismiss={() => void dismissExport(visibleExportJob.id)}
        />
      ) : null}

      <section className="tl-workspace" aria-label="Filtros de respuestas">
        <div className="tl-workspace__header">
          <div>
            <p>Modulo TL</p>
            <h2>Respuestas por encuesta</h2>
          </div>
          <PrimaryButton type="button" onClick={handleView} disabled={isViewing || !dashboard}>
            {isViewing ? 'Cargando...' : 'View'}
          </PrimaryButton>
        </div>

        <div className="tl-filter-grid">
          <MultiSelectColumn title="Week" emptyText="No hay semanas disponibles.">
            <WeekMultiSelect
              weeks={dashboard?.weeks ?? []}
              selectedWeeks={selectedWeeks}
              onToggle={(weekId) => setSelectedWeeks((current) => toggleValue(current, weekId))}
            />
          </MultiSelectColumn>

          <MultiSelectColumn title="Encuesta" emptyText="No hay encuestas para la operacion seleccionada.">
            <label className="tl-campaign-search">
              <span className="sr-only">Buscar campana</span>
              <input
                value={campaignSearch}
                onChange={(event) => setCampaignSearch(event.target.value)}
                placeholder="Buscar campaña"
              />
            </label>
            {availableCampaigns.map((campaign) => (
              <CampaignOptionRow
                key={campaign.id}
                campaign={campaign}
                caption={formatCampaignWeeks(campaign, dashboard?.weeks ?? [])}
                checked={selectedCampaigns.includes(campaign.id)}
                onChange={() => setSelectedCampaigns((current) => toggleValue(current, campaign.id))}
              />
            ))}
          </MultiSelectColumn>

          <MultiSelectColumn title="Respuesta" emptyText="Selecciona una encuesta o revisa todas las respuestas disponibles.">
            {selectedCampaignOptions.flatMap((campaign) => campaign.questions.map((question) => (
              <QuestionAnswers
                key={`${campaign.id}-${question.id}`}
                campaign={campaign}
                question={question}
                selectedValues={selectedAnswers[question.id] ?? []}
                onToggle={(value) => {
                  setSelectedAnswers((current) => ({
                    ...current,
                    [question.id]: toggleValue(current[question.id] ?? [], value),
                  }))
                }}
              />
            )))}
          </MultiSelectColumn>
        </div>
      </section>

      {modalDashboard ? (
        <ResultsModal
          dashboard={modalDashboard}
          selectedWeeks={selectedWeeks}
          selectedCampaigns={selectedCampaigns}
          selectedAnswers={selectedAnswers}
          onClose={() => setModalDashboard(null)}
          exportLabel={exportInProgress ? <LoadingLabel label={isCreatingExport ? 'Preparando...' : 'Generando...'} /> : 'Exportar'}
          exportDisabled={exportInProgress}
          onExport={createExport}
        />
      ) : null}
    </main>
  )
}

function formatSessionOperations(session: TlSession) {
  return session.operations?.length ? session.operations.join(', ') : session.operation
}

function buildRequest(weekIds: string[], campaignIds: string[], selectedAnswers: Record<string, string[]>): TlDashboardRequest {
  return {
    weekIds,
    campaignIds,
    answerFilters: Object.entries(selectedAnswers)
      .filter(([, values]) => values.length > 0)
      .map(([questionId, values]) => ({ questionId, values })),
  }
}

function toggleValue(values: string[], value: string) {
  return values.includes(value)
    ? values.filter((item) => item !== value)
    : [...values, value]
}

function upsertExportJob(jobs: TlExportJob[], job: TlExportJob) {
  return [job, ...jobs.filter((item) => item.id !== job.id)]
    .sort((left, right) => new Date(right.createdAtUtc).getTime() - new Date(left.createdAtUtc).getTime())
}

function isRunningExportStatus(status: string) {
  return status === 'Pending' || status === 'Processing'
}

function LoadingLabel({ label }: { label: string }) {
  return (
    <span className="tl-loading-label">
      <span className="tl-spinner" aria-hidden="true" />
      {label}
    </span>
  )
}

function TlExportToast({
  job,
  onDownload,
  onDismiss,
}: {
  job: TlExportJob
  onDownload: () => void
  onDismiss: () => void
}) {
  const failed = job.status === 'Failed'
  const running = isRunningExportStatus(job.status)
  return (
    <div className={`${failed ? 'tl-export-toast tl-export-toast--failed' : 'tl-export-toast'} ${running ? 'tl-export-toast--running' : ''}`} role="status" aria-live="polite">
      <div>
        <strong>
          {running ? <LoadingLabel label="Generando tu archivo" /> : failed ? 'No se pudo generar el export' : 'Tu export está listo'}
        </strong>
        <span>
          {running
            ? 'Puedes seguir navegando. Te avisaremos cuando la descarga esté lista.'
            : failed
              ? job.error || 'Intenta generarlo de nuevo.'
              : `${job.responseCount} respuestas listas para descargar.`}
        </span>
      </div>
      <div className="tl-export-toast__actions">
        {!failed && !running ? <PrimaryButton type="button" size="small" onClick={onDownload}>Descargar</PrimaryButton> : null}
        {!running ? <SoftButton type="button" size="small" onClick={onDismiss}>Cerrar</SoftButton> : null}
      </div>
    </div>
  )
}

async function safeJson(response: Response) {
  try {
    return await response.json()
  } catch {
    return null
  }
}
