# Step 5 — Docker Swarm: একাধিক server-এ scaling

> Project files: [`swarm/stack.yaml`](../swarm/stack.yaml)

## লক্ষ্য

একই app-কে একটি server থেকে তিনটি server-এ নিয়ে যাওয়া: SQL Server (১টি), API (৩টি) ও web (২টি)।

## 5.1 তিনটি Linux server

| Server | Private IP | Role |
|--------|-----------|------|
| manager-1 | 10.0.1.10 | Swarm manager (এখানেই database চলবে) |
| worker-1 | 10.0.1.11 | API / web containers |
| worker-2 | 10.0.1.12 | API / web containers |

IP-গুলো উদাহরণ। সব server-এ Docker Engine ইনস্টল করবে এবং private network connectivity নিশ্চিত করবে।

## 5.2 Swarm initialize

Manager-এ:

```bash
docker swarm init --advertise-addr 10.0.1.10
docker swarm join-token worker
```

Manager যে `docker swarm join ...` command দেখাবে (token ও manager IP সহ) সেটি `worker-1` ও `worker-2`-তে চালাও। তারপর manager-এ:

```bash
docker node ls
```

তিনটি node `Ready` দেখালে cluster প্রস্তুত।

### Firewall

Cluster-এর trusted private network-এর জন্য:

| Port | Protocol | কাজ |
|------|----------|-----|
| 2377 | TCP | Cluster management |
| 7946 | TCP/UDP | Node communication |
| 4789 | UDP | Overlay network (VXLAN) |

এছাড়া বাইরের ব্যবহারকারীদের জন্য app-র port (এই stack-এ `80/tcp`) খুলতে হবে।

## 5.3 Image registry-তে publish

সব worker যেন একই image পায়, তাই Docker Hub বা private registry ব্যবহার করো। Build machine-এ (repo root থেকে):

```bash
docker login

docker build -t YOUR_USERNAME/scaling-demo-api:1.0 src/ScalingDemo.Api
docker build -t YOUR_USERNAME/scaling-demo-web:1.0 src/ScalingDemo.Web

docker push YOUR_USERNAME/scaling-demo-api:1.0
docker push YOUR_USERNAME/scaling-demo-web:1.0
```

`YOUR_USERNAME` নিজের Docker Hub username দিয়ে বদলাবে।

## 5.4 Stack file

`swarm/stack.yaml` (সংক্ষেপে; পূর্ণ ফাইল repo-তে):

```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "${MSSQL_SA_PASSWORD:?export MSSQL_SA_PASSWORD before deploying}"
    volumes:
      - mssql-data:/var/opt/mssql
    networks: [app-net]
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.role == manager

  api:
    image: YOUR_USERNAME/scaling-demo-api:1.0
    environment:
      ConnectionStrings__BmiDb: "Server=sqlserver,1433;Database=BmiDb;User Id=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True"
    networks: [app-net]
    deploy:
      replicas: 3
      update_config: { parallelism: 1, delay: 10s, order: start-first }
      resources:
        limits: { cpus: "1.0", memory: 512M }

  web:
    image: YOUR_USERNAME/scaling-demo-web:1.0
    ports:
      - { target: 80, published: 80, protocol: tcp, mode: ingress }
    networks: [app-net]
    deploy:
      replicas: 2

volumes:
  mssql-data:

networks:
  app-net:
    driver: overlay
```

- `sqlserver` ১টি replica-তে `manager`-এ আটকানো, কারণ volume ওই node-এর local disk-এ। Database scale হয় না; এটি single point of failure।
- `api` নামটি Swarm-এ একটি **service VIP**: web-এর nginx-এর default `server api:8080;` এই VIP-তে পাঠায় এবং Swarm task-গুলোর মধ্যে ভাগ করে। তাই `upstream.conf` বদলাতে হয় না।
- `web` published port-এ routing mesh থাকায় যেকোনো node-এর IP-তে request করলেই চলে।
- Password stack file-এ লেখা নেই; deploy-এর shell থেকে আসে।

## 5.5 Deploy করো

Manager-এ (repo clone করা অবস্থায়):

```bash
export MSSQL_SA_PASSWORD='YourStr0ng!Pass'

docker stack deploy \
  --with-registry-auth \
  -c swarm/stack.yaml \
  scalingdemo
```

Status:

```bash
docker stack services scalingdemo
docker stack ps scalingdemo
```

কিছুক্ষণ পরে (SQL Server চালু হয়ে API database তৈরি করা পর্যন্ত) browser-এ `http://10.0.1.10` খোলো এবং BMI হিসাব করো।

কোন API task উত্তর দিচ্ছে:

```bash
for i in $(seq 1 10); do curl -s http://10.0.1.10/instance; echo; done
```

## 5.6 Scale up / down

```bash
docker service scale scalingdemo_api=5
docker service scale scalingdemo_api=3
```

`docker stack ps scalingdemo` দিয়ে দেখো task-গুলো কোন node-এ গেল।

## Cleanup

```bash
docker stack rm scalingdemo
docker swarm leave --force     # প্রতিটি node-এ, ইচ্ছে হলে
```

## ✅ এই ধাপে যা শিখলে

- Task ব্যর্থ হলে Swarm desired replica count বজায় রাখার চেষ্টা করে এবং service VIP load balance করে।
- কিন্তু এটি CPU usage অনুযায়ী replica সংখ্যা **স্বয়ংক্রিয়ভাবে** বাড়ায় না — Kubernetes HPA-এর মতো ব্যবস্থা এখানে নেই।
- বাস্তব production-এ TLS, secrets management, replicated database ও আলাদা ingress আলাদাভাবে configure করতে হবে।

⬅️ [Step 4](04-monitoring.md) | ➡️ [Step 6 — Kubernetes HPA](06-kubernetes-hpa.md)
