# prom/prometheus plus a static curl binary so the compose healthcheck can probe with cURL (REPOSITORY_REQUIREMENTS.md item 11).
FROM tarampampam/curl:8.11.1 AS curl
FROM prom/prometheus:v3.5.4
COPY --from=curl /bin/curl /usr/local/bin/curl
# The configuration is part of the image, so a deployment needs only compose.yaml.
COPY prometheus.yaml /etc/prometheus/prometheus.yaml
