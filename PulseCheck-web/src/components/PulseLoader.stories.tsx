import type { Meta, StoryObj } from '@storybook/react-vite'
import { PulseLoader } from './PulseLoader'

const meta = {
  title: 'Components/PulseLoader',
  component: PulseLoader,
  parameters: {
    layout: 'centered',
  },
} satisfies Meta<typeof PulseLoader>

export default meta
type Story = StoryObj<typeof meta>

export const Inline: Story = {
  args: {
    title: 'Cargando panel administrativo',
    caption: 'Sincronizando campanas, agentes y respuestas',
    fullScreen: false,
  },
}

export const FullScreen: Story = {
  args: {
    title: 'Restaurando sesion',
    caption: 'Validando tu acceso administrativo',
    fullScreen: true,
  },
  parameters: {
    layout: 'fullscreen',
  },
}
