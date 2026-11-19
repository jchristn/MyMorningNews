# grafana/tempo plus a static curl binary so the compose healthcheck can probe with cURL (REPOSITORY_REQUIREMENTS.md item 11).
FROM tarampampam/curl:8.11.1 AS curl
FROM grafana/tempo:2.6.1
COPY --from=curl /bin/curl /usr/local/bin/curl
