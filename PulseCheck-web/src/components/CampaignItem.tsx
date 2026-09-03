import { useEffect, useMemo, useState, type MouseEvent } from 'react'
import {
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  Menu,
  MenuItem,
  Paper,
  Typography,
} from '@mui/material'
import MoreVertOutlinedIcon from '@mui/icons-material/MoreVertOutlined'
import PlayArrowOutlinedIcon from '@mui/icons-material/PlayArrowOutlined'
import PauseOutlinedIcon from '@mui/icons-material/PauseOutlined'
import { AudienceSelector, parseAudienceSelection } from './AudienceSelector'
import type { Campaign, CampaignQuestion, CampaignStatus, DeliveryMode, FrequencyMode } from '../types'
import { buildImmediateRule, buildScheduledRule, describeRule, parseScheduleRule } from '../utils/campaigns'
import { QuestionBuilder } from './QuestionBuilder'
import { inputClassName, ModePill, SmallBtn } from './ui'
import { dayOptions } from '../constants'

type PendingAction =
  | { kind: 'status'; status: CampaignStatus; title: string; body: string; confirmLabel: string }
  | { kind: 'delete'; title: string; body: string; confirmLabel: string }

export function CampaignItem({
  campaign,
  isEditing,
  onEdit,
  onCancel,
  onSave,
  onDelete,
  onSetStatus,
  audienceOptions,
}: {
  campaign: Campaign
  isEditing: boolean
  onEdit: () => void
  onCancel: () => void
  onSave: (campaign: Campaign) => Promise<void>
  onDelete: (id: string) => Promise<void>
  onSetStatus: (id: string, status: CampaignStatus) => Promise<void>
  audienceOptions: string[]
}) {
  const [draft, setDraft] = useState(campaign)
  const [selectedAudienceOperations, setSelectedAudienceOperations] = useState<string[]>([])
  const [deliveryMode, setDeliveryMode] = useState<DeliveryMode>('scheduled')
  const [frequencyMode, setFrequencyMode] = useState<FrequencyMode>('custom')
  const [scheduleTime, setScheduleTime] = useState('10:00')
  const [scheduleDays, setScheduleDays] = useState<string[]>([...dayOptions])
  const [forceResponse, setForceResponse] = useState(false)
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null)
  const [pendingAction, setPendingAction] = useState<PendingAction | null>(null)
  const [isWorking, setIsWorking] = useState(false)

  const scheduleSummary = useMemo(() => describeRule(campaign.scheduleRule), [campaign.scheduleRule])
  const primaryStatus = campaign.status === 'Active' ? 'Paused' : 'Active'
  const primaryLabel = campaign.status === 'Active' ? 'Desactivar' : 'Activar'

  useEffect(() => {
    if (isEditing) {
      return
    }

    setDraft(campaign)
    setSelectedAudienceOperations(parseAudienceSelection(campaign.audience, audienceOptions))
    const parsedRule = parseScheduleRule(campaign.scheduleRule)
    setDeliveryMode(parsedRule.deliveryMode)
    setFrequencyMode(parsedRule.frequencyMode)
    setScheduleTime(parsedRule.scheduleTime)
    setScheduleDays(parsedRule.scheduleDays)
    setForceResponse(parsedRule.forceResponse)
  }, [campaign, isEditing, audienceOptions])

  function buildStatusAction(status: CampaignStatus): PendingAction {
    const label = status === 'Active' ? 'activar' : status === 'Paused' ? 'desactivar' : 'pasar a borrador'
    return {
      kind: 'status',
      status,
      title: `${status === 'Active' ? 'Activar' : status === 'Paused' ? 'Desactivar' : 'Pasar a borrador'} campaña`,
      body: `Estas seguro de que quieres ${label} "${campaign.name}"?`,
      confirmLabel: status === 'Active' ? 'Activar campaña' : status === 'Paused' ? 'Desactivar campaña' : 'Pasar a borrador',
    }
  }

  function handleSave() {
    const nextScheduleRule = deliveryMode === 'now'
      ? buildImmediateRule(forceResponse)
      : buildScheduledRule(frequencyMode, scheduleTime, scheduleDays, forceResponse)

    void onSave({
      ...draft,
      audience: selectedAudienceOperations.length === 0 ? 'Todas las operaciones' : selectedAudienceOperations.join(', '),
      scheduleRule: nextScheduleRule,
      deliveryWindowStart: deliveryMode === 'now' ? '00:00:00' : draft.deliveryWindowStart,
      deliveryWindowEnd: deliveryMode === 'now' ? '23:59:00' : draft.deliveryWindowEnd,
    })
  }

  async function confirmPendingAction(event?: MouseEvent<HTMLButtonElement>) {
    event?.preventDefault()
    event?.stopPropagation()
    if (!pendingAction) return

    setIsWorking(true)
    try {
      if (pendingAction.kind === 'status') {
        await onSetStatus(campaign.id, pendingAction.status)
      } else {
        await onDelete(campaign.id)
      }
      setPendingAction(null)
    } finally {
      setIsWorking(false)
    }
  }

  if (isEditing) {
    return (
      <Paper elevation={0} className="campaign-row campaign-row--editing">
        <div className="grid gap-3">
          <input value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} className={inputClassName} placeholder="Nombre de la campaña" />
          <AudienceSelector
            operations={audienceOptions}
            value={selectedAudienceOperations}
            onChange={setSelectedAudienceOperations}
            compact
          />

          <div className="rounded-2xl border border-[#d5e5eb] bg-[#f8fbfd] p-3 space-y-3">
            <p className="text-xs text-[#5b7480]">Modo de envio</p>
            <div className="grid gap-2 sm:grid-cols-2">
              <ModePill active={deliveryMode === 'now'} label="Enviar ahora" onClick={() => setDeliveryMode('now')} />
              <ModePill active={deliveryMode === 'scheduled'} label="Programar" onClick={() => setDeliveryMode('scheduled')} />
            </div>

            <label className="flex items-center gap-2 text-sm text-[#4d6671]">
              <input type="checkbox" checked={forceResponse} onChange={(event) => setForceResponse(event.target.checked)} />
              Obligar respuesta (sin botón "Posponer")
            </label>

            {deliveryMode === 'scheduled' ? (
              <>
                <select value={frequencyMode} onChange={(event) => setFrequencyMode(event.target.value as FrequencyMode)} className={inputClassName}>
                  <option value="hourly">Cada hora</option>
                  <option value="custom">Horario personalizado</option>
                  <option value="weekly">Cada semana</option>
                  <option value="biweekly">Bisemanal</option>
                  <option value="monthly">Cada mes</option>
                  <option value="quarterly">Trimestral</option>
                </select>
                <input type="time" value={scheduleTime} onChange={(event) => setScheduleTime(event.target.value)} className={inputClassName} />
                <div className="flex flex-wrap gap-2">
                  {dayOptions.map((day) => (
                    <Button
                      key={day}
                      type="button"
                      onClick={() => setScheduleDays(scheduleDays.includes(day) ? scheduleDays.filter((item) => item !== day) : [...scheduleDays, day])}
                      size="small"
                      variant={scheduleDays.includes(day) ? 'contained' : 'outlined'}
                      sx={{
                        minWidth: 0,
                        borderRadius: '999px',
                        borderColor: '#c4d8e0',
                        backgroundColor: scheduleDays.includes(day) ? '#00758d' : '#ffffff',
                        color: scheduleDays.includes(day) ? '#ffffff' : '#4c6975',
                        fontSize: 12,
                        fontWeight: 800,
                        textTransform: 'none',
                        px: 1.5,
                        py: 0.5,
                        boxShadow: 'none',
                        '&:hover': {
                          borderColor: '#00758d',
                          backgroundColor: scheduleDays.includes(day) ? '#006b80' : '#f6fbfd',
                          boxShadow: 'none',
                        },
                      }}
                    >
                      {day}
                    </Button>
                  ))}
                </div>
                <div className="grid grid-cols-2 gap-2">
                  <input type="time" value={draft.deliveryWindowStart.slice(0, 5)} onChange={(event) => setDraft({ ...draft, deliveryWindowStart: `${event.target.value}:00` })} className={inputClassName} />
                  <input type="time" value={draft.deliveryWindowEnd.slice(0, 5)} onChange={(event) => setDraft({ ...draft, deliveryWindowEnd: `${event.target.value}:00` })} className={inputClassName} />
                </div>
              </>
            ) : (
              <p className="rounded-2xl border border-[#d5e5eb] bg-white px-4 py-3 text-sm text-[#56707b]">
                La campaña se enviara de inmediato y usara la ventana completa del dia.
              </p>
            )}
          </div>

          <QuestionBuilder questions={draft.questions} onChange={(updated) => setDraft({ ...draft, questions: updated })} compact />
          <div className="flex gap-2">
            <SmallBtn label="Guardar" onClick={handleSave} />
            <SmallBtn label="Cancelar" onClick={onCancel} />
          </div>
        </div>
      </Paper>
    )
  }

  return (
    <>
      <Paper elevation={0} className="campaign-row">
        <Box className="campaign-row__main">
          <Box className="campaign-row__title-line">
            <Typography className="campaign-row__title">{campaign.name}</Typography>
            <Chip size="small" className={`campaign-row__status campaign-row__status--${campaign.status.toLowerCase()}`} label={campaign.status === 'Draft' ? 'Borrador' : campaign.status === 'Paused' ? 'Pausada' : 'Activa'} />
          </Box>
          <Typography className="campaign-row__meta">
            {campaign.audience} - {scheduleSummary} - {campaign.questions.length} preguntas
          </Typography>
          <Typography className="campaign-row__question" noWrap>
            {campaign.questions[0]
              ? `${formatQuestionType(campaign.questions[0])} - ${campaign.questions[0].text}`
              : 'Sin preguntas configuradas'}
          </Typography>
        </Box>

        <Box className="campaign-row__actions">
          <Button
            type="button"
            variant={campaign.status === 'Active' ? 'outlined' : 'contained'}
            color={campaign.status === 'Active' ? 'warning' : 'primary'}
            startIcon={campaign.status === 'Active' ? <PauseOutlinedIcon /> : <PlayArrowOutlinedIcon />}
            onClick={(event) => {
              event.preventDefault()
              event.stopPropagation()
              setPendingAction(buildStatusAction(primaryStatus))
            }}
          >
            {primaryLabel}
          </Button>
          <IconButton
            type="button"
            aria-label={`Mas opciones para ${campaign.name}`}
            onClick={(event) => {
              event.preventDefault()
              event.stopPropagation()
              setMenuAnchor(event.currentTarget)
            }}
          >
            <MoreVertOutlinedIcon />
          </IconButton>
        </Box>
      </Paper>

      <Menu anchorEl={menuAnchor} open={Boolean(menuAnchor)} onClose={() => setMenuAnchor(null)}>
        <MenuItem
          onClick={() => {
            setMenuAnchor(null)
            onEdit()
          }}
        >
          Editar
        </MenuItem>
        {campaign.status !== 'Draft' ? (
          <MenuItem
            onClick={() => {
              setMenuAnchor(null)
              setPendingAction(buildStatusAction('Draft'))
            }}
          >
            Pasar a borrador
          </MenuItem>
        ) : null}
        {campaign.status !== 'Active' && campaign.status !== 'Paused' ? (
          <MenuItem
            onClick={() => {
              setMenuAnchor(null)
              setPendingAction(buildStatusAction('Paused'))
            }}
          >
            Desactivar
          </MenuItem>
        ) : null}
        <MenuItem
          onClick={() => {
            setMenuAnchor(null)
            setPendingAction({
              kind: 'delete',
              title: 'Borrar campaña',
              body: `Esta accion eliminara "${campaign.name}". Quieres continuar?`,
              confirmLabel: 'Borrar campaña',
            })
          }}
          sx={{ color: 'error.main' }}
        >
          Borrar
        </MenuItem>
      </Menu>

      <Dialog open={Boolean(pendingAction)} onClose={() => (isWorking ? undefined : setPendingAction(null))} fullWidth maxWidth="xs">
        <DialogTitle>{pendingAction?.title}</DialogTitle>
        <DialogContent>
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {pendingAction?.body}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button type="button" disabled={isWorking} onClick={() => setPendingAction(null)}>
            Cancelar
          </Button>
          <Button
            disabled={isWorking}
            variant="contained"
            color={pendingAction?.kind === 'delete' ? 'error' : 'primary'}
            type="button"
            onClick={(event) => void confirmPendingAction(event)}
          >
            {isWorking ? 'Procesando...' : pendingAction?.confirmLabel}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  )
}

function formatQuestionType(question: CampaignQuestion) {
  if (question.type === 'Scale') return `Escala ${question.minValue}-${question.maxValue}`
  if (question.type === 'YesNo') return 'Si o No'
  if (question.type === 'Choice') return 'Personalizada'
  return 'Respuesta abierta'
}
