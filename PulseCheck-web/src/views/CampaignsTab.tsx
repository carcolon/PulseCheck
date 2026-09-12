import type { FormEvent } from 'react'
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  TextField,
  Typography,
} from '@mui/material'
import AddCircleOutlineOutlinedIcon from '@mui/icons-material/AddCircleOutlineOutlined'
import SearchOutlinedIcon from '@mui/icons-material/SearchOutlined'
import { dayOptions } from '../constants'
import type { Campaign, CampaignFilter, CampaignQuestion, CampaignStatus, DeliveryMode, FrequencyMode } from '../types'
import { AudienceSelector } from '../components/AudienceSelector'
import { CampaignItem } from '../components/CampaignItem'
import { QuestionBuilder } from '../components/QuestionBuilder'
import { inputClassName, ModePill } from '../components/ui'

type CampaignsTabProps = {
  campaignFilter: CampaignFilter
  deliveryMode: DeliveryMode
  editingCampaignId: string | null
  filteredCampaigns: Campaign[]
  forceResponse: boolean
  frequencyMode: FrequencyMode
  isCreateDialogOpen: boolean
  isSaving: boolean
  audienceOptions: { operations: string[] }
  questions: CampaignQuestion[]
  scheduleDays: string[]
  scheduleTime: string
  selectedAudienceOperations: string[]
  searchTerm: string
  onCampaignFilterChange: (filter: CampaignFilter) => void
  onCancelEdit: () => void
  onCloseCreateDialog: () => void
  onCreateCampaign: (event: FormEvent<HTMLFormElement>) => Promise<void> | void
  onDeleteCampaign: (id: string) => Promise<void>
  onDeliveryModeChange: (mode: DeliveryMode) => void
  onEditCampaign: (id: string) => void
  onForceResponseChange: (value: boolean) => void
  onFrequencyModeChange: (mode: FrequencyMode) => void
  onOpenCreateDialog: () => void
  onQuestionsChange: (questions: CampaignQuestion[]) => void
  onScheduleDaysChange: (days: string[]) => void
  onScheduleTimeChange: (time: string) => void
  onSelectedAudienceOperationsChange: (operations: string[]) => void
  onSearchTermChange: (value: string) => void
  onSetStatus: (id: string, status: CampaignStatus) => Promise<void>
  onUpdateCampaign: (campaign: Campaign) => Promise<void>
}

export function CampaignsTab(props: CampaignsTabProps) {
  const {
    campaignFilter,
    deliveryMode,
    editingCampaignId,
    filteredCampaigns,
    forceResponse,
    frequencyMode,
    isCreateDialogOpen,
    isSaving,
    audienceOptions,
    questions,
    scheduleDays,
    scheduleTime,
    selectedAudienceOperations,
    searchTerm,
    onCampaignFilterChange,
    onCancelEdit,
    onCloseCreateDialog,
    onCreateCampaign,
    onDeleteCampaign,
    onDeliveryModeChange,
    onEditCampaign,
    onForceResponseChange,
    onFrequencyModeChange,
    onOpenCreateDialog,
    onQuestionsChange,
    onScheduleDaysChange,
    onScheduleTimeChange,
    onSelectedAudienceOperationsChange,
    onSearchTermChange,
    onSetStatus,
    onUpdateCampaign,
  } = props

  async function handleCreateCampaign(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    event.stopPropagation()
    await onCreateCampaign(event)
    onCloseCreateDialog()
  }

  return (
    <>
      <Paper data-animate elevation={0} className="campaign-console">
        <Box className="campaign-console__toolbar">
          <Box>
            <Typography variant="h5" className="campaign-console__title">
              Campañas
            </Typography>
            <Typography variant="body2" className="campaign-console__subtitle">
              Gestiona envios, estados y preguntas sin salir del listado.
            </Typography>
          </Box>
          <Button variant="contained" startIcon={<AddCircleOutlineOutlinedIcon />} onClick={onOpenCreateDialog}>
            Nueva campaña
          </Button>
        </Box>

        <Box className="campaign-console__filters">
          <TextField
            value={searchTerm}
            onChange={(event) => onSearchTermChange(event.target.value)}
            placeholder="Buscar por nombre o audiencia"
            size="small"
            fullWidth
            slotProps={{
              input: {
                startAdornment: <SearchOutlinedIcon sx={{ mr: 1, color: '#78909a' }} />,
              },
            }}
          />
          <FormControl size="small" className="campaign-console__filter-select">
            <InputLabel>Estado</InputLabel>
            <Select
              label="Estado"
              value={campaignFilter}
              onChange={(event) => onCampaignFilterChange(event.target.value as CampaignFilter)}
            >
              <MenuItem value="All">Todas</MenuItem>
              <MenuItem value="Active">Activas</MenuItem>
              <MenuItem value="Draft">Borradores</MenuItem>
              <MenuItem value="Paused">Pausadas</MenuItem>
            </Select>
          </FormControl>
          <Box className="campaign-console__result-count">{filteredCampaigns.length} resultados</Box>
        </Box>

        <Box className="campaign-console__list">
          {filteredCampaigns.length === 0 ? (
            <Box className="campaign-console__empty">
              No hay campañas con los filtros actuales.
            </Box>
          ) : (
            filteredCampaigns.map((campaign) => (
              <CampaignItem
                key={campaign.id}
                campaign={campaign}
                isEditing={editingCampaignId === campaign.id}
                onEdit={() => onEditCampaign(campaign.id)}
                onCancel={onCancelEdit}
                onSave={onUpdateCampaign}
                onDelete={onDeleteCampaign}
                onSetStatus={onSetStatus}
                audienceOptions={audienceOptions.operations}
              />
            ))
          )}
        </Box>
      </Paper>

      <Dialog
        open={isCreateDialogOpen}
        onClose={onCloseCreateDialog}
        fullWidth
        maxWidth="md"
        slotProps={{ paper: { className: 'campaign-create-dialog' } }}
      >
        <form onSubmit={handleCreateCampaign}>
          <DialogTitle>Nueva campaña</DialogTitle>
          <DialogContent>
            <div className="grid gap-3 pt-2">
              <p className="text-xs text-[#5b7480]">1. Identidad de la campaña</p>
              <input name="name" className={inputClassName} placeholder="Nombre" required />
              <AudienceSelector
                operations={audienceOptions.operations}
                value={selectedAudienceOperations}
                onChange={onSelectedAudienceOperationsChange}
              />

              <p className="mt-1 text-xs text-[#5b7480]">2. Modo de envio</p>
              <div className="grid gap-2 sm:grid-cols-2">
                <ModePill active={deliveryMode === 'now'} label="Enviar ahora" onClick={() => onDeliveryModeChange('now')} />
                <ModePill active={deliveryMode === 'scheduled'} label="Programar" onClick={() => onDeliveryModeChange('scheduled')} />
              </div>

              <label className="flex items-center gap-2 text-sm text-[#4d6671]">
                <input type="checkbox" checked={forceResponse} onChange={(event) => onForceResponseChange(event.target.checked)} />
                Obligar respuesta (sin botón "Posponer")
              </label>

              {deliveryMode === 'scheduled' ? (
                <div className="rounded-2xl border border-[#d5e5eb] bg-[#f8fbfd] p-3 space-y-2">
                  <p className="text-xs text-[#5b7480]">3. Frecuencia</p>
                  <select value={frequencyMode} onChange={(event) => onFrequencyModeChange(event.target.value as FrequencyMode)} className={inputClassName}>
                    <option value="hourly">Cada hora</option>
                    <option value="custom">Horario personalizado</option>
                    <option value="weekly">Cada semana</option>
                    <option value="biweekly">Bisemanal</option>
                    <option value="monthly">Cada mes</option>
                    <option value="quarterly">Trimestral</option>
                  </select>
                  <input type="time" value={scheduleTime} onChange={(event) => onScheduleTimeChange(event.target.value)} className={inputClassName} />
                  <div className="flex flex-wrap gap-2">
                    {dayOptions.map((day) => (
                      <Button
                        key={day}
                        type="button"
                        onClick={() => onScheduleDaysChange(scheduleDays.includes(day) ? scheduleDays.filter((item) => item !== day) : [...scheduleDays, day])}
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
                </div>
              ) : null}

              {deliveryMode === 'now' ? (
                <p className="rounded-2xl border border-[#d5e5eb] bg-[#f8fbfd] px-4 py-3 text-sm text-[#56707b]">
                  Enviar ahora usa ventana completa (00:00 a 23:59) para mostrar el popup de inmediato.
                </p>
              ) : null}

              <p className="mt-1 text-xs text-[#5b7480]">4. Preguntas</p>
              <QuestionBuilder questions={questions} onChange={onQuestionsChange} />
            </div>
          </DialogContent>
          <DialogActions>
            <Button type="button" onClick={onCloseCreateDialog}>Cancelar</Button>
            <Button type="submit" disabled={isSaving} variant="contained" color={deliveryMode === 'now' ? 'secondary' : 'primary'}>
              {isSaving
                ? 'Guardando...'
                : deliveryMode === 'now'
                  ? 'Crear y enviar ahora'
                  : 'Crear campaña programada'}
            </Button>
          </DialogActions>
        </form>
      </Dialog>
    </>
  )
}
