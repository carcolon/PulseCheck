import { dayOptions } from '../constants'
import type { Campaign, CampaignQuestion, CampaignStatus, DeliveryMode, FrequencyMode, QuestionType } from '../types'

const maxChoiceOptionLength = 120

export type ParsedScheduleRule = {
  deliveryMode: DeliveryMode
  frequencyMode: FrequencyMode
  scheduleTime: string
  scheduleDays: string[]
  hideAfterAnswered: boolean
  forceResponse: boolean
}

const dayLabels: Record<string, string> = {
  MON: 'lunes',
  TUE: 'martes',
  WED: 'miércoles',
  THU: 'jueves',
  FRI: 'viernes',
  SAT: 'sábado',
  SUN: 'domingo',
}

export const allOperationsAudience = 'Todas las operaciones'

export function normalizeCampaign(raw: unknown): Campaign {
  const candidate = raw as Campaign & { question?: { text?: string; minValue?: number; maxValue?: number } }
  const baseQuestions = Array.isArray(candidate.questions) ? candidate.questions : []
  const normalizedQuestions: CampaignQuestion[] = baseQuestions.length > 0
    ? baseQuestions.map((item): CampaignQuestion => {
      const normalizedType = normalizeQuestionType(item.type)
      return {
        id: item.id,
        text: item.text,
        type: normalizedType,
        minValue: normalizedType === 'Scale' ? (item.minValue ?? 1) : null,
        maxValue: normalizedType === 'Scale' ? (item.maxValue ?? 5) : null,
        placeholder: normalizedType === 'Text' ? item.placeholder ?? null : null,
        options: normalizedType === 'Choice' ? normalizeChoiceOptions(item.options) : null,
      }
    })
    : [createScaleQuestion(1, 5, candidate.question?.text ?? 'Pregunta sin texto')]

  return {
    ...candidate,
    status: normalizeCampaignStatus(candidate.status),
    questions: normalizedQuestions,
    deletedAtUtc: candidate.deletedAtUtc ?? null,
  }
}

export function parseAudienceOperations(audience: string) {
  if (!audience.trim() || isAllOperationsAudience(audience)) {
    return []
  }

  return audience
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
}

export function formatAudience(operations: string[]) {
  return operations.length === 0 ? allOperationsAudience : operations.join(', ')
}

export function isAllOperationsAudience(audience: string) {
  const normalized = audience.trim().toLowerCase()
  return normalized === allOperationsAudience.toLowerCase() ||
    normalized === 'all operations' ||
    normalized === 'todos' ||
    normalized === 'all'
}

export function createScaleQuestion(minValue: number, maxValue: number, text = ''): CampaignQuestion {
  return {
    id: crypto.randomUUID(),
    text,
    type: 'Scale',
    minValue,
    maxValue,
    placeholder: null,
    options: null,
  }
}

export function createTextQuestion(): CampaignQuestion {
  return {
    id: crypto.randomUUID(),
    text: '',
    type: 'Text',
    minValue: null,
    maxValue: null,
    placeholder: 'Escribe tu respuesta',
    options: null,
  }
}

export function createYesNoQuestion(): CampaignQuestion {
  return {
    id: crypto.randomUUID(),
    text: '',
    type: 'YesNo',
    minValue: null,
    maxValue: null,
    placeholder: null,
    options: null,
  }
}

export function createChoiceQuestion(): CampaignQuestion {
  return {
    id: crypto.randomUUID(),
    text: '',
    type: 'Choice',
    minValue: null,
    maxValue: null,
    placeholder: null,
    options: ['', '', '', '', ''],
  }
}

export function normalizeQuestion(question: CampaignQuestion): CampaignQuestion {
  if (question.type === 'Text') {
    return {
      ...question,
      text: question.text.trim(),
      minValue: null,
      maxValue: null,
      placeholder: question.placeholder?.trim() || null,
      options: null,
    }
  }

  if (question.type === 'YesNo') {
    return {
      ...question,
      text: question.text.trim(),
      minValue: null,
      maxValue: null,
      placeholder: null,
      options: null,
    }
  }

  if (question.type === 'Choice') {
    return {
      ...question,
      text: question.text.trim(),
      minValue: null,
      maxValue: null,
      placeholder: null,
      options: normalizeChoiceOptions(question.options),
    }
  }

  const minValue = Math.max(0, Number(question.minValue ?? 1))
  const maxValue = Math.max(minValue, Number(question.maxValue ?? 5))
  return {
    ...question,
    text: question.text.trim(),
    minValue,
    maxValue,
    placeholder: null,
    options: null,
  }
}

export function buildScheduledRule(
  mode: FrequencyMode,
  time: string,
  days: string[],
  forceResponse: boolean,
) {
  const [hour, minute] = time.split(':')
  const selectedDays = days.length > 0 ? days.join(',') : dayOptions.join(',')
  const cron = mode === 'hourly'
    ? `0 ${Number(minute)} * ? * ${selectedDays}`
    : `0 ${Number(minute)} ${Number(hour)} ? * ${selectedDays}`
  return appendMetadata(cron, forceResponse, mode)
}

export function buildImmediateRule(forceResponse: boolean) {
  return appendMetadata('0 * * ? * *', forceResponse, 'immediate')
}

export function parseScheduleRule(rule: string): ParsedScheduleRule {
  const [cron, metadata] = rule.split('#', 2)
  const parts = cron.trim().split(/\s+/).filter(Boolean)
  const flags = metadata
    ? metadata.split(',').map((item) => item.trim().toLowerCase())
    : []

  const scheduleDays = parts.length >= 6
    ? normalizeDayTokens(parts[5])
    : [...dayOptions]

  const scheduleTime = parts.length >= 3 && parts[2] !== '*'
    ? `${parts[2].padStart(2, '0')}:${parts[1].padStart(2, '0')}`
    : `10:${(parts[1] && parts[1] !== '*') ? parts[1].padStart(2, '0') : '00'}`

  const metadataFrequency = flags
    .find((item) => item.startsWith('freq='))
    ?.slice(5)

  const frequencyMode: FrequencyMode = isFrequencyMode(metadataFrequency)
    ? metadataFrequency
    : parts.length >= 6 && parts[2] === '*'
      ? 'hourly'
      : 'custom'

  const deliveryMode: DeliveryMode = metadataFrequency === 'immediate' ||
    (parts.length >= 6 && parts[1] === '*' && parts[2] === '*' && scheduleDays.length === 1 && scheduleDays[0] === currentDayToken())
    ? 'now'
    : 'scheduled'

  return {
    deliveryMode,
    frequencyMode,
    scheduleTime,
    scheduleDays,
    hideAfterAnswered: flags.includes('hide-after-answered'),
    forceResponse: flags.includes('force-response') || flags.includes('no-dismiss'),
  }
}

export function describeRule(rule: string) {
  const parsed = parseScheduleRule(rule)
  const notes: string[] = []

  let base: string
  if (parsed.deliveryMode === 'now') {
    base = 'Se envía de inmediato'
  } else if (parsed.frequencyMode === 'hourly') {
    base = `Se envía cada hora a los ${parsed.scheduleTime.slice(3, 5)} minutos${describeDays(parsed.scheduleDays)}`
  } else if (parsed.frequencyMode === 'weekly') {
    base = `Se envía cada semana a las ${formatTime(parsed.scheduleTime)}${describeDays(parsed.scheduleDays)}`
  } else if (parsed.frequencyMode === 'biweekly') {
    base = `Se envía cada dos semanas a las ${formatTime(parsed.scheduleTime)}${describeDays(parsed.scheduleDays)}`
  } else if (parsed.frequencyMode === 'monthly') {
    base = `Se envía cada mes a las ${formatTime(parsed.scheduleTime)}${describeDays(parsed.scheduleDays)}`
  } else if (parsed.frequencyMode === 'quarterly') {
    base = `Se envía cada trimestre a las ${formatTime(parsed.scheduleTime)}${describeDays(parsed.scheduleDays)}`
  } else {
    base = `Se envía a las ${formatTime(parsed.scheduleTime)}${describeDays(parsed.scheduleDays)}`
  }

  if (parsed.forceResponse) {
    notes.push('requiere respuesta')
  }

  return notes.length > 0 ? `${base}; ${notes.join(' · ')}` : base
}

function appendMetadata(cron: string, forceResponse: boolean, frequencyMode?: FrequencyMode | 'immediate') {
  const flags: string[] = []
  if (frequencyMode && frequencyMode !== 'custom') flags.push(`freq=${frequencyMode}`)
  if (forceResponse) flags.push('force-response')
  return flags.length > 0 ? `${cron}#${flags.join(',')}` : cron
}

function isFrequencyMode(value: unknown): value is FrequencyMode {
  return value === 'hourly' ||
    value === 'custom' ||
    value === 'weekly' ||
    value === 'biweekly' ||
    value === 'monthly' ||
    value === 'quarterly'
}

function normalizeQuestionType(type: unknown): QuestionType {
  return type === 'Text' || type === 1
    ? 'Text'
    : type === 'YesNo' || type === 2
      ? 'YesNo'
      : type === 'Choice' || type === 3
        ? 'Choice'
        : 'Scale'
}

function normalizeChoiceOptions(options: unknown) {
  const rawOptions = Array.isArray(options) ? options : []
  return rawOptions
    .map((item) => String(item ?? '').trim())
    .map((item) => Array.from(item).slice(0, maxChoiceOptionLength).join(''))
    .filter(Boolean)
    .filter((item, index, items) => items.findIndex((candidate) => candidate.toLowerCase() === item.toLowerCase()) === index)
    .slice(0, 5)
}

function normalizeCampaignStatus(status: unknown): CampaignStatus {
  if (status === 'Draft' || status === 0) {
    return 'Draft'
  }

  if (status === 'Active' || status === 1) {
    return 'Active'
  }

  if (status === 'Paused' || status === 2) {
    return 'Paused'
  }

  const normalized = String(status ?? '').trim().toLowerCase()
  if (normalized === 'draft' || normalized === '0') {
    return 'Draft'
  }

  if (normalized === 'active' || normalized === '1') {
    return 'Active'
  }

  if (normalized === 'paused' || normalized === '2') {
    return 'Paused'
  }

  return 'Draft'
}

function normalizeDayTokens(rawDays: string) {
  if (!rawDays || rawDays === '*') {
    return [...dayOptions]
  }

  const tokens = rawDays.split(',').flatMap((item) => {
    const token = item.trim().toUpperCase()
    if (token.includes('-')) {
      const [start, end] = token.split('-')
      return expandDayRange(start, end)
    }
    return token
  })

  const normalized = tokens.filter((item): item is string => dayOptions.includes(item))
  return normalized.length > 0 ? normalized : [...dayOptions]
}

function expandDayRange(start: string, end: string) {
  const startIndex = dayOptions.indexOf(start)
  const endIndex = dayOptions.indexOf(end)
  if (startIndex < 0 || endIndex < 0) return []
  if (startIndex <= endIndex) return dayOptions.slice(startIndex, endIndex + 1)
  return [...dayOptions.slice(startIndex), ...dayOptions.slice(0, endIndex + 1)]
}

function currentDayToken() {
  return ['SUN', 'MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT'][new Date().getDay()]
}

function describeDays(days: string[]) {
  if (days.length === dayOptions.length) {
    return ''
  }

  if (sameDays(days, ['MON', 'TUE', 'WED', 'THU', 'FRI'])) {
    return ' de lunes a viernes'
  }

  if (sameDays(days, ['SAT', 'SUN'])) {
    return ' los fines de semana'
  }

  const translated = days.map((item) => dayLabels[item] ?? item.toLowerCase())
  if (translated.length === 1) return ` los ${translated[0]}`
  if (translated.length === 2) return ` los ${translated[0]} y ${translated[1]}`
  return ` los ${translated.slice(0, -1).join(', ')} y ${translated.at(-1)}`
}

function sameDays(left: string[], right: string[]) {
  return left.length === right.length && left.every((item, index) => item === right[index])
}

function formatTime(time: string) {
  const [hourText, minuteText] = time.split(':')
  const hour = Number(hourText)
  const minute = Number(minuteText)
  const suffix = hour >= 12 ? 'PM' : 'AM'
  const displayHour = hour % 12 === 0 ? 12 : hour % 12
  return `${displayHour}:${String(minute).padStart(2, '0')} ${suffix}`
}
