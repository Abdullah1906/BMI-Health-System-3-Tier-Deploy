# Step 1 — App পরিচিতি ও একটি Docker container চালানো

> Project files: [`src/ScalingDemo.Api/`](../src/ScalingDemo.Api) · [`src/ScalingDemo.Web/`](../src/ScalingDemo.Web)

## লক্ষ্য

BMI Health Calculator app-টি বুঝে নেওয়া, Docker image বানানো এবং container হিসেবে চালানো — প্রথমে শুধু API, তারপর database ও website সহ।

## 1.1 Architecture

```text
Browser ──► web (nginx + Angular) ──► api (ASP.NET Core 8) ──► SQL Server
```

- **web**: Angular 20 build করা static file serve করে এবং `/api`, `/health`, `/instance`, `/cpu` API-তে proxy করে।
- **api**: ASP.NET Core Web API। Database access শুধু **ADO.NET + Stored Procedure** দিয়ে।
- **SQL Server**: `BmiDb` database।

## 1.2 Backend (`ScalingDemo.Api`)

| Folder | কাজ |
|--------|-----|
| `Controllers/` | `BmiController` — `/api/bmi` endpoint |
| `Services/` | `BmiCalculator` (শুধু হিসাব, I/O নেই), `BmiService` |
| `Data/` | `BmiRepository` (ADO.NET), `SqlConnectionFactory`, `DatabaseInitializer` |
| `Database/` | `001_schema.sql` (table), `002_procedures.sql` (stored procedure) |
| `Infrastructure/` | `/health`, `/health/ready`, `/instance`, `/cpu`, `X-Served-By` header, database error → 503 |
| `Models/` | Request ও response DTO |

### Database ও stored procedure

Table `dbo.BmiRecords` (`Id`, `Name`, `Age`, `HeightCm`, `WeightKg`, `Bmi`, `Category`, `CreatedAtUtc`) — primary key, `CHECK` constraint (বয়স 18–120, উচ্চতা 50–260, ওজন 10–500, category-র চারটি মান) ও `DEFAULT` সহ।

| Stored procedure | কাজ |
|------------------|-----|
| `SP_Bmi_Create` | Record insert করে ও insert হওয়া row ফেরত দেয় |
| `SP_Bmi_GetById` | একটি record |
| `SP_Bmi_GetAll` | Paged history (result set ১: মোট সংখ্যা, ২: page) |
| `SP_Bmi_GetStats` | মোট, গড় BMI, category-ভিত্তিক গণনা |
| `SP_Bmi_Delete` | Record মোছে; `@RowsAffected` output parameter |

`BmiRepository` প্রতিটি call-এ `CommandType.StoredProcedure` ও typed `SqlParameter` ব্যবহার করে।

### Database নিজে নিজে তৈরি হয়

`DatabaseInitializer` background-এ SQL Server-এ ঢুকে (না পেলে প্রতি ৫ সেকেন্ডে আবার চেষ্টা করে):

1. `BmiDb` না থাকলে তৈরি করে,
2. `Database/*.sql` ক্রমানুযায়ী চালায় (সব script বারবার চালানো যায়: `IF ... IS NULL`, `CREATE OR ALTER`)।

একসাথে কয়েকটি replica চালু হলে তারা একই সময়ে init করতে চায়; তাই SQL Server application lock (`sp_getapplock`) দিয়ে কাজটা এক এক করে হয়। Initialization background-এ চলে, তাই API সাথে সাথেই চালু হয় এবং database ছাড়াও `/health` সবুজ থাকে।

### Health ও lab endpoint

| Endpoint | অর্থ |
|----------|------|
| `/health` | Liveness: process জীবিত। Database ছোঁয় না। |
| `/health/ready` | Readiness: database পৌঁছানো যায় ও schema আছে। না হলে `503`। |
| `/instance` | `Hostname` ও সময় — কোন container উত্তর দিল। |
| `/cpu?ms=300` | নির্দিষ্ট সময় CPU busy রাখে (`/health` এত হালকা যে HPA load test-এ কাজে লাগে না)। |

প্রতিটি response-এ `X-Served-By: <hostname>` header যায়।

## 1.3 Frontend (`ScalingDemo.Web`)

```text
src/app/
├── core/       bmi-api.ts (HTTP), bmi-store.ts (signals state), models.ts, served-by.ts, api-error.ts
├── features/   bmi-form, bmi-result, bmi-stats, bmi-history
└── shared/     bmi-category.ts
```

API URL গুলো relative (`/api/bmi`), তাই একই code container-এ (nginx proxy), `ng serve`-এ (`proxy.conf.json`) ও Kubernetes-এ চলে।

## 1.4 Dockerfile

**API** ([`Dockerfile`](../src/ScalingDemo.Api/Dockerfile)) — multi-stage; restore আলাদা layer-এ (cache); non-root user:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ScalingDemo.Api.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish ScalingDemo.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "ScalingDemo.Api.dll"]
```

**Web** ([`Dockerfile`](../src/ScalingDemo.Web/Dockerfile)) — Node-এ Angular build, nginx-এ serve:

```dockerfile
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build -- --configuration production

FROM nginx:1.28-alpine AS runtime
COPY nginx/nginx.conf /etc/nginx/nginx.conf
COPY nginx/upstream.conf /etc/nginx/upstream.conf
COPY --from=build /app/dist/scaling-demo-web/browser /usr/share/nginx/html
EXPOSE 80
```

`nginx.conf` API-র তালিকা নেয় `upstream.conf` থেকে (image-এ default: `server api:8080;`)। পরের ধাপগুলোতে Compose ও Kubernetes এই ফাইল বদলে দেয়।

## 1.5 Image build ও শুধু API চালানো

Repo root থেকে:

```bash
docker build -t scaling-demo-api:1.0 src/ScalingDemo.Api
docker build -t scaling-demo-web:1.0 src/ScalingDemo.Web
```

API একা চালাও:

```bash
docker run -d \
  --name api-single \
  -p 8080:8080 \
  scaling-demo-api:1.0
```

> PowerShell-এ `\`-এর জায়গায় backtick (`` ` ``) বা পুরো command এক লাইনে লেখো।

```bash
docker ps
curl http://localhost:8080/health
curl http://localhost:8080/instance
curl -i http://localhost:8080/health/ready
curl -i http://localhost:8080/api/bmi/stats
```

প্রত্যাশিত আচরণ — database এখনো নেই বলে:

- `/health` ও `/instance` চলবে (`/health` → `{"status":"Healthy"}`)।
- `/health/ready` ও `/api/bmi/stats` **503** দেবে (`NotReady` / "Database unavailable")। এটি ভুল নয়; সঠিক আচরণ।

## 1.6 SQL Server ও website যোগ করা

আগের container সরিয়ে একটি network বানাও, যাতে container-গুলো নাম ধরে একে অপরকে পায়:

```bash
docker rm -f api-single
docker network create scaling-net
```

**Database:**

```bash
docker run -d \
  --name sqlserver \
  --network scaling-net \
  -e ACCEPT_EULA=Y \
  -e 'MSSQL_SA_PASSWORD=Lab_Passw0rd!' \
  -v mssql-data:/var/opt/mssql \
  mcr.microsoft.com/mssql/server:2022-latest
```

**API** (`--network-alias api` দিয়ে, কারণ web-এর nginx `api:8080` খোঁজে):

```bash
docker run -d \
  --name api-single \
  --network scaling-net \
  --network-alias api \
  -p 8080:8080 \
  -e 'ConnectionStrings__BmiDb=Server=sqlserver,1433;Database=BmiDb;User Id=sa;Password=Lab_Passw0rd!;Encrypt=True;TrustServerCertificate=True' \
  scaling-demo-api:1.0
```

**Website:**

```bash
docker run -d \
  --name web \
  --network scaling-net \
  -p 4200:80 \
  scaling-demo-web:1.0
```

SQL Server চালু হতে কিছুটা সময় লাগে; API log-এ retry দেখা যাবে, শেষে initialise হবে:

```bash
docker logs -f api-single      # "Database is initialised." দেখলে Ctrl+C
curl http://localhost:8080/health/ready
```

## 1.7 পরীক্ষা

Browser-এ <http://localhost:4200> খোলো, একটি BMI হিসাব করো, তারপর history ও stats দেখো। Page-এর নিচে "Last response served by" লেখায় container-এর নাম দেখাবে।

Command line থেকে:

```bash
curl -X POST http://localhost:8080/api/bmi \
  -H 'Content-Type: application/json' \
  -d '{"name":"Rahim","age":25,"heightCm":170,"weightKg":68}'

curl http://localhost:8080/api/bmi/stats
```

Response-এ `record` (id, bmi, category …), `healthyWeightMinKg`, `healthyWeightMaxKg` ও `advice` থাকবে। অবৈধ ইনপুট (যেমন `"age":10`) দিলে `400` ও validation message ফেরত আসবে।

Database-এ সরাসরি দেখতে (image-ভেদে `sqlcmd`-এর path `/opt/mssql-tools18/bin/` বা `/opt/mssql-tools/bin/`; একটি না মিললে অন্যটি):

```bash
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa \
  -P 'Lab_Passw0rd!' -d BmiDb -Q "SELECT TOP 5 * FROM dbo.BmiRecords ORDER BY Id DESC"
```

## Cleanup

```bash
docker rm -f web api-single sqlserver
docker network rm scaling-net
docker volume rm mssql-data
```

## ✅ এই ধাপে যা শিখলে

- Multi-stage Dockerfile দিয়ে .NET ও Angular আলাদাভাবে build করা যায়।
- Liveness ও readiness আলাদা: database না থাকলেও process জীবিত থাকে, কিন্তু "ready" হয় না।
- `/instance` ও `X-Served-By` পরের ধাপগুলোতে load balancing যাচাই করতে কাজে লাগবে।

➡️ পরের ধাপ: [Step 2 — Compose Replicas](02-compose-replicas.md)
