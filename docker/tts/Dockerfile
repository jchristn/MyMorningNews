# Kokoro-FastAPI (CPU) text-to-speech: an OpenAI-compatible /v1/audio/speech endpoint used for briefing audio
# and podcast episodes (plan section 22), plus a static curl binary for the compose healthcheck.
# Part of the default stack since Phase 8.
FROM tarampampam/curl:8.11.1 AS curl
FROM ghcr.io/remsky/kokoro-fastapi-cpu:v0.9.2
COPY --from=curl /bin/curl /usr/local/bin/curl
