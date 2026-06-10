# SDK single-source policy: this pin MUST satisfy global.json (the source of
# truth - CI consumes it via setup-dotnet global-json-file). Bump global.json
# and this digest together.
FROM mcr.microsoft.com/dotnet/sdk:10.0.301@sha256:548d93f8a18a1acbe6cc127bc4f47281430d34a9e35c18afa80a8d6741c2adc3 AS build
WORKDIR /app
COPY . .
RUN dotnet publish src/Tapestry.Server -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0.8@sha256:8c0b6857eab7b2aa57884c839bf4678414606bd7d17370f18a842ac5cf414711 AS runtime
ARG GIT_SHA=dev
ARG ENGINE_VERSION=dev
ENV ENGINE_BUILD_SHA=$GIT_SHA
ENV ENGINE_BUILD_VERSION=$ENGINE_VERSION
WORKDIR /app
COPY --from=build /out .
EXPOSE 4000 4001
ENTRYPOINT ["dotnet", "Tapestry.Server.dll"]
