# Step 4 — Monitoring: Prometheus + Grafana

> Project files: [`compose/step4-monitoring/`](../compose/step4-monitoring)

## লক্ষ্য

Scaling-এর সিদ্ধান্ত নেওয়ার আগে জানা — application ও server-এর ওপর কতটা চাপ পড়ছে।

- **Prometheus**: metrics সংগ্রহ করে ও সময় অনুযায়ী সংরক্ষণ করে।
- **Grafana**: metrics-এর dashboard ও graph দেখায়।
- **cAdvisor**: প্রতিটি container-এর CPU, memory ইত্যাদি metrics প্রকাশ করে।

শুধু container count দেখে scaling-এর সিদ্ধান্ত নেওয়া যায় না; CPU, memory, latency, request rate, error rate এবং database load-ও দেখতে হয়।

## 4.1 Architecture

```text
api1/api2/api3 + sqlserver + web containers
        │
        ▼
cAdvisor — container metrics
        │
        ▼
Prometheus — metrics collection
        │
        ▼
Grafana — dashboards
```

## 4.2 Prometheus configuration

`monitoring/prometheus.yml`:

```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: prometheus
    static_configs:
      - targets: ["prometheus:9090"]

  - job_name: cadvisor
    static_configs:
      - targets: ["cadvisor:8080"]
```

## 4.3 Compose

Step 3-এর সব service-এর সাথে নিচের তিনটি যোগ করা হয়েছে (পূর্ণ ফাইল: [`compose.yaml`](../compose/step4-monitoring/compose.yaml)); `web` এখানে Step 3-এর `upstream.conf`-ই ব্যবহার করে:

```yaml
  cadvisor:
    image: gcr.io/cadvisor/cadvisor:v0.52.1
    privileged: true
    devices:
      - /dev/kmsg:/dev/kmsg
    volumes:
      - /:/rootfs:ro
      - /var/run:/var/run:ro
      - /sys:/sys:ro
      - /var/lib/docker:/var/lib/docker:ro
      - /dev/disk:/dev/disk:ro
    expose:
      - "8080"
    restart: unless-stopped

  prometheus:
    image: prom/prometheus:v3.5.0
    volumes:
      - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheus-data:/prometheus
    command:
      - "--config.file=/etc/prometheus/prometheus.yml"
    ports:
      - "9090:9090"
    restart: unless-stopped

  grafana:
    image: grafana/grafana:12.1.1
    ports:
      - "3000:3000"
    volumes:
      - grafana-data:/var/lib/grafana
    restart: unless-stopped
```

> ⚠️ এটি **Linux Docker host**-এর lab configuration। Docker Desktop-এ cAdvisor-এর host-level metrics সীমিত বা ভিন্ন হতে পারে। `privileged: true` ও host filesystem mount নিরাপত্তার দিক থেকে সংবেদনশীল — production-এ permissions ও mount সীমিত করবে।

## 4.4 চালাও

```bash
cd compose/step3-nginx && docker compose down && cd ../step4-monitoring
docker compose up -d --build
docker compose ps
```

| Tool | URL |
|------|-----|
| App (via Nginx) | <http://localhost:8088> |
| Prometheus | <http://localhost:9090> |
| Grafana | <http://localhost:3000> |

## 4.5 Grafana-তে Prometheus data source

1. Grafana খোলো (প্রথমবার `admin` / `admin`; তারপর password বদলাতে বলবে)।
2. **Connections → Data sources → Add data source → Prometheus**।
3. URL: `http://prometheus:9090`
4. **Save & test** সফল হলে হয়ে গেল।

## 4.6 Targets ও query

Prometheus-এর **Status → Targets** পেজে `prometheus` ও `cadvisor` target `UP` কি না দেখো।

Raw metric:

```promql
container_cpu_usage_seconds_total
container_memory_working_set_bytes
```

অর্থপূর্ণ হিসাব — API container-গুলোর CPU (১.০ = এক core) ও memory:

```promql
sum by (name) (rate(container_cpu_usage_seconds_total{name=~".*api.*"}[1m]))
container_memory_working_set_bytes{name=~".*api.*"}
```

Compose project-এর নাম folder থেকে আসে, তাই container-এর নাম হয় `step4-monitoring-api1-1`-এর মতো।

## 4.7 Load দিয়ে graph নড়াও

`/health` এত হালকা যে CPU নড়ে না; তাই API-র `/cpu` endpoint ব্যবহার করো (৮টি সমান্তরাল connection, ৬০০টি request, প্রতিটি ~০.৩ সেকেন্ড CPU busy):

```bash
seq 1 600 | xargs -P 8 -I{} curl -s -o /dev/null "http://localhost:8088/cpu?ms=300"
```

এর মধ্যে Prometheus-এ উপরের CPU query চালালে তিনটি API container-এ CPU বাড়তে দেখবে।

## Cleanup

```bash
docker compose down        # volume রেখে
docker compose down -v     # volume সহ মুছে
```

## ✅ এই ধাপে যা শিখলে

Monitoring জানায় **কখন** load বাড়ছে। কিন্তু এখনো autoscaling নেই — replica সংখ্যা বদলানোর কাজ আলাদা orchestrator বা automation-কে করতে হয়।

⬅️ [Step 3](03-nginx-load-balancer.md) | ➡️ [Step 5 — Docker Swarm](05-docker-swarm.md)
