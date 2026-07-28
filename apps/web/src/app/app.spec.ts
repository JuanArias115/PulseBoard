import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render PulseBoard workspace', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/v1/meta').flush({
      name: 'PulseBoard',
      status: 'online',
      defaultLanguage: 'es',
      supportedLanguages: ['es', 'en'],
      timeZoneId: 'Europe/Vienna',
      units: 'metric',
      modules: [],
    });
    http.expectOne('/api/v1/dashboard').flush({
      generatedAtUtc: '2026-07-28T11:00:00Z',
      localDate: '2026-07-28',
      timeZoneId: 'Europe/Vienna',
      readinessScore: 72,
      today: {
        sleepHours: 7.5,
        energy: 4,
        recovery: 4,
        completedHabits: 1,
        totalHabits: 2,
      },
      habits: {
        active: 2,
        completedToday: 1,
        completionRate7Days: 50,
        streakDays: 1,
      },
      nutrition: {
        today: {
          meals: 1,
          caloriesKcal: 720,
          proteinGrams: 48,
          carbohydrateGrams: 82,
          fatGrams: 18,
          vegetableMeals: 1,
          fiberGrams: 8,
          sugarGrams: 20,
          waterLiters: 1.5,
        },
        average7Days: {
          meals: 1,
          caloriesKcal: 720,
          proteinGrams: 48,
          carbohydrateGrams: 82,
          fatGrams: 18,
          vegetableMeals: 1,
          fiberGrams: 8,
          sugarGrams: 20,
          waterLiters: 1.5,
        },
        loggedDays7: 1,
        latestMeals: [],
        latestDailyNutritions: [],
      },
      activity: {
        today: {
          steps: 8500,
          activeEnergyKcal: 420,
          restingEnergyKcal: 1360,
          exerciseMinutes: 35,
          standHours: 8,
          standMinutes: 60,
          walkingRunningDistanceKm: 5.2,
          cyclingDistanceKm: 0,
          flightsClimbed: 4,
          physicalEffortMet: 4.3,
          workoutCount: 1,
        },
        average7Days: {
          steps: 8500,
          activeEnergyKcal: 420,
          restingEnergyKcal: 1360,
          exerciseMinutes: 35,
          standHours: 8,
          standMinutes: 60,
          walkingRunningDistanceKm: 5.2,
          cyclingDistanceKm: 0,
          flightsClimbed: 4,
          physicalEffortMet: 4.3,
          workoutCount: 1,
        },
        loggedDays7: 1,
        latestActivities: [],
      },
      recovery: {
        today: {
          heartRateBpm: 59,
          restingHeartRateBpm: 60,
          heartRateVariabilityMs: 26,
          bloodOxygenPercentage: 95,
          respiratoryRateBreathsPerMinute: 18,
          sleepHours: 6.8,
          sleepScore: 80,
          vo2Max: 39.3,
          walkingHeartRateAverageBpm: 83,
        },
        average7Days: {
          heartRateBpm: 59,
          restingHeartRateBpm: 60,
          heartRateVariabilityMs: 26,
          bloodOxygenPercentage: 95,
          respiratoryRateBreathsPerMinute: 18,
          sleepHours: 6.8,
          sleepScore: 80,
          vo2Max: 39.3,
          walkingHeartRateAverageBpm: 83,
        },
        loggedDays7: 1,
        latestRecoveries: [],
      },
      body: {
        latest: null,
        trends: [],
        history: [],
      },
      insights: [
        {
          category: 'data',
          severity: 'info',
          messageEs: 'Aun faltan datos.',
          messageEn: 'More data is needed.',
        },
      ],
    });
    http.expectOne('/api/v1/analysis').flush({
      generatedAtUtc: '2026-07-28T11:00:00Z',
      localDate: '2026-07-28',
      timeZoneId: 'Europe/Vienna',
      components: [
        {
          key: 'recovery',
          labelEs: 'Recuperacion',
          labelEn: 'Recovery',
          score: 72,
          status: 'steady',
          summaryEs: 'Promedio reciente: 7.5 h de sueno.',
          summaryEn: 'Recent average: 7.5 h of sleep.',
          evidence: ['checkIns:1'],
        },
      ],
      bodyData: {
        trend: 'insufficient',
        summaryEs: 'Aun faltan mediciones.',
        summaryEn: 'More measurements are needed.',
        dataPoints: 0,
        trends: [],
      },
      completeness: {
        score: 40,
        presentDomains: ['check-in', 'nutrition'],
        missingDomains: ['activity'],
        summaryEs: 'Datos disponibles en 2/5 areas.',
        summaryEn: 'Data is available in 2/5 areas.',
      },
      observations: [
        {
          category: 'data',
          severity: 'info',
          messageEs: 'Faltan datos en: activity.',
          messageEn: 'Missing data in: activity.',
          rule: 'dataCompleteness<100',
        },
      ],
    });
    http.expectOne('/api/v1/habits').flush([]);
    http.expectOne((request) => request.url.startsWith('/api/v1/habit-completions')).flush([]);
    http.expectOne('/api/v1/check-ins?limit=7').flush([]);
    http.expectOne('/api/v1/body-measurements?limit=7').flush([]);
    http.expectOne((request) => request.url.startsWith('/api/v1/meals')).flush([]);
    http.expectOne('/api/v1/daily-activities?limit=7').flush([]);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('PulseBoard');
    expect(compiled.textContent).toContain('Check-in diario');
    http.verify();
  });
});
