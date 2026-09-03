import type { Meta, StoryObj } from '@storybook/react-vite'
import { useState } from 'react'
import type { CampaignQuestion } from '../types'
import { QuestionBuilder } from './QuestionBuilder'

const sampleQuestions: CampaignQuestion[] = [
  {
    id: 'question-1',
    text: 'Como calificas tu jornada de hoy?',
    type: 'Scale',
    minValue: 1,
    maxValue: 5,
    placeholder: null,
    options: null,
  },
  {
    id: 'question-2',
    text: 'Que bloqueo deberiamos revisar?',
    type: 'Text',
    minValue: null,
    maxValue: null,
    placeholder: 'Describe brevemente la situacion',
    options: null,
  },
]

const meta = {
  title: 'Components/QuestionBuilder',
  component: QuestionBuilder,
  parameters: {
    layout: 'centered',
  },
} satisfies Meta<typeof QuestionBuilder>

export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {
  render: function QuestionBuilderStory() {
    const [questions, setQuestions] = useState(sampleQuestions)

    return (
      <div className="w-[720px] max-w-[calc(100vw-48px)]">
        <QuestionBuilder questions={questions} onChange={setQuestions} />
      </div>
    )
  },
}

export const Compact: Story = {
  render: function CompactQuestionBuilderStory() {
    const [questions, setQuestions] = useState<CampaignQuestion[]>([
      {
        id: 'choice-question',
        text: 'Como te sientes hoy?',
        type: 'Choice',
        minValue: null,
        maxValue: null,
        placeholder: null,
        options: ['Con energia', 'Neutral', 'Sobrecargado', '', ''],
      },
    ])

    return (
      <div className="w-[560px] max-w-[calc(100vw-48px)]">
        <QuestionBuilder questions={questions} onChange={setQuestions} compact />
      </div>
    )
  },
}
