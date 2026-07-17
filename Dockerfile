# SDK single-source policy: this pin MUST satisfy global.json (the source of
# truth - CI consumes it via setup-dotnet global-json-file). Bump global.json
# and this digest together.
FROM mcr.microsoft.com/dotnet/sdk:10.0.302@sha256:ed034a8bf0b24ded0cbbac07e17825d8e9ebfe21e308191d0f7421eaf5ad4664 AS build
WORKDIR /app
COPY . .
RUN dotnet publish src/Tapestry.Server -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0.9@sha256:ddcf70ad1ab963a4fcd41fbd722a6b660e404e87567cfbd46fd2809c21b02088 AS runtime
ARG GIT_SHA=dev
ARG ENGINE_VERSION=dev
ENV ENGINE_BUILD_SHA=$GIT_SHA
ENV ENGINE_BUILD_VERSION=$ENGINE_VERSION
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /out .
EXPOSE 4000 4001
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -sf http://localhost:4001/config || exit 1
ENTRYPOINT ["dotnet", "Tapestry.Server.dll"]
