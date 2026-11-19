FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./src/
COPY assets/ ./assets/
RUN dotnet publish src/MyMorningNews.Server/MyMorningNews.Server.csproj -c Release -f net10.0 -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ffmpeg \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV MYMORNINGNEWS_SETTINGS_FILE=/app/config/mymorningnews.json
EXPOSE 8000
HEALTHCHECK --interval=5s --timeout=5s --retries=2 CMD curl -f http://127.0.0.1:8000/v1.0/api/health || exit 1
ENTRYPOINT ["dotnet", "MyMorningNews.Server.dll"]
