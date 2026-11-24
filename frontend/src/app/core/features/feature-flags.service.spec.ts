import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FeatureFlagsService } from './feature-flags.service';

describe('deployment feature flags', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('starts with every feature off and issues no request until asked', () => {
    const flags = TestBed.inject(FeatureFlagsService);
    expect(flags.kanban()).toBe(false);
    TestBed.inject(HttpTestingController).verify();
  });

  it('reads the reported flags and never trusts a missing or non-boolean value', () => {
    const flags = TestBed.inject(FeatureFlagsService);
    const http = TestBed.inject(HttpTestingController);

    flags.load();
    http.expectOne('/api/v1/features').flush({ kanban: true });
    expect(flags.kanban()).toBe(true);

    // A second call must not refetch while the first result stands.
    flags.load();
    http.verify();

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const fresh = TestBed.inject(FeatureFlagsService);
    fresh.load();
    TestBed.inject(HttpTestingController).expectOne('/api/v1/features').flush({ kanban: 'yes' });
    expect(fresh.kanban()).toBe(false);
  });

  it('keeps the pre-feature behaviour when the lookup fails and allows a later retry', () => {
    const flags = TestBed.inject(FeatureFlagsService);
    const http = TestBed.inject(HttpTestingController);

    flags.load();
    http.expectOne('/api/v1/features').flush(null, { status: 503, statusText: 'Unavailable' });
    expect(flags.kanban()).toBe(false);

    flags.load();
    http.expectOne('/api/v1/features').flush({ kanban: true });
    expect(flags.kanban()).toBe(true);
  });
});
