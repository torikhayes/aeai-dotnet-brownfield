import { test, expect } from '@playwright/test';

test('Browse Items', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Tee Up. Sell. Score.' })).toBeVisible();

  await page.getByRole('link', { name: 'Driver Callaway Club 001' }).click(); 
  await page.getByRole('heading', { name: 'Driver Callaway Club 001' }).click();
  
  //Expect
  await expect(page.getByRole('heading', { name: 'Driver Callaway Club 001' })).toBeVisible();
});