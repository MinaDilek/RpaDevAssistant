FROM node:22-bookworm-slim AS frontend-build
WORKDIR /src
COPY package.json package-lock.json ./
COPY frontend/package.json frontend/package.json
RUN npm ci
COPY frontend frontend
ENV VITE_API_BASE_URL=/
RUN npm run frontend:build

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS api-build
WORKDIR /src
COPY RpaDevAssistant.sln Directory.Build.props ./
COPY src/RpaDevAssistant.Api/RpaDevAssistant.Api.csproj src/RpaDevAssistant.Api/
COPY src/RpaDevAssistant.Core/RpaDevAssistant.Core.csproj src/RpaDevAssistant.Core/
COPY src/RpaDevAssistant.Infrastructure/RpaDevAssistant.Infrastructure.csproj src/RpaDevAssistant.Infrastructure/
RUN dotnet restore src/RpaDevAssistant.Api/RpaDevAssistant.Api.csproj
COPY src src
RUN dotnet publish src/RpaDevAssistant.Api/RpaDevAssistant.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false
COPY --from=frontend-build /src/frontend/dist /out/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime
WORKDIR /app
COPY --from=api-build /out ./
RUN mkdir -p /data /projects && chown -R app:app /app /data /projects
USER app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    RpaDevAssistant__Central__Enabled=true \
    RpaDevAssistant__Central__StorageRoot=/data/central \
    RpaDevAssistant__Storage__AnalysisHistoryRoot=/data/history \
    RpaDevAssistant__Storage__CustomRulesFile=/data/custom-rules.json \
    RpaDevAssistant__Storage__RuleProfilesFile=/data/rule-profiles.json
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080; printf 'GET /api/health HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3; grep -q '200 OK' <&3"]
ENTRYPOINT ["dotnet", "RpaDevAssistant.Api.dll"]
