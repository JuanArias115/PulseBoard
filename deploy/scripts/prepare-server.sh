#!/usr/bin/env bash
set -euo pipefail

APP_USER="${APP_USER:-pulseboard-deploy}"
APP_DIR="${APP_DIR:-/opt/pulseboard}"
PUBLIC_KEY="${PUBLIC_KEY:-}"

sudo useradd --system --create-home --shell /bin/bash "$APP_USER" 2>/dev/null || true
sudo usermod -aG docker "$APP_USER"
sudo mkdir -p "$APP_DIR"
sudo chown -R "$APP_USER:$APP_USER" "$APP_DIR"
sudo chmod 750 "$APP_DIR"

if [ -n "$PUBLIC_KEY" ]; then
  sudo install -d -m 700 -o "$APP_USER" -g "$APP_USER" "/home/$APP_USER/.ssh"
  printf '%s\n' "$PUBLIC_KEY" | sudo tee "/home/$APP_USER/.ssh/authorized_keys" >/dev/null
  sudo chown "$APP_USER:$APP_USER" "/home/$APP_USER/.ssh/authorized_keys"
  sudo chmod 600 "/home/$APP_USER/.ssh/authorized_keys"
fi

if [ ! -f "$APP_DIR/.env" ]; then
  db_password="$(openssl rand -base64 36 | tr -d '\n')"
  jwt_key="$(openssl rand -base64 48 | tr -d '\n')"

  sudo tee "$APP_DIR/.env" >/dev/null <<ENV
COMPOSE_PROJECT_NAME=pulseboard
PULSEBOARD_ENVIRONMENT=Production
PULSEBOARD_TIMEZONE=Europe/Vienna
PULSEBOARD_CORS_ORIGINS=https://pulseboard.juanariasdev.com,https://api.pulseboard.juanariasdev.com
PULSEBOARD_AUTO_MIGRATE=true
PULSEBOARD_POSTGRES_HOST=pulseboard-db
PULSEBOARD_POSTGRES_PORT=5432
PULSEBOARD_POSTGRES_DB=pulseboard
PULSEBOARD_POSTGRES_USER=pulseboard
PULSEBOARD_POSTGRES_PASSWORD=$db_password
ConnectionStrings__Default=Host=pulseboard-db;Port=5432;Database=pulseboard;Username=pulseboard;Password=$db_password
JWT__ISSUER=PulseBoard
JWT__AUDIENCE=PulseBoard
JWT__SIGNING_KEY=$jwt_key
IMAGE_TAG=latest
GHCR_OWNER=juanarias115
ENV

  sudo chown "$APP_USER:$APP_USER" "$APP_DIR/.env"
  sudo chmod 600 "$APP_DIR/.env"
fi

echo "Created $APP_DIR for $APP_USER."
echo "Authorized key installed when PUBLIC_KEY was provided."
