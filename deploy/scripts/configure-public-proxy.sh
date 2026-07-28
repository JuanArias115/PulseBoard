#!/usr/bin/env bash
set -euo pipefail

PROXY_DIR="${PROXY_DIR:-/opt/deliciasBakery}"
PROXY_CONTAINER="${PROXY_CONTAINER:-deliciasbakery-web-1}"
CERTBOT_IMAGE="${CERTBOT_IMAGE:-certbot/certbot:v5.6.0}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"

sudo cp "$PROXY_DIR/docker-compose.yml" "$PROXY_DIR/docker-compose.yml.bak-pulseboard-$STAMP"
sudo cp "$PROXY_DIR/nginx.conf" "$PROXY_DIR/nginx.conf.bak-pulseboard-$STAMP"

sudo python3 - <<'PY'
from pathlib import Path

path = Path("/opt/deliciasBakery/docker-compose.yml")
content = path.read_text()

if "pulseboard_ingress" not in content:
    content = content.replace(
        "      - allegro_ingress\n",
        "      - allegro_ingress\n      - pulseboard_ingress\n",
    )
    content += """
  pulseboard_ingress:
    external: true
    name: pulseboard_ingress
"""

path.write_text(content)
PY

sudo python3 - <<'PY'
from pathlib import Path
import re

path = Path("/opt/deliciasBakery/nginx.conf")
content = path.read_text()
content = re.sub(r"\n?# BEGIN PULSEBOARD ACME.*?# END PULSEBOARD ACME\n?", "\n", content, flags=re.S)
content = re.sub(r"\n?# BEGIN PULSEBOARD HTTPS.*?# END PULSEBOARD HTTPS\n?", "\n", content, flags=re.S)
content = content.rstrip() + r"""

# BEGIN PULSEBOARD ACME
server {
  listen 80;
  server_name pulseboard.juanariasdev.com;

  location /.well-known/acme-challenge/ {
    root /var/www/certbot;
  }

  location / {
    return 308 https://$host$request_uri;
  }
}

server {
  listen 80;
  server_name api.pulseboard.juanariasdev.com;

  location /.well-known/acme-challenge/ {
    root /var/www/certbot;
  }

  location / {
    return 308 https://$host$request_uri;
  }
}
# END PULSEBOARD ACME
""" + "\n"
path.write_text(content)
PY

sudo docker network connect pulseboard_ingress "$PROXY_CONTAINER" 2>/dev/null || true
cd "$PROXY_DIR"
sudo docker compose up -d
sudo docker exec "$PROXY_CONTAINER" nginx -t
sudo docker exec "$PROXY_CONTAINER" nginx -s reload

sudo docker run --rm \
  -v "$PROXY_DIR/certbot/conf:/etc/letsencrypt" \
  -v "$PROXY_DIR/certbot/www:/var/www/certbot" \
  "$CERTBOT_IMAGE" certonly \
  --webroot \
  --webroot-path /var/www/certbot \
  --agree-tos \
  --register-unsafely-without-email \
  --non-interactive \
  --key-type ecdsa \
  -d pulseboard.juanariasdev.com \
  -d api.pulseboard.juanariasdev.com

sudo python3 - <<'PY'
from pathlib import Path
import re

path = Path("/opt/deliciasBakery/nginx.conf")
content = path.read_text()
content = re.sub(r"\n?# BEGIN PULSEBOARD ACME.*?# END PULSEBOARD ACME\n?", "\n", content, flags=re.S)
content = re.sub(r"\n?# BEGIN PULSEBOARD HTTPS.*?# END PULSEBOARD HTTPS\n?", "\n", content, flags=re.S)
content = content.rstrip() + r"""

# BEGIN PULSEBOARD HTTPS
server {
  listen 80;
  server_name pulseboard.juanariasdev.com;

  location /.well-known/acme-challenge/ {
    root /var/www/certbot;
  }

  location / {
    return 308 https://$host$request_uri;
  }
}

server {
  listen 443 ssl;
  server_name pulseboard.juanariasdev.com;

  ssl_certificate /etc/letsencrypt/live/pulseboard.juanariasdev.com/fullchain.pem;
  ssl_certificate_key /etc/letsencrypt/live/pulseboard.juanariasdev.com/privkey.pem;
  ssl_protocols TLSv1.2 TLSv1.3;

  resolver 127.0.0.11 ipv6=off valid=10s;

  location / {
    set $pulseboard_web http://pulseboard-web:80;
    proxy_pass $pulseboard_web;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_connect_timeout 5s;
    proxy_read_timeout 60s;
  }
}

server {
  listen 80;
  server_name api.pulseboard.juanariasdev.com;

  location /.well-known/acme-challenge/ {
    root /var/www/certbot;
  }

  location / {
    return 308 https://$host$request_uri;
  }
}

server {
  listen 443 ssl;
  server_name api.pulseboard.juanariasdev.com;

  ssl_certificate /etc/letsencrypt/live/pulseboard.juanariasdev.com/fullchain.pem;
  ssl_certificate_key /etc/letsencrypt/live/pulseboard.juanariasdev.com/privkey.pem;
  ssl_protocols TLSv1.2 TLSv1.3;

  resolver 127.0.0.11 ipv6=off valid=10s;

  location / {
    set $pulseboard_api http://pulseboard-api:8080;
    proxy_pass $pulseboard_api;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_connect_timeout 5s;
    proxy_read_timeout 60s;
  }
}
# END PULSEBOARD HTTPS
""" + "\n"
path.write_text(content)
PY

sudo docker compose up -d
sudo docker exec "$PROXY_CONTAINER" nginx -t
sudo docker exec "$PROXY_CONTAINER" nginx -s reload
