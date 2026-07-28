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

La primera version no calcula objetivos nutricionales automaticos. Solo registra y resume datos.

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
