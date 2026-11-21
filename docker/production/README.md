# Production deployment

A self-contained deployment for a host behind a load balancer that terminates TLS (for example an AWS ALB). Every
service runs a published image from Docker Hub (`jchristn77/mymorningnews-<type>:latest`, built and pushed with the
repository's `build-*.sh` scripts), so the host needs no source code.

Set up a host (Docker Engine with the Compose plugin):

```bash
mkdir -p ~/mymorningnews && cd ~/mymorningnews
# copy in: compose.yaml and update.sh (this folder), mymorningnews.json (docker/hosted/mymorningnews.json),
# and .env (docker/.env.example with real secrets; chmod 600)
./update.sh
```

`update.sh` pulls the latest images, stops the stack, starts it again, and lists the containers:
`docker compose pull && docker compose down && docker compose up -d && docker ps -a`. Volumes (the database,
media, and metrics) are kept.

Load balancer targets: port 8000 for the API (`/v1.0/*`, `/openapi.json`, `/swagger*`, `/.well-known/*`; health
check `GET /v1.0/api/health`) and port 8080 for the dashboard (every other path; health check `GET /health`). Allow
both only from the load balancer's security group. See `docs/HOSTING.md`, "Behind a load balancer".

`compose.yaml` here is generated from `docker/compose.yaml` and `docker/hosted/compose.elb.yaml`; regenerate it when
either changes (the command is at the top of the file).
