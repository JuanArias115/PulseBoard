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
  "restingEnergyKcal": 1360,
  "exerciseMinutes": 42,
  "standHours": 8,
  "standMinutes": 60,
  "walkingRunningDistanceKm": 5.8,
  "cyclingDistanceKm": 0,
  "flightsClimbed": 4,
  "physicalEffortMet": 4.3,
  "workoutCount": 1,
  "notes": "Apple Health via Shortcuts"
}
```

### Nutricion diaria

```text
POST https://api.pulseboard.juanariasdev.com/api/v1/integrations/apple-health/daily-nutrition
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
  "caloriesKcal": 2100,
  "proteinGrams": 145,
  "carbohydrateGrams": 220,
  "fatGrams": 70,
  "fiberGrams": 28,
  "sugarGrams": 45,
  "waterLiters": 2.4,
  "notes": "Cal AI via Apple Health"
}
```

Si Atajos devuelve texto con unidad, tambien sirve. Por ejemplo: `"2100 kcal"`, `"145 g"` o `"2,4 L"`.

### Recuperacion y sueno

```text
POST https://api.pulseboard.juanariasdev.com/api/v1/integrations/apple-health/daily-recovery
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
  "heartRate": 59,
  "restingHeartRate": 60,
  "heartRateVariability": 26,
  "bloodOxygenPercentage": 95,
  "respiratoryRateBreathsPerMinute": 18,
  "sleepHours": 6.8,
  "sleepScore": 80,
  "vo2Max": 39.3,
  "walkingHeartRateAverageBpm": 83,
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
7. Opcional: buscar `Energia en reposo`.
8. Buscar muestras de salud de `Minutos de ejercicio` entre inicio de hoy y ahora.
9. Sumar los minutos.
10. Opcional: buscar `Horas de pie` y `Minutos de pie`.
11. Buscar muestras de salud de `Distancia andando + corriendo` entre inicio de hoy y ahora.
12. Sumar la distancia y convertirla a kilometros si Atajos la entrega en metros.
13. Opcional: buscar `Distancia en bicicleta`.
14. Opcional: buscar `Pisos subidos`.
15. Opcional: buscar `Esfuerzo fisico` o METs, si aparece en Atajos.
16. Opcional: buscar entrenamientos de hoy y contar resultados.
17. Crear diccionario JSON con:

```json
{
  "localDate": "yyyy-MM-dd",
  "steps": 0,
  "activeEnergyKcal": 0,
  "restingEnergyKcal": 0,
  "exerciseMinutes": 0,
  "standHours": 0,
  "standMinutes": 0,
  "walkingRunningDistanceKm": 0,
  "cyclingDistanceKm": 0,
  "flightsClimbed": 0,
  "physicalEffortMet": 0,
  "workoutCount": 0,
  "notes": "Apple Health via Shortcuts"
}
```

18. Enviar con `Obtener contenido de URL`.
19. Metodo `POST`.
20. Body `JSON`.
21. Header `X-PulseBoard-Bridge-Key`.
22. URL:

```text
https://api.pulseboard.juanariasdev.com/api/v1/integrations/apple-health/daily-activity
```

El endpoint reemplaza el registro del mismo dia y fuente `AppleHealth`, asi que se puede ejecutar varias veces al dia sin duplicar datos.

## Atajo: nutricion desde Cal AI / Apple Health

Primero verifica si Cal AI realmente escribe en Apple Health:

1. Abrir `Salud`.
2. Ir a `Explorar` > `Nutricion`.
3. Abrir `Energia alimentaria` / `Dietary Energy`.
4. Tocar `Mostrar todos los datos`.
5. Si ves registros creados por Cal AI, entonces el Atajo puede enviarlos a PulseBoard.
6. Repetir con proteina, carbohidratos, grasa, fibra, azucar y agua si aparecen.

Si `Dietary Energy` esta vacio, Cal AI puede estar conectado solo para leer actividad o ajustar objetivos, no para exportar comida.

Crear un atajo llamado `Enviar nutricion a PulseBoard`:

1. Obtener fecha actual.
2. Formatear fecha como `yyyy-MM-dd`.
3. Buscar muestras de salud de `Energia alimentaria` de hoy y sumar.
4. Buscar muestras de salud de `Proteina` de hoy y sumar.
5. Buscar muestras de salud de `Carbohidratos` de hoy y sumar.
6. Buscar muestras de salud de `Grasa total` de hoy y sumar.
7. Opcional: buscar `Fibra`, `Azucar` y `Agua`.
8. Crear diccionario JSON con `localDate`, `caloriesKcal`, `proteinGrams`, `carbohydrateGrams`, `fatGrams`, `fiberGrams`, `sugarGrams`, `waterLiters` y `notes`.
9. Enviar por POST a:

```text
https://api.pulseboard.juanariasdev.com/api/v1/integrations/apple-health/daily-nutrition
```

## Atajo: recuperacion

Crear un atajo llamado `Enviar recuperacion a PulseBoard`:

1. Obtener fecha actual.
2. Formatear fecha como `yyyy-MM-dd`.
3. Buscar la ultima muestra de `Frecuencia cardiaca`.
4. Buscar la ultima muestra de `Frecuencia cardiaca en reposo`.
5. Buscar la ultima muestra de `Variabilidad de frecuencia cardiaca`.
6. Buscar la ultima muestra de `Oxigeno en sangre`.
7. Buscar la ultima muestra de `Frecuencia respiratoria`.
8. Buscar muestras de `Sueno` de hoy o de la ultima noche y sumar horas dormidas.
9. Opcional: buscar `Cardio fitness` / VO2 max y `Frecuencia cardiaca media caminando`.
10. Crear diccionario JSON con los campos del body de recuperacion. En Atajos puedes usar `heartRate` y `heartRateVariability` directamente.
11. Enviar por POST a:

```text
https://api.pulseboard.juanariasdev.com/api/v1/integrations/apple-health/daily-recovery
```

## Limitaciones

- Apple Health no ofrece una API web directa para leer datos desde el servidor.
- Atajos puede requerir permisos y, en algunos casos, que el iPhone este desbloqueado.
- Si Soehnle no escribe una metrica concreta en Apple Health, el Atajo no podra leerla.
- No hay que usar este numero de calorias como objetivo automatico de alimentacion hasta confirmar que significa exactamente en tu modelo de bascula.
- Algunos nombres de muestras pueden variar segun idioma de iOS y permisos de Salud.

## Alternativa posterior

Si Atajos resulta inestable, la alternativa limpia es una app iOS pequena que use HealthKit, lea los datos con permiso y los envie a este mismo endpoint.
