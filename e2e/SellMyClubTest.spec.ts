import { test, expect } from '@playwright/test';

test('Sell My Club requires at least one photo URL', async ({ page }) => {
  await page.goto('/user/sell-my-club');

  await expect(page.getByRole('heading', { name: 'Sell My Club' })).toBeVisible();

  const uniqueName = `PW Photo Validation ${Date.now()}`;

  await page.getByLabel('Club name').fill(uniqueName);
  await page.getByLabel('Type').selectOption({ index: 1 });
  await page.getByLabel('Brand').selectOption({ index: 1 });
  await page.getByLabel('Condition').selectOption('Good');
  await page.getByLabel('Price (USD)').fill('225');
  await page.getByLabel('Description').fill('Playwright validation test listing.');

  await page.getByRole('button', { name: 'List my club' }).click();

  await expect(page.getByText('Please provide at least one photo URL.')).toBeVisible();
});

test('Sell My Club creates listing and redirects to My Listings', async ({ page }) => {
  await page.goto('/user/sell-my-club');

  await expect(page.getByRole('heading', { name: 'Sell My Club' })).toBeVisible();

  const uniqueName = `PW New Listing ${Date.now()}`;

  await page.getByLabel('Club name').fill(uniqueName);
  await page.getByLabel('Type').selectOption({ index: 1 });
  await page.getByLabel('Brand').selectOption({ index: 1 });
  await page.getByLabel('Condition').selectOption('Excellent');
  await page.getByLabel('Manufacture year').fill('2022');
  await page.getByLabel('Price (USD)').fill('399.99');
  await page.getByLabel('Description').fill('Playwright happy-path listing for Sell My Club.');
  await page.getByLabel('Tags (comma-separated)').fill('playwright,test');
  await page.getByLabel('Photo URLs (one per line)').fill('https://example.com/club-photo.jpg');

  await page.getByRole('button', { name: 'List my club' }).click();

  await expect(page).toHaveURL(/\/user\/my-listings/);
  await expect(page.getByRole('heading', { name: 'My Listings' })).toBeVisible();
  await expect(page.getByRole('heading', { name: uniqueName })).toBeVisible();
  await expect(page.getByText('Active')).toBeVisible();
});
