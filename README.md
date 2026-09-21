# SistemaEnvios

Sistema logístico de envíos de la ADR. El API en .NET 10 y el cliente Vue 3 se publican como un
**único artefacto**: la aplicación sirve el SPA compilado desde `wwwroot` y el navegador pide
todo al mismo origen, sin CORS de por medio.

Tres nombres para lo mismo, que conviene no confundir: el repositorio y la solución se llaman
`SistemaEnvios`, el producto se llama **LogiTrack** (es lo que ve el usuario y el `client_id`
registrado en AuthManager es `logi-track-app`), y la imagen Docker es `track-adr`.

```
SistemaEnvios.Api/             API, autenticación y composición del pipeline
SistemaEnvios.Application/     Casos de uso, DTOs y validadores
SistemaEnvios.Domain/          Entidades y reglas
SistemaEnvios.Infrastructure/  EF Core, repositorios e integración con GLPI
SistemaEnvios.Tests/           Pruebas unitarias y de integración
FrontEnd/                      SPA en Vue 3 + Vite
Database/                      Esquema y migraciones de SQL Server
.teamcity/                     Pipeline de build y despliegue
```

## Configuración local

La cadena de conexión no se guarda en el repositorio. Debe configurarse mediante
User Secrets o con la variable de entorno `ConnectionStrings__SistemaEnvios`.

```powershell
dotnet user-secrets init --project SistemaEnvios.Api
dotnet user-secrets set "ConnectionStrings:SistemaEnvios" "SU_CADENA" --project SistemaEnvios.Api
```

`Database/SistemaEnviosDB.sql` levanta el entorno entero en un solo archivo: esquema, datos de
referencia y el usuario `sistema_envios_app` con el que se conecta el API. Crea una base llamada
**ADRTrack**, y la recrea desde cero —hace `DROP DATABASE`—, así que no sirve para actualizar una
base con datos. Antes de la primera ejecución hay que poner una contraseña real en `@Clave`, en
el PASO FINAL; si el login ya existe no hace falta tocar nada.

Los scripts de `Database/Migrations/` **no hacen falta en una base nueva**: reparan bases ya
creadas, y lo que hacían está incorporado en el archivo principal.

Para comprobar que una base está al nivel del código, apunte las pruebas a ella:
`ElModeloCoincideConElEsquemaReal` compara tabla por tabla y nombra la columna que falte.

```powershell
$env:SISTEMAENVIOS_TEST_SQL = "Server=localhost;Database=ADRTrack;Trusted_Connection=True;TrustServerCertificate=True"
dotnet test SistemaEnvios.Tests
```

## Desarrollo

**F5 en Visual Studio levanta los dos procesos.** El proyecto usa `Microsoft.AspNetCore.SpaProxy`:
al arrancar el API en Development, ejecuta `npm run dev` en `FrontEnd/`, espera a que Vite
responda y abre el navegador. No hay que lanzar el frontend aparte.

| Proceso | Puerto |
| ------- | ------ |
| API (perfil `http`)  | 5068 |
| API (perfil `https`) | 7279 |
| Vite                 | 5173 |

Si Vite ya está corriendo en el 5173, SpaProxy lo reutiliza en vez de abrir otro. Y si un
arranque anterior quedó vivo, el siguiente falla con `Failed to bind to address ... 5068:
address already in use`: hay un proceso huérfano que cerrar, no un problema de configuración.

Desde la línea de comandos, `dotnet run --project SistemaEnvios.Api --launch-profile http` hace
exactamente lo mismo, porque lee el mismo `launchSettings.json`.

## Configuración por entorno

Hay **dos entornos**, y cada uno se declara en dos lugares que tienen que concordar: uno para el
API y otro para el SPA. Son ejes independientes —ASP.NET Core y Vite no se conocen— y apuntan al
mismo AuthManager por caminos distintos.

| Entorno | `ASPNETCORE_ENVIRONMENT` | Archivo del API             | Modo de Vite | Archivo del SPA    |
| ------- | ------------------------ | --------------------------- | ------------ | ------------------ |
| Local   | `Development`            | `appsettings.Development.json` | `development` | `.env.development` |
| QA      | `Development`            | `appsettings.Development.json` | `qa`          | `.env.qa`          |
| PROD    | `Production`             | `appsettings.Production.json`  | `production`  | `.env.production`  |

Desalineados, el usuario inicia sesión contra un AuthManager y el API valida contra otro: el
login parece funcionar y toda llamada responde 403. El script de despliegue lo comprueba y
aborta antes de tocar el Swarm.

`appsettings.json` conserva únicamente lo que no cambia entre entornos: el `client_id` admitido,
la URL de GLPI y los niveles de log. **El emisor (`Authentication:Authority`) no está ahí a
propósito**: un valor por defecto compartido significaría que Producción hereda el emisor de QA
sin que nada lo delate. Su ausencia hace fallar el arranque con un mensaje que nombra el entorno
activo.

En Linux el archivo se busca literalmente como `appsettings.${ASPNETCORE_ENVIRONMENT}.json`, así
que la capitalización importa: `development` no carga `appsettings.Development.json`.

Los `.env.<modo>` del frontend **sí están versionados** —solo llevan URLs públicas y el
identificador de la aplicación— y son autocontenidos: al abrir `.env.production` se ve
exactamente con qué se construye Producción. El `.env` local es opcional, está ignorado por git,
y los archivos por modo tienen prioridad sobre él.

Las credenciales nunca viven en ninguno de estos archivos: van por User Secrets en local y por
variables de entorno en despliegue.

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
- `transportes.administrar`
- `recepciones.gestionar`
- `incidencias.gestionar`
- `equipos.gestionar`
- `catalogos.administrar`

En desarrollo, Vite está autorizado desde `http://localhost:5173`.

## Imagen Docker

Tres etapas: Node compila el SPA, el SDK de .NET publica el API, y la imagen de runtime junta
ambos con un usuario sin privilegios. Escucha en el 8080.

```bash
docker build -t track-adr:local --build-arg BUILD_MODE=production .
docker build -t track-adr:qa    --build-arg BUILD_MODE=qa .
```

**`BUILD_MODE` es el único argumento de construcción**, y es deliberado. Pasar cada variable del
frontend por separado era una trampa: un `--build-arg` olvidado llega como variable de entorno
vacía, Vite le da precedencia sobre los archivos `.env.<modo>`, y el bundler elimina el código de
autenticación por dead-code elimination. La imagen se construía sin error, arrancaba sana y
mostraba el botón de login deshabilitado. Como `import.meta.env` se resuelve al construir,
ninguna variable del contenedor lo arregla. Ahora `vite.config.js` rompe el build si el emisor o
el `client_id` llegan vacíos.

Un `dotnet publish` normal en Windows también produce el paquete completo: el `.csproj` compila
el SPA y lo copia a `wwwroot`. El Dockerfile lo desactiva con `/p:SpaRoot=""` porque allí el
frontend ya viene compilado de su propia etapa y la imagen del SDK no tiene Node.

El `HEALTHCHECK` consulta `/health/ready`, que comprueba también SQL Server. Una comprobación por
TCP daba por sano un contenedor con Kestrel escuchando y sin base de datos, y Swarm completaba el
despliegue sin disparar el rollback.

## Despliegue

`docker-stack.yml` describe el servicio para Docker Swarm: réplicas, límites, rolling update
`start-first` con rollback automático y rotación de logs. Solo transporta **secretos** —la
cadena de conexión y las tres credenciales de GLPI— porque las variables de entorno tienen mayor
precedencia que los `appsettings.json` y declarar ahí el emisor anularía la configuración por
entorno.

```bash
export APP_IMAGE=<registry>/rehabilitacion/track-adr:<tag>
export ASPNETCORE_ENVIRONMENT=Production
export SISTEMA_ENVIOS_CONNECTION="..." REPLICAS=2
export GLPI_APP_TOKEN="..." GLPI_USERNAME="..." GLPI_PASSWORD="..."
docker stack deploy -c docker-stack.yml --with-registry-auth track-adr
```

Requiere que el nodo sea manager de un Swarm activo, que exista la red externa `adr_network` y
el directorio `/opt/track-adr`. Para revertir un despliegue: `docker service rollback track-adr_app`.

## Integración continua

`.teamcity/settings.kts` define dos configuraciones —build con push a Harbor, y despliegue por
SSH al Swarm— parametrizadas por entorno. Una misma definición sirve a QA y a Producción; lo que
cambia son los parámetros de cada instancia de TeamCity.

El repositorio vive en Azure DevOps y TeamCity lo clona con un PAT configurado en el VCS Root, no
como parámetro de build. El proyecto no consume feeds privados de NuGet: todos sus paquetes son
de nuget.org, así que el `Dockerfile` no monta ningún secreto durante el `dotnet restore`.

**Los valores concretos y su significado están en [`.teamcity/PARAMETROS.md`](.teamcity/PARAMETROS.md)**,
incluida la lista de los 14 parámetros a crear, cuáles van como `Password`, y —importante— qué
parámetros de otros proyectos de la institución **no** aplican aquí y por qué.
