# Puente Apple Health con Atajos

Objetivo:

```text
Soehnle / Apple Fitness -> Apple Health -> Atajo iPhone -> PulseBoard
```

## Requisitos

- La app Soehnle debe estar escribiendo datos en Apple Health.
- El iPhone debe tener permiso para leer esos datos desde Atajos.
- `/opt/pulseboard/.env` debe tener `PULSEBOARD_APPLE_HEALTH_BRIDGE_KEY`.

## Endpoint

### Composicion corporal

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

### Actividad diaria

```text
POST https://api.pulseboard.juanariasdev.com/api/v1/integrations/apple-health/daily-activity
```

Header:

```text
X-PulseBoard-Bridge-Key: <clave privada del servidor>
Content-Type: application/json
```

Body esperado:

```json
{
  "localDate": "2026-07-28",
  "steps": 8450,
  "activeEnergyKcal": 520,
  "exerciseMinutes": 42,
  "walkingRunningDistanceKm": 5.8,
  "cyclingDistanceKm": 0,
  "workoutCount": 1,
  "notes": "Apple Health via Shortcuts"
}
```

## Atajo: composicion corporal

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

## Atajo: actividad diaria

Crear un segundo atajo llamado `Enviar actividad a PulseBoard`.

Acciones recomendadas:

1. Obtener fecha actual.
2. Formatear fecha como `yyyy-MM-dd`.
3. Buscar muestras de salud de `Pasos` entre inicio de hoy y ahora.
4. Sumar las muestras de pasos.
5. Buscar muestras de salud de `Energia activa` entre inicio de hoy y ahora.
6. Sumar las calorias activas.
7. Buscar muestras de salud de `Minutos de ejercicio` entre inicio de hoy y ahora.
8. Sumar los minutos.
9. Buscar muestras de salud de `Distancia andando + corriendo` entre inicio de hoy y ahora.
10. Sumar la distancia y convertirla a kilometros si Atajos la entrega en metros.
11. Opcional: buscar `Distancia en bicicleta`.
12. Opcional: buscar entrenamientos de hoy y contar resultados.
13. Crear diccionario JSON con:

```json
{
  "localDate": "yyyy-MM-dd",
  "steps": 0,
  "activeEnergyKcal": 0,
  "exerciseMinutes": 0,
  "walkingRunningDistanceKm": 0,
  "cyclingDistanceKm": 0,
  "workoutCount": 0,
  "notes": "Apple Health via Shortcuts"
}
```

14. Enviar con `Obtener contenido de URL`.
15. Metodo `POST`.
16. Body `JSON`.
17. Header `X-PulseBoard-Bridge-Key`.
18. URL:

```text
https://api.pulseboard.juanariasdev.com/api/v1/integrations/apple-health/daily-activity
```

El endpoint reemplaza el registro del mismo dia y fuente `AppleHealth`, asi que se puede ejecutar varias veces al dia sin duplicar datos.

## Limitaciones

- Apple Health no ofrece una API web directa para leer datos desde el servidor.
- Atajos puede requerir permisos y, en algunos casos, que el iPhone este desbloqueado.
- Si Soehnle no escribe una metrica concreta en Apple Health, el Atajo no podra leerla.
- No hay que usar este numero de calorias como objetivo automatico de alimentacion hasta confirmar que significa exactamente en tu modelo de bascula.
- Algunos nombres de muestras pueden variar segun idioma de iOS y permisos de Salud.

## Alternativa posterior

Si Atajos resulta inestable, la alternativa limpia es una app iOS pequena que use HealthKit, lea los datos con permiso y los envie a este mismo endpoint.
