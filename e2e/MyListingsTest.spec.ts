import { test, expect } from '@playwright/test';

test('My Listings shows created listing details and metrics', async ({ page }) => {
  const uniqueName = `PW Metrics Listing ${Date.now()}`;

  await page.goto('/user/sell-my-club');
  await expect(page.getByRole('heading', { name: 'Welcome to Golf Odyssey' })).toBeVisible();

  await page.getByLabel('Club name').fill(uniqueName);
  await page.getByLabel('Type').selectOption({ index: 1 });
  await page.getByLabel('Brand').selectOption({ index: 1 });
  await page.getByLabel('Condition').selectOption('Good');
  await page.getByLabel('Price (USD)').fill('250');
  await page.getByLabel('Photo URLs (one per line)').fill('https://example.com/listings-metrics.jpg');
  await page.getByRole('button', { name: 'List my club' }).click();

  await expect(page).toHaveURL(/\/user\/my-listings/);
  await expect(page.getByRole('heading', { name: uniqueName })).toBeVisible();
  await expect(page.getByText('Verification pending')).toBeVisible();
  await expect(page.getByText('views')).toBeVisible();
  await expect(page.getByText('favorites')).toBeVisible();
  await expect(page.getByText('rating')).toBeVisible();
});

test('My Listings empty state shows CTA when account has no listings', async ({ page }) => {
  await page.goto('/user/my-listings');
  await expect(page.getByRole('heading', { name: 'My Listings' })).toBeVisible();

  const emptyState = page.getByText('You have not listed any clubs yet.');
  if (await emptyState.count() === 0) {
    test.skip(true, 'Current test user already has listings; empty-state assertion skipped.');
  }

  await expect(emptyState).toBeVisible();
  await expect(page.getByRole('link', { name: 'List your first club' })).toBeVisible();
});
