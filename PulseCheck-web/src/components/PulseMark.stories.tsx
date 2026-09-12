import type { Meta, StoryObj } from '@storybook/react-vite'
import { PulseMark } from './PulseMark'

const meta = {
  title: 'Components/PulseMark',
  component: PulseMark,
  parameters: {
    layout: 'centered',
  },
} satisfies Meta<typeof PulseMark>

export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {
  render: () => <PulseMark className="h-32 w-32" />,
}
