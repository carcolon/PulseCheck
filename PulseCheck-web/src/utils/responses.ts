import type { QuestionType, ResponseItem } from '../types'

export function normalizeResponse(raw: unknown): ResponseItem {
  const candidate = raw as Partial<ResponseItem>
  const rawType = (raw as { questionType?: unknown }).questionType
  const normalizedType: QuestionType = rawType === 'Text' || rawType === 1
    ? 'Text'
    : rawType === 'YesNo' || rawType === 2
      ? 'YesNo'
      : rawType === 'Choice' || rawType === 3
        ? 'Choice'
        : 'Scale'

  return {
    id: String(candidate.id ?? crypto.randomUUID()),
    campaignId: String(candidate.campaignId ?? ''),
    questionId: String(candidate.questionId ?? ''),
    questionText: String(candidate.questionText ?? '').trim(),
    questionType: normalizedType,
    deviceId: String(candidate.deviceId ?? ''),
    userId: String(candidate.userId ?? ''),
    userName: String(candidate.userName ?? 'Usuario'),
    email: String(candidate.email ?? ''),
    department: String(candidate.department ?? ''),
    hostname: String(candidate.hostname ?? ''),
    numericValue: typeof candidate.numericValue === 'number' ? candidate.numericValue : null,
    minValue: typeof candidate.minValue === 'number' ? candidate.minValue : null,
    maxValue: typeof candidate.maxValue === 'number' ? candidate.maxValue : null,
    textValue: typeof candidate.textValue === 'string' ? candidate.textValue : null,
    submissionId: String(candidate.submissionId ?? ''),
    answeredAtUtc: String(candidate.answeredAtUtc ?? new Date().toISOString()),
  }
}

export function formatResponseAnswer(response: ResponseItem) {
  if (response.questionType === 'Text') {
    return response.textValue ?? '(sin texto)'
  }

  if (response.questionType === 'YesNo') {
    return response.textValue ?? '(sin respuesta)'
  }

  if (response.questionType === 'Choice') {
    return response.textValue ?? '(sin respuesta)'
  }

  if (response.numericValue === null) {
    return '(sin valor)'
  }

  return `${response.numericValue}`
}
