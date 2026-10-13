#!/bin/bash
# =============================================================================
# Zabbix 7.0 LTS Server Installation — Ubuntu 24.04
# =============================================================================
# Run as root (or with sudo) on the zabbix-server EC2 instance.
#
# Usage:
#   sudo bash install-server.sh <DB_PASSWORD>
#
# Example:
#   sudo bash install-server.sh "ZabbixSecure@2025"
#
# What this script does:
#   1. Updates system packages
#   2. Installs & configures PostgreSQL 16
#   3. Creates zabbix DB user + database
#   4. Adds Zabbix 7.0 apt repository
#   5. Installs zabbix-server-pgsql, frontend, nginx, agent, sender
#   6. Imports initial DB schema (idempotent)
#   7. Configures /etc/zabbix/zabbix_server.conf
#   8. Configures /etc/zabbix/nginx.conf  (port 8080)
#   9. Enables and starts all services
#  10. Prints status and web UI URL
# =============================================================================

set -euo pipefail

# ── Require root ──────────────────────────────────────────────────────────────
if [[ $EUID -ne 0 ]]; then
  echo "ERROR: This script must be run as root or with sudo." >&2
  exit 1
fi

# ── Arguments ─────────────────────────────────────────────────────────────────
DB_PASSWORD="${1:?ERROR: DB password is required. Usage: sudo bash $0 <DB_PASSWORD>}"
ZABBIX_VERSION="7.0"
ZABBIX_REPO_DEB="https://repo.zabbix.com/zabbix/${ZABBIX_VERSION}/ubuntu/pool/main/z/zabbix-release/zabbix-release_latest_${ZABBIX_VERSION}+ubuntu24.04_all.deb"

echo "============================================================"
echo "  Zabbix ${ZABBIX_VERSION} Server Installation on Ubuntu 24.04"
echo "============================================================"

# ── 1. System Update ──────────────────────────────────────────────────────────
echo "--> [1/9] Updating system packages..."
export DEBIAN_FRONTEND=noninteractive
apt-get update -y -q
apt-get upgrade -y -q

# ── 2. Install PostgreSQL ──────────────────────────────────────────────────────
echo "--> [2/9] Installing PostgreSQL..."
apt-get install -y -q postgresql postgresql-contrib

systemctl enable postgresql
systemctl start  postgresql

# Wait for PostgreSQL to accept connections
for i in {1..10}; do
  sudo -u postgres psql -c "SELECT 1;" &>/dev/null && break
  echo "    Waiting for PostgreSQL... ($i/10)"
  sleep 3
done

# ── 3. Create Zabbix DB User & Database ───────────────────────────────────────
echo "--> [3/9] Creating PostgreSQL user and database..."

# Create user (skip if exists)
if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname='zabbix'" | grep -q 1; then
  sudo -u postgres psql -c "CREATE USER zabbix WITH PASSWORD '${DB_PASSWORD}';"
  echo "    User 'zabbix' created."
else
  # Update password in case it changed
  sudo -u postgres psql -c "ALTER USER zabbix WITH PASSWORD '${DB_PASSWORD}';"
  echo "    User 'zabbix' already exists — password updated."
fi

# Create database (skip if exists)
if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='zabbix'" | grep -q 1; then
  sudo -u postgres createdb -O zabbix zabbix
  echo "    Database 'zabbix' created."
else
  echo "    Database 'zabbix' already exists — skipping."
fi

# ── 4. Add Zabbix Repository ──────────────────────────────────────────────────
echo "--> [4/9] Adding Zabbix ${ZABBIX_VERSION} repository..."

if ! dpkg -l zabbix-release &>/dev/null; then
  wget -q "${ZABBIX_REPO_DEB}" -O /tmp/zabbix-release.deb
  dpkg -i /tmp/zabbix-release.deb
  apt-get update -y -q
  rm -f /tmp/zabbix-release.deb
  echo "    Repository added."
else
  echo "    Repository already installed — skipping."
fi

# ── 5. Install Zabbix Packages ────────────────────────────────────────────────
echo "--> [5/9] Installing Zabbix server, frontend, agent, and sender..."
apt-get install -y -q \
  zabbix-server-pgsql \
  zabbix-frontend-php \
  php8.3-pgsql \
  zabbix-nginx-conf \
  zabbix-sql-scripts \
  zabbix-agent \
  zabbix-sender

# ── 6. Import Initial Database Schema ─────────────────────────────────────────
echo "--> [6/9] Importing Zabbix database schema (skipped if already done)..."

SCHEMA_CHECK=$(sudo -u zabbix psql zabbix -tAc "SELECT count(*) FROM information_schema.tables WHERE table_name='hosts'" 2>/dev/null || echo "0")
if [[ "$SCHEMA_CHECK" == "0" ]]; then
  zcat /usr/share/zabbix-sql-scripts/postgresql/server.sql.gz | sudo -u zabbix psql zabbix
  echo "    Schema imported successfully."
else
  echo "    Schema already imported — skipping."
fi

# ── 7. Configure zabbix_server.conf ───────────────────────────────────────────
echo "--> [7/9] Configuring /etc/zabbix/zabbix_server.conf..."

SERVER_CONF="/etc/zabbix/zabbix_server.conf"

# Set DBPassword (handles both commented and uncommented forms)
if grep -qE "^[# ]*DBPassword=" "${SERVER_CONF}"; then
  sed -i -E "s|^[# ]*DBPassword=.*|DBPassword=${DB_PASSWORD}|" "${SERVER_CONF}"
else
  echo "DBPassword=${DB_PASSWORD}" >> "${SERVER_CONF}"
fi

echo "    DBPassword set."

# ── 8. Configure Nginx ────────────────────────────────────────────────────────
echo "--> [8/9] Configuring /etc/zabbix/nginx.conf (port 8080)..."

NGINX_CONF="/etc/zabbix/nginx.conf"
PRIVATE_IP=$(hostname -I | awk '{print $1}')

# Uncomment and set 'listen 8080'
sed -i -E 's|^[[:space:]]*#[[:space:]]*(listen[[:space:]]+8080;)|\t\1|' "${NGINX_CONF}"
# Uncomment and set 'server_name'
sed -i -E "s|^[[:space:]]*#[[:space:]]*(server_name[[:space:]]+).*;|\tserver_name ${PRIVATE_IP};|" "${NGINX_CONF}"

echo "    Nginx configured on port 8080 — server_name: ${PRIVATE_IP}"

# Disable the default nginx site to avoid port conflicts
if [ -f /etc/nginx/sites-enabled/default ]; then
  rm -f /etc/nginx/sites-enabled/default
  echo "    Default nginx site disabled."
fi

# ── 9. Enable and Start Services ──────────────────────────────────────────────
echo "--> [9/9] Enabling and starting services..."
systemctl enable  zabbix-server zabbix-agent nginx php8.3-fpm
systemctl restart zabbix-server zabbix-agent nginx php8.3-fpm

# ── Status Report ─────────────────────────────────────────────────────────────
echo ""
echo "============================================================"
echo "  Installation Complete!"
echo "============================================================"
for svc in zabbix-server zabbix-agent nginx php8.3-fpm; do
  if systemctl is-active --quiet "${svc}"; then
    echo "  [OK] ${svc} — running"
  else
    echo "  [!!] ${svc} — NOT running (check: journalctl -u ${svc} -n 30)"
  fi
done

PUBLIC_IP=$(curl -s --max-time 5 http://169.254.169.254/latest/meta-data/public-ipv4 2>/dev/null || echo "")
echo ""
echo "  Private IP : ${PRIVATE_IP}"
[ -n "${PUBLIC_IP}" ] && echo "  Public EIP : ${PUBLIC_IP}"
echo ""
echo "  Web UI     : http://${PRIVATE_IP}:8080"
[ -n "${PUBLIC_IP}" ] && echo "  Web UI     : http://${PUBLIC_IP}:8080   (from internet)"
echo "  Login      : Admin / zabbix  *** CHANGE THE PASSWORD IMMEDIATELY ***"
echo ""
echo "  Complete the 7-step setup wizard in the browser, then return to INSTALL.md."
echo "============================================================"
