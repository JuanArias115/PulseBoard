# Puente Apple Health con Atajos

Objetivo:

```text
Soehnle -> Apple Health -> Atajo iPhone -> PulseBoard
```

## Requisitos

- La app Soehnle debe estar escribiendo datos en Apple Health.
- El iPhone debe tener permiso para leer esos datos desde Atajos.
- `/opt/pulseboard/.env` debe tener `PULSEBOARD_APPLE_HEALTH_BRIDGE_KEY`.

## Endpoint

```text
POST https://api.pulseboard.juanariasdev.com/api/v1/integrations/apple-health/body-measurements
```

Header:

```text
X-PulseBoard-Bridge-Key: <clave privada del servidor>
Content-Type: application/json
```

Body esperado:

```json
{
  "measuredAt": "2026-07-28T07:00:00+02:00",
  "weightKg": 74.2,
  "bodyFatPercentage": 19.8,
  "musclePercentage": 42.1,
  "bodyWaterPercentage": 55.1,
  "bodyMassIndex": 23.4,
  "estimatedCaloriesKcal": 3087,
  "notes": "Apple Health via Shortcuts"
}
```

## Atajo inicial

En la app Atajos del iPhone:

1. Crear un atajo nuevo llamado `Enviar Soehnle a PulseBoard`.
2. Añadir acciones `Buscar muestras de salud` para los datos disponibles:
   - peso corporal
   - porcentaje de grasa corporal
   - masa muscular o masa magra, si Apple Health la expone
   - agua corporal, si Apple Health la expone
3. Filtrar por `hoy` o por las ultimas 24 horas.
4. Ordenar por fecha de inicio descendente.
5. Tomar la primera muestra de cada tipo.
6. Crear un diccionario con los nombres del JSON esperado.
7. Añadir `Obtener contenido de URL`.
8. Configurar metodo `POST`.
9. Configurar el body como `JSON`.
10. Añadir el header `X-PulseBoard-Bridge-Key`.

## Limitaciones

- Apple Health no ofrece una API web directa para leer datos desde el servidor.
- Atajos puede requerir permisos y, en algunos casos, que el iPhone este desbloqueado.
- Si Soehnle no escribe una metrica concreta en Apple Health, el Atajo no podra leerla.
- No hay que usar este numero de calorias como objetivo automatico de alimentacion hasta confirmar que significa exactamente en tu modelo de bascula.

## Alternativa posterior

Si Atajos resulta inestable, la alternativa limpia es una app iOS pequena que use HealthKit, lea los datos con permiso y los envie a este mismo endpoint.
