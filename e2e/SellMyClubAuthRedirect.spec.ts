import { test, expect } from '@playwright/test';

test('Unauthenticated user is redirected from Sell My Club to Login', async ({ page }) => {
  await page.goto('/user/sell-my-club');

  await expect(page.getByRole('heading', { name: 'Login' })).toBeVisible();
  await expect(page).toHaveURL(/\/login/);
});
