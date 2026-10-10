# Step 3 — Nginx Load Balancer

> Project files: [`compose/step3-nginx/`](../compose/step3-nginx)

## লক্ষ্য

Client-এর সব request একটি জায়গায় (Nginx) আসবে; Nginx সেগুলো স্পষ্টভাবে তালিকাভুক্ত API replica-তে ভাগ করে দেবে।

## 3.1 Design

আলাদা load balancer container লাগে না: `web` container-এর nginx-ই Angular file serve করে এবং `/api`, `/health`, `/instance`, `/cpu` backend-এ পাঠায়। কোন backend-এ পাঠাবে তা `upstream.conf` ফাইলে — web image-এর `nginx.conf` এটিকে `upstream api_backend { include /etc/nginx/upstream.conf; }`-এর ভেতরে নেয়।

```text
Browser ──► web:8088 (nginx)
              ├── /           → Angular static files
              └── /api, …     → least_conn → api1, api2, api3 → sqlserver
```

## 3.2 কেন `api1`, `api2`, `api3` আলাদা service?

Step 2-এ nginx শুধু শুরুর সময় `api` resolve করত। এখানে তিনটি backend স্পষ্ট নামে লেখা, তাই load balancing predictable এবং প্রতিটি backend আলাদাভাবে বন্ধ করে পরীক্ষা (fail-over) করা যায়। দাম: replica সংখ্যা বদলালে `upstream.conf` হাতে বদলাতে হয়।

## 3.3 `nginx/upstream.conf`

```nginx
least_conn;

server api1:8080;
server api2:8080;
server api3:8080;
```

`least_conn` সবচেয়ে কম active connection-ওয়ালা backend বেছে নেয়; তাই পরপর প্রতিটি request আলাদা instance-এ যাবে এমন নিশ্চয়তা নেই।

## 3.4 `compose.yaml`

```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "${MSSQL_SA_PASSWORD:-Lab_Passw0rd!}"
    volumes:
      - mssql-data:/var/opt/mssql
    restart: unless-stopped

  api1: &api
    image: scaling-demo-api:1.0
    build:
      context: ../../src/ScalingDemo.Api
    environment:
      ConnectionStrings__BmiDb: "Server=sqlserver,1433;Database=BmiDb;User Id=sa;Password=${MSSQL_SA_PASSWORD:-Lab_Passw0rd!};Encrypt=True;TrustServerCertificate=True"
    expose:
      - "8080"
    depends_on:
      - sqlserver
    restart: unless-stopped

  api2:
    <<: *api

  api3:
    <<: *api

  web:
    image: scaling-demo-web:1.0
    build:
      context: ../../src/ScalingDemo.Web
    ports:
      - "8088:80"
    volumes:
      - ./nginx/upstream.conf:/etc/nginx/upstream.conf:ro
    depends_on:
      - api1
      - api2
      - api3
    restart: unless-stopped

volumes:
  mssql-data:
```

`&api` ও `<<: *api` YAML anchor: তিনটি API service-এর সাধারণ অংশ একবারই লেখা।

## 3.5 চালাও ও পরীক্ষা করো

আগের Compose project বন্ধ করে নতুনটি চালাও:

```bash
cd compose/step2-scale && docker compose down && cd ../step3-nginx
docker compose up -d --build
docker compose ps
```

Browser-এ <http://localhost:8088> খোলো এবং BMI হিসাব করো।

কোন API instance request পেয়েছে দেখো:

```bash
for i in $(seq 1 10); do curl -s http://localhost:8088/instance; echo; done

curl -si http://localhost:8088/health | grep -i x-served-by
```

`Hostname` / `X-Served-By` দেখে বোঝা যাবে কোন replica উত্তর দিয়েছে। UI-র নিচে "Last response served by" লেখাও request-এর সাথে বদলায়।

### Fail-over পরীক্ষা

```bash
docker compose stop api2
for i in $(seq 1 10); do curl -s http://localhost:8088/instance; echo; done   # সাধারণত api2 আর দেখা যাবে না
docker compose start api2
```

## Cleanup

```bash
docker compose down
```

## ✅ এই ধাপে যা শিখলে

- Nginx upstream দিয়ে একাধিক backend-এ request distribution এবং একটি backend বন্ধ হলে বাকিগুলো চালিয়ে যাওয়া।
- Replica সংখ্যা বদলালে `upstream.conf`-ও হাতে আপডেট করতে হয়। আরও dynamic setup-এর জন্য Traefik বা service discovery সমর্থিত load balancer বিবেচনা করা যায়।

⬅️ [Step 2](02-compose-replicas.md) | ➡️ [Step 4 — Monitoring](04-monitoring.md)
