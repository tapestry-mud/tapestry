# SDK single-source policy: this pin MUST satisfy global.json (the source of
# truth - CI consumes it via setup-dotnet global-json-file). Bump global.json
# and this digest together.
FROM mcr.microsoft.com/dotnet/sdk:10.0.301@sha256:548d93f8a18a1acbe6cc127bc4f47281430d34a9e35c18afa80a8d6741c2adc3 AS build
WORKDIR /app
COPY . .
RUN dotnet publish src/Tapestry.Server -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0.9@sha256:7644f992230d35cf230017189d4038c0ae0f7388b13f4f7ae1900a155bafb597 AS runtime
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
