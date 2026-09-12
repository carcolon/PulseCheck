import { createTheme } from '@mui/material/styles'

export const pulseTheme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: '#00758d',
      dark: '#073544',
      light: '#00bed6',
      contrastText: '#ffffff',
    },
    secondary: {
      main: '#ee7623',
      dark: '#b74d0d',
      light: '#f09f54',
      contrastText: '#ffffff',
    },
    success: {
      main: '#0f8f78',
    },
    warning: {
      main: '#bc7311',
    },
    error: {
      main: '#b03d37',
    },
    background: {
      default: '#edf5f8',
      paper: '#ffffff',
    },
    text: {
      primary: '#0d3140',
      secondary: '#5f7782',
    },
  },
  typography: {
    fontFamily: '"Manrope", "Segoe UI Variable", sans-serif',
    h1: {
      fontFamily: '"Sora", "Manrope", sans-serif',
      fontWeight: 800,
      letterSpacing: 0,
    },
    h2: {
      fontFamily: '"Sora", "Manrope", sans-serif',
      fontWeight: 800,
      letterSpacing: 0,
    },
    h3: {
      fontFamily: '"Sora", "Manrope", sans-serif',
      fontWeight: 800,
      letterSpacing: 0,
    },
    h4: {
      fontFamily: '"Sora", "Manrope", sans-serif',
      fontWeight: 800,
      letterSpacing: 0,
    },
    button: {
      fontWeight: 800,
      textTransform: 'none',
      letterSpacing: 0,
    },
  },
  shape: {
    borderRadius: 16,
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          background:
            'linear-gradient(135deg, rgba(7,53,68,0.06) 0%, rgba(0,117,141,0.06) 36%, rgba(238,118,35,0.08) 100%)',
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
        },
      },
    },
    MuiButton: {
      styleOverrides: {
        root: {
          borderRadius: 999,
          boxShadow: 'none',
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: {
          fontWeight: 800,
        },
      },
    },
  },
})
