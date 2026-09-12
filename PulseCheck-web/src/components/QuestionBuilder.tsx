import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import InsertEmoticonOutlinedIcon from '@mui/icons-material/InsertEmoticonOutlined'
import EmojiPicker, { EmojiStyle, Theme, type EmojiClickData } from 'emoji-picker-react'
import { useState } from 'react'
import type { CampaignQuestion, QuestionType } from '../types'
import { createChoiceQuestion, createScaleQuestion, createTextQuestion, createYesNoQuestion } from '../utils/campaigns'
import { inputClassName, SmallBtn } from './ui'

const maxChoiceOptionLength = 120

export function QuestionBuilder({
  questions,
  onChange,
  compact,
}: {
  questions: CampaignQuestion[]
  onChange: (questions: CampaignQuestion[]) => void
  compact?: boolean
}) {
  const [activeEmojiPicker, setActiveEmojiPicker] = useState<string | null>(null)

  return (
    <div className="rounded-2xl border border-[#d5e5eb] bg-[#f9fcfe] p-3">
      <p className="mb-2 text-sm font-semibold text-[#153845]">Preguntas de la campaña</p>
      <p className="mb-3 text-xs text-[#5f7782]">Puedes combinar preguntas con escala, respuestas abiertas, Sí/No y opciones personalizadas. El agente las muestra una por una.</p>
      <div className="space-y-2">
        {questions.map((question, index) => (
          <div key={question.id} className="space-y-2 rounded-xl border border-[#d6e7ed] bg-white p-3">
            <div className="flex items-center justify-between gap-2">
              <p className="text-xs text-[#5f7782]">Pregunta {index + 1}</p>
              {questions.length > 1 ? (
                <Button
                  type="button"
                  onClick={() => onChange(questions.filter((item) => item.id !== question.id))}
                  size="small"
                  color="warning"
                  sx={{ minWidth: 0, p: 0.5, fontSize: 12, fontWeight: 800, textTransform: 'none' }}
                >
                  Quitar
                </Button>
              ) : null}
            </div>

            <input
              className={inputClassName}
              placeholder="Escribe la pregunta"
              value={question.text}
              onChange={(event) => onChange(questions.map((item) => item.id === question.id ? { ...item, text: event.target.value } : item))}
            />

            <div className="grid gap-2 sm:grid-cols-3">
              <select
                className={inputClassName}
                value={question.type}
                onChange={(event) => onChange(questions.map((item) => item.id === question.id ? mapQuestionType(item, event.target.value as QuestionType) : item))}
              >
                <option value="Scale">Escala numérica</option>
                <option value="Text">Respuesta abierta</option>
                <option value="YesNo">Sí o No</option>
                <option value="Choice">Personalizada</option>
              </select>

              {question.type === 'Scale' ? (
                <>
                  <input
                    type="number"
                    className={inputClassName}
                    placeholder="Valor mínimo"
                    value={question.minValue ?? 1}
                    onChange={(event) => onChange(questions.map((item) => item.id === question.id ? { ...item, minValue: Number(event.target.value), maxValue: Math.max(Number(event.target.value), item.maxValue ?? Number(event.target.value)) } : item))}
                  />
                  <input
                    type="number"
                    className={inputClassName}
                    placeholder="Valor máximo"
                    value={question.maxValue ?? 5}
                    onChange={(event) => onChange(questions.map((item) => item.id === question.id ? { ...item, maxValue: Math.max(item.minValue ?? 1, Number(event.target.value)) } : item))}
                  />
                </>
              ) : question.type === 'Text' ? (
                <input
                  className={`${inputClassName} sm:col-span-2`}
                  placeholder="Texto guía opcional para la respuesta"
                  value={question.placeholder ?? ''}
                  onChange={(event) => onChange(questions.map((item) => item.id === question.id ? { ...item, placeholder: event.target.value } : item))}
                />
              ) : question.type === 'Choice' ? (
                <div className="grid gap-2 sm:col-span-2">
                  {getEditableChoiceOptions(question).map((option, optionIndex) => (
                    <div key={`${question.id}-choice-${optionIndex}`} className="relative flex gap-2">
                      <input
                        className={inputClassName}
                        maxLength={maxChoiceOptionLength}
                        placeholder={`Opción ${optionIndex + 1}`}
                        value={option}
                        onChange={(event) => onChange(questions.map((item) => item.id === question.id ? updateChoiceOption(item, optionIndex, event.target.value) : item))}
                      />
                      <IconButton
                        type="button"
                        aria-label={`Insertar emoji en opción ${optionIndex + 1}`}
                        onClick={() => setActiveEmojiPicker(activeEmojiPicker === getEmojiPickerKey(question.id, optionIndex) ? null : getEmojiPickerKey(question.id, optionIndex))}
                        sx={{
                          border: '1px solid #b7dbe4',
                          color: '#00758d',
                          width: 44,
                          height: 44,
                          flex: '0 0 auto',
                          '&:hover': { backgroundColor: '#eff9fc' },
                        }}
                      >
                        <InsertEmoticonOutlinedIcon fontSize="small" />
                      </IconButton>
                      {getFilledChoiceOptions(question).length > 2 && option.trim() ? (
                        <button
                          type="button"
                          onClick={() => onChange(questions.map((item) => item.id === question.id ? clearChoiceOption(item, optionIndex) : item))}
                          className="px-2 text-xs font-bold text-[#00758d] hover:text-[#005f73]"
                        >
                          Eliminar
                        </button>
                      ) : null}
                      {activeEmojiPicker === getEmojiPickerKey(question.id, optionIndex) ? (
                        <div className="absolute right-0 top-12 z-30 rounded-2xl border border-[#cde3ea] bg-white p-2 shadow-2xl">
                          <EmojiPicker
                            width={320}
                            height={360}
                            theme={Theme.LIGHT}
                            emojiStyle={EmojiStyle.NATIVE}
                            lazyLoadEmojis
                            previewConfig={{ showPreview: false }}
                            onEmojiClick={(emojiData: EmojiClickData) => {
                              onChange(questions.map((item) => item.id === question.id ? appendEmojiToChoiceOption(item, optionIndex, emojiData.emoji) : item))
                              setActiveEmojiPicker(null)
                            }}
                          />
                        </div>
                      ) : null}
                    </div>
                  ))}
                  <p className="text-xs text-[#5f7782]">Configura entre 2 y 5 opciones. El colaborador escogerá una sola respuesta.</p>
                </div>
              ) : (
                <div className="sm:col-span-2 rounded-2xl border border-[#d5e5eb] bg-[#f8fbfd] px-4 py-3 text-sm text-[#56707b]">
                  El colaborador verá dos botones: <strong>Sí</strong> y <strong>No</strong>.
                </div>
              )}
            </div>
          </div>
        ))}
      </div>

      <div className={`mt-3 flex gap-2 ${compact ? 'flex-wrap' : ''}`}>
        <SmallBtn label="Agregar escala" onClick={() => onChange([...questions, createScaleQuestion(1, 5)])} />
        <SmallBtn label="Agregar texto" onClick={() => onChange([...questions, createTextQuestion()])} />
        <SmallBtn label="Agregar Sí/No" onClick={() => onChange([...questions, createYesNoQuestion()])} />
        <SmallBtn label="Agregar personalizada" onClick={() => onChange([...questions, createChoiceQuestion()])} />
      </div>
    </div>
  )
}

function mapQuestionType(question: CampaignQuestion, type: QuestionType): CampaignQuestion {
  if (type === 'Scale') {
    return {
      ...question,
      type,
      minValue: question.minValue ?? 1,
      maxValue: question.maxValue ?? 5,
      placeholder: null,
      options: null,
    }
  }

  if (type === 'Text') {
    return {
      ...question,
      type,
      minValue: null,
      maxValue: null,
      placeholder: question.placeholder ?? '',
      options: null,
    }
  }

  if (type === 'Choice') {
    return {
      ...question,
      type,
      minValue: null,
      maxValue: null,
      placeholder: null,
      options: getEditableChoiceOptions(question),
    }
  }

  return {
    ...question,
    type,
    minValue: null,
    maxValue: null,
    placeholder: null,
    options: null,
  }
}

function getEditableChoiceOptions(question: CampaignQuestion) {
  const options = question.options ? question.options.slice(0, 5) : []
  while (options.length < 5) {
    options.push('')
  }
  return options
}

function getFilledChoiceOptions(question: CampaignQuestion) {
  return getEditableChoiceOptions(question)
    .map((option) => option.trim())
    .filter(Boolean)
}

function updateChoiceOption(question: CampaignQuestion, index: number, value: string): CampaignQuestion {
  const options = getEditableChoiceOptions(question)
  options[index] = limitChoiceOption(value)
  return {
    ...question,
    options,
  }
}

function appendEmojiToChoiceOption(question: CampaignQuestion, index: number, emoji: string): CampaignQuestion {
  const options = getEditableChoiceOptions(question)
  options[index] = limitChoiceOption(`${options[index] ?? ''}${emoji}`)
  return {
    ...question,
    options,
  }
}

function clearChoiceOption(question: CampaignQuestion, index: number): CampaignQuestion {
  const options = getEditableChoiceOptions(question)
  options[index] = ''
  return {
    ...question,
    options,
  }
}

function getEmojiPickerKey(questionId: string, optionIndex: number) {
  return `${questionId}:${optionIndex}`
}

function limitChoiceOption(value: string) {
  return Array.from(value).slice(0, maxChoiceOptionLength).join('')
}
