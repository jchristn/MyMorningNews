# Kokoro-FastAPI (CPU) text-to-speech: an OpenAI-compatible /v1/audio/speech endpoint used for briefing audio
# and podcast episodes (plan section 22), plus a static curl binary for the compose healthcheck.
# Started with the "tts" profile until Phase 8 wires audio generation; then it becomes part of the default stack.
FROM tarampampam/curl:8.11.1 AS curl
FROM ghcr.io/remsky/kokoro-fastapi-cpu:v0.9.2
COPY --from=curl /bin/curl /usr/local/bin/curl
