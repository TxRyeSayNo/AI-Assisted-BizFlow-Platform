import { test as base, expect } from '@playwright/test';

// All real browser cases share one loopback client and the unchanged production
// authentication limit (30 requests/minute). Pace case starts so this functional
// suite does not become a burst/load test as it grows. This is traffic scheduling,
// not a substitute for UI readiness assertions or a retry of failed requests.
export const test = base.extend<{ requestPacing: void }>({
  requestPacing: [async ({}, use) => {
    await new Promise(resolve => setTimeout(resolve, 3000));
    await use();
  }, { auto: true }],
});
export { expect };
