import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { SessionIdentity, SessionService } from '../auth/session';
import { INBOX_CONNECTION, InboxConnection, InboxUpdates } from './inbox-updates';

class Connection implements InboxConnection {
  changed = () => {};
  ready = () => {};
  reconnecting = () => {};
  reconnected = () => {};
  closed = () => {};
  start = vi.fn(async () => {});
  stop = vi.fn(async () => {});
  on(name: string, callback: () => void) {
    if (name === 'InboxChanged') this.changed = callback;
    else {
      expect(name).toBe('InboxReady');
      this.ready = callback;
    }
  }
  onreconnecting(callback: () => void) {
    this.reconnecting = callback;
  }
  onreconnected(callback: () => void) {
    this.reconnected = callback;
  }
  onclose(callback: () => void) {
    this.closed = callback;
  }
}

describe('recipient realtime lifecycle', () => {
  const account = {
    userId: 'user',
    tenantId: 'tenant',
    fullName: 'User',
    tenantName: 'Workspace',
    permissions: [],
  };
  let identity = signal<SessionIdentity | null>(account);
  let generation = 0;
  let connection: Connection;
  let factory: ReturnType<typeof vi.fn>;
  let token: () => Promise<string>;
  beforeEach(() => {
    identity = signal<SessionIdentity | null>(account);
    generation = 0;
    connection = new Connection();
    factory = vi.fn((getToken: () => Promise<string>) => {
      token = getToken;
      return connection;
    });
    TestBed.configureTestingModule({
      providers: [
        InboxUpdates,
        { provide: INBOX_CONNECTION, useValue: factory },
        {
          provide: SessionService,
          useValue: {
            identity,
            sessionGeneration: () => generation,
            realtimeAccessToken: async () => 'in-memory-token',
          },
        },
      ],
    });
  });
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.useRealTimers();
  });

  it('refetches on initial connection, hint and reconnect, without reconnecting for token-only identity refresh', async () => {
    const updates = TestBed.inject(InboxUpdates);
    const events: string[] = [];
    updates.changes.subscribe((event) => events.push(event));
    TestBed.tick();
    await Promise.resolve();
    expect(updates.status()).toBe('live');
    expect(await token()).toBe('in-memory-token');
    connection.ready();
    connection.changed();
    connection.reconnecting();
    expect(updates.status()).toBe('reconnecting');
    connection.reconnected();
    expect(events).toEqual(['refresh', 'refresh', 'refresh', 'refresh']);
    identity.set({ ...account });
    TestBed.tick();
    expect(factory).toHaveBeenCalledTimes(1);
  });

  it('stops on logout and discards late events and credentials from the old session', async () => {
    const updates = TestBed.inject(InboxUpdates);
    const events: string[] = [];
    updates.changes.subscribe((event) => events.push(event));
    TestBed.tick();
    await Promise.resolve();
    generation++;
    identity.set(null);
    TestBed.tick();
    expect(connection.stop).toHaveBeenCalledOnce();
    expect(updates.status()).toBe('offline');
    connection.changed();
    connection.reconnected();
    connection.closed();
    expect(events).toEqual(['refresh', 'clear']);
    await expect(token()).rejects.toThrow('Session changed');
  });

  it('retries an initial failure and cancels retry timers when the screen is destroyed', async () => {
    vi.useFakeTimers();
    connection.start.mockRejectedValueOnce(new Error('offline'));
    const updates = TestBed.inject(InboxUpdates);
    TestBed.tick();
    await Promise.resolve();
    expect(updates.status()).toBe('offline');
    await vi.advanceTimersByTimeAsync(5000);
    expect(connection.start).toHaveBeenCalledTimes(2);
    expect(updates.status()).toBe('live');
    connection.closed();
    TestBed.resetTestingModule();
    await vi.advanceTimersByTimeAsync(10000);
    expect(connection.start).toHaveBeenCalledTimes(2);
  });

  it('never connects platform or unauthenticated accounts to a tenant inbox', () => {
    identity.set({ ...account, tenantId: null });
    TestBed.inject(InboxUpdates);
    TestBed.tick();
    expect(factory).not.toHaveBeenCalled();
    identity.set(null);
    TestBed.tick();
    expect(factory).not.toHaveBeenCalled();
  });
});
