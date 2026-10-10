# Step 2 — Docker Compose দিয়ে তিনটি replica

> Project files: [`compose/step2-scale/compose.yaml`](../compose/step2-scale/compose.yaml)

## লক্ষ্য

একই API image থেকে একাধিক container (replica) চালানো, আর পুরো app Compose দিয়ে একসাথে চালানো।

## প্রস্তুতি

Step 1-এর container ও network বন্ধ করো, যাতে port ও নাম নিয়ে বিভ্রান্তি না হয়:

```bash
docker rm -f web api-single sqlserver
docker network rm scaling-net
```

## 2.1 `compose.yaml`

তিনটি service: `sqlserver`, `api` (scale হবে), `web`।

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

  api:
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

  web:
    image: scaling-demo-web:1.0
    build:
      context: ../../src/ScalingDemo.Web
    ports:
      - "4200:80"
    depends_on:
      - api
    restart: unless-stopped

volumes:
  mssql-data:
```

- `api`-তে `ports` নেই: প্রতিটি replica একই host port নিতে পারে না। `expose` শুধু internal port document করে; একই network-এর `web` সরাসরি `api:8080`-এ যেতে পারে।
- Password বদলাতে: `MSSQL_SA_PASSWORD='YourStr0ng!Pass' docker compose up -d --build --scale api=3`।
- Database connection string-এ container-এর নাম (`sqlserver`) ব্যবহার হয়েছে — Compose-এর DNS এটি resolve করে।

## 2.2 তিনটি replica চালাও

```bash
cd compose/step2-scale
docker compose up -d --build --scale api=3
docker compose ps
```

`api` service-এর তিনটি container দেখতে পাবে। তিনটি replica একসাথে database তৈরি করতে চায়, কিন্তু SQL Server application lock-এর কারণে কাজটা একবারই হয়। যাচাই:

```bash
docker compose logs api | grep -i "initialised"
```

## 2.3 App ব্যবহার

<http://localhost:4200> খোলো এবং কয়েকটি BMI হিসাব করো। Command line থেকে কোন replica উত্তর দিচ্ছে দেখো:

```bash
for i in $(seq 1 10); do curl -s http://localhost:4200/instance; echo; done
```

`Hostname` একাধিক মান দেখাতে পারে। কারণ: web-এর nginx শুরুর সময় `api` নামটি resolve করে — Compose DNS তখন তিনটি container-এর IP দেয় এবং nginx সেগুলোতে round-robin করে।

## 2.4 Replica সংখ্যা পরিবর্তন

```bash
docker compose up -d --scale api=5
docker compose exec web nginx -s reload     # নতুন replica-গুলো nginx-কে চিনিয়ে দেওয়া

docker compose up -d --scale api=2
docker compose exec web nginx -s reload
```

`reload` না করলে nginx পুরোনো IP-ই ব্যবহার করে — কারণ hostname শুধু nginx চালু/reload হওয়ার সময় resolve হয়।

## Cleanup

```bash
docker compose down          # database volume রেখে
docker compose down -v       # database সহ মুছে
```

## ✅ এই ধাপে যা শিখলে

একই application image থেকে একাধিক container চালানো যায়। তবে:

- Compose নিজে থেকে CPU usage দেখে replica বাড়ায় না।
- Replica-গুলোর মধ্যে ভাগ করা এখানে DNS-ভিত্তিক ও nginx শুরুর সময়ের ওপর নির্ভরশীল — স্পষ্টভাবে নিয়ন্ত্রিত load balancer নয়।

⬅️ [Step 1](01-single-container.md) | ➡️ [Step 3 — Nginx Load Balancer](03-nginx-load-balancer.md)
