const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1200 } });
  page.on('console', (msg) => console.log('[console]', msg.type(), msg.text()));
  page.on('pageerror', (err) => console.log('[pageerror]', err.message));

  await page.goto('http://localhost:4173', { waitUntil: 'networkidle' });
  await page.screenshot({ path: 'E:/btl-fullstack/screens/01-home.png', fullPage: true });

  const loginForm = page.locator('form.auth-form');
  if (await loginForm.count()) {
    await loginForm.locator('input[type="email"]').fill('admin@library.local');
    await loginForm.locator('input[type="password"]').fill('Admin@123');
    await loginForm.locator('button').first().click();
    await page.waitForLoadState('networkidle');
    await page.screenshot({ path: 'E:/btl-fullstack/screens/02-after-login.png', fullPage: true });
  }

  await page.locator('#books').scrollIntoViewIfNeeded();
  await page.waitForTimeout(500);
  console.log('book cards visible:', await page.locator('.book-card').count());
  await page.screenshot({ path: 'E:/btl-fullstack/screens/03-books.png', fullPage: true });

  await page.locator('#transactions').scrollIntoViewIfNeeded();
  await page.waitForTimeout(300);
  await page.screenshot({ path: 'E:/btl-fullstack/screens/04-transactions.png', fullPage: true });

  await page.locator('#reports').scrollIntoViewIfNeeded();
  await page.waitForTimeout(300);
  await page.screenshot({ path: 'E:/btl-fullstack/screens/05-reports-summary.png', fullPage: true });
  await page.locator('#reports .filter-pill').filter({ hasText: 'Tài kho?n' }).click().catch(() => {});
  await page.waitForTimeout(300);
  await page.screenshot({ path: 'E:/btl-fullstack/screens/06-reports-accounts.png', fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload({ waitUntil: 'networkidle' });
  await page.screenshot({ path: 'E:/btl-fullstack/screens/07-mobile-home.png', fullPage: true });

  await browser.close();
})();
