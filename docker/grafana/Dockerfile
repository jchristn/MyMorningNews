# Grafana with the My Morning News data sources and dashboards built in, so a deployment needs only compose.yaml.
# Dashboards live under /etc/grafana/dashboards (not /var/lib/grafana, which is the grafana data volume).
FROM grafana/grafana-oss:13.0.2
COPY provisioning /etc/grafana/provisioning
COPY dashboards /etc/grafana/dashboards
