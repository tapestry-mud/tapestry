FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY . .
RUN dotnet publish src/Tapestry.Server -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG GIT_SHA=dev
ENV ENGINE_BUILD_SHA=$GIT_SHA
WORKDIR /app
COPY --from=build /out .
EXPOSE 4000 4001
ENTRYPOINT ["dotnet", "Tapestry.Server.dll"]
