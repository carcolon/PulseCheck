import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { apiBaseUrl, dayOptions } from '../constants'
import type {
  Campaign,
  CampaignAudienceOptions,
  CampaignFilter,
  CampaignQuestion,
  CampaignStatus,
  DeliveryMode,
  FrequencyMode,
  Tab,
} from '../types'
import {
  buildImmediateRule,
  buildScheduledRule,
  createScaleQuestion,
  normalizeCampaign,
  normalizeQuestion,
} from '../utils/campaigns'
import type { AuthorizedFetch } from './adminPanelTypes'

export function useCampaignsDomain({
  authorizedFetch,
  loadData,
  setError,
  setTab,
}: {
  authorizedFetch: AuthorizedFetch
  loadData: () => Promise<void>
  setError: (message: string | null) => void
  setTab: (tab: Tab) => void
}) {
  const [campaigns, setCampaigns] = useState<Campaign[]>([])
  const [audienceOptions, setAudienceOptions] = useState<CampaignAudienceOptions>({ operations: [] })
  const [deliveryMode, setDeliveryMode] = useState<DeliveryMode>('now')
  const [frequencyMode, setFrequencyMode] = useState<FrequencyMode>('custom')
  const [scheduleTime, setScheduleTime] = useState('10:00')
  const [scheduleDays, setScheduleDays] = useState<string[]>([...dayOptions])
  const [forceResponse, setForceResponse] = useState(false)
  const [questions, setQuestions] = useState<CampaignQuestion[]>([createScaleQuestion(1, 5)])
  const [selectedAudienceOperations, setSelectedAudienceOperations] = useState<string[]>([])
  const [isSaving, setIsSaving] = useState(false)
  const [campaignFilter, setCampaignFilter] = useState<CampaignFilter>('All')
  const [searchTerm, setSearchTerm] = useState('')
  const [editingCampaignId, setEditingCampaignId] = useState<string | null>(null)

  const filteredCampaigns = useMemo(() => {
    const normalizedSearch = searchTerm.trim().toLowerCase()
    return campaigns.filter((campaign) => {
      if (campaign.deletedAtUtc) return false
      if (campaignFilter !== 'All' && campaign.status !== campaignFilter) return false
      if (!normalizedSearch) return true
      return campaign.name.toLowerCase().includes(normalizedSearch) || campaign.audience.toLowerCase().includes(normalizedSearch)
    })
  }, [campaigns, campaignFilter, searchTerm])

  async function handleCreateCampaign(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const data = new FormData(form)
    const normalizedQuestions = questions
      .filter((item) => item.text.trim().length > 0)
      .map(normalizeQuestion)

    if (normalizedQuestions.length === 0) {
      setError('Debes agregar al menos una pregunta valida.')
      return
    }

    const questionValidationError = validateQuestions(normalizedQuestions)
    if (questionValidationError) {
      setError(questionValidationError)
      return
    }

    const scheduleRule = deliveryMode === 'now'
      ? buildImmediateRule(forceResponse)
      : buildScheduledRule(frequencyMode, scheduleTime, scheduleDays, forceResponse)

    const payload = {
      name: String(data.get('name') ?? ''),
      audience: selectedAudienceOperations.length === 0 ? 'Todas las operaciones' : selectedAudienceOperations.join(', '),
      scheduleRule,
      questions: normalizedQuestions,
      questionText: normalizedQuestions[0].text,
      deliveryWindowStart: '00:00:00',
      deliveryWindowEnd: '23:59:00',
      createdBy: 'PulseCheck admin',
    }

    try {
      setError(null)
      setIsSaving(true)
      const createResponse = await authorizedFetch(`${apiBaseUrl}/api/campaigns`, {
        method: 'POST',
        body: JSON.stringify(payload),
      })

      if (!createResponse.ok) throw new Error('No fue posible crear la campana.')
      const createdCampaign = normalizeCampaign(await createResponse.json())
      if (deliveryMode === 'now') {
        await setStatus(createdCampaign.id, 'Active')
      }

      form.reset()
      setQuestions([createScaleQuestion(1, 5)])
      setSelectedAudienceOperations([])
      setFrequencyMode('custom')
      setScheduleDays([...dayOptions])
      setForceResponse(false)
      await loadData()
      setTab('campaigns')
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : 'Error inesperado.')
    } finally {
      setIsSaving(false)
    }
  }

  async function setStatus(id: string, status: CampaignStatus) {
    const response = await authorizedFetch(`${apiBaseUrl}/api/campaigns/${id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status }),
    })
    if (!response.ok) return
    await loadData()
  }

  async function deleteCampaign(id: string) {
    const response = await authorizedFetch(`${apiBaseUrl}/api/campaigns/${id}`, { method: 'DELETE' })
    if (!response.ok) return
    await loadData()
  }

  async function updateCampaign(campaign: Campaign) {
    const normalizedQuestions = campaign.questions.map(normalizeQuestion)
    const questionValidationError = validateQuestions(normalizedQuestions)
    if (questionValidationError) {
      setError(questionValidationError)
      return
    }

    const response = await authorizedFetch(`${apiBaseUrl}/api/campaigns/${campaign.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        name: campaign.name,
        audience: campaign.audience,
        scheduleRule: campaign.scheduleRule,
        questions: normalizedQuestions,
        questionText: normalizedQuestions[0]?.text ?? '',
        deliveryWindowStart: campaign.deliveryWindowStart.slice(0, 5),
        deliveryWindowEnd: campaign.deliveryWindowEnd.slice(0, 5),
      }),
    })
    if (!response.ok) return
    setEditingCampaignId(null)
    await loadData()
  }

  return {
    campaigns,
    setCampaigns,
    audienceOptions,
    setAudienceOptions,
    deliveryMode,
    setDeliveryMode,
    frequencyMode,
    setFrequencyMode,
    scheduleTime,
    setScheduleTime,
    scheduleDays,
    setScheduleDays,
    forceResponse,
    setForceResponse,
    questions,
    setQuestions,
    selectedAudienceOperations,
    setSelectedAudienceOperations,
    isSaving,
    campaignFilter,
    setCampaignFilter,
    searchTerm,
    setSearchTerm,
    editingCampaignId,
    setEditingCampaignId,
    filteredCampaigns,
    handleCreateCampaign,
    setStatus,
    deleteCampaign,
    updateCampaign,
  }
}

function validateQuestions(questions: CampaignQuestion[]) {
  const invalidChoiceIndex = questions.findIndex((question) => question.type === 'Choice' && (question.options?.length ?? 0) < 2)
  if (invalidChoiceIndex >= 0) {
    return `La pregunta personalizada ${invalidChoiceIndex + 1} debe tener minimo 2 opciones.`
  }

  return null
}

