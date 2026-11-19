# grafana/loki plus a static curl binary so the compose healthcheck can probe with cURL (REPOSITORY_REQUIREMENTS.md item 11).
FROM tarampampam/curl:8.11.1 AS curl
FROM grafana/loki:3.5.5
COPY --from=curl /bin/curl /usr/local/bin/curl
