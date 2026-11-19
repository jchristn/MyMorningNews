# Ollama for Linux hosts (profile "ollama"). On macOS, run Ollama natively for GPU access; the server reaches it at
# http://host.docker.internal:11434. Adds a static curl binary for the compose healthcheck.
FROM tarampampam/curl:8.11.1 AS curl
FROM ollama/ollama:0.40.16
COPY --from=curl /bin/curl /usr/local/bin/curl
