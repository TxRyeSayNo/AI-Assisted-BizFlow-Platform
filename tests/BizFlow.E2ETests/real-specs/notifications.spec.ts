import { test, expect } from './fixtures';
import type { WebSocketRoute } from '@playwright/test';

test('recipient inbox reconnects and performs an authorized catch-up read after a transport disconnect', async ({ page }) => {
  let transport: WebSocketRoute | undefined;
  let connections = 0;
  await page.routeWebSocket(url => url.pathname === '/api/v1/realtime/notifications', route => {
    // Pass every frame to/from the real API unchanged. Only close the transport once below.
    route.connectToServer(); transport = route; connections++;
  });
  await page.goto('/login');
  await page.getByLabel('Employee code', { exact: true }).fill('EMP001');
  await page.getByLabel('Password', { exact: true }).fill(process.env.BIZFLOW_E2E_PASSWORD!);
  await page.getByLabel('Workspace key', { exact: true }).fill(process.env.BIZFLOW_E2E_TENANT_KEY!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('link', { name: 'Notifications', exact: true }).click();
  const state = page.getByRole('status', { name: 'Live notification connection' });
  await expect(state).toHaveText('Live updates connected.');
  await expect(page.getByRole('region', { name: 'Your notifications' })).toHaveAttribute('aria-busy', 'false');
  expect(transport).toBeDefined();
  await transport!.close({ code: 1001, reason: 'Test-only interrupted transport' });
  await expect(state).toContainText('Reconnecting live updates.');
  const catchup = page.waitForResponse(response => response.url().includes('/api/v1/notifications?') && response.status() === 200);
  await expect(state).toHaveText('Live updates connected.', { timeout: 15000 });
  await catchup;
  expect(connections).toBeGreaterThan(1);
  await expect(page.getByRole('navigation', { name: 'Notification pages' })).toContainText('26 notifications');
});

test('recipient inbox pages real notifications and preserves read receipts through refresh', async ({ page }, testInfo) => {
  await page.goto('/login');
  await page.getByLabel('Employee code', { exact: true }).fill('EMP001');
  await page.getByLabel('Password', { exact: true }).fill(process.env.BIZFLOW_E2E_PASSWORD!);
  await page.getByLabel('Workspace key', { exact: true }).fill(process.env.BIZFLOW_E2E_TENANT_KEY!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('link', { name: 'Notifications', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Notifications', exact: true })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Notification pages' })).toContainText('26 notifications');
  const unreadText = await page.getByRole('status').filter({ hasText: /\d+\s+unread notifications?/ }).textContent();
  const initialUnread = Number(unreadText!.match(/\d+/)![0]);
  await expect(page.getByRole('article', { name: "Other user's private event", exact: true })).toHaveCount(0);
  await expect(page.getByRole('article', { name: 'Other tenant event', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Next', exact: true }).click();
  await expect(page.getByRole('navigation', { name: 'Notification pages' })).toContainText('Page 2');
  const article = page.getByRole('article').first();
  // Desktop/mobile share fixtures; each run marks a still-unread item.
  const mark = article.getByRole('button', { name: /^Mark read:/ });
  if (!(await mark.count())) {
    await page.getByRole('button', { name: 'Previous', exact: true }).click();
    await expect(page.getByRole('navigation', { name: 'Notification pages' })).toContainText('Page 1');
  }
  await mark.click();
  await expect(page.getByRole('status').filter({ hasText: 'Notification marked read.' })).toBeVisible();
  await expect(article.getByText(/Read\s+\d{4}-/)).toBeVisible();
  const receipt = await article.getByText(/Read\s+\d{4}-/).textContent();
  const refreshed = page.waitForResponse(response => response.url().includes('/api/v1/notifications?') && response.request().method() === 'GET');
  await page.getByRole('button', { name: 'Refresh inbox', exact: true }).click();
  expect((await refreshed).status()).toBe(200);
  await expect(page.getByRole('region', { name: 'Your notifications', exact: true })).toHaveAttribute('aria-busy', 'false');
  await expect(article.getByText(/Read\s+\d{4}-/)).toHaveText(receipt!);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: testInfo.outputPath('notification-inbox.png') });
  await article.screenshot({ path: testInfo.outputPath('notification-receipt.png') });
  await page.getByRole('checkbox', { name: 'Unread only', exact: true }).check();
  await expect(page.getByRole('navigation', { name: 'Notification pages' })).toContainText(`${initialUnread - 1} notifications`);
  await expect(page.getByRole('button', { name: 'Next', exact: true })).toBeDisabled();
  await expect(page.getByRole('article').getByText(/Read\s+\d{4}-/)).toHaveCount(0);
});
