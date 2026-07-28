# PulseBoard

PulseBoard es una web personal de salud y rendimiento con frontend Angular, API ASP.NET Core, PostgreSQL y Docker Compose.

## Alcance inicial

- Check-in diario.
- Habitos y suplementos.
- Mediciones corporales manuales de bascula Soehnle.
- Base preparada para alimentacion, actividad, Strava e importacion CSV/Excel.
- Espanol e ingles desde el inicio.
- Zona horaria fija del usuario: `Europe/Vienna`.
- Despliegue aislado en `/opt/pulseboard`.

## Estructura

```text
apps/
  api/        ASP.NET Core Web API
  web/        Angular
deploy/
  nginx/      plantillas Nginx para subdominios
  scripts/    preparacion del servidor
docs/         documentacion inicial
tests/        pruebas de API
```

## Desarrollo local

1. Crea el archivo local de variables:

```bash
cp .env.example .env
```

2. Cambia los valores sensibles de `.env`.

3. Arranca todo con Docker Desktop abierto:

```bash
npm run compose:up
```

4. Abre:

```text
Web: http://localhost:4200
API: http://localhost:8080
Health: http://localhost:8080/health
Ready: http://localhost:8080/health/ready
```

## Comandos utiles

```bash
npm run web:build
npm run web:test
npm run api:test:docker
```

El Mac actual tiene `dotnet` 3.1 instalado, por eso el test de API esta preparado para ejecutarse con Docker y SDK .NET 8.

## Produccion

Produccion usa:

```text
/opt/pulseboard
pulseboard.juanariasdev.com
api.pulseboard.juanariasdev.com
ghcr.io/juanarias115/pulseboard-web
ghcr.io/juanarias115/pulseboard-api
```

PulseBoard no comparte contenedores, base de datos, volumenes ni secretos con Allegro, BakeryFlow o Delicias.

Consulta [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) antes del primer despliegue.
