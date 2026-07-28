# PulseBoard - Documento Maestro

## Producto

PulseBoard centraliza datos personales de salud y rendimiento para responder:

> Como estoy evolucionando, que factores pueden influir y que datos faltan para entenderlo mejor.

## Decisiones cerradas

```text
Nombre: PulseBoard
Frontend: Angular
Backend: ASP.NET Core Web API
Base de datos: PostgreSQL 16
Contenedores: Docker Compose
Idiomas: es, en
Zona horaria: Europe/Vienna
Unidades: metricas
Dominio web propuesto: pulseboard.juanariasdev.com
Dominio API propuesto: api.pulseboard.juanariasdev.com
Servidor: 217.216.92.208
Ruta servidor: /opt/pulseboard
```

## Modulos MVP

- Registro e inicio de sesion.
- Check-in diario.
- Habitos y suplementos.
- Mediciones Soehnle manuales.
- Dashboard inicial.

## Check-in diario

Campos:

```text
LocalDate
SleepHours
SleepQuality 1-5
Energy 1-5
Mood 1-5
Fatigue 1-5
MuscleSoreness 1-5
Hunger 1-5
Stress 1-5
Recovery 1-5
Note
```

## Bascula Soehnle

Campos iniciales:

```text
WeightKg
BodyFatPercentage
MusclePercentage
BodyWaterPercentage
BodyMassIndex
EstimatedCaloriesKcal
MeasuredAt
Notes
Source = SoehnleManual
```

El valor de calorias se guarda como estimacion de la bascula. No se usa como recomendacion automatica de ingesta.

## Seguridad

- No guardar secretos en Git.
- Usar `.env.example` solo con placeholders.
- Usar usuario de despliegue dedicado.
- No reutilizar base de datos, volumenes ni credenciales de otros proyectos.
- Publicar PostgreSQL solo dentro de Docker, sin puerto publico.
