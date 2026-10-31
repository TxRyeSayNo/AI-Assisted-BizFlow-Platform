import { TestBed } from '@angular/core/testing';
import { ToastService } from './toast.service';

describe('ToastService', () => {
  let service: ToastService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ToastService],
    });
    service = TestBed.inject(ToastService);
    service.clear();
  });

  afterEach(() => {
    service.clear();
  });

  it('adds an error toast with correct type and message', () => {
    service.error('Failed to log in');
    const toasts = service.toasts();
    expect(toasts.length).toBe(1);
    expect(toasts[0].type).toBe('error');
    expect(toasts[0].message).toBe('Failed to log in');
  });

  it('adds success, info, and warning toasts', () => {
    service.success('Operation succeeded');
    service.info('Update available');
    service.warning('Session expiring soon');

    const toasts = service.toasts();
    expect(toasts.length).toBe(3);
    expect(toasts[0].type).toBe('success');
    expect(toasts[1].type).toBe('info');
    expect(toasts[2].type).toBe('warning');
  });

  it('deduplicates identical message and type to avoid alert fatigue', () => {
    service.error('Duplicate error');
    service.error('Duplicate error');

    const toasts = service.toasts();
    expect(toasts.length).toBe(1);
    expect(toasts[0].message).toBe('Duplicate error');
  });

  it('dismisses a toast by id', async () => {
    const id = service.info('Temporary message');
    expect(service.toasts().length).toBe(1);

    service.dismiss(id);
    expect(service.toasts()[0].leaving).toBe(true);

    await new Promise((resolve) => setTimeout(resolve, 250));
    expect(service.toasts().length).toBe(0);
  });

  it('clears all toasts immediately', () => {
    service.error('Err 1');
    service.success('Succ 2');
    expect(service.toasts().length).toBe(2);

    service.clear();
    expect(service.toasts().length).toBe(0);
  });

  it('ignores empty or blank messages', () => {
    service.error('');
    service.success('   ');
    expect(service.toasts().length).toBe(0);
  });
});

