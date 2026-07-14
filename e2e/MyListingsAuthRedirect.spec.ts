import { test, expect } from '@playwright/test';

test('Unauthenticated user is redirected from My Listings to Login', async ({ page }) => {
  await page.goto('/user/my-listings');

  await expect(page.getByRole('heading', { name: 'Login' })).toBeVisible();
  await expect(page).toHaveURL(/\/login/);
});
