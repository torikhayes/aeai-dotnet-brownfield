import { test as setup, expect } from '@playwright/test';
import { STORAGE_STATE } from '../playwright.config';
import { assert } from 'console';

assert(process.env.USERNAME1, 'USERNAME1 is not set');
assert(process.env.PASSWORD, 'PASSWORD is not set');

setup('Login', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Tee Up. Sell. Score.' })).toBeVisible();

  await page.getByLabel('Sign in').click();
  await expect(page.getByPlaceholder('Username')).toBeVisible();
  await expect(page.getByPlaceholder('Password')).toBeVisible();

  await page.getByPlaceholder('Username').fill(process.env.USERNAME1!);
  await page.getByPlaceholder('Password').fill(process.env.PASSWORD!);
  await page.getByRole('button', { name: 'Login' }).click();
  await expect(page.getByRole('heading', { name: 'Tee Up. Sell. Score.' })).toBeVisible();
  await page.context().storageState({ path: STORAGE_STATE });
})
