import CssBaseline from '@mui/material/CssBaseline'
import { ThemeProvider } from '@mui/material/styles'
import type { Preview } from '@storybook/react-vite'
import { pulseTheme } from '../src/theme'
import '../src/styles.css'

const preview: Preview = {
  decorators: [
    (Story) => (
      <ThemeProvider theme={pulseTheme}>
        <CssBaseline />
        <div style={{ minHeight: '100vh', padding: 24 }}>
          <Story />
        </div>
      </ThemeProvider>
    ),
  ],
  parameters: {
    layout: 'fullscreen',
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
    a11y: {
      test: 'todo',
    },
  },
}

export default preview
