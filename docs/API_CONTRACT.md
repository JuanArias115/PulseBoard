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
