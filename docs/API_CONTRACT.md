# API inicial

Base local:

```text
http://localhost:8080/api/v1
```

Produccion:

```text
https://api.pulseboard.juanariasdev.com/api/v1
```

## GET /meta

Devuelve nombre, estado, idiomas, zona horaria y modulos disponibles.

## GET /dashboard

```text
GET /api/v1/dashboard
```

Devuelve el resumen principal de PulseBoard:

- estado del dia;
- score de preparacion;
- habitos completados hoy;
- constancia de 7 dias;
- ultima medicion corporal;
- tendencias de peso, grasa, musculo y agua;
- promedios de 7, 14 y 30 dias;
- observaciones automaticas en espanol e ingles.

Las tendencias posibles son:

```text
insufficient
stable
down
up
```

La composicion corporal se interpreta como tendencia, no como veredicto exacto de grasa o musculo perdido.

## GET /analysis

```text
GET /api/v1/analysis
```

Devuelve el motor de analisis transparente de PulseBoard:

- actividad: inicialmente `insufficient`, hasta tener Strava o actividades manuales;
- recuperacion: score basado en check-ins, sueno, energia, fatiga y estres;
- alimentacion: score basado en dias registrados, proteina registrada y comidas con verduras;
- constancia: score basado en check-ins y cumplimiento de habitos;
- composicion corporal: tendencia separada, sin score diario;
- integridad de datos: areas presentes y areas faltantes;
- observaciones en espanol e ingles, cada una con la regla que la genero.

Ejemplo parcial:

```json
{
  "components": [
    {
      "key": "recovery",
      "score": 72,
      "status": "steady",
      "summaryEs": "Promedio reciente: 7.5 h de sueno, energia 4/5.",
      "evidence": ["checkIns:4", "sleepAverageHours:7.5"]
    }
  ],
  "bodyData": {
    "trend": "stable",
    "summaryEs": "Tendencia de peso a 30 dias: estable.",
    "dataPoints": 12
  },
  "completeness": {
    "score": 80,
    "presentDomains": ["check-in", "habits", "nutrition", "body"],
    "missingDomains": ["activity"]
  },
  "observations": [
    {
      "category": "data",
      "severity": "info",
      "messageEs": "Faltan datos en: activity.",
      "rule": "dataCompleteness<100"
    }
  ]
}
```

## POST /check-ins

```json
{
  "localDate": "2026-07-28",
  "sleepHours": 7.5,
  "sleepQuality": 4,
  "energy": 4,
  "mood": 4,
  "fatigue": 2,
  "muscleSoreness": 2,
  "hunger": 3,
  "stress": 2,
  "recovery": 4,
  "note": "Good morning check-in"
}
```

## POST /habits

```json
{
  "name": "Creatina",
  "category": "supplement",
  "frequency": "daily",
  "targetAmount": 5,
  "unit": "g",
  "notes": null
}
```

## Meals

`POST /api/v1/meals`

```json
{
  "localDate": "2026-07-28",
  "eatenAt": "2026-07-28T12:30:00+02:00",
  "name": "Pollo con arroz",
  "mealType": "lunch",
  "caloriesKcal": 720,
  "proteinGrams": 48,
  "carbohydrateGrams": 82,
  "fatGrams": 18,
  "hasVegetables": true,
  "isFavorite": true,
  "notes": ""
}
```

`GET /api/v1/meals?localDate=2026-07-28`

Devuelve las comidas del dia.

`GET /api/v1/meal-favorites`

Devuelve hasta 20 comidas marcadas como favoritas.

`GET /api/v1/nutrition-summary?localDate=2026-07-28`

Devuelve totales del dia, promedio de los ultimos 7 dias con datos y ultimas comidas.

Incluye comidas manuales y nutricion diaria enviada desde Apple Health, por ejemplo si una app como Cal AI escribe `Dietary Energy`, proteina, carbohidratos o grasa en Salud.

La primera version no calcula objetivos nutricionales automaticos. Solo registra y resume datos.

## Daily activity

`GET /api/v1/daily-activities?limit=30`

Devuelve los ultimos registros diarios de actividad.

`GET /api/v1/activity-summary?localDate=2026-07-28`

Devuelve actividad del dia, promedios de los ultimos 7 dias con datos y ultimos registros.

`POST /api/v1/daily-activities`

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
  "notes": "Manual entry"
}
```

Este endpoint hace upsert por `UserId + LocalDate + Source`, para que un dia no se duplique si se vuelve a enviar.

## POST /body-measurements

```json
{
  "measuredAt": "2026-07-28T07:00:00+02:00",
  "weightKg": 74.2,
  "bodyFatPercentage": 19.8,
  "musclePercentage": 42.1,
  "bodyWaterPercentage": 55.1,
  "bodyMassIndex": 23.4,
  "estimatedCaloriesKcal": 3087,
  "notes": "Soehnle manual entry"
}
```

## GET /body-measurements

```text
GET /api/v1/body-measurements?limit=30
```

Devuelve las ultimas mediciones, ordenadas desde la mas reciente.

## Apple Health bridge

```text
POST /api/v1/integrations/apple-health/body-measurements
```

Usa el mismo cuerpo de `POST /body-measurements`, pero guarda `source` como `AppleHealth`.

Header obligatorio:

```text
X-PulseBoard-Bridge-Key: <server bridge key>
```

Este endpoint esta pensado para un Atajo de iPhone o una app iOS puente. La clave vive solo en `/opt/pulseboard/.env`; no debe estar en GitHub, Angular ni documentacion publica.

## Apple Health daily activity bridge

```text
POST /api/v1/integrations/apple-health/daily-activity
```

Header obligatorio:

```text
X-PulseBoard-Bridge-Key: <server bridge key>
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

El registro queda con `source = AppleHealth`. Si el Atajo se ejecuta varias veces el mismo dia, reemplaza el dato anterior del dia en lugar de crear duplicados.

## Apple Health daily nutrition bridge

```text
POST /api/v1/integrations/apple-health/daily-nutrition
```

Header obligatorio:

```text
X-PulseBoard-Bridge-Key: <server bridge key>
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

Tambien acepta alias practicos del Atajo como `dietaryEnergy`, `protein`, `carbs`, `fat`, `fiber`, `sugar` y valores con texto de unidad.

## Apple Health daily recovery bridge

```text
POST /api/v1/integrations/apple-health/daily-recovery
```

Header obligatorio:

```text
X-PulseBoard-Bridge-Key: <server bridge key>
```

Body esperado:

```json
{
  "localDate": "2026-07-28",
  "heartRateBpm": 59,
  "restingHeartRateBpm": 60,
  "heartRateVariabilityMs": 26,
  "bloodOxygenPercentage": 95,
  "respiratoryRateBreathsPerMinute": 18,
  "sleepHours": 6.8,
  "sleepScore": 80,
  "vo2Max": 39.3,
  "walkingHeartRateAverageBpm": 83,
  "notes": "Apple Health via Shortcuts"
}
```

Tambien acepta alias practicos como `heartRate`, `restingHeartRate`, `hrv`, `bloodOxygen`, `respiratoryRate`, `timeAsleepHours`, `cardioFitness` y `walkingHeartRateAverage`.
