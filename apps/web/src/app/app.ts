import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import {
  LucideActivity,
  LucideDatabase,
  LucideDroplet,
  LucideDumbbell,
  LucideHeartPulse,
  LucideLanguages,
  LucideMoon,
  LucideScale,
  LucideServer,
  LucideShieldCheck,
  LucideUtensils,
} from '@lucide/angular';

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

const translations = {
  es: {
    apiOffline: 'API pendiente',
    apiOnline: 'API conectada',
    checking: 'Comprobando API',
    body: 'Composicion',
    checkIn: 'Check-in',
    data: 'Datos',
    food: 'Alimentacion',
    habits: 'Habitos',
    headline: 'Panel personal de salud y rendimiento',
    intro:
      'Base inicial para registrar check-ins, habitos, alimentacion, actividad y mediciones Soehnle.',
    language: 'Idioma',
    mood: 'Animo',
    muscle: 'Musculo',
    next: 'Siguientes modulos',
    readiness: 'Preparado',
    recovery: 'Recuperacion',
    sleep: 'Sueno',
    source: 'Fuente',
    status: 'Estado',
    timezone: 'Zona horaria',
    trend: 'Tendencia',
    water: 'Agua',
    weight: 'Peso',
  },
  en: {
    apiOffline: 'API pending',
    apiOnline: 'API connected',
    checking: 'Checking API',
    body: 'Body',
    checkIn: 'Check-in',
    data: 'Data',
    food: 'Nutrition',
    habits: 'Habits',
    headline: 'Personal health and performance board',
    intro:
      'Initial base for check-ins, habits, nutrition, activity, and Soehnle measurements.',
    language: 'Language',
    mood: 'Mood',
    muscle: 'Muscle',
    next: 'Next modules',
    readiness: 'Ready',
    recovery: 'Recovery',
    sleep: 'Sleep',
    source: 'Source',
    status: 'Status',
    timezone: 'Time zone',
    trend: 'Trend',
    water: 'Water',
    weight: 'Weight',
  },
} as const;

@Component({
  selector: 'app-root',
  imports: [
    LucideActivity,
    LucideDatabase,
    LucideDroplet,
    LucideDumbbell,
    LucideHeartPulse,
    LucideLanguages,
    LucideMoon,
    LucideScale,
    LucideServer,
    LucideShieldCheck,
    LucideUtensils,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly http = inject(HttpClient);

  readonly language = signal<Language>('es');
  readonly apiStatus = signal<ApiStatus>('checking');
  readonly meta = signal<ApiMeta | null>(null);

  readonly modules = computed(() => [
    { label: this.t('checkIn'), value: '4/9', icon: 'moon', color: 'blue' },
    { label: this.t('habits'), value: '0', icon: 'shield', color: 'violet' },
    { label: this.t('body'), value: '6', icon: 'scale', color: 'teal' },
    { label: this.t('food'), value: '0', icon: 'food', color: 'orange' },
  ]);

  readonly bodyMetrics = [
    { key: 'weight', value: '74,2 kg', color: 'green' },
    { key: 'muscle', value: '42,1 %', color: 'teal' },
    { key: 'water', value: '55,1 %', color: 'blue' },
    { key: 'trend', value: '7/14/30', color: 'gray' },
  ] as const;

  constructor() {
    this.http.get<ApiMeta>('/api/v1/meta').subscribe({
      next: (meta) => {
        this.meta.set(meta);
        this.apiStatus.set('online');
      },
      error: () => this.apiStatus.set('offline'),
    });
  }

  setLanguage(language: Language): void {
    this.language.set(language);
  }

  t(key: keyof (typeof translations)['es']): string {
    return translations[this.language()][key];
  }

  statusLabel(): string {
    return this.t(
      this.apiStatus() === 'online'
        ? 'apiOnline'
        : this.apiStatus() === 'offline'
          ? 'apiOffline'
          : 'checking',
    );
  }
}
