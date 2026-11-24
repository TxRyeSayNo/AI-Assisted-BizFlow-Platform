import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { vi } from 'vitest';
import { PersonalProfileService } from './personal-profile.service';

describe('personal profile loading recovery', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    vi.useRealTimers();
  });
  const data = (id = 'owner') => ({
    userId: id,
    fullName: 'Official Name',
    nickname: 'Nickname',
    roles: [],
    hasAvatar: false,
  });

  it('recovers from a transient server error with a single bounded retry', () => {
    const state = TestBed.inject(PersonalProfileService);
    const http = TestBed.inject(HttpTestingController);
    state.load('owner');
    http.expectOne('/api/v1/auth/profile').flush({}, { status: 503, statusText: 'Unavailable' });
    vi.advanceTimersByTime(500);
    http.expectOne('/api/v1/auth/profile').flush(data());
    expect(state.failure()).toBe(false);
    expect(state.loading()).toBe(false);
    expect(state.profile()?.nickname).toBe('Nickname');
  });

  it('does not keep an earlier load failure when entering the profile page', () => {
    const state = TestBed.inject(PersonalProfileService);
    const http = TestBed.inject(HttpTestingController);
    state.load('owner');
    http.expectOne('/api/v1/auth/profile').flush({}, { status: 503, statusText: 'Unavailable' });
    vi.advanceTimersByTime(500);
    http.expectOne('/api/v1/auth/profile').flush({}, { status: 503, statusText: 'Unavailable' });
    expect(state.failure()).toBe(true);
    vi.advanceTimersByTime(5000);
    http.expectNone('/api/v1/auth/profile');
    state.load('owner', true);
    http.expectOne('/api/v1/auth/profile').flush(data());
    expect(state.failure()).toBe(false);
    expect(state.profile()?.fullName).toBe('Official Name');
  });

  it('updates only the nickname and cancels data from a previous account', () => {
    const state = TestBed.inject(PersonalProfileService);
    const http = TestBed.inject(HttpTestingController);
    state.load('owner');
    http.expectOne('/api/v1/auth/profile').flush(data());
    state.updateNickname('New nickname');
    expect(state.profile()?.fullName).toBe('Official Name');
    expect(state.profile()?.nickname).toBe('New nickname');
    state.refresh();
    const previous = http.expectOne('/api/v1/auth/profile');
    state.load('another-owner');
    expect(previous.cancelled).toBe(true);
    expect(state.profile()).toBeNull();
    http.expectOne('/api/v1/auth/profile').flush(data('another-owner'));
    expect(state.profile()?.userId).toBe('another-owner');
  });
});
