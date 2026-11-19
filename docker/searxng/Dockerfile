# SearXNG metasearch (the default keyless web search provider) plus a static curl binary so the compose
# healthcheck can probe with cURL. The SearXNG image is Void Linux without curl or a package manager.
FROM tarampampam/curl:8.11.1 AS curl
FROM searxng/searxng:2026.11.15-2d500cb6b
COPY --from=curl /bin/curl /usr/local/bin/curl
COPY settings.yml /etc/searxng/settings.yml
