# Zabbix 7.0 LTS — সার্ভার

**ইনস্ট্যান্স:** `zabbix-server` · t3.medium · Ubuntu 24.04 · Public subnet · EIP যুক্ত  
**যে পোর্টে শোনে:** TCP 8080 (Web UI) · TCP 10051 (প্রক্সি চেক-ইন, এজেন্ট ডেটা)  
**ডাটাবেস:** PostgreSQL 16 (লোকাল)

---

## অটোমেটেড ইনস্টল

```bash
# 1. সার্ভারে SSM দিয়ে ঢুকুন
aws ssm start-session --target <zabbix-server-instance-id> \
    --profile joni --region ap-south-1

# 2. রিপো ক্লোন করে রান করুন
sudo -s
git clone https://github.com/Abdullah1906/BMI-Health-System-3-Tier-Deploy.git
cd BMI-Health-System-3-Tier-Deploy
bash monitoring/install-server.sh 'YourPassword'

```

স্ক্রিপ্টটি সব ধাপ idempotent ভাবে সম্পন্ন করে — তাই বারবার রান করা নিরাপদ।

---

## ম্যানুয়াল ইনস্টল

### ধাপ ১ — সিস্টেম আপডেট

```bash
sudo -s
export DEBIAN_FRONTEND=noninteractive
apt-get update -y && apt-get upgrade -y
```

### ধাপ ২ — PostgreSQL ইনস্টল

```bash
apt-get install -y postgresql postgresql-contrib
systemctl enable postgresql && systemctl start postgresql
```

### ধাপ ৩ — ডাটাবেস ইউজার ও ডাটাবেস তৈরি

```bash
sudo -u postgres psql <<'PSQL'
CREATE USER zabbix WITH PASSWORD 'YourPassword';
CREATE DATABASE zabbix OWNER zabbix;
PSQL
```

### ধাপ ৪ — Zabbix 7.0 রিপোজিটরি যোগ করা

> ভার্সন দেখে নিন এই URL থেকে: https://www.zabbix.com/download?zabbix=7.4&os_distribution=ubuntu&os_version=24.04&components=server_frontend_agent&db=pgsql&ws=nginx

```bash
wget -q  https://repo.zabbix.com/zabbix/7.4/release/ubuntu/pool/main/z/zabbix-release/zabbix-release_latest_7.4+ubuntu24.04_all.deb \
     -O /tmp/zabbix-release.deb
dpkg -i /tmp/zabbix-release.deb
apt-get update -y
```

### ধাপ ৫ — প্যাকেজ ইনস্টল

> Zabbix ভার্সন অনুযায়ী সঠিক php8.x ভার্সন ব্যবহার করুন।

```bash
apt-get install -y \
  zabbix-server-pgsql \
  zabbix-frontend-php \
  php8.3-pgsql \
  zabbix-nginx-conf \
  zabbix-sql-scripts \
  zabbix-agent \
  zabbix-sender
```

### ধাপ ৬ — ডাটাবেস স্কিমা ইমপোর্ট

```bash
zcat /usr/share/zabbix/sql-scripts/postgresql/server.sql.gz | sudo -u zabbix psql zabbix
```

### ধাপ ৭ — `/etc/zabbix/zabbix_server.conf` লেখা

আগে ব্যাকআপ রেখে দিন:

```bash
cp /etc/zabbix/zabbix_server.conf /etc/zabbix/zabbix_server.conf.bak
```
### N.B: Cachsize need to change for agent-linux-direct

পুরো ফাইলটি নিচের কনটেন্ট দিয়ে প্রতিস্থাপন করুন।  
`YourPassword` এর জায়গায় আপনার আসল DB পাসওয়ার্ড বসান।

```bash
cat > /etc/zabbix/zabbix_server.conf <<'EOF'
# Zabbix Server 7.0 — /etc/zabbix/zabbix_server.conf

# ── Database ──────────────────────────────────────────────────────────────────
DBHost=localhost
DBName=zabbix
DBUser=zabbix
DBPassword=YourPassword

# ── Network ───────────────────────────────────────────────────────────────────
ListenPort=10051
ListenIP=0.0.0.0

# ── Logging ───────────────────────────────────────────────────────────────────
LogFile=/var/log/zabbix/zabbix_server.log
LogFileSize=0
PidFile=/run/zabbix/zabbix_server.pid
SocketDir=/run/zabbix

# ── Performance ───────────────────────────────────────────────────────────────
StartPollers=5
StartTrappers=5
StartPingers=1
StartDiscoverers=1
CacheSize=128M
HistoryCacheSize=16M
TrendCacheSize=4M

# ── Timeouts & Paths ──────────────────────────────────────────────────────────
Timeout=4
FpingLocation=/usr/bin/fping
Fping6Location=/usr/bin/fping6
LogSlowQueries=3000
SNMPTrapperFile=/var/log/snmptrap/snmptrap.log
StatsAllowedIP=127.0.0.1

# ── Include ───────────────────────────────────────────────────────────────────
#Include=/etc/zabbix/zabbix_server.conf.d/*.conf
EOF
```

### ধাপ ৮ — `/etc/zabbix/nginx.conf` লেখা

```bash
PRIVATE_IP=$(hostname -I | awk '{print $1}')

cat > /etc/zabbix/nginx.conf <<EOF
server {
        listen          8080;
        server_name     ${PRIVATE_IP};

        root    /usr/share/zabbix/ui;

        index   index.php;

        location = /favicon.ico {
                log_not_found   off;
        }

        location / {
                try_files       \$uri \$uri/ =404;
        }

        location /assets {
                access_log      off;
                expires         10d;
        }

        location ~ /\.ht {
                deny            all;
        }

        location ~ /(api\/|conf[^\.]|include|locale) {
                deny            all;
                return          404;
        }

        location /vendor {
                deny            all;
                return          404;
        }

        location ~ [^/]\.php(/|$) {
                fastcgi_pass    unix:/var/run/php/zabbix.sock;
                fastcgi_split_path_info ^(.+\.php)(/.+)$;
                fastcgi_index   index.php;

                fastcgi_param   DOCUMENT_ROOT   /usr/share/zabbix;
                fastcgi_param   SCRIPT_FILENAME /usr/share/zabbix\$fastcgi_script_name;
                fastcgi_param   PATH_TRANSLATED /usr/share/zabbix\$fastcgi_script_name;

                include fastcgi_params;
                fastcgi_param   QUERY_STRING    \$query_string;
                fastcgi_param   REQUEST_METHOD  \$request_method;
                fastcgi_param   CONTENT_TYPE    \$content_type;
                fastcgi_param   CONTENT_LENGTH  \$content_length;

                fastcgi_intercept_errors        on;
                fastcgi_ignore_client_abort     off;
                fastcgi_connect_timeout         60;
                fastcgi_send_timeout            180;
                fastcgi_read_timeout            180;
                fastcgi_buffer_size             128k;
                fastcgi_buffers                 4 256k;
                fastcgi_busy_buffers_size       256k;
                fastcgi_temp_file_write_size    256k;
        }
}
EOF

rm -f /etc/nginx/sites-enabled/default
```

### ধাপ ৯ — `/etc/zabbix/zabbix_agentd.conf` লেখা

সার্ভার নিজেকে নিজের লোকাল এজেন্টের মাধ্যমে মনিটর করে (সার্ভার localhost-এর এজেন্টকে poll করে)।

আগে ব্যাকআপ রেখে দিন:

```bash
cp /etc/zabbix/zabbix_agentd.conf /etc/zabbix/zabbix_agentd.conf.bak
```

```bash
cat > /etc/zabbix/zabbix_agentd.conf <<'EOF'
# Zabbix Agent — zabbix-server (self-monitoring)
# Server polls its own agent on 127.0.0.1:10050

PidFile=/run/zabbix/zabbix_agentd.pid
LogFile=/var/log/zabbix/zabbix_agentd.log
LogFileSize=0

# Only localhost is allowed to poll (passive mode — server polls itself)
Server=127.0.0.1
# if have poxy server then add proxy server private id 
# Server=127.0.0.1,10.0.131.29
# ServerActive disabled — passive mode only

Hostname=zabbix-server

Include=/etc/zabbix/zabbix_agentd.d/*.conf
EOF

mkdir -p /etc/zabbix/zabbix_agentd.d
```

### ধাপ ১০ — সার্ভিস Enable ও Start করা

```bash
systemctl enable  zabbix-server zabbix-agent nginx php8.3-fpm
systemctl restart zabbix-server zabbix-agent nginx php8.3-fpm
```

### ধাপ ১১ — যাচাই (Verify)

```bash
for svc in zabbix-server zabbix-agent nginx php8.3-fpm; do
  systemctl is-active --quiet "$svc" && echo "[OK] $svc" || echo "[!!] $svc FAILED"
done

# সার্ভার লগে কোনো error আছে কিনা দেখুন
tail -20 /var/log/zabbix/zabbix_server.log
```

---

## অতিরিক্ত চেক (ঐচ্ছিক — প্রয়োজন হলে করুন)

```bash
sudo systemctl status nginx --no-pager

nginx -t
sudo systemctl enable nginx
sudo systemctl start nginx
sudo systemctl status nginx --no-pager
```
# Security group custom tcp 8080 0.0.0.0/0 need to open at instance (important)

## ওয়েব সেটআপ উইজার্ড

ব্রাউজারে `http://<EIP>:8080` খুলে ৭ ধাপের উইজার্ডটি সম্পন্ন করুন:

| ধাপ | মান |
|------|-------|
| Database type | PostgreSQL |
| Database host | `localhost` |
| Database port | `0` (ডিফল্ট 5432) |
| Database name | `zabbix` |
| User | `zabbix` |
| Password | `YourPassword` |
| Zabbix server host | `localhost` |
| Zabbix server port | `10051` |
| Timezone | `Asia/Kolkata` |

**লগইন:** `Admin` / `zabbix` — **সাথে সাথে পাসওয়ার্ড পরিবর্তন করুন।**

---

## সার্ভারকে মনিটরড হোস্ট হিসেবে যোগ করা

উইজার্ড শেষ করার পর সার্ভারকেই একটি হোস্ট হিসেবে যোগ করুন, যাতে Zabbix নিজের CPU, মেমোরি ও ডিস্ক মনিটর করতে পারে।

**Configuration → Hosts → Create host**

| ট্যাব | ফিল্ড | মান |
|-----|-------|-------|
| Host | Host name | `zabbix-server` |
| Host | Monitored by proxy | *(খালি রাখুন — localhost দিয়ে নিজেই মনিটর হয়)* |
| Host | Groups | `Linux servers` |
| Interfaces | Type | Agent |
| Interfaces | IP address | `127.0.0.1` |
| Interfaces | Port | `10050` |
| Interfaces | Connect to | IP |
| Templates | | `Linux by Zabbix agent` |

**Add** ক্লিক করুন। এজেন্ট লোকাল হওয়ায় হোস্ট সাথে সাথেই সবুজ (green) হয়ে যাবে।

---

## ট্রাবলশুটিং

```bash
# সার্ভার চালু না হলে
journalctl -u zabbix-server -n 50

# ডাটাবেস কানেকশন refused হলে
sudo -u postgres psql -c "\l"           # ডাটাবেসের তালিকা
sudo -u postgres psql -c "\du"          # ইউজারের তালিকা

# Nginx পোর্ট কনফ্লিক্ট
nginx -t                                 # কনফিগ টেস্ট
ss -tlnp | grep 8080                    # 8080 পোর্ট কে ব্যবহার করছে দেখুন

# PHP-FPM সকেট না পাওয়া গেলে
ls -la /var/run/php/zabbix.sock
```
## problem hole krbe 
```bash 
আপনার Zabbix এখন কাজ করছে কারণ আমরা Nginx-এর ভুল document root ঠিক করেছি। শুরুতে মূল সমস্যা ছিল Nginx কোন directory থেকে Zabbix-এর frontend ফাইল খুঁজবে, সেটি ভুলভাবে configure করা ছিল।
চলুন সহজভাবে পুরো বিষয়টা বুঝি।
১. সমস্যাটা কোথায় ছিল?
আপনার /etc/nginx/conf.d/zabbix.conf ফাইলে প্রথমে ছিল:
root /usr/share/zabbix/ui/ui;


কিন্তু আপনার সার্ভারের প্রকৃত directory structure ছিল:
/usr/share/zabbix/

ui/

index.php

setup.php

assets/




অর্থাৎ, ui directory-টি দুইবার লেখা হয়েছিল। Nginx খুঁজছিল:
/usr/share/zabbix/ui/ui/index.php


কিন্তু ফাইলটি ছিল:
/usr/share/zabbix/ui/index.php


তাই Nginx সঠিক frontend ফাইল খুঁজে পাচ্ছিল না।
২. কী পরিবর্তন করায় কাজ করেছে?
আপনি configuration-এ পরিবর্তন করে লিখেছেন:
root /usr/share/zabbix/ui;


এখন Nginx সঠিক directory থেকে ফাইল খুঁজছে।
এরপর এই command চালিয়েছেন:
sudo nginx -t && sudo systemctl reload nginx


এখানে:
- nginx -t — configuration-এর syntax ঠিক আছে কি না পরীক্ষা করেছে।
- systemctl reload nginx — নতুন configuration চালু করেছে, সার্ভার বন্ধ না করেই।
```

## Browser search 
```bash 
http://100.48.96.115:8080/setup.php
```

---

## এই ডিরেক্টরির ফাইলসমূহ

| ফাইল | উদ্দেশ্য |
|------|---------|
| `install-server.sh` | অটোমেটেড ইনস্টল স্ক্রিপ্ট (idempotent) |
| `zabbix_server.conf.template` | প্লেসহোল্ডারসহ কনফিগ টেমপ্লেট |
| `nginx.conf.template` | Nginx কনফিগ টেমপ্লেট |
