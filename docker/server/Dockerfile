FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./src/
COPY assets/ ./assets/
RUN dotnet publish src/MyMorningNews.Server/MyMorningNews.Server.csproj -c Release -f net10.0 -o /app/publish

# Node.js and npm come from the official image (no remote install script).
FROM node:22-bookworm-slim AS node

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
ARG TARGETARCH
# Agent CLIs available inside the server container. Override at build time, for example
# docker compose build --build-arg CLAUDE_CODE_VERSION=2.1.329 server
ARG CLAUDE_CODE_VERSION=latest
ARG CODEX_VERSION=latest
ARG GEMINI_CLI_VERSION=latest
ARG MUX_VERSION=1.8.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl ffmpeg git ripgrep \
    && rm -rf /var/lib/apt/lists/*

# npm, Claude Code, and Gemini CLI.
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -s ../lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
    && ln -s ../lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx \
    && ln -s ../lib/node_modules/corepack/dist/corepack.js /usr/local/bin/corepack \
    && npm install -g --no-fund --no-audit "@anthropic-ai/claude-code@${CLAUDE_CODE_VERSION}" "@google/gemini-cli@${GEMINI_CLI_VERSION}" \
    && npm cache clean --force \
    && node --version && npm --version && claude --version && gemini --version

# Codex. npm sometimes skips its platform binary (an optional dependency), so install the matching one explicitly.
RUN case "${TARGETARCH:-amd64}" in amd64) CODEX_ARCH=x64 ;; arm64) CODEX_ARCH=arm64 ;; *) echo "unsupported architecture ${TARGETARCH}" && exit 1 ;; esac \
    && npm install -g --no-fund --no-audit "@openai/codex@${CODEX_VERSION}" \
    && CODEX_INSTALLED=$(node -p "require('/usr/local/lib/node_modules/@openai/codex/package.json').version") \
    && npm install -g --no-fund --no-audit "@openai/codex-linux-${CODEX_ARCH}@npm:@openai/codex@${CODEX_INSTALLED}-linux-${CODEX_ARCH}" \
    && npm cache clean --force \
    && codex --version

# Mux (https://github.com/jchristn/Mux), the self-contained Linux build from its GitHub release.
RUN case "${TARGETARCH:-amd64}" in amd64) MUX_ARCH=x64 ;; arm64) MUX_ARCH=arm64 ;; *) echo "unsupported architecture ${TARGETARCH}" && exit 1 ;; esac \
    && mkdir -p /opt/mux \
    && curl -fsSL "https://github.com/jchristn/Mux/releases/download/v${MUX_VERSION}/mux-${MUX_VERSION}-linux-${MUX_ARCH}.tar.gz" | tar -xz -C /opt/mux \
    && chmod +x /opt/mux/mux \
    && ln -s /opt/mux/mux /usr/local/bin/mux \
    && mux --version

WORKDIR /app
COPY --from=build /app/publish .
ENV MYMORNINGNEWS_SETTINGS_FILE=/app/config/mymorningnews.json
EXPOSE 8000
HEALTHCHECK --interval=5s --timeout=5s --retries=2 CMD curl -f http://127.0.0.1:8000/v1.0/api/health || exit 1
ENTRYPOINT ["dotnet", "MyMorningNews.Server.dll"]
