self.addEventListener('push', (event) => {
  let data;
  try { data = event.data?.json(); } catch { data = {}; }
  event.waitUntil(self.registration.showNotification('BizFlow', {
    body: 'Bạn có thông báo công việc mới. Mở BizFlow để xem chi tiết.',
    data: { url: data?.data?.url ?? '/notifications' },
  }));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  let target = new URL('/notifications', self.location.origin);
  try {
    const requested = new URL(event.notification.data?.url, self.location.origin);
    if (requested.origin === self.location.origin && ['http:', 'https:'].includes(requested.protocol)) target = requested;
  } catch { /* Fall back to the inbox. */ }
  event.waitUntil((async () => {
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    const existing = windows.find((client) => new URL(client.url).origin === self.location.origin);
    if (existing) { await existing.navigate(target.href); await existing.focus(); }
    else await self.clients.openWindow(target.href);
  })());
});
