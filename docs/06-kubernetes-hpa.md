# Step 6 — Kubernetes HPA: Automatic Scaling

> Project files: [`k8s/`](../k8s)

## লক্ষ্য

Kubernetes-এ পুরো app (SQL Server + API + web) চালিয়ে API-র CPU utilization বাড়লে replica স্বয়ংক্রিয়ভাবে বাড়ানো।

ধরে নিচ্ছি kubeadm cluster প্রস্তুত এবং `kubectl` দিয়ে access আছে।

## 6.1 Cluster পরীক্ষা

```bash
kubectl get nodes
kubectl get pods -A
```

সব node `Ready` হওয়া উচিত। SQL Server pod-এর জন্য কোনো node-এ কমপক্ষে ~2 GiB খালি memory লাগবে।

Metrics Server পরীক্ষা:

```bash
kubectl top nodes
```

`Metrics API not available` জাতীয় error এলে আগে **Metrics Server** install ও configure করতে হবে (kubeadm cluster-এ এটি অনেক সময় আলাদাভাবে যোগ করতে হয়)। Metrics পাওয়া শুরু হলে:

```bash
kubectl top pods -A
```

## 6.2 Image registry-তে push

Swarm-এর মতোই দুটি image লাগবে (Step 5.3-এর কমান্ড):

```text
YOUR_USERNAME/scaling-demo-api:1.0
YOUR_USERNAME/scaling-demo-web:1.0
```

`k8s/scaling-demo.yaml` ও `k8s/web.yaml`-এ `YOUR_USERNAME` বদলে নাও। Node-গুলো যেন registry থেকে pull করতে পারে তা নিশ্চিত করবে; private registry হলে `imagePullSecrets` configure করবে।

## 6.3 Secret ও database

Database password Git-এর file-এ থাকবে না; Secret হিসেবে বানাও:

```bash
kubectl create secret generic mssql-secret \
  --from-literal=MSSQL_SA_PASSWORD='YourStr0ng!Pass'
```

[`k8s/sqlserver.yaml`](../k8s/sqlserver.yaml): `mssql` Deployment (১টি pod) ও `mssql` ClusterIP Service (port 1433)।

> ⚠️ Lab-এ ডেটা `emptyDir`-এ থাকে — pod মুছলে বা অন্য node-এ গেলে হারাবে। স্থায়ী করতে `emptyDir`-এর জায়গায় `PersistentVolumeClaim` (StorageClass সহ) ব্যবহার করো।

## 6.4 API Deployment ও Service

[`k8s/scaling-demo.yaml`](../k8s/scaling-demo.yaml) (মূল অংশ):

```yaml
spec:
  replicas: 2
  template:
    spec:
      containers:
        - name: api
          image: YOUR_USERNAME/scaling-demo-api:1.0
          ports:
            - containerPort: 8080
          env:
            - name: MSSQL_SA_PASSWORD
              valueFrom:
                secretKeyRef: { name: mssql-secret, key: MSSQL_SA_PASSWORD }
            - name: ConnectionStrings__BmiDb
              value: "Server=mssql,1433;Database=BmiDb;User Id=sa;Password=$(MSSQL_SA_PASSWORD);Encrypt=True;TrustServerCertificate=True"
          livenessProbe:
            httpGet: { path: /health, port: 8080 }
          readinessProbe:
            httpGet: { path: /health/ready, port: 8080 }
          resources:
            requests: { cpu: 100m, memory: 128Mi }
            limits:   { cpu: "1",  memory: 512Mi }
```

| Field | অর্থ |
|-------|------|
| `replicas: 2` | শুরুতে দুটি pod (HPA পরে বদলাবে) |
| `$(MSSQL_SA_PASSWORD)` | আগে define করা env থেকে Kubernetes নিজে বসায় — তাই env দুটির ক্রম বদলাবে না |
| `livenessProbe: /health` | Process আটকে গেলে restart; database ছোঁয় না |
| `readinessProbe: /health/ready` | Database ও schema তৈরি না হওয়া পর্যন্ত pod Service থেকে traffic পায় না |
| `requests.cpu: 100m` | Scheduling ও HPA-র utilization হিসাবের ভিত্তি |
| `limits.cpu: "1"` | প্রতি pod-এ সর্বোচ্চ 1 CPU core |

Service `scaling-demo-service` (ClusterIP, port 80 → 8080) pod-গুলোর সামনে স্থিতিশীল internal endpoint।

## 6.5 Web (Angular + nginx)

[`k8s/web.yaml`](../k8s/web.yaml) তিনটি জিনিস বানায়:

- **ConfigMap** `scaling-demo-web-upstream` — nginx-এর `upstream.conf`: `server scaling-demo-service:80;` (Step 3-এর ফাইলের Kubernetes সংস্করণ)।
- **Deployment** `scaling-demo-web` (২টি pod) — ConfigMap-টি `/etc/nginx/upstream.conf`-এ mount করে।
- **Service** `scaling-demo-web` — lab-এর জন্য `NodePort 30080`।

## 6.6 সব চালাও

```bash
kubectl apply -f k8s/sqlserver.yaml
kubectl apply -f k8s/scaling-demo.yaml
kubectl apply -f k8s/web.yaml

kubectl rollout status deployment/mssql
kubectl get pods -o wide
kubectl get services
```

API pod-গুলো `Ready` হবে database initialise হওয়ার পরে — কিছুক্ষণ `0/1` দেখা স্বাভাবিক। `kubectl logs deploy/scaling-demo-api`-এ "Database is initialised." খোঁজো।

App খোলো: `http://<যেকোনো-node-IP>:30080`, অথবা:

```bash
kubectl port-forward service/scaling-demo-web 8080:80
# http://localhost:8080
```

CPU metrics:

```bash
kubectl top pods
```

## 6.7 HPA

[`k8s/hpa.yaml`](../k8s/hpa.yaml):

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: scaling-demo-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: scaling-demo-api
  minReplicas: 2
  maxReplicas: 6
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
  behavior:
    scaleUp:
      stabilizationWindowSeconds: 0
    scaleDown:
      stabilizationWindowSeconds: 300
```

HPA শুধু **API** Deployment-কে scale করে; target CPU utilization 70%। Utilization হিসাব হয় **CPU usage বনাম pod-এর CPU request** দিয়ে — limit দিয়ে নয়। Request `100m` হলে 70% মানে প্রতি pod-এ গড়ে প্রায় `70m`। বাস্তবে replica সংখ্যা traffic, observed metrics ও scaling algorithm অনুযায়ী ঠিক হবে।

```bash
kubectl apply -f k8s/hpa.yaml

kubectl get hpa
kubectl describe hpa scaling-demo-hpa
kubectl get pods -w
```

Desired replica count, current CPU utilization ও events পর্যবেক্ষণ করো।

## 6.8 Load test করে automatic scaling দেখো

**Terminal 1** — HPA দেখো:

```bash
kubectl get hpa -w
```

**Terminal 2** — cluster-এর ভেতর থেকে load তৈরি করো। [`k8s/load-test.yaml`](../k8s/load-test.yaml) চারটি loop চালিয়ে API Service-এর `/cpu?ms=300` endpoint-এ request পাঠায়:

```bash
kubectl apply -f k8s/load-test.yaml
```

> `/health` loop (`wget .../health`) দিয়েও চেষ্টা করা যায়, কিন্তু endpoint এত হালকা যে যথেষ্ট CPU load তৈরি হবে এমন নিশ্চয়তা নেই। এজন্যই API-তে `/cpu` যোগ করা হয়েছে। এটি database ছোঁয় না, তাই শুধু API-র CPU scaling দেখা যায়।

পর্যবেক্ষণ:

```bash
kubectl get hpa
kubectl get deployment scaling-demo-api
kubectl get pods -o wide
kubectl top pods
```

নতুন pod `Ready` হলেই Service-এ traffic পায় — browser-এ app-র footer-এ "Last response served by" বিভিন্ন pod-এর নাম দেখাতে শুরু করবে।

পরীক্ষা শেষে load বন্ধ করো:

```bash
kubectl delete pod load-test
```

Load কমার পর `scaleDown` stabilization window (300 সেকেন্ড) অনুযায়ী replica সংখ্যা ধীরে কমবে।

## Cleanup

```bash
kubectl delete -f k8s/hpa.yaml -f k8s/web.yaml -f k8s/scaling-demo.yaml -f k8s/sqlserver.yaml
kubectl delete secret mssql-secret
```

## মনে রাখো

HPA কাজ করার জন্য লাগে:

1. Metrics Server (বা উপযুক্ত metrics API)
2. Pod-এ CPU **resource requests**
3. পর্যাপ্ত cluster capacity

Request ও limit সঠিকভাবে না দিলে scaling অপ্রত্যাশিত হতে পারে। আরও: API-র readiness database-এর ওপর নির্ভর করে — database না থাকলে সব API pod "not ready" হয়ে যায় এবং site-এ error দেখায়।

## ✅ সারসংক্ষেপ

| ধাপ | কী করলে | কী শিখলে |
|-----|---------|----------|
| 1 | `docker run` | একটি container ও app-এর গঠন |
| 2 | `docker compose --scale` | একাধিক replica |
| 3 | Nginx | Request load balancing |
| 4 | Prometheus + Grafana | Monitoring |
| 5 | Docker Swarm | Multi-server orchestration |
| 6 | Kubernetes HPA | Metrics-ভিত্তিক automatic scaling |

⬅️ [Step 5](05-docker-swarm.md) | 🏠 [README](../README.md)
