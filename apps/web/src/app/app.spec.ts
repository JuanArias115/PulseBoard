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
    http.expectOne('/api/v1/habits').flush([]);
    http.expectOne((request) => request.url.startsWith('/api/v1/habit-completions')).flush([]);
    http.expectOne('/api/v1/check-ins?limit=7').flush([]);
    http.expectOne('/api/v1/body-measurements?limit=7').flush([]);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('PulseBoard');
    expect(compiled.textContent).toContain('Check-in diario');
    http.verify();
  });
});
