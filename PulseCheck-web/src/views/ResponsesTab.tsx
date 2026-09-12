import { useMemo, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import SearchOutlinedIcon from '@mui/icons-material/SearchOutlined'
import CalendarMonthOutlinedIcon from '@mui/icons-material/CalendarMonthOutlined'
import FileDownloadOutlinedIcon from '@mui/icons-material/FileDownloadOutlined'
import CloseOutlinedIcon from '@mui/icons-material/CloseOutlined'
import ChevronLeftOutlinedIcon from '@mui/icons-material/ChevronLeftOutlined'
import ChevronRightOutlinedIcon from '@mui/icons-material/ChevronRightOutlined'
import type { Campaign, CampaignStatus, ResponseItem } from '../types'
import { formatResponseAnswer } from '../utils/responses'
import { isAllOperationsAudience, parseAudienceOperations } from '../utils/campaigns'
import { PrimaryButton, SoftButton } from '../components/ui'
import type { ReportExportFilters } from '../hooks/useResponsesDomain'

type ResponsesTabProps = {
  campaigns: Campaign[]
  responses: ResponseItem[]
  operations: string[]
  isExportingReport: boolean
  onExportReport: (filters?: ReportExportFilters) => void
}

type CampaignSummary = {
  campaign: Campaign
  responseCount: number
  lastResponseAtUtc: string | null
}

const statusLabels: Record<CampaignStatus, string> = {
  Active: 'Activa',
  Paused: 'Pausada',
  Draft: 'Borrador',
}
const deletedStatusLabel = 'Eliminada'
const responsesPageSize = 50

export function ResponsesTab({
  campaigns,
  responses,
  operations,
  isExportingReport,
  onExportReport,
}: ResponsesTabProps) {
  const [campaignSearch, setCampaignSearch] = useState('')
  const [operationFilter, setOperationFilter] = useState('')
  const [dateFilter, setDateFilter] = useState('')
  const [selectedCampaignId, setSelectedCampaignId] = useState<string | null>(null)
  const dateInputRef = useRef<HTMLInputElement | null>(null)

  const responsesByCampaign = useMemo(() => {
    const map = new Map<string, ResponseItem[]>()
    for (const response of responses) {
      map.set(response.campaignId, [...(map.get(response.campaignId) ?? []), response])
    }

    for (const items of map.values()) {
      items.sort((left, right) => new Date(right.answeredAtUtc).getTime() - new Date(left.answeredAtUtc).getTime())
    }

    return map
  }, [responses])

  const campaignSummaries = useMemo(() => {
    const search = campaignSearch.trim().toLowerCase()
    return campaigns
      .filter((campaign) => {
        if (search && !campaign.name.toLowerCase().includes(search)) {
          return false
        }

        if (operationFilter && !campaignMatchesOperation(campaign, operationFilter)) {
          return false
        }

        if (dateFilter && formatDateInput(campaign.createdAtUtc) !== dateFilter) {
          return false
        }

        return true
      })
      .map((campaign): CampaignSummary => {
        const campaignResponses = responsesByCampaign.get(campaign.id) ?? []
        return {
          campaign,
          responseCount: campaignResponses.length,
          lastResponseAtUtc: campaignResponses[0]?.answeredAtUtc ?? null,
        }
      })
      .sort((left, right) => {
        const statusRank = getCampaignStatusRank(right.campaign) - getCampaignStatusRank(left.campaign)
        if (statusRank !== 0) return statusRank
        return (new Date(right.lastResponseAtUtc ?? right.campaign.updatedAtUtc).getTime()) - (new Date(left.lastResponseAtUtc ?? left.campaign.updatedAtUtc).getTime())
      })
  }, [campaignSearch, campaigns, dateFilter, operationFilter, responsesByCampaign])

  const selectedCampaign = useMemo(
    () => campaigns.find((campaign) => campaign.id === selectedCampaignId) ?? null,
    [campaigns, selectedCampaignId],
  )

  const filteredResponses = useMemo(
    () => campaignSummaries.flatMap((summary) => responsesByCampaign.get(summary.campaign.id) ?? []),
    [campaignSummaries, responsesByCampaign],
  )

  const selectedCampaignResponses = useMemo(
    () => selectedCampaign ? responsesByCampaign.get(selectedCampaign.id) ?? [] : [],
    [responsesByCampaign, selectedCampaign],
  )

  const hasFilters = Boolean(campaignSearch.trim() || operationFilter || dateFilter)
  const deletedCount = campaigns.filter((campaign) => campaign.deletedAtUtc).length
  const activeCount = campaigns.filter((campaign) => !campaign.deletedAtUtc && campaign.status === 'Active').length
  const inactiveCount = campaigns.filter((campaign) => !campaign.deletedAtUtc && campaign.status !== 'Active').length

  function clearFilters() {
    setCampaignSearch('')
    setOperationFilter('')
    setDateFilter('')
  }

  function openDatePicker() {
    const input = dateInputRef.current
    if (!input) {
      return
    }

    input.focus()
    ;(input as HTMLInputElement & { showPicker?: () => void }).showPicker?.()
  }

  return (
    <section className="mt-4 grid gap-4 font-sans">
      <div className="rounded-[22px] border border-[#cfe1e8] bg-white p-5 shadow-[0_18px_44px_rgba(9,55,69,0.08)]">
        <div className="grid gap-3 rounded-[18px] border border-[#d7e8ee] bg-[#f8fcfd] p-4 lg:grid-cols-[minmax(240px,1.2fr)_180px_minmax(190px,0.8fr)_48px_auto] lg:items-center">
          <label className="relative">
            <span className="sr-only">Buscar campaña</span>
            <input
              value={campaignSearch}
              onChange={(event) => setCampaignSearch(event.target.value)}
              className="h-12 w-full rounded-xl border border-[#c6dce4] bg-white px-4 pr-11 text-sm text-[#17313c] outline-none transition placeholder:text-[#8da0aa] focus:border-[#008aab] focus:ring-4 focus:ring-[#dff5f8]"
              placeholder="Buscar campaña por nombre"
            />
            <SearchOutlinedIcon className="pointer-events-none absolute right-3 top-3 text-[#607985]" fontSize="small" />
          </label>

          <label className="relative">
            <span className="sr-only">Fecha</span>
            <input
              ref={dateInputRef}
              type="date"
              value={dateFilter}
              onChange={(event) => setDateFilter(event.target.value)}
              className="h-12 w-full rounded-xl border border-[#c6dce4] bg-white px-4 pr-11 text-sm text-[#17313c] outline-none transition focus:border-[#008aab] focus:ring-4 focus:ring-[#dff5f8] [&::-webkit-calendar-picker-indicator]:hidden [&::-webkit-calendar-picker-indicator]:appearance-none"
            />
            <button
              type="button"
              onClick={openDatePicker}
              className="absolute right-2 top-2 grid h-8 w-8 place-items-center rounded-lg text-[#00758d] transition hover:bg-[#eff9fc] hover:text-[#005f73]"
              aria-label="Abrir selector de fecha"
            >
              <CalendarMonthOutlinedIcon fontSize="small" />
            </button>
          </label>

          <label>
            <span className="sr-only">Operacion</span>
            <select
              value={operationFilter}
              onChange={(event) => setOperationFilter(event.target.value)}
              className="h-12 w-full rounded-xl border border-[#c6dce4] bg-white px-4 text-sm font-semibold text-[#17313c] outline-none transition focus:border-[#008aab] focus:ring-4 focus:ring-[#dff5f8]"
            >
              <option value="">Operacion</option>
              {operations.map((operation) => (
                <option key={operation} value={operation}>{operation}</option>
              ))}
            </select>
          </label>

          <button
            type="button"
            onClick={clearFilters}
            disabled={!hasFilters}
            className="grid h-12 w-12 place-items-center rounded-full border border-[#c6dce4] bg-white text-[#607985] transition hover:border-[#008aab] hover:bg-[#eff9fc] hover:text-[#00758d] disabled:cursor-not-allowed disabled:border-[#d9e5ea] disabled:bg-[#f4f7f8] disabled:text-[#b1c0c7]"
            aria-label="Limpiar filtros"
            title="Limpiar filtros"
          >
            <CloseOutlinedIcon />
          </button>

          <button
            type="button"
            onClick={() => onExportReport(buildExportFilters({ campaignSearch, operation: operationFilter, date: dateFilter }))}
            disabled={isExportingReport || filteredResponses.length === 0}
            className="inline-flex h-12 items-center justify-center gap-2 rounded-xl bg-[#008aab] px-5 text-sm font-bold text-white shadow-[0_12px_24px_rgba(0,138,171,0.22)] transition hover:bg-[#00758d] disabled:cursor-not-allowed disabled:bg-[#9eb7bf] disabled:shadow-none"
          >
            <FileDownloadOutlinedIcon fontSize="small" />
            {isExportingReport ? 'Exportando...' : 'Exportar'}
          </button>
        </div>

        <div className="mt-5 flex flex-wrap items-center gap-2">
          <span className="rounded-full bg-[#e8edf0] px-4 py-2 text-sm font-bold text-[#17313c]">{campaigns.length} campañas</span>
          <span className="rounded-full bg-[#e8fbfd] px-4 py-2 text-sm font-bold text-[#00758d]">{activeCount} activas</span>
          <span className="rounded-full bg-[#f1f4f5] px-4 py-2 text-sm font-bold text-[#607985]">{inactiveCount} no activas</span>
          {deletedCount > 0 ? (
            <span className="rounded-full bg-[#fff4ed] px-4 py-2 text-sm font-bold text-[#9a5a00]">{deletedCount} eliminada{deletedCount === 1 ? '' : 's'}</span>
          ) : null}
          <span className="rounded-full bg-[#e8edf0] px-4 py-2 text-sm font-bold text-[#17313c]">{filteredResponses.length} respuestas en vista</span>
        </div>
      </div>

      <article className="rounded-[22px] border border-[#cfe1e8] bg-white p-5 shadow-[0_18px_44px_rgba(9,55,69,0.08)]">
        <div className="mb-4 flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.16em] text-[#00758d]">Campañas</p>
            <h3 className="mt-1 text-xl font-bold text-[#0d3140]">{hasFilters ? 'Resultados filtrados' : 'Estado de campañas'}</h3>
          </div>
          <span className="w-fit rounded-full bg-[#e8edf0] px-4 py-2 text-sm font-bold text-[#17313c]">
            {campaignSummaries.length} en vista
          </span>
        </div>

        {campaignSummaries.length === 0 ? (
          <div className="rounded-2xl border border-[#d7e8ee] bg-[#f8fcfd] px-4 py-10 text-center text-sm text-[#607985]">
            No hay campañas que coincidan con los filtros.
          </div>
        ) : (
          <div className="grid gap-3">
            {campaignSummaries.map(({ campaign, responseCount, lastResponseAtUtc }) => (
              <button
                key={campaign.id}
                type="button"
                onClick={() => setSelectedCampaignId(campaign.id)}
                className="grid gap-3 rounded-2xl border border-[#d7e8ee] bg-[#f8fcfd] px-4 py-4 text-left transition hover:border-[#9bd3db] hover:bg-[#f1fbfd] lg:grid-cols-[minmax(240px,1.3fr)_minmax(180px,1fr)_120px_160px] lg:items-center"
              >
                <div className="min-w-0">
                  <p className="truncate text-sm font-bold text-[#17313c]">{campaign.name}</p>
                  <p className="mt-1 truncate text-xs text-[#607985]">{formatAudienceLabel(campaign)}</p>
                </div>
                <p className="text-xs text-[#607985]">
                  Actualizada {formatDateTime(campaign.updatedAtUtc)}
                </p>
                <span className={getCampaignStatusClassName(campaign)}>
                  {getCampaignStatusLabel(campaign)}
                </span>
                <div className="text-sm font-bold text-[#17313c]">
                  {responseCount} respuesta{responseCount === 1 ? '' : 's'}
                  <p className="mt-1 text-xs font-semibold text-[#607985]">
                    {lastResponseAtUtc ? formatDateTime(lastResponseAtUtc) : 'Sin respuestas'}
                  </p>
                </div>
              </button>
            ))}
          </div>
        )}
      </article>

      {selectedCampaign && typeof document !== 'undefined'
        ? createPortal(
            <div className="admin-modal-overlay" role="dialog" aria-modal="true">
          <div className="admin-modal-shell responses-detail-modal w-full max-w-[1180px] overflow-hidden rounded-[24px] border border-[#cfe1e8] bg-white shadow-[0_24px_70px_rgba(5,31,42,0.28)]">
            <div className="flex flex-col gap-4 border-b border-[#d8e8ee] px-6 py-5 lg:flex-row lg:items-start lg:justify-between">
              <div className="min-w-0">
                <p className="text-xs font-semibold uppercase tracking-[0.16em] text-[#00758d]">Detalle de campaña</p>
                <h3 className="mt-1 truncate text-xl font-bold text-[#0d3140]">{selectedCampaign.name}</h3>
                <p className="mt-1 text-sm text-[#607985]">{formatAudienceLabel(selectedCampaign)} · {getCampaignStatusLabel(selectedCampaign)}</p>
              </div>
              <div className="flex flex-wrap gap-2">
                <PrimaryButton
                  type="button"
                  onClick={() => onExportReport(buildExportFilters({ campaignId: selectedCampaign.id, operation: operationFilter, date: dateFilter }))}
                  disabled={isExportingReport || selectedCampaignResponses.length === 0}
                >
                  {isExportingReport ? 'Exportando...' : 'Exportar'}
                </PrimaryButton>
                <SoftButton type="button" onClick={() => setSelectedCampaignId(null)}>
                  <CloseOutlinedIcon fontSize="small" />
                  Cerrar
                </SoftButton>
              </div>
            </div>

            <div className="admin-modal-body responses-detail-modal__body p-5">
              <ResponsesTable responses={selectedCampaignResponses} campaignName={selectedCampaign.name} />
            </div>
          </div>
            </div>,
            document.body,
          )
        : null}
    </section>
  )
}

function ResponsesTable({ responses, campaignName }: { responses: ResponseItem[]; campaignName: string }) {
  const [page, setPage] = useState(0)
  const totalPages = Math.max(1, Math.ceil(responses.length / responsesPageSize))
  const safePage = Math.min(page, totalPages - 1)
  const startIndex = safePage * responsesPageSize
  const pagedResponses = responses.slice(startIndex, startIndex + responsesPageSize)

  if (responses.length === 0) {
    return (
      <div className="rounded-2xl border border-[#d7e8ee] bg-[#f8fcfd] px-4 py-10 text-center text-sm text-[#607985]">
        Esta campaña no tiene respuestas para los filtros seleccionados.
      </div>
    )
  }

  return (
    <div className="grid gap-3">
      <div className="flex flex-col gap-2 rounded-2xl border border-[#d7e8ee] bg-[#f8fcfd] px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm font-semibold text-[#17313c]">
          Mostrando {startIndex + 1}-{Math.min(startIndex + responsesPageSize, responses.length)} de {responses.length} respuestas
        </p>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setPage((current) => Math.max(0, current - 1))}
            disabled={safePage === 0}
            className="grid h-9 w-9 place-items-center rounded-full border border-[#c6dce4] bg-white text-[#00758d] transition hover:bg-[#eff9fc] disabled:cursor-not-allowed disabled:text-[#b1c0c7]"
            aria-label="Pagina anterior"
          >
            <ChevronLeftOutlinedIcon fontSize="small" />
          </button>
          <span className="min-w-[92px] text-center text-sm font-bold text-[#17313c]">
            {safePage + 1} / {totalPages}
          </span>
          <button
            type="button"
            onClick={() => setPage((current) => Math.min(totalPages - 1, current + 1))}
            disabled={safePage >= totalPages - 1}
            className="grid h-9 w-9 place-items-center rounded-full border border-[#c6dce4] bg-white text-[#00758d] transition hover:bg-[#eff9fc] disabled:cursor-not-allowed disabled:text-[#b1c0c7]"
            aria-label="Pagina siguiente"
          >
            <ChevronRightOutlinedIcon fontSize="small" />
          </button>
        </div>
      </div>

      <div className="overflow-x-auto rounded-2xl border border-[#cfe1e8]">
        <table className="min-w-[980px] w-full border-separate border-spacing-y-2 bg-white p-3 text-left text-xs text-[#17313c]">
          <thead>
            <tr className="bg-[#f1fbfd]">
              <th className="rounded-l-xl px-4 py-3 font-bold">Usuario</th>
              <th className="px-4 py-3 font-bold">Nombre del dispositivo</th>
              <th className="px-4 py-3 font-bold">Fecha de campaña</th>
              <th className="px-4 py-3 font-bold">Campaña</th>
              <th className="px-4 py-3 font-bold">Pregunta</th>
              <th className="px-4 py-3 font-bold">Tipo respuesta</th>
              <th className="px-4 py-3 font-bold">Respuesta</th>
              <th className="rounded-r-xl px-4 py-3 font-bold">Fecha de respuesta</th>
            </tr>
          </thead>
          <tbody>
            {pagedResponses.map((response) => (
              <tr key={response.id} className="bg-[#f8fcfd]">
                <td className="rounded-l-xl border-y border-l border-[#d7e8ee] px-4 py-3">{response.userName || response.userId}</td>
                <td className="border-y border-[#d7e8ee] px-4 py-3">{response.hostname || response.deviceId}</td>
                <td className="border-y border-[#d7e8ee] px-4 py-3">{formatDateTime(response.answeredAtUtc)}</td>
                <td className="border-y border-[#d7e8ee] px-4 py-3">{campaignName}</td>
                <td className="border-y border-[#d7e8ee] px-4 py-3">{response.questionText || '(Pregunta sin texto)'}</td>
                <td className="border-y border-[#d7e8ee] px-4 py-3">{formatQuestionType(response)}</td>
                <td className="border-y border-[#d7e8ee] px-4 py-3">{formatResponseAnswer(response)}</td>
                <td className="rounded-r-xl border-y border-r border-[#d7e8ee] px-4 py-3">{formatDateTime(response.answeredAtUtc)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function campaignMatchesOperation(campaign: Campaign, operation: string) {
  if (isAllOperationsAudience(campaign.audience)) {
    return true
  }

  return parseAudienceOperations(campaign.audience).some((item) => item.toLowerCase() === operation.toLowerCase())
}

function getCampaignStatusLabel(campaign: Campaign) {
  return campaign.deletedAtUtc ? deletedStatusLabel : statusLabels[campaign.status]
}

function getCampaignStatusClassName(campaign: Campaign) {
  if (campaign.deletedAtUtc) {
    return 'w-fit rounded-full bg-[#fff4ed] px-3 py-1 text-xs font-bold text-[#9a5a00]'
  }

  return campaign.status === 'Active'
    ? 'w-fit rounded-full bg-[#e8fbfd] px-3 py-1 text-xs font-bold text-[#00758d]'
    : 'w-fit rounded-full bg-[#e8edf0] px-3 py-1 text-xs font-bold text-[#607985]'
}

function getCampaignStatusRank(campaign: Campaign) {
  if (campaign.deletedAtUtc) {
    return 0
  }

  return campaign.status === 'Active' ? 2 : 1
}

function formatAudienceLabel(campaign: Campaign) {
  return isAllOperationsAudience(campaign.audience) ? 'Todas las operaciones' : campaign.audience
}

function formatQuestionType(response: ResponseItem) {
  if (response.questionType === 'Text') return 'Texto'
  if (response.questionType === 'YesNo') return 'Si o No'
  if (response.questionType === 'Choice') return 'Personalizada'
  if (response.minValue !== null && response.maxValue !== null) {
    return `${response.minValue} - ${response.maxValue}`
  }

  return 'Escala'
}

function formatDateInput(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return ''
  }

  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function formatDateTime(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return 'Sin fecha'
  }

  return date.toLocaleString()
}

function buildExportFilters(filters: ReportExportFilters): ReportExportFilters {
  return {
    campaignId: filters.campaignId || null,
    campaignSearch: filters.campaignSearch?.trim() || undefined,
    operation: filters.operation?.trim() || undefined,
    date: filters.date || undefined,
  }
}
