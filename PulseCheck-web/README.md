# PulseCheck Web

Panel admin React + Vite para campañas, métricas y operación de PulseCheck.

## Desarrollo local

```powershell
npm install
npm run dev
```

## Build

```powershell
npm run build
```

## Ambientes

- `.env.development`
- `.env.production`
- `.env.example`

## Pipeline

- `azure-pipelines-web.yml`

## Despliegue esperado

- `dev`: Azure Static Web App `swa-front-dev`
- `prod`: Azure Static Web App `swa-front-prod`

El build toma `VITE_API_BASE_URL` y, cuando se habilite Entra ID, tambien `VITE_ENTRA_TENANT_ID` y `VITE_ENTRA_CLIENT_ID`.
