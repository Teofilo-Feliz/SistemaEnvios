# SistemaEnvios

Backend del sistema logístico de envíos REH.

## Configuración local

La cadena de conexión no se guarda en el repositorio. Debe configurarse mediante
User Secrets o con la variable de entorno `ConnectionStrings__SistemaEnvios`.

```powershell
dotnet user-secrets init --project SistemaEnvios.Api
dotnet user-secrets set "ConnectionStrings:SistemaEnvios" "SU_CADENA" --project SistemaEnvios.Api
```

El esquema completo de SQL Server se encuentra en `Database/SistemaEnviosDB.sql`.

## Verificación

```powershell
dotnet test SistemaEnvios.slnx
```

La API exige autenticación por defecto. El token externo debe incluir el identificador
`Guid` del usuario en el claim `sub` y los permisos en uno o varios claims `permissions`.
Los valores admitidos son:

- `envios.consultar`
- `envios.crear`
- `envios.editar`
- `envios.despachar`
- `transportes.gestionar`
- `transportes.confirmar`
- `recepciones.gestionar`
- `incidencias.gestionar`
- `equipos.gestionar`
- `catalogos.administrar`

En desarrollo, Vite está autorizado desde `http://localhost:5173`.
