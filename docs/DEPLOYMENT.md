# Despliegue

## Verificacion realizada el 2026-07-28

```text
GitHub auth local: conectado como JuanArias115
Repositorio JuanArias115/PulseBoard: creado, publico
pulseboard.juanariasdev.com A: 217.216.92.208
api.pulseboard.juanariasdev.com A: 217.216.92.208
SSH juan@217.216.92.208: funciona con la llave local conocida
Docker en servidor: disponible
/opt/pulseboard: no existe todavia
Docker Desktop local: disponible despues de arrancarlo
SDK .NET local: 3.1.426, las pruebas de API se validaron con Docker y SDK .NET 8
GitHub Secrets: DEPLOY_HOST, DEPLOY_PORT, DEPLOY_USER, DEPLOY_SSH_KEY, DEPLOY_KNOWN_HOSTS, GHCR_USERNAME configurados
Produccion web: https://pulseboard.juanariasdev.com
Produccion API: https://api.pulseboard.juanariasdev.com/health
Certificado Let's Encrypt: emitido para pulseboard.juanariasdev.com y api.pulseboard.juanariasdev.com
```

## Antes del primer deploy

1. Crear el repositorio:

```bash
gh repo create JuanArias115/PulseBoard --private --source . --remote origin
```

2. Crear registros DNS tipo A:

```text
pulseboard.juanariasdev.com      217.216.92.208
api.pulseboard.juanariasdev.com  217.216.92.208
```

Estado: completado en GoDaddy.

3. Crear usuario, ruta y `.env` en servidor:

```bash
PUBLIC_KEY="$(cat ~/.ssh/pulseboard_deploy.pub)"
ssh -i ~/.ssh/bakeryflow_deploy juan@217.216.92.208 \
  "PUBLIC_KEY='$PUBLIC_KEY' bash -s" < deploy/scripts/prepare-server.sh
```

Este paso requiere `sudo` en el servidor. El usuario `juan` pertenece a `sudo`, pero no tiene `sudo` sin password.

Estado: completado.

5. Configurar Nginx con:

```text
deploy/nginx/delicias-proxy-pulseboard.conf
```

El servidor publica 80/443 desde el contenedor `deliciasbakery-web-1`, definido en `/opt/deliciasBakery`.
Para que ese proxy resuelva `pulseboard-web` y `pulseboard-api`, hay que conectar el proxy a la red Docker `pulseboard_ingress`.

Estado: completado mediante `deploy/scripts/configure-public-proxy.sh`.

6. Emitir certificados Let's Encrypt para:

```text
pulseboard.juanariasdev.com
api.pulseboard.juanariasdev.com
```

Estado: completado. El certificado vence el 2026-10-26.

## GitHub Secrets requeridos

En el repositorio `JuanArias115/PulseBoard`:

```text
DEPLOY_HOST          217.216.92.208
DEPLOY_USER          pulseboard-deploy
DEPLOY_PORT          22
DEPLOY_SSH_KEY       llave privada nueva para PulseBoard
DEPLOY_KNOWN_HOSTS   salida de ssh-keyscan del servidor
GHCR_USERNAME        JuanArias115
GHCR_READ_TOKEN      token con read:packages si GHCR queda privado
```

`GITHUB_TOKEN` lo provee GitHub Actions y se usa para publicar imagenes en GHCR.

Despues de crear DNS, servidor y secrets:

```bash
git add .
git commit -m "Initial PulseBoard scaffold"
git push -u origin main
```

## Flujo automatico

```text
Push a main
  -> CI web
  -> CI API
  -> build imagen API
  -> build imagen web
  -> push a GHCR
  -> SSH a servidor
  -> docker compose pull
  -> docker compose up -d
  -> health checks locales
```

El workflow solo reinicia `pulseboard-web`, `pulseboard-api` y `pulseboard-db`.
