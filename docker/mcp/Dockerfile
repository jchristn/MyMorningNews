# My Morning News MCP server (Voltaic, Streamable HTTP on port 8100). Implemented in Phase 11; until then the
# "mcp" profile builds the image but the process exits immediately.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./src/
RUN dotnet publish src/MyMorningNews.McpServer/MyMorningNews.McpServer.csproj -c Release -f net10.0 -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8100
ENTRYPOINT ["dotnet", "MyMorningNews.McpServer.dll"]
