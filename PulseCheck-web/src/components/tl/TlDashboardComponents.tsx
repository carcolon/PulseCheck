import { useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { PrimaryButton, SoftButton } from '../ui'
import type { TlCampaignOption, TlDashboard, TlQuestionOption, TlResponseRow, TlWeekOption } from '../../types'

const pageSize = 50

export function MultiSelectColumn({ title, emptyText, children }: { title: string; emptyText: string; children: ReactNode }) {
  const items = Array.isArray(children) ? children.filter(Boolean) : children
  const hasItems = Array.isArray(items) ? items.length > 0 : Boolean(items)

  return (
    <div className="tl-filter-column">
      <h3>{title}</h3>
      <div className="tl-filter-column__body">
        {hasItems ? items : <p className="tl-empty">{emptyText}</p>}
      </div>
    </div>
  )
}

export function CheckboxRow({ label, caption, checked, onChange }: { label: string; caption?: string; checked: boolean; onChange: () => void }) {
  return (
    <label className={checked ? 'tl-check-row tl-check-row--selected' : 'tl-check-row'}>
      <input type="checkbox" checked={checked} onChange={onChange} />
      <span>
        <strong>{label}</strong>
        {caption ? <small>{caption}</small> : null}
      </span>
    </label>
  )
}

export function WeekMultiSelect({
  weeks,
  selectedWeeks,
  onToggle,
}: {
  weeks: TlWeekOption[]
  selectedWeeks: string[]
  onToggle: (weekId: string) => void
}) {
  return (
    <details className="tl-week-dropdown">
      <summary>
        <span>{selectedWeeks.length === 0 ? 'Todas las semanas' : `${selectedWeeks.length} semanas seleccionadas`}</span>
      </summary>
      <div className="tl-week-dropdown__menu">
        {weeks.length > 0 ? weeks.map((week) => (
          <CheckboxRow
            key={week.id}
            label={week.label}
            checked={selectedWeeks.includes(week.id)}
            onChange={() => onToggle(week.id)}
          />
        )) : <p className="tl-empty">No hay semanas disponibles.</p>}
      </div>
    </details>
  )
}

export function CampaignOptionRow({
  campaign,
  caption,
  checked,
  onChange,
}: {
  campaign: TlCampaignOption
  caption?: string
  checked: boolean
  onChange: () => void
}) {
  return (
    <label className={checked ? 'tl-check-row tl-check-row--selected' : 'tl-check-row'}>
      <input type="checkbox" checked={checked} onChange={onChange} />
      <span>
        <span className="tl-campaign-title-line">
          <strong>{campaign.name}</strong>
          <CampaignStatusBadge campaign={campaign} />
        </span>
        {caption ? <small>{caption}</small> : null}
      </span>
    </label>
  )
}

export function QuestionAnswers({
  campaign,
  question,
  selectedValues,
  onToggle,
}: {
  campaign: TlCampaignOption
  question: TlQuestionOption
  selectedValues: string[]
  onToggle: (value: string) => void
}) {
  return (
    <div className="tl-question">
      <p>{campaign.name}</p>
      <h4>{question.text}</h4>
      {question.type === 'Text' ? (
        <span className="tl-question__note">Texto libre: no requiere filtro de respuesta.</span>
      ) : question.options.length > 0 ? (
        <div className="tl-answer-options">
          {question.options.map((option) => (
            <button
              key={option}
              type="button"
              className={selectedValues.includes(option) ? 'tl-answer-option tl-answer-option--selected' : 'tl-answer-option'}
              onClick={() => onToggle(option)}
            >
              {option}
            </button>
          ))}
        </div>
      ) : (
        <span className="tl-question__note">Sin opciones registradas para esta pregunta.</span>
      )}
    </div>
  )
}

export function ResultsModal({
  dashboard,
  selectedWeeks,
  selectedCampaigns,
  selectedAnswers,
  exportLabel = 'Exportar',
  exportDisabled = false,
  onClose,
  onExport,
}: {
  dashboard: TlDashboard
  selectedWeeks: string[]
  selectedCampaigns: string[]
  selectedAnswers: Record<string, string[]>
  exportLabel?: ReactNode
  exportDisabled?: boolean
  onClose: () => void
  onExport: () => void | Promise<void>
}) {
  const [page, setPage] = useState(1)
  const selectedAnswerCount = Object.values(selectedAnswers).reduce((total, values) => total + values.length, 0)
  const totalPages = Math.max(1, Math.ceil(dashboard.responses.length / pageSize))
  const visibleResponses = useMemo(
    () => dashboard.responses.slice((page - 1) * pageSize, page * pageSize),
    [dashboard.responses, page],
  )

  return (
    <div className="admin-modal-overlay" role="dialog" aria-modal="true">
      <div className="admin-modal-shell tl-results-modal">
        <div className="tl-results-modal__header">
          <div>
            <p>Resultados</p>
            <h2>{dashboard.responses.length} respuestas</h2>
            <span>
              {selectedWeeks.length || 'Todas'} weeks / {selectedCampaigns.length || 'Todas'} encuestas / {selectedAnswerCount || 'Todas'} respuestas
            </span>
          </div>
          <div className="tl-results-modal__actions">
            <PrimaryButton type="button" onClick={() => void onExport()} disabled={exportDisabled || dashboard.responses.length === 0}>{exportLabel}</PrimaryButton>
            <SoftButton type="button" onClick={onClose}>Cerrar</SoftButton>
          </div>
        </div>

        <div className="admin-modal-body tl-results-modal__body">
          {dashboard.responses.length > 0 ? (
            <table className="tl-results-table">
              <thead>
                <tr>
                  <th>Week</th>
                  <th>Encuesta</th>
                  <th>Pregunta</th>
                  <th>Respuesta</th>
                  <th>Persona</th>
                  <th>Fecha</th>
                </tr>
              </thead>
              <tbody>
                {visibleResponses.map((response) => (
                  <tr key={response.id}>
                    <td>{resolveWeekLabel(response.weekId, dashboard.weeks)}</td>
                    <td>{response.campaignName}</td>
                    <td>{response.questionText}</td>
                    <td>{formatResponseValue(response)}</td>
                    <td>
                      <strong>{response.userName}</strong>
                      <span>{response.email}</span>
                    </td>
                    <td>{formatDateTime(response.answeredAtUtc)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <p className="tl-empty">No hay respuestas que coincidan con la selección.</p>
          )}
        </div>

        <div className="tl-results-modal__footer">
          <span>Página {page} de {totalPages} / 50 respuestas por página</span>
          <div>
            <SoftButton type="button" size="small" disabled={page <= 1} onClick={() => setPage((current) => Math.max(1, current - 1))}>
              Anterior
            </SoftButton>
            <SoftButton type="button" size="small" disabled={page >= totalPages} onClick={() => setPage((current) => Math.min(totalPages, current + 1))}>
              Siguiente
            </SoftButton>
          </div>
        </div>
      </div>
    </div>
  )
}

export function formatCampaignWeeks(campaign: TlCampaignOption, weeks: TlWeekOption[]) {
  const labels = campaign.weekIds
    .map((weekId) => weeks.find((week) => week.id === weekId)?.label)
    .filter(Boolean)

  return labels.length > 0 ? labels.join(', ') : undefined
}

function CampaignStatusBadge({ campaign }: { campaign: TlCampaignOption }) {
  const deleted = Boolean(campaign.deletedAtUtc)
  const label = deleted ? 'Eliminada' : campaign.status === 'Active' ? 'Activa' : campaign.status === 'Paused' ? 'Pausada' : 'Borrador'
  const className = deleted
    ? 'tl-campaign-status tl-campaign-status--deleted'
    : campaign.status === 'Active'
      ? 'tl-campaign-status tl-campaign-status--active'
      : 'tl-campaign-status tl-campaign-status--paused'

  return <em className={className}>{label}</em>
}

export function formatResponseValue(response: TlResponseRow) {
  if (response.questionType === 'Scale') {
    return response.numericValue?.toString() ?? '-'
  }

  return response.textValue || '-'
}

function resolveWeekLabel(weekId: string, weeks: TlWeekOption[]) {
  return weeks.find((week) => week.id === weekId)?.label ?? weekId
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('es-CO', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}
