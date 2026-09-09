import jetbrains.buildServer.configs.kotlin.*
import jetbrains.buildServer.configs.kotlin.buildFeatures.dockerSupport
import jetbrains.buildServer.configs.kotlin.buildFeatures.perfmon
import jetbrains.buildServer.configs.kotlin.buildSteps.script
import jetbrains.buildServer.configs.kotlin.buildSteps.SSHUpload
import jetbrains.buildServer.configs.kotlin.buildSteps.sshExec
import jetbrains.buildServer.configs.kotlin.buildSteps.sshUpload
import jetbrains.buildServer.configs.kotlin.projectFeatures.dockerRegistry
import jetbrains.buildServer.configs.kotlin.triggers.finishBuildTrigger
import jetbrains.buildServer.configs.kotlin.triggers.vcs

version = "2025.11"

project {
    description = "Sistema logístico de envíos de la ADR (LogiTrack)."

    features {
        dockerRegistry {
            id = "HarborRegistry"
            name = "Harbor"
            url = "%env.HARBOR_REGISTRY%"
            userName = "%env.HARBOR_USER%"
            password = "%env.HARBOR_PASSWORD%"
        }
    }

    buildType(ApiSpaBuildPush)
    buildType(ApiSpaDeploy)
}

object ApiSpaBuildPush : BuildType({
    name = "API + SPA — Build & Push"
    artifactRules = "docker-stack.yml"

    params {
        param("env.APP_IMAGE",
            "%env.HARBOR_REGISTRY%/rehabilitacion/track-adr:%build.number%")
        param("env.APP_IMAGE_LATEST",
            "%env.HARBOR_REGISTRY%/rehabilitacion/track-adr:latest")
    }

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        script {
            name = "Docker Build & Push API+SPA"
            id = "Docker_Build_Push"
            // Una sola bandera de construcción: el Dockerfile declara únicamente BUILD_MODE y
            // los valores del frontend salen de .env.<modo>, versionados en el repositorio.
            // Pasar cada VITE_* por separado fue lo que se descartó: un --build-arg olvidado
            // llega como variable de entorno vacía, Vite le da precedencia sobre el archivo y
            // la imagen sale con el emisor en blanco sin que el build falle.
            //
            // Tampoco hay --secret id=nuget_pat: este proyecto no consume ningún feed privado,
            // y declararlo con required=true haría fallar todas las construcciones.
            scriptContent = """
                #!/usr/bin/env bash
                set -euo pipefail

                export DOCKER_BUILDKIT=1

                docker build \
                  -t "%env.APP_IMAGE%" \
                  -t "%env.APP_IMAGE_LATEST%" \
                  --pull \
                  --build-arg BUILD_MODE=%env.BUILD_MODE% \
                  -f Dockerfile \
                  .

                docker push "%env.APP_IMAGE%"
                docker push "%env.APP_IMAGE_LATEST%"
            """.trimIndent()
        }
    }

    triggers {
        vcs {
            branchFilter = "%env.BRANCH_FILTER%"
            triggerRules = """
                +:**
                -:.teamcity/**
                -:Database/**
                -:README.md
            """.trimIndent()
        }
    }

    features {
        perfmon {}
        dockerSupport {
            cleanupPushedImages = true
            loginToRegistry = on {
                dockerRegistryId = "HarborRegistry"
            }
        }
    }
})

object ApiSpaDeploy : BuildType({
    name = "API + SPA — Deploy"

    params {
        param("env.BUILD_TAG",
            "%dep.${DslContext.projectId.value}_ApiSpaBuildPush.build.number%")
    }

    steps {
        sshUpload {
            name = "Send Stack File"
            id = "Send_Stack"
            transportProtocol = SSHUpload.TransportProtocol.SCP
            sourcePath = "docker-stack.yml"
            targetUrl = "%env.DEPLOY_SERVER%:/opt/track-adr"
            authMethod = password {
                username = "%env.DEPLOY_USER%"
                password = "%env.DEPLOY_PASSWORD%"
            }
        }
        sshExec {
            name = "Deploy to Swarm"
            id = "Deploy_Swarm"
            targetUrl = "%env.DEPLOY_SERVER%"
            authMethod = password {
                username = "%env.DEPLOY_USER%"
                password = "%env.DEPLOY_PASSWORD%"
            }
            commands = """
                #!/usr/bin/env bash
                set -euo pipefail

                WORK_DIR="/opt/track-adr"
                STACK_NAME="track-adr"
                HARBOR_REGISTRY="%env.HARBOR_REGISTRY%"
                BUILD_TAG="%env.BUILD_TAG%"
                BUILD_MODE="%env.BUILD_MODE%"
                ENVIRONMENT="%env.ASPNETCORE_ENVIRONMENT%"
                APP_IMAGE="${'$'}{HARBOR_REGISTRY}/rehabilitacion/track-adr:${'$'}{BUILD_TAG}"
                SERVICE_NAME="${'$'}{STACK_NAME}_app"

                echo "================================================================"
                echo " track-adr — Deploy"
                echo " BUILD_TAG   : ${'$'}{BUILD_TAG}"
                echo " BUILD_MODE  : ${'$'}{BUILD_MODE}"
                echo " ENVIRONMENT : ${'$'}{ENVIRONMENT}"
                echo " IMAGE       : ${'$'}{APP_IMAGE}"
                echo " SERVICE     : ${'$'}{SERVICE_NAME}"
                echo "================================================================"

                # ==========================================
                # Coherencia entre el frontend y el backend
                # ==========================================
                # Son dos variables independientes que apuntan al mismo emisor por caminos
                # distintos: BUILD_MODE elige el .env.<modo> con que se compiló el SPA y
                # ASPNETCORE_ENVIRONMENT elige el appsettings con que el API valida los tokens.
                # Desalineadas, el usuario inicia sesión contra un AuthManager y el API valida
                # contra otro: el login parece funcionar y toda llamada devuelve 403. Nada más
                # en el despliegue lo detecta, así que se comprueba aquí.
                echo ""
                echo ">>> [COHERENCIA] Verificando BUILD_MODE contra ASPNETCORE_ENVIRONMENT..."
                case "${'$'}{BUILD_MODE}:${'$'}{ENVIRONMENT}" in
                  qa:Development|production:Production)
                    echo "    OK"
                    ;;
                  *)
                    echo ""
                    echo "!!! BUILD_MODE='${'$'}{BUILD_MODE}' no concuerda con ASPNETCORE_ENVIRONMENT='${'$'}{ENVIRONMENT}'."
                    echo "!!! Combinaciones admitidas: qa:Development y production:Production."
                    echo "!!! Revise los parámetros del proyecto en TeamCity antes de reintentar."
                    exit 1
                    ;;
                esac

                echo ""
                echo ">>> [PRE-CLEANUP] Eliminando contenedores exited del servicio..."
                docker ps -a --filter "name=${'$'}{SERVICE_NAME}" --filter "status=exited" -q \
                  | xargs --no-run-if-empty docker rm -f
                echo "    OK"

                echo ""
                echo ">>> [HARBOR] Autenticando en ${'$'}{HARBOR_REGISTRY}..."
                printf '%%s' "%env.HARBOR_PASSWORD%" \
                  | docker login "${'$'}{HARBOR_REGISTRY}" \
                      --username '%env.HARBOR_USER%' \
                      --password-stdin
                echo "    OK"

                echo ""
                echo ">>> [DEPLOY] Verificando si ${'$'}{SERVICE_NAME} existe..."
                if docker service inspect "${'$'}{SERVICE_NAME}" >/dev/null 2>&1; then
                  echo "    Servicio existe — actualizando imagen y env vars..."
                  docker service update \
                    --detach \
                    --image "${'$'}{APP_IMAGE}" \
                    --replicas %env.REPLICAS% \
                    --env-add 'ASPNETCORE_ENVIRONMENT=%env.ASPNETCORE_ENVIRONMENT%' \
                    --env-add 'ConnectionStrings__SistemaEnvios=%env.SISTEMA_ENVIOS_CONNECTION%' \
                    --env-add 'Glpi__AppToken=%env.GLPI_APP_TOKEN%' \
                    --env-add 'Glpi__Username=%env.GLPI_USERNAME%' \
                    --env-add 'Glpi__Password=%env.GLPI_PASSWORD%' \
                    --with-registry-auth \
                    "${'$'}{SERVICE_NAME}"
                  echo "    Update iniciado."
                else
                  echo "    Servicio no existe — bootstrap con stack deploy..."
                  export APP_IMAGE="${'$'}{APP_IMAGE}"
                  export ASPNETCORE_ENVIRONMENT='%env.ASPNETCORE_ENVIRONMENT%'
                  export SISTEMA_ENVIOS_CONNECTION='%env.SISTEMA_ENVIOS_CONNECTION%'
                  export GLPI_APP_TOKEN='%env.GLPI_APP_TOKEN%'
                  export GLPI_USERNAME='%env.GLPI_USERNAME%'
                  export GLPI_PASSWORD='%env.GLPI_PASSWORD%'
                  export REPLICAS='%env.REPLICAS%'
                  cd "${'$'}{WORK_DIR}"
                  docker stack deploy \
                    -c docker-stack.yml \
                    --with-registry-auth \
                    "${'$'}{STACK_NAME}"
                  echo "    Stack desplegado."
                fi

                echo ""
                echo ">>> [CONVERGENCIA] Esperando que ${'$'}{SERVICE_NAME} alcance estado deseado..."
                TIMEOUT=120
                INTERVAL=5
                ELAPSED=0
                DESIRED=${'$'}(docker service inspect --format '{{.Spec.Mode.Replicated.Replicas}}' "${'$'}{SERVICE_NAME}")

                while true; do
                  RUNNING=${'$'}(docker service ps "${'$'}{SERVICE_NAME}" \
                    --filter "desired-state=running" \
                    --format '{{.CurrentState}}' \
                    | grep -c "^Running" || true)

                  echo "    Replicas corriendo: ${'$'}{RUNNING}/${'$'}{DESIRED} (${'$'}{ELAPSED}s)"

                  if [ "${'$'}{RUNNING}" -ge "${'$'}{DESIRED}" ]; then
                    echo "    Convergencia alcanzada."
                    break
                  fi

                  if [ "${'$'}{ELAPSED}" -ge "${'$'}{TIMEOUT}" ]; then
                    echo ""
                    echo "!!! TIMEOUT: el servicio no convergió en ${'$'}{TIMEOUT}s — iniciando rollback..."
                    docker service rollback "${'$'}{SERVICE_NAME}" || true
                    exit 1
                  fi

                  sleep "${'$'}{INTERVAL}"
                  ELAPSED=${'$'}((${'$'}{ELAPSED} + ${'$'}{INTERVAL}))
                done

                echo ""
                echo ">>> [ESTADO] Servicios del stack ${'$'}{STACK_NAME}:"
                docker service ls --filter "name=${'$'}{STACK_NAME}"
                echo ""
                echo ">>> [ESTADO] Tareas de ${'$'}{SERVICE_NAME}:"
                docker service ps "${'$'}{SERVICE_NAME}" --no-trunc

                echo ""
                echo ">>> [POST-CLEANUP] Esperando 15s antes de limpiar contenedores exited..."
                sleep 15
                docker ps -a --filter "name=${'$'}{SERVICE_NAME}" --filter "status=exited" -q \
                  | xargs --no-run-if-empty docker rm -f
                echo "    OK"

                echo ""
                echo ">>> [IMAGES] Eliminando imágenes viejas de track-adr (conservando ${'$'}{BUILD_TAG})..."
                docker images "${'$'}{HARBOR_REGISTRY}/rehabilitacion/track-adr" \
                  --format '{{.Tag}}\t{{.ID}}' \
                  | grep -v "^${'$'}{BUILD_TAG}" \
                  | awk '{print ${'$'}2}' \
                  | xargs --no-run-if-empty docker rmi -f || true
                echo "    OK"

                echo ""
                echo "================================================================"
                echo " track-adr — Deploy completado exitosamente"
                echo " BUILD_TAG : ${'$'}{BUILD_TAG}"
                echo "================================================================"
            """.trimIndent()
        }
    }

    triggers {
        finishBuildTrigger {
            buildTypeExtId = "${DslContext.projectId.value}_ApiSpaBuildPush"
            successfulOnly = true
        }
    }

    dependencies {
        snapshot(ApiSpaBuildPush) {
            onDependencyFailure = FailureAction.FAIL_TO_START
        }
        artifacts(ApiSpaBuildPush) {
            buildRule = lastSuccessful()
            artifactRules = "docker-stack.yml => ."
        }
    }
})
