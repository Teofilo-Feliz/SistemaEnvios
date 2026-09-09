# ==========================================
# Stage 1: Build Vue SPA
# ==========================================
FROM node:20-alpine AS spa-build

WORKDIR /app/spa

COPY FrontEnd/package*.json ./

RUN npm ci

COPY FrontEnd/ ./

# Se elige el entorno, no cada valor. Pasar las VITE_* una por una era una trampa: un ARG sin
# --build-arg se convierte en una variable de entorno vacía, y Vite da precedencia al entorno
# sobre los archivos .env.<modo>, de modo que olvidar una bandera vaciaba el emisor sin que el
# build fallara. Con el modo, los valores salen del archivo versionado y son los mismos que
# revisa cualquiera leyendo el repositorio.
ARG BUILD_MODE=production

RUN npm run build -- --mode $BUILD_MODE

# ==========================================
# Stage 2: Build .NET API
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build

WORKDIR /src

COPY SistemaEnvios.Api/*.csproj SistemaEnvios.Api/
COPY SistemaEnvios.Application/*.csproj SistemaEnvios.Application/
COPY SistemaEnvios.Domain/*.csproj SistemaEnvios.Domain/
COPY SistemaEnvios.Infrastructure/*.csproj SistemaEnvios.Infrastructure/

RUN dotnet restore SistemaEnvios.Api/SistemaEnvios.Api.csproj

COPY SistemaEnvios.Api/ SistemaEnvios.Api/
COPY SistemaEnvios.Application/ SistemaEnvios.Application/
COPY SistemaEnvios.Domain/ SistemaEnvios.Domain/
COPY SistemaEnvios.Infrastructure/ SistemaEnvios.Infrastructure/

# SpaRoot vacío apaga el target CompilarSpa del .csproj: aquí el frontend lo compiló la etapa
# de Node, con su propia caché de capas, y esta imagen no tiene Node. SpaProxyLaunchCommand
# vacío evita que el proxy de desarrollo —que intentaría arrancar Vite— llegue a la imagen.
RUN dotnet publish SistemaEnvios.Api/SistemaEnvios.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:SpaRoot="" \
    /p:SpaProxyLaunchCommand=""

# ==========================================
# Stage 3: Runtime (Unified)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# curl es para el HEALTHCHECK del final: la imagen de aspnet no lo trae.
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

# Los servicios internos de la institución (SQL Server, AuthManager, GLPI) negocian TLS por
# debajo de lo que Debian acepta por defecto. En Windows la conexión abre y dentro del
# contenedor falla con un error de handshake que no dice nada del protocolo.
RUN sed -i '/\[openssl_init\]/a ssl_conf = ssl_sect' /etc/ssl/openssl.cnf && \
    printf "\n[ssl_sect]\nsystem_default = system_default_sect\n" >> /etc/ssl/openssl.cnf && \
    printf "\n[system_default_sect]\nMinProtocol = TLSv1.0\nCipherString = DEFAULT@SECLEVEL=0\n" >> /etc/ssl/openssl.cnf

RUN groupadd -g 1001 appgroup && \
    useradd -u 1001 -g appgroup -s /sbin/nologin -r appuser

COPY --from=api-build /app/publish ./
COPY --from=spa-build /app/spa/dist ./wwwroot/

RUN mkdir -p /app/.aspnet/DataProtection-Keys && \
    chown -R appuser:appgroup /app

USER appuser

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# Contra /health/ready, que comprueba también la base de datos: una comprobación por TCP daba
# por sano un contenedor con Kestrel escuchando y sin conexión a SQL Server, y Swarm completaba
# el despliegue sin disparar el rollback. La cabecera reenviada evita el 307 de
# UseHttpsRedirection, que convertiría en "enfermo" a un contenedor sano.
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -fsS -H "X-Forwarded-Proto: https" http://localhost:8080/health/ready || exit 1

ENTRYPOINT ["dotnet", "SistemaEnvios.Api.dll"]
