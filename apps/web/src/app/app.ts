import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  LucideActivity,
  LucideCheck,
  LucideDatabase,
  LucideDroplet,
  LucideDumbbell,
  LucideHeartPulse,
  LucideLanguages,
  LucideMoon,
  LucidePlus,
  LucideRefreshCw,
  LucideSave,
  LucideScale,
  LucideShieldCheck,
  LucideUtensils,
} from '@lucide/angular';
import { forkJoin } from 'rxjs';

type Language = 'es' | 'en';
type ApiStatus = 'checking' | 'online' | 'offline';

interface ApiMeta {
  name: string;
  status: string;
  defaultLanguage: string;
  supportedLanguages: string[];
  timeZoneId: string;
  units: string;
  modules: string[];
}

interface Habit {
  id: string;
  name: string;
  category: string;
  frequency: string;
  targetAmount?: number;
  unit?: string;
}

interface HabitCompletion {
  habitId: string;
  localDate: string;
}

interface CheckIn {
  localDate: string;
  sleepHours: number;
  energy: number;
  mood: number;
  recovery: number;
}

interface BodyMeasurement {
  measuredAtUtc: string;
  weightKg: number;
  bodyFatPercentage?: number;
  musclePercentage?: number;
  bodyWaterPercentage?: number;
  bodyMassIndex?: number;
  estimatedCaloriesKcal?: number;
  source: string;
}

interface Meal {
  id: string;
  localDate: string;
  name: string;
  mealType: string;
  caloriesKcal: number;
  proteinGrams: number;
  carbohydrateGrams: number;
  fatGrams: number;
  hasVegetables: boolean;
  isFavorite: boolean;
  eatenAtUtc: string;
}

interface DashboardSummary {
  generatedAtUtc: string;
  localDate: string;
  timeZoneId: string;
  readinessScore: number;
  today: {
    sleepHours?: number;
    energy?: number;
    recovery?: number;
    completedHabits: number;
    totalHabits: number;
  };
  habits: {
    active: number;
    completedToday: number;
    completionRate7Days: number;
    streakDays: number;
  };
  nutrition: NutritionSummary;
  body: {
    latest?: BodyMeasurement;
    trends: TrendMetric[];
    history: BodyHistoryPoint[];
  };
  insights: Insight[];
}

interface AnalysisSummary {
  generatedAtUtc: string;
  localDate: string;
  timeZoneId: string;
  components: AnalysisComponent[];
  bodyData: BodyDataSignal;
  completeness: DataCompleteness;
  observations: AnalysisObservation[];
}

interface AnalysisComponent {
  key: string;
  labelEs: string;
  labelEn: string;
  score: number | null;
  status: string;
  summaryEs: string;
  summaryEn: string;
  evidence: string[];
}

interface BodyDataSignal {
  trend: string;
  summaryEs: string;
  summaryEn: string;
  dataPoints: number;
  trends: TrendMetric[];
}

interface DataCompleteness {
  score: number;
  presentDomains: string[];
  missingDomains: string[];
  summaryEs: string;
  summaryEn: string;
}

interface AnalysisObservation {
  category: string;
  severity: string;
  messageEs: string;
  messageEn: string;
  rule: string;
}

interface NutritionSummary {
  today: NutritionTotals;
  average7Days: NutritionTotals;
  loggedDays7: number;
  latestMeals: Meal[];
}

interface NutritionTotals {
  meals: number;
  caloriesKcal: number;
  proteinGrams: number;
  carbohydrateGrams: number;
  fatGrams: number;
  vegetableMeals: number;
}

interface TrendMetric {
  key: string;
  labelEs: string;
  labelEn: string;
  unit: string;
  latest?: number;
  average7?: number;
  average14?: number;
  average30?: number;
  change30?: number;
  trend: string;
  trendEs: string;
  trendEn: string;
  dataPoints: number;
}

interface BodyHistoryPoint {
  localDate: string;
  weightKg: number;
  bodyFatPercentage?: number;
  musclePercentage?: number;
  bodyWaterPercentage?: number;
}

interface Insight {
  category: string;
  severity: string;
  messageEs: string;
  messageEn: string;
}

const translations = {
  es: {
    average14: 'Prom. 14 dias',
    average30: 'Prom. 30 dias',
    average7: 'Prom. 7 dias',
    analysis: 'Analisis',
    apiOffline: 'API sin conexion',
    apiOnline: 'API conectada',
    body: 'Composicion',
    carbs: 'Carbohidratos',
    calories: 'Calorias',
    checkIn: 'Check-in',
    complete: 'Completar',
    completed: 'Completado',
    createHabit: 'Crear habito',
    data: 'Datos',
    dataQuality: 'Calidad de datos',
    date: 'Fecha',
    dinner: 'Cena',
    energy: 'Energia',
    estimatedCalories: 'Calorias estimadas',
    fatigue: 'Fatiga',
    food: 'Alimentacion',
    fat: 'Grasas',
    habitName: 'Nombre del habito',
    habits: 'Habitos',
    hunger: 'Hambre',
    language: 'Idioma',
    insight: 'Observaciones',
    integrity: 'Integridad',
    latest: 'Actual',
    latestBody: 'Ultima medicion',
    lunch: 'Almuerzo',
    mealName: 'Nombre de comida',
    mealType: 'Tipo',
    meals: 'Comidas',
    mood: 'Animo',
    muscle: 'Musculo',
    newHabit: 'Nuevo habito',
    noTrend: 'Sin tendencia',
    note: 'Nota',
    nutrition: 'Nutricion',
    protein: 'Proteina',
    quickCheckIn: 'Check-in diario',
    readiness: 'Preparacion',
    recovery: 'Recuperacion',
    refresh: 'Actualizar',
    save: 'Guardar',
    saveMeasurement: 'Guardar medicion',
    saveMeal: 'Guardar comida',
    sevenDays: '7 dias',
    sleep: 'Sueno',
    sleepHours: 'Horas de sueno',
    sleepQuality: 'Calidad de sueno',
    soreness: 'Dolor muscular',
    source: 'Fuente',
    status: 'Estado',
    stress: 'Estres',
    timezone: 'Zona horaria',
    today: 'Hoy',
    trend: 'Tendencia',
    trends: 'Tendencias',
    water: 'Agua',
    weight: 'Peso',
  },
  en: {
    average14: '14 day avg',
    average30: '30 day avg',
    average7: '7 day avg',
    analysis: 'Analysis',
    apiOffline: 'API offline',
    apiOnline: 'API connected',
    body: 'Body',
    carbs: 'Carbs',
    calories: 'Calories',
    checkIn: 'Check-in',
    complete: 'Complete',
    completed: 'Completed',
    createHabit: 'Create habit',
    data: 'Data',
    dataQuality: 'Data quality',
    date: 'Date',
    dinner: 'Dinner',
    energy: 'Energy',
    estimatedCalories: 'Estimated calories',
    fatigue: 'Fatigue',
    food: 'Nutrition',
    fat: 'Fat',
    habitName: 'Habit name',
    habits: 'Habits',
    hunger: 'Hunger',
    language: 'Language',
    insight: 'Insights',
    integrity: 'Integrity',
    latest: 'Current',
    latestBody: 'Latest measurement',
    lunch: 'Lunch',
    mealName: 'Meal name',
    mealType: 'Type',
    meals: 'Meals',
    mood: 'Mood',
    muscle: 'Muscle',
    newHabit: 'New habit',
    noTrend: 'No trend',
    note: 'Note',
    nutrition: 'Nutrition',
    protein: 'Protein',
    quickCheckIn: 'Daily check-in',
    readiness: 'Readiness',
    recovery: 'Recovery',
    refresh: 'Refresh',
    save: 'Save',
    saveMeasurement: 'Save measurement',
    saveMeal: 'Save meal',
    sevenDays: '7 days',
    sleep: 'Sleep',
    sleepHours: 'Sleep hours',
    sleepQuality: 'Sleep quality',
    soreness: 'Muscle soreness',
    source: 'Source',
    status: 'Status',
    stress: 'Stress',
    timezone: 'Time zone',
    today: 'Today',
    trend: 'Trend',
    trends: 'Trends',
    water: 'Water',
    weight: 'Weight',
  },
} as const;

@Component({
  selector: 'app-root',
  imports: [
    FormsModule,
    LucideActivity,
    LucideCheck,
    LucideDatabase,
    LucideDroplet,
    LucideDumbbell,
    LucideHeartPulse,
    LucideLanguages,
    LucideMoon,
    LucidePlus,
    LucideRefreshCw,
    LucideSave,
    LucideScale,
    LucideShieldCheck,
    LucideUtensils,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly http = inject(HttpClient);
  private readonly today = this.getViennaDate();

  readonly language = signal<Language>('es');
  readonly apiStatus = signal<ApiStatus>('checking');
  readonly meta = signal<ApiMeta | null>(null);
  readonly habits = signal<Habit[]>([]);
  readonly completions = signal<HabitCompletion[]>([]);
  readonly checkIns = signal<CheckIn[]>([]);
  readonly measurements = signal<BodyMeasurement[]>([]);
  readonly meals = signal<Meal[]>([]);
  readonly dashboardSummary = signal<DashboardSummary | null>(null);
  readonly analysisSummary = signal<AnalysisSummary | null>(null);
  readonly message = signal('');

  readonly completedHabitIds = computed(
    () => new Set(this.completions().map((completion) => completion.habitId)),
  );

  readonly latestMeasurement = computed(() => this.measurements()[0]);

  readonly dashboard = computed(() => {
    const summary = this.dashboardSummary();
    const latestCheckIn = this.checkIns()[0];
    const completed = summary?.today.completedHabits ?? this.completions().length;
    const total = summary?.today.totalHabits ?? this.habits().length;

    return {
      score: summary ? `${summary.readinessScore}` : '-',
      sleep: summary?.today.sleepHours
        ? `${summary.today.sleepHours} h`
        : latestCheckIn
          ? `${latestCheckIn.sleepHours} h`
          : '-',
      energy: summary?.today.energy
        ? `${summary.today.energy}/5`
        : latestCheckIn
          ? `${latestCheckIn.energy}/5`
          : '-',
      recovery: summary?.today.recovery
        ? `${summary.today.recovery}/5`
        : latestCheckIn
          ? `${latestCheckIn.recovery}/5`
          : '-',
      habits: total > 0 ? `${completed}/${total}` : '0',
    };
  });

  readonly bodyTrends = computed(() => this.dashboardSummary()?.body.trends ?? []);
  readonly insights = computed(() => this.dashboardSummary()?.insights ?? []);
  readonly nutrition = computed(() => this.dashboardSummary()?.nutrition);
  readonly analysisComponents = computed(() => this.analysisSummary()?.components ?? []);
  readonly analysisObservations = computed(() => this.analysisSummary()?.observations ?? []);

  readonly weightChart = computed(() => {
    const points = this.dashboardSummary()?.body.history ?? [];
    if (points.length === 0) {
      return [];
    }

    const weights = points.map((point) => point.weightKg);
    const min = Math.min(...weights);
    const max = Math.max(...weights);
    const range = Math.max(max - min, 0.1);

    return points.slice(-14).map((point) => ({
      label: point.localDate.slice(5),
      value: point.weightKg,
      height: Math.round(((point.weightKg - min) / range) * 64) + 18,
    }));
  });

  checkInForm = {
    localDate: this.today,
    sleepHours: 7.5,
    sleepQuality: 4,
    energy: 4,
    mood: 4,
    fatigue: 2,
    muscleSoreness: 2,
    hunger: 3,
    stress: 2,
    recovery: 4,
    note: '',
  };

  habitForm = {
    name: '',
    category: 'habit',
    frequency: 'daily',
    targetAmount: undefined as number | undefined,
    unit: '',
    notes: '',
  };

  measurementForm = {
    measuredAt: this.toDateTimeLocal(new Date()),
    weightKg: 74.2,
    bodyFatPercentage: 19.8,
    musclePercentage: 42.1,
    bodyWaterPercentage: 55.1,
    bodyMassIndex: 23.4,
    estimatedCaloriesKcal: 3087,
    notes: 'Soehnle',
  };

  mealForm = {
    localDate: this.today,
    eatenAt: this.toDateTimeLocal(new Date()),
    name: '',
    mealType: 'meal',
    caloriesKcal: 650,
    proteinGrams: 35,
    carbohydrateGrams: 70,
    fatGrams: 20,
    hasVegetables: false,
    isFavorite: false,
    notes: '',
  };

  constructor() {
    this.load();
  }

  setLanguage(language: Language): void {
    this.language.set(language);
  }

  t(key: keyof (typeof translations)['es']): string {
    return translations[this.language()][key];
  }

  statusLabel(): string {
    return this.apiStatus() === 'online' ? this.t('apiOnline') : this.t('apiOffline');
  }

  load(): void {
    this.apiStatus.set('checking');
    forkJoin({
      meta: this.http.get<ApiMeta>('/api/v1/meta'),
      dashboard: this.http.get<DashboardSummary>('/api/v1/dashboard'),
      analysis: this.http.get<AnalysisSummary>('/api/v1/analysis'),
      habits: this.http.get<Habit[]>('/api/v1/habits'),
      completions: this.http.get<HabitCompletion[]>(
        `/api/v1/habit-completions?localDate=${this.checkInForm.localDate}`,
      ),
      checkIns: this.http.get<CheckIn[]>('/api/v1/check-ins?limit=7'),
      measurements: this.http.get<BodyMeasurement[]>('/api/v1/body-measurements?limit=7'),
      meals: this.http.get<Meal[]>(`/api/v1/meals?localDate=${this.mealForm.localDate}`),
    }).subscribe({
      next: ({ meta, dashboard, analysis, habits, completions, checkIns, measurements, meals }) => {
        this.meta.set(meta);
        this.dashboardSummary.set(dashboard);
        this.analysisSummary.set(analysis);
        this.habits.set(habits);
        this.completions.set(completions);
        this.checkIns.set(checkIns);
        this.measurements.set(measurements);
        this.meals.set(meals);
        this.apiStatus.set('online');
      },
      error: () => this.apiStatus.set('offline'),
    });
  }

  trendLabel(trend: TrendMetric): string {
    return this.language() === 'es' ? trend.trendEs : trend.trendEn;
  }

  metricLabel(trend: TrendMetric): string {
    return this.language() === 'es' ? trend.labelEs : trend.labelEn;
  }

  insightMessage(insight: Insight): string {
    return this.language() === 'es' ? insight.messageEs : insight.messageEn;
  }

  analysisComponentLabel(component: AnalysisComponent): string {
    return this.language() === 'es' ? component.labelEs : component.labelEn;
  }

  analysisComponentSummary(component: AnalysisComponent): string {
    return this.language() === 'es' ? component.summaryEs : component.summaryEn;
  }

  bodyDataSummary(bodyData?: BodyDataSignal): string {
    if (!bodyData) {
      return '-';
    }

    return this.language() === 'es' ? bodyData.summaryEs : bodyData.summaryEn;
  }

  completenessSummary(completeness?: DataCompleteness): string {
    if (!completeness) {
      return '-';
    }

    return this.language() === 'es' ? completeness.summaryEs : completeness.summaryEn;
  }

  analysisObservationMessage(observation: AnalysisObservation): string {
    return this.language() === 'es' ? observation.messageEs : observation.messageEn;
  }

  saveCheckIn(): void {
    this.http.post('/api/v1/check-ins', this.checkInForm).subscribe({
      next: () => this.afterSave('Check-in guardado'),
      error: () => this.message.set('No se pudo guardar el check-in'),
    });
  }

  createHabit(): void {
    if (!this.habitForm.name.trim()) {
      return;
    }

    this.http.post('/api/v1/habits', this.habitForm).subscribe({
      next: () => {
        this.habitForm = {
          name: '',
          category: 'habit',
          frequency: 'daily',
          targetAmount: undefined,
          unit: '',
          notes: '',
        };
        this.afterSave('Habito creado');
      },
      error: () => this.message.set('No se pudo crear el habito'),
    });
  }

  completeHabit(habit: Habit): void {
    this.http
      .post(`/api/v1/habits/${habit.id}/completions`, {
        localDate: this.checkInForm.localDate,
        amount: habit.targetAmount,
        notes: '',
      })
      .subscribe({
        next: () => this.afterSave('Habito completado'),
        error: () => this.message.set('No se pudo completar el habito'),
      });
  }

  saveMeasurement(): void {
    this.http
      .post('/api/v1/body-measurements', {
        ...this.measurementForm,
        measuredAt: new Date(this.measurementForm.measuredAt).toISOString(),
      })
      .subscribe({
        next: () => this.afterSave('Medicion guardada'),
        error: () => this.message.set('No se pudo guardar la medicion'),
      });
  }

  saveMeal(): void {
    this.http
      .post('/api/v1/meals', {
        ...this.mealForm,
        eatenAt: new Date(this.mealForm.eatenAt).toISOString(),
      })
      .subscribe({
        next: () => {
          this.mealForm = {
            ...this.mealForm,
            name: '',
            hasVegetables: false,
            isFavorite: false,
            notes: '',
          };
          this.afterSave('Comida guardada');
        },
        error: () => this.message.set('No se pudo guardar la comida'),
      });
  }

  formatNumber(value?: number, suffix = ''): string {
    if (value === undefined || value === null) {
      return '-';
    }

    const locale = this.language() === 'es' ? 'es-ES' : 'en-US';
    return `${new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(value)}${suffix}`;
  }

  private afterSave(message: string): void {
    this.message.set(message);
    this.load();
  }

  private getViennaDate(): string {
    const formatter = new Intl.DateTimeFormat('en-CA', {
      timeZone: 'Europe/Vienna',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    });

    return formatter.format(new Date());
  }

  private toDateTimeLocal(date: Date): string {
    const offsetMs = date.getTimezoneOffset() * 60_000;
    return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16);
  }
}
