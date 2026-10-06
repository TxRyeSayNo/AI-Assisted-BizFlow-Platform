import { test, expect } from './fixtures';

for (const method of ['code', 'email'] as const) {
  test(`TST-AUTH-001: real ${method} login enters the correct tenant workspace`, async ({ page }) => {
    let loginBody: { refreshToken: string; session: { tenantId: string } } | undefined;
    // Forward the request to the real API and return its exact response, without fabricating data.
    // Capture before delivery: Chromium can discard a fetch response body after SPA navigation.
    await page.route('**/api/v1/auth/login', async route => {
      const actual = await route.fetch();
      loginBody = await actual.json();
      await route.fulfill({ response: actual });
    });
    await page.goto('/login');
    if (method === 'email') await page.getByRole('radio', { name: 'Company email' }).click();
    await page.getByLabel(method === 'email' ? 'Company email' : 'Employee code', { exact: true })
      .fill(method === 'email' ? 'employee@example.test' : 'EMP001');
    await page.getByLabel('Password', { exact: true }).fill(process.env.BIZFLOW_E2E_PASSWORD!);
    await page.getByLabel('Workspace key', { exact: true }).fill(process.env.BIZFLOW_E2E_TENANT_KEY!);
    const responsePromise = page.waitForResponse(response => response.url().endsWith('/api/v1/auth/login'));
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    const response = await responsePromise;
    expect(response.status()).toBe(200);
    expect(loginBody).toBeDefined();
    const session = loginBody!;
    expect(session.session.tenantId).toBe(process.env.BIZFLOW_E2E_TENANT_ID);
    await expect(page).toHaveURL(/\/workspace$/);
    await expect(page.getByRole('heading', { name: 'Welcome, Test Employee' })).toBeVisible();
    await expect(page.getByText('You are signed in to Test Company.')).toBeVisible();
    expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
    // Refresh through the same frontend proxy, without mocking the API or database.
    const refresh = await page.request.post('/api/v1/auth/refresh', { data: { refreshToken: session.refreshToken } });
    expect(refresh.status()).toBe(200);
    const successor = await refresh.json();
    expect(successor.session.tenantId).toBe(session.session.tenantId);
    expect(successor.refreshToken === session.refreshToken).toBe(false);
    // Reload loses in-memory credentials by design; no hidden persistence restores a session.
    await page.reload();
    await expect(page).toHaveURL(/\/login$/);
  });
}
