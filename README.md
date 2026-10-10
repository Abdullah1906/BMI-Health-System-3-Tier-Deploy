# Docker থেকে Kubernetes Auto Scaling — BMI Health Calculator Lab

একটি সহজ **BMI Health Calculator** website নিয়ে একটি container থেকে শুরু করে Kubernetes HPA পর্যন্ত ধাপে ধাপে scaling lab। পুরো lab-এ একই application ব্যবহার হয়েছে, তাই প্রতিটি ধাপ আগের ধাপের ওপর তৈরি।

| Layer | Technology |
|-------|------------|
| Frontend | Angular 20 (standalone components, signals, reactive forms), nginx |
| Backend | ASP.NET Core 8 Web API |
| Database | Microsoft SQL Server — **ADO.NET + Stored Procedure** (EF Core নেই) |
| Scaling | Docker Compose, Nginx load balancer, Docker Swarm, Kubernetes HPA |
| Monitoring | cAdvisor, Prometheus, Grafana |

## Application কী করে

উচ্চতা (cm), ওজন (kg) ও বয়স (১৮+) দিলে BMI, category (Underweight / Normal / Overweight / Obese, WHO adult range), স্বাস্থ্যকর ওজনের সীমা ও পরামর্শ দেখায়। প্রতিটি হিসাব SQL Server-এ জমা হয়; পাশে history (pagination সহ), গড় BMI ও category-ভিত্তিক গণনা দেখা যায়, এবং record মোছা যায়।

Page-এর নিচে **"Last response served by …"** লেখায় দেখা যায় কোন API replica শেষ response দিয়েছে — load balancing ও scaling চোখে দেখার জন্য।

```text
Browser ──► web (nginx + Angular) ──► api (ASP.NET Core, ADO.NET) ──► SQL Server
              │  /api  /health  /instance  /cpu  proxy হয়              (Stored Procedures)
              └─ বাকি সব path-এ Angular static file
```

## Roadmap

| # | Chapter | বিষয় | Project files |
|---|---------|-------|---------------|
| 1 | [Single Container](docs/01-single-container.md) | App পরিচিতি, Dockerfile, image build ও run | `src/` |
| 2 | [Compose Replicas](docs/02-compose-replicas.md) | `docker compose --scale` | `compose/step2-scale/` |
| 3 | [Nginx Load Balancer](docs/03-nginx-load-balancer.md) | Request distribution | `compose/step3-nginx/` |
| 4 | [Monitoring](docs/04-monitoring.md) | Prometheus + Grafana + cAdvisor | `compose/step4-monitoring/` |
| 5 | [Docker Swarm](docs/05-docker-swarm.md) | Multi-server orchestration | `swarm/` |
| 6 | [Kubernetes HPA](docs/06-kubernetes-hpa.md) | CPU-ভিত্তিক automatic scaling | `k8s/` |

## Project structure

```text
scaling-demo/
├── README.md
├── docs/                          chapter 1–6 (.md)
├── src/
│   ├── ScalingDemo.Api/           ASP.NET Core 8 Web API
│   │   ├── Controllers/           BmiController.cs            (/api/bmi)
│   │   ├── Services/              BmiCalculator.cs, BmiService.cs
│   │   ├── Data/                  BmiRepository.cs            (ADO.NET → SP_Bmi_*)
│   │   │                          DatabaseInitializer.cs, SqlConnectionFactory.cs
│   │   ├── Database/              001_schema.sql, 002_procedures.sql
│   │   ├── Infrastructure/        LabEndpoints.cs             (/health, /health/ready, /instance, /cpu)
│   │   │                          ServedByHeader.cs, DatabaseExceptionHandler.cs
│   │   ├── Models/                BmiRequest.cs, BmiDtos.cs
│   │   ├── Program.cs, appsettings.json, ScalingDemo.Api.csproj
│   │   └── Dockerfile
│   └── ScalingDemo.Web/           Angular 20
│       ├── src/app/
│       │   ├── core/              bmi-api.ts, bmi-store.ts, models.ts, served-by.ts, api-error.ts
│       │   ├── features/          bmi-form, bmi-result, bmi-stats, bmi-history
│       │   └── shared/            bmi-category.ts
│       ├── nginx/                 nginx.conf, upstream.conf
│       ├── proxy.conf.json        (ng serve → API)
│       └── Dockerfile
├── compose/
│   ├── step2-scale/compose.yaml
│   ├── step3-nginx/               compose.yaml, nginx/upstream.conf
│   └── step4-monitoring/          compose.yaml, monitoring/prometheus.yml
├── swarm/stack.yaml
└── k8s/                           sqlserver.yaml, scaling-demo.yaml, web.yaml, hpa.yaml, load-test.yaml
```

## প্রয়োজনীয় জিনিস

- Windows 10/11-এ Docker Desktop অথবা Linux server-এ Docker Engine (Compose v2)
- SQL Server container-এর জন্য কমপক্ষে ~2 GB খালি RAM (image শুধু Linux x64-এর জন্য)
- শুধু local dev করতে চাইলে: .NET 8 SDK ও Node.js 20.19+ / 22.12+
- Swarm-এর জন্য ৩টি Linux VM/EC2 instance
- Kubernetes অংশের জন্য একটি kubeadm cluster বা উপযুক্ত lab cluster (Metrics Server সহ)

## দ্রুত শুরু (পুরো app, ৩টি API replica + load balancer)

```bash
git clone <your-repo-url>
cd scaling-demo/compose/step3-nginx
docker compose up -d --build
```

এক-দুই মিনিট পরে (SQL Server চালু হয়ে API database তৈরি করা পর্যন্ত) <http://localhost:8088> খোলো। শেষ করতে:

```bash
docker compose down        # volume রেখে
docker compose down -v     # database সহ মুছে
```

ধাপে ধাপে শিখতে [Chapter 1](docs/01-single-container.md) থেকে শুরু করো।

## Local development (Docker ছাড়া app)

```bash
# 1) শুধু database
docker run -d --name sqlserver -p 1433:1433 \
  -e ACCEPT_EULA=Y -e 'MSSQL_SA_PASSWORD=Lab_Passw0rd!' \
  mcr.microsoft.com/mssql/server:2022-latest

# 2) API → http://localhost:5080  (database ও table নিজে তৈরি করে)
cd src/ScalingDemo.Api
export ConnectionStrings__BmiDb='Server=localhost,1433;Database=BmiDb;User Id=sa;Password=Lab_Passw0rd!;Encrypt=True;TrustServerCertificate=True'
dotnet run

# 3) Angular → http://localhost:4200  (/api proxy হয় 5080-এ)
cd ../ScalingDemo.Web
npm ci
npm start
```

## API endpoints

| Method | Path | কাজ |
|--------|------|-----|
| POST | `/api/bmi` | BMI হিসাব করে database-এ রাখে |
| GET | `/api/bmi?page=1&pageSize=10` | History (নতুন আগে) |
| GET | `/api/bmi/{id}` | একটি record |
| GET | `/api/bmi/stats` | মোট, গড় BMI, category-ভিত্তিক গণনা |
| DELETE | `/api/bmi/{id}` | Record মোছা |
| GET | `/health` | Liveness — database ছোঁয় না |
| GET | `/health/ready` | Readiness — database ও schema আছে কি না |
| GET | `/instance` | কোন replica উত্তর দিল |
| GET | `/cpu?ms=300` | CPU busy রাখে (HPA load test-এর জন্য) |

প্রতিটি API response-এ `X-Served-By` header থাকে।

## গুরুত্বপূর্ণ নোট

- Docker Compose-এর replica ও Kubernetes HPA এক জিনিস নয়। Compose/Swarm নিজে CPU দেখে replica বাড়ায় না; HPA বাড়ায়।
- Scaling হয় stateless অংশে (API ও web)। Database এখানে **একটিই** instance — এটি single point of failure; production-এ managed বা replicated database লাগবে।
- **Password:** compose file-এ lab-এর জন্য default `Lab_Passw0rd!` আছে। নিজের মান দিতে `MSSQL_SA_PASSWORD` environment variable ব্যবহার করো। Swarm-এ না দিলে deploy-ই হয় না; Kubernetes-এ Secret লাগে। Production-এ কখনো default রাখবে না।
- App `sa` account-এ database-এ ঢোকে এবং `TrustServerCertificate=True` ব্যবহার করে — শুধু lab-এর জন্য। Production-এ শুধু `EXECUTE` অধিকারসহ আলাদা login ও বৈধ certificate ব্যবহার করবে।
- SQL Server `Developer` edition শুধু development/test-এর জন্য licensed।
- `YOUR_USERNAME` লেখা জায়গাগুলো (`swarm/stack.yaml`, `k8s/scaling-demo.yaml`, `k8s/web.yaml`) নিজের Docker Hub username দিয়ে বদলাতে হবে।
- BMI একটি screening পরিমাপ, medical diagnosis নয়; Category গুলো প্রাপ্তবয়স্কদের (WHO) জন্য।
