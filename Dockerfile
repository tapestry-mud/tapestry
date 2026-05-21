FROM mcr.microsoft.com/dotnet/sdk:10.0.300@sha256:c0790639332692a0d56cdd81ed581cfd24d040d9839764c138994866df89a3b6 AS build
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
