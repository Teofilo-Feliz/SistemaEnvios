# Parámetros TeamCity por instancia

El `settings.kts` define **solo lógica**. Los valores que cambian entre entornos viven **fuera del VCS**, configurados a mano en cada instancia TeamCity (QA y PROD).

Son **14 parámetros manuales**. Otros tres (`env.APP_IMAGE`, `env.APP_IMAGE_LATEST`, `env.BUILD_TAG`) los calcula el propio `settings.kts`: no hay que crearlos.

## Dónde configurar

En cada servidor TeamCity:

**Administration → `<Root project>` → Parameters** *(recomendado — heredados por todos los proyectos)*

o, si no hay acceso a Root:

**Project `TrackAdr` → Parameters** *(siempre que UI editing esté DESHABILITADO para evitar patches al VCS — ver "Reglas de oro")*

Tipos:
- `Configuration parameter` → texto plano.
- `Password` → credenciales (almacenado cifrado, no se versiona).

---

## Reglas de oro

1. **Una sola instancia TC con `Allow editing project settings via UI` activado** sobre este repo. La otra debe quedar en read-only.
2. Si una instancia debe editar UI, sus cambios commitearán al VCS y propagarán a la otra → coordinar.
3. Para valores env-specific (que difieren entre QA y PROD): definirlos en `<Root>` de cada instancia. Nunca en `TrackAdr` UI con sync activo.
4. Credenciales: usar `Password` type con valor real por instancia.

---

## La regla que no se puede romper

`env.BUILD_MODE` y `env.ASPNETCORE_ENVIRONMENT` apuntan al mismo emisor de tokens por caminos distintos: el primero elige el `.env.<modo>` con que se compila el SPA, el segundo elige el `appsettings.<entorno>.json` con que el API valida. **Solo hay dos combinaciones válidas:**

| Instancia | `env.BUILD_MODE` | `env.ASPNETCORE_ENVIRONMENT` |
| --------- | ---------------- | ---------------------------- |
| QA        | `qa`             | `Development`                |
| PROD      | `production`     | `Production`                 |

Desalineadas, el usuario inicia sesión contra un AuthManager y el API valida contra otro: el login parece funcionar y **toda llamada devuelve 403**. El script de deploy lo comprueba antes de tocar el Swarm y aborta con exit 1 si no concuerdan.

Que QA use `Development` no es un descuido: solo hay dos archivos de configuración, `appsettings.Development.json` (emisor de QA) y `appsettings.Production.json`. Tiene tres consecuencias que conviene conocer:

- El endpoint de **OpenAPI existe** en QA (`Program.cs` lo publica solo en Development) y no en Producción. No es anónimo: como cualquier endpoint, lo cubre la política de autorización por defecto, así que `/openapi/v1.json` responde 401 sin token y queda legible para cualquier llamante autenticado. Es deliberado: en un entorno de pruebas interno tener el esquema a mano ayuda.
- No se aplica `UseHttpsRedirection`. Sin efecto real: el contenedor escucha solo HTTP y la redirección ya era inoperante por no poder determinar el puerto.
- El archivo lleva `Cors:AllowedOrigins` con `http://localhost:5173`, que viaja a la imagen. Inofensivo —nadie sirve el SPA desde localhost contra QA— pero explica por qué QA registra una política CORS y Producción no.

Ojo con las mayúsculas: en Linux el archivo se busca literalmente como `appsettings.${ASPNETCORE_ENVIRONMENT}.json`. `development` en minúscula no carga `appsettings.Development.json`, y la aplicación no arranca (mensaje explícito de `Authentication:Authority`). El guard también rechaza esa capitalización.

---

## Parámetros requeridos

### Docker / Harbor

| Parámetro             | Tipo     | QA                               | PROD                           |
| --------------------- | -------- | -------------------------------- | ------------------------------ |
| `env.HARBOR_REGISTRY` | text     | `harborqa.rehabilitacion.org.do` | `harbor.rehabilitacion.org.do` |
| `env.HARBOR_USER`     | text     | *(robot account QA)*             | *(robot account PROD)*         |
| `env.HARBOR_PASSWORD` | password | *(token QA)*                     | *(token PROD)*                 |

> Los hosts de Harbor son los de la infraestructura compartida de la institución; el robot account debe crearse con permiso de push sobre el proyecto `rehabilitacion` para el repositorio `track-adr`. La imagen se publica como `<registry>/rehabilitacion/track-adr:<build.number>` y además como `:latest`.

### Deploy SSH

| Parámetro             | Tipo     | QA               | PROD              |
| --------------------- | -------- | ---------------- | ----------------- |
| `env.DEPLOY_SERVER`   | text     | *(IP Swarm QA)*  | *(IP Swarm PROD)* |
| `env.DEPLOY_USER`     | text     | `usr.deploy`     | `usr.deploy`      |
| `env.DEPLOY_PASSWORD` | password | *(pass QA)*      | *(pass PROD)*     |

### Aplicación

| Parámetro                       | Tipo     | QA                                   | PROD                                   |
| ------------------------------- | -------- | ------------------------------------ | -------------------------------------- |
| `env.BUILD_MODE`                | text     | `qa`                                 | `production`                           |
| `env.ASPNETCORE_ENVIRONMENT`    | text     | `QA`                                 | `Production`                           |
| `env.SISTEMA_ENVIOS_CONNECTION` | password | *(conn string SQL QA — DB SistemaEnvios)* | *(conn string SQL PROD — DB SistemaEnvios)* |
| `env.GLPI_APP_TOKEN`            | password | *(App Token del API Client en GLPI)* | *(App Token del API Client en GLPI)*   |
| `env.GLPI_USERNAME`             | password | *(usuario de servicio GLPI)*         | *(usuario de servicio GLPI)*           |
| `env.GLPI_PASSWORD`             | password | *(clave del usuario GLPI)*           | *(clave del usuario GLPI)*             |
| `env.REPLICAS`                  | text     | `1`                                  | `2`                                    |

> `env.SISTEMA_ENVIOS_CONNECTION` se inyecta como `ConnectionStrings__SistemaEnvios`. Es **fail-fast**: si llega vacía la aplicación lanza `InvalidOperationException` al arrancar, las réplicas no convergen y el deploy hace rollback a los 120 s. El usuario de base de datos esperado es `sistema_envios_app`, que crean los scripts de `Database/Migrations/20260908_UsuarioDelApi.sql`.

> Las tres de GLPI se inyectan como `Glpi__AppToken`, `Glpi__Username` y `Glpi__Password`, y también son **fail-fast**: `GlpiOptions.Validar()` enumera las que falten y aborta el arranque. La URL (`Glpi:BaseUrl`) **no** es parámetro: vive en `appsettings.json` porque es la misma mesa de ayuda en los tres entornos.
>
> Advertencia operativa: el fail-fast detecta la credencial **ausente**, no la **incorrecta**. Con un token inválido el contenedor arranca, converge y solo falla al consultar un ticket real, devolviendo 502. El primer despliegue de cada ambiente exige probar `GET /api/glpi/ticket/<id>/existe`, no basta con que las réplicas converjan.

> `env.REPLICAS` controla el número de réplicas en Swarm. QA usa `1` (sin HA), PROD usa `2` (rolling update `start-first`). En el `service update` se pasa por `--replicas`; en el bootstrap `stack deploy` se exporta y `docker-stack.yml` lo interpola como `${REPLICAS:-2}`. Debe estar definido en ambas instancias; si falta, el `service update` falla con `Parameter not defined`.

### Triggers

| Parámetro           | Tipo | QA          | PROD       |
| ------------------- | ---- | ----------- | ---------- |
| `env.BRANCH_FILTER` | text | `+:testing` | `+:master` |

> Cada instancia escucha una sola rama: `testing` alimenta QA y `master` alimenta Producción. Una rama de trabajo no dispara ningún despliegue hasta que se integra en una de las dos.
>
> El trigger ignora cambios en `.teamcity/**`, `Database/**` y `README.md`: editar el pipeline o un script SQL no reconstruye la imagen.

---

## Lo que NO es parámetro (y por qué)

Esta sección existe para que nadie copie parámetros de otros proyectos de la institución que aquí no aplican.

| No configurar          | Dónde vive realmente                                                  |
| ---------------------- | --------------------------------------------------------------------- |
| `vite.*` (5 params)    | `FrontEnd/.env.qa` y `.env.production`, versionados. El pipeline pasa un solo `--build-arg BUILD_MODE`. |
| `env.AUTH_AUTHORITY`   | `appsettings.Development.json` (QA) / `appsettings.Production.json`    |
| `env.AUTH_CLIENT_ID`   | `appsettings.json` (`Authentication:ClientIds`, igual en los tres entornos) |
| `azuredevops.pat`      | **No como parámetro de build.** El PAT de Azure DevOps sí hace falta, pero para clonar el repositorio, y eso se configura en el VCS Root — ver la sección siguiente. Lo que no aplica es el uso que le da GestionHumana: alimentar un `dotnet restore` contra un feed privado. |
| `env.KUMA_*`           | No aplica: el deploy no abre ventana de mantenimiento.                 |

**Sobre los `vite.*`:** pasarlos uno por uno fue descartado deliberadamente. Un `--build-arg` olvidado llega al build como variable de entorno **vacía**, Vite le da precedencia sobre los archivos `.env.<modo>`, y el bundler elimina el código de autenticación por dead-code elimination. El resultado es una imagen que construye sin error, arranca sana, pasa el healthcheck, y muestra el botón de login deshabilitado con el texto *"AuthManager no configurado"*. Como `import.meta.env` se resuelve al construir, ninguna variable del contenedor lo arregla: hay que rehacer la imagen. `vite.config.js` ahora rompe el build si esas variables llegan vacías.

**Sobre el emisor:** las variables de entorno tienen **mayor** precedencia que los `appsettings.json` en ASP.NET Core. Declarar `Authentication__Authority` en el stack anularía la segmentación por entorno, y sin exportar se expandiría a cadena vacía tumbando el arranque. Por eso `docker-stack.yml` solo transporta secretos.

---

## VCS Root

El repositorio vive en **Azure DevOps**. TeamCity necesita credenciales para clonarlo, y **no son
un parámetro de build**: se configuran en el VCS Root, que tiene sus propios campos.

**Administration → VCS Roots → `<el root del proyecto>` → Authentication**

| Campo             | Valor                                                           |
| ----------------- | --------------------------------------------------------------- |
| Fetch URL         | `https://azuredevops.rehabilitacion.org.do/ADRCollection/ADRTrack/_git/ADRTrack` |
| Authentication    | `Password / access token`                                        |
| User name         | cualquiera (Azure DevOps ignora el usuario cuando se usa PAT)     |
| Password / token  | *(PAT de Azure DevOps con permiso `Code (read)`)*                 |

El segmento `/_git/` es obligatorio. La URL de navegación
(`https://azuredevops.rehabilitacion.org.do/ADRCollection/ADRTrack`) no sirve como Fetch URL:
git responde `repository not found`.

El `settings.kts` no lo menciona: usa `DslContext.settingsRoot`, que **es** este VCS Root, ya
autenticado por TeamCity al leer la configuración. No hay nada que agregar al Kotlin.

Dos consecuencias prácticas:

- El PAT es de la **instancia**, no del repositorio. Cada servidor TeamCity necesita el suyo, y
  cuando expira los builds fallan al clonar, antes de ejecutar ningún step.
- Si el repositorio también tiene un espejo en GitHub, el VCS Root debe apuntar al de Azure
  DevOps: es el que recibe los merges y el que alimenta `env.BRANCH_FILTER`.

> **No confundir con el PAT de GestionHumana.** Allá, `azuredevops.pat` es un parámetro de build
> que viaja como secreto BuildKit hasta un `dotnet restore` contra el feed privado
> `RehabilitacionPackages`. Aquí ese uso no existe: no hay `nuget.config` en el repositorio y los
> catorce paquetes referenciados (OpenIddict, FluentValidation, EF Core, xunit…) son de
> nuget.org. Declarar el secreto con `required=true` haría fallar **todas** las construcciones
> pidiendo un valor que nadie define.

---

## Docker Registry Connection

El `dockerRegistry` Connection **vive en el kts** con ID estable `HarborRegistry`. URL, usuario y password se resuelven desde los params de Root de cada instancia (`%env.HARBOR_REGISTRY%`, `%env.HARBOR_USER%`, `%env.HARBOR_PASSWORD%`). No hay que crear nada manualmente.

Los builds usan la **`Docker Support` build feature** para:
- Auto `docker login` antes de cada step (sin login manual en bash).
- `cleanupPushedImages = true` → limpia en el agente TC las imágenes pusheadas tras el build.

### Limitación

`cleanupPushedImages` opera en el **agente TC** (build host). NO toca el **servidor de deploy** (Swarm node). El script SSH de deploy mantiene su loop de limpieza (`docker images | grep -v BUILD_TAG | xargs docker rmi`) para limpiar imágenes viejas en el host Swarm.

---

## Preparación del servidor de deploy

No hay script de setup. Estos tres pasos son manuales y se hacen **una sola vez por ambiente**, en el nodo manager del Swarm:

```bash
# 1. Directorio donde aterriza docker-stack.yml por SCP
mkdir -p /opt/track-adr && chown usr.deploy /opt/track-adr

# 2. Red externa que docker-stack.yml espera (si no existe ya por otra app)
docker network create --driver overlay --attachable adr_network

# 3. Comprobar que el nodo es manager de un Swarm activo
docker info --format '{{.Swarm.LocalNodeState}} {{.Swarm.ControlAvailable}}'   # -> "active true"
```

El script de deploy no instala nada en el servidor: usa solo `docker`, `grep`, `awk` y `xargs` (este último con `--no-run-if-empty`, que es de GNU findutils — presente en cualquier distribución de servidor habitual).

---

## Checklist de migración por instancia

Antes del primer build:

- [ ] Configurar el VCS Root contra Azure DevOps con un PAT vigente (`Code (read)`).
- [ ] Crear los 14 parámetros en `<Root>` (o `TrackAdr` con sync OFF).
- [ ] Verificar tipo correcto (text vs password) — las 6 credenciales van como `Password`.
- [ ] Verificar que `env.BUILD_MODE` y `env.ASPNETCORE_ENVIRONMENT` forman una pareja válida (`qa`/`QA` o `production`/`Production`).
- [ ] Preparar el servidor de deploy con los tres pasos de arriba.
- [ ] Aplicar el esquema y las migraciones de `Database/` contra el SQL Server del ambiente, incluida `20260908_UsuarioDelApi.sql`.
- [ ] Definir `env.BRANCH_FILTER` de QA según la política de ramas que se acuerde.
- [ ] Ejecutar un build manual y revisar los logs por `Parameter X is not defined`.
- [ ] Tras el primer deploy: entrar a la aplicación y probar un endpoint que consulte GLPI, porque una credencial incorrecta converge igual.

---

## Rollback

Si el pipeline introduce problemas:

1. `git revert <commit-del-pipeline>` → vuelve el repo a su estado previo.
2. En la instancia TC: borrar el proyecto `TrackAdr` y reimportar desde el commit anterior.

Para revertir solo un despliegue, sin tocar el pipeline: `docker service rollback track-adr_app` en el nodo manager. El script ya lo ejecuta solo si el servicio no converge en 120 s.
