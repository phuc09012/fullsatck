const { chromium } = require('playwright');

const TARGET_URL = 'http://localhost:4173';

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1200 } });
  page.on('console', msg => console.log('[console]', msg.type(), msg.text()));
  page.on('pageerror', err => console.log('[pageerror]', err.message));
  await page.goto(TARGET_URL, { waitUntil: 'networkidle' });
  console.log('title:', await page.title());
  console.log('body sample:', (await page.locator('body').innerText()).slice(0, 300));
  await page.screenshot({ path: 'E:/btl-fullstack/bundle-restore-check.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload({ waitUntil: 'networkidle' });
  console.log('mobile sample:', (await page.locator('body').innerText()).slice(0, 300));
  await page.screenshot({ path: 'E:/btl-fullstack/bundle-restore-mobile.png', fullPage: true });
  await browser.close();
})();
