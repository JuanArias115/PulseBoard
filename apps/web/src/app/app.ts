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

const translations = {
  es: {
    apiOffline: 'API sin conexion',
    apiOnline: 'API conectada',
    body: 'Composicion',
    calories: 'Calorias',
    checkIn: 'Check-in',
    complete: 'Completar',
    completed: 'Completado',
    createHabit: 'Crear habito',
    data: 'Datos',
    date: 'Fecha',
    energy: 'Energia',
    estimatedCalories: 'Calorias estimadas',
    fatigue: 'Fatiga',
    food: 'Alimentacion',
    habitName: 'Nombre del habito',
    habits: 'Habitos',
    hunger: 'Hambre',
    language: 'Idioma',
    latestBody: 'Ultima medicion',
    mood: 'Animo',
    muscle: 'Musculo',
    newHabit: 'Nuevo habito',
    note: 'Nota',
    quickCheckIn: 'Check-in diario',
    recovery: 'Recuperacion',
    refresh: 'Actualizar',
    save: 'Guardar',
    saveMeasurement: 'Guardar medicion',
    sleep: 'Sueno',
    sleepHours: 'Horas de sueno',
    sleepQuality: 'Calidad de sueno',
    soreness: 'Dolor muscular',
    source: 'Fuente',
    status: 'Estado',
    stress: 'Estres',
    timezone: 'Zona horaria',
    today: 'Hoy',
    water: 'Agua',
    weight: 'Peso',
  },
  en: {
    apiOffline: 'API offline',
    apiOnline: 'API connected',
    body: 'Body',
    calories: 'Calories',
    checkIn: 'Check-in',
    complete: 'Complete',
    completed: 'Completed',
    createHabit: 'Create habit',
    data: 'Data',
    date: 'Date',
    energy: 'Energy',
    estimatedCalories: 'Estimated calories',
    fatigue: 'Fatigue',
    food: 'Nutrition',
    habitName: 'Habit name',
    habits: 'Habits',
    hunger: 'Hunger',
    language: 'Language',
    latestBody: 'Latest measurement',
    mood: 'Mood',
    muscle: 'Muscle',
    newHabit: 'New habit',
    note: 'Note',
    quickCheckIn: 'Daily check-in',
    recovery: 'Recovery',
    refresh: 'Refresh',
    save: 'Save',
    saveMeasurement: 'Save measurement',
    sleep: 'Sleep',
    sleepHours: 'Sleep hours',
    sleepQuality: 'Sleep quality',
    soreness: 'Muscle soreness',
    source: 'Source',
    status: 'Status',
    stress: 'Stress',
    timezone: 'Time zone',
    today: 'Today',
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
  readonly message = signal('');

  readonly completedHabitIds = computed(
    () => new Set(this.completions().map((completion) => completion.habitId)),
  );

  readonly latestMeasurement = computed(() => this.measurements()[0]);

  readonly dashboard = computed(() => {
    const latestCheckIn = this.checkIns()[0];
    const completed = this.completions().length;
    const total = this.habits().length;

    return {
      sleep: latestCheckIn ? `${latestCheckIn.sleepHours} h` : '-',
      energy: latestCheckIn ? `${latestCheckIn.energy}/5` : '-',
      recovery: latestCheckIn ? `${latestCheckIn.recovery}/5` : '-',
      habits: total > 0 ? `${completed}/${total}` : '0',
    };
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
      habits: this.http.get<Habit[]>('/api/v1/habits'),
      completions: this.http.get<HabitCompletion[]>(
        `/api/v1/habit-completions?localDate=${this.checkInForm.localDate}`,
      ),
      checkIns: this.http.get<CheckIn[]>('/api/v1/check-ins?limit=7'),
      measurements: this.http.get<BodyMeasurement[]>('/api/v1/body-measurements?limit=7'),
    }).subscribe({
      next: ({ meta, habits, completions, checkIns, measurements }) => {
        this.meta.set(meta);
        this.habits.set(habits);
        this.completions.set(completions);
        this.checkIns.set(checkIns);
        this.measurements.set(measurements);
        this.apiStatus.set('online');
      },
      error: () => this.apiStatus.set('offline'),
    });
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
