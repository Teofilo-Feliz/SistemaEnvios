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

## Integración con GLPI

La API consulta la mesa de ayuda para confirmar que un ticket o un activo existe. Solo la URL
vive en `appsettings.json`; las tres credenciales van por User Secrets en local y por variables
de entorno (`Glpi__AppToken`, `Glpi__Username`, `Glpi__Password`) en despliegue.

```powershell
dotnet user-secrets set "Glpi:AppToken" "SU_APP_TOKEN" --project SistemaEnvios.Api
dotnet user-secrets set "Glpi:Username" "SU_USUARIO"   --project SistemaEnvios.Api
dotnet user-secrets set "Glpi:Password" "SU_CLAVE"     --project SistemaEnvios.Api
```

Sin ellas la API no arranca, igual que sin la cadena de conexión: una credencial ausente
descubierta en la primera consulta real es un 502 en la cara del usuario.

El endpoint es `GET /api/glpi/{itemType}/{id}/existe` (con atajos `ticket` y `computer`) y exige
el permiso `envios.consultar`. Devuelve `200` con `{ itemType, id, existe }`; si GLPI está caído
responde `502`, nunca `existe: false`, para que una integración rota no se confunda con un
ticket inválido.

Las pruebas contra la instancia real se saltan si no hay credenciales en el entorno:

```powershell
$env:GLPI_BASE_URL="..."; $env:GLPI_APP_TOKEN="..."; $env:GLPI_USERNAME="..."; $env:GLPI_PASSWORD="..."
dotnet test SistemaEnvios.slnx --filter "FullyQualifiedName~Glpi"
```

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
