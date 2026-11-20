# My Morning News MCP server (Voltaic): Streamable HTTP on /mcp, port 8100, health on /.
# It holds no privileges of its own: every call carries the caller's REST credentials.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./src/
COPY sdk/csharp/MyMorningNews.Sdk/ ./sdk/csharp/MyMorningNews.Sdk/
RUN dotnet publish src/MyMorningNews.McpServer/MyMorningNews.McpServer.csproj -c Release -f net10.0 -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV MYMORNINGNEWS_MCP_HOSTNAME=* \
    MYMORNINGNEWS_MCP_PORT=8100
EXPOSE 8100
HEALTHCHECK --interval=5s --timeout=5s --retries=2 CMD curl -fs http://127.0.0.1:8100/ || exit 1
ENTRYPOINT ["dotnet", "MyMorningNews.McpServer.dll"]
