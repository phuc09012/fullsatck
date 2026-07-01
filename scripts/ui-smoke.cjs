const path = require("path");
const fs = require("fs");
const { chromium } = require("C:/Users/Admin/.agents/skills/playwright/node_modules/playwright");

const appUrl = process.argv[2] || "http://localhost:4173";
const apiBase = process.env.VITE_API_BASE_URL || "http://localhost:5000";
const screenshotDir = path.join(process.cwd(), "dist", "smoke");
const appOrigin = new URL(appUrl).origin;
fs.mkdirSync(screenshotDir, { recursive: true });

async function ensureNoToast(page, label) {
  const toast = page.locator(".toast.error");
  if (await toast.count()) {
    const text = (await toast.first().textContent())?.trim();
    if (text) {
      throw new Error(`${label}: unexpected error toast: ${text}`);
    }
  }
}

async function login(page, email, password) {
  await page.waitForSelector(".auth-form input[type='email']", { timeout: 15000 });
  await page.fill(".auth-form input[type='email']", email);
  await page.fill(".auth-form input[type='password']", password);
  await page.click(".auth-form button[type='submit']");
  await page.waitForSelector("button.ghost-button.full-width", { timeout: 15000 });
  await page.waitForLoadState("networkidle");
}

async function openSection(page, index) {
  await page.locator(".menu .menu-item").nth(index).click();
  await page.waitForLoadState("networkidle");
}

async function main() {
  const consoleErrors = [];
  const failedRequests = [];
  const badApiResponses = [];

  const browser = await chromium.launch({
    headless: process.env.PLAYWRIGHT_HEADLESS === "1",
    slowMo: process.env.PLAYWRIGHT_HEADLESS === "1" ? 0 : 35,
  });

  const context = await browser.newContext({
    viewport: { width: 1440, height: 950 },
  });
  const page = await context.newPage();

  page.on("console", (msg) => {
    const text = msg.text();
    if (msg.type() === "error" && !text.startsWith("Failed to load resource:")) {
      consoleErrors.push(text);
    }
  });

  page.on("requestfailed", (request) => {
    const url = request.url();
    if (url.startsWith(appOrigin) || url.startsWith(apiBase)) {
      failedRequests.push(`${request.method()} ${url} :: ${request.failure()?.errorText}`);
    }
  });

  page.on("response", (response) => {
    if (response.url().startsWith(apiBase) && response.status() >= 400) {
      badApiResponses.push(`${response.status()} ${response.url()}`);
    }
  });

  try {
    await page.goto(appUrl, { waitUntil: "networkidle", timeout: 30000 });
    await page.evaluate(() => localStorage.clear());
    await page.reload({ waitUntil: "networkidle" });

    await login(page, "admin@library.local", "Admin@123");
    await openSection(page, 1);
    await page.waitForSelector("#books .book-card", { timeout: 15000 });
    await page.waitForSelector("#books .add-book-btn", { timeout: 15000 });
    await ensureNoToast(page, "admin");

    await page.click(".add-book-btn");
    await page.waitForSelector(".book-form-panel", { timeout: 10000 });
    await page.waitForSelector(".category-manager", { timeout: 10000 });
    const categoryOptionCount = await page.locator(".book-form select option").count();
    if (categoryOptionCount <= 1) {
      throw new Error("admin: book category dropdown has no managed options");
    }

    const tempCategoryName = `Smoke Delete ${Date.now()}`;
    await page.locator(".category-form input").first().fill(tempCategoryName);
    await page.locator(".category-form input").nth(1).fill("Temporary smoke-test category");
    await page.click(".category-form .primary-button");
    await page.waitForSelector(`button.category-chip:has-text("${tempCategoryName}")`, { timeout: 15000 });
    await page.once("dialog", (dialog) => dialog.accept());
    await page.getByLabel(`Xóa thể loại ${tempCategoryName}`).click();
    await page.waitForSelector(`button[aria-label="Xóa thể loại ${tempCategoryName}"]`, { state: "detached", timeout: 15000 });

    await page.click(".book-form-panel .ghost-button");
    await page.waitForSelector(".book-form-panel", { state: "detached", timeout: 10000 });
    await page.screenshot({ path: path.join(screenshotDir, "admin-dashboard.png"), fullPage: true });

    await openSection(page, 2);
    await page.waitForSelector("#transactions .transaction-table", { timeout: 15000 });

    await openSection(page, 3);
    await page.waitForSelector("#reports .policy-card", { timeout: 15000 });

    await openSection(page, 4);
    await page.waitForSelector("#accounts .info-card", { timeout: 15000 });

    await page.click("button.ghost-button.full-width");
    await page.waitForSelector(".auth-form", { timeout: 10000 });

    await login(page, "reader1@library.local", "Reader@123");
    await openSection(page, 1);
    await page.waitForSelector("#books .borrow-settings", { timeout: 15000 });
    await page.waitForSelector("#books .books-grid .book-card", { timeout: 15000 });
    await openSection(page, 3);
    await page.waitForSelector("#borrows .reader-summary", { timeout: 30000 });
    await page.waitForSelector("#borrows .reader-borrow-card", { timeout: 30000 });
    const borrowButtons = page.locator(".book-card button.primary-button.small:not([disabled])");
    if (await borrowButtons.count()) {
      await borrowButtons.first().click();
      await page.waitForSelector(".borrow-confirm-modal", { timeout: 10000 });
      const borrowModalText = (await page.locator(".borrow-confirm-modal").textContent()) || "";
      if (!borrowModalText.includes("Hạn trả dự kiến")) {
        throw new Error("reader: borrow confirmation modal does not show due date preview");
      }
      await page.selectOption(".borrow-confirm-modal select", "21");
      await page.locator(".borrow-confirm-modal").getByRole("button", { name: "Hủy" }).click();
      await page.waitForSelector(".borrow-confirm-modal", { state: "detached", timeout: 10000 });
    }
    await ensureNoToast(page, "reader");
    await page.screenshot({ path: path.join(screenshotDir, "reader-dashboard.png"), fullPage: true });

    await page.setViewportSize({ width: 390, height: 844 });
    await page.reload({ waitUntil: "networkidle" });
    await page.waitForSelector(".content", { timeout: 15000 });
    await openSection(page, 3);
    await page.waitForSelector("#borrows .reader-summary", { timeout: 30000 });
    await ensureNoToast(page, "mobile reader");
    await page.screenshot({ path: path.join(screenshotDir, "reader-mobile.png"), fullPage: true });

    if (consoleErrors.length || failedRequests.length || badApiResponses.length) {
      throw new Error([
        consoleErrors.length ? `Console errors:\n${consoleErrors.join("\n")}` : "",
        failedRequests.length ? `Failed requests:\n${failedRequests.join("\n")}` : "",
        badApiResponses.length ? `Bad API responses:\n${badApiResponses.join("\n")}` : "",
      ].filter(Boolean).join("\n\n"));
    }

    console.log("ALL_UI_SMOKE_TESTS_PASSED");
    console.log(`Screenshots: ${screenshotDir}`);
  }
  finally {
    await browser.close();
  }
}

main().catch((error) => {
  console.error(error.stack || error.message || error);
  process.exit(1);
});
