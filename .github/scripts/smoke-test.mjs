// End-to-end smoke test against the running docker compose stack.
// Walks through the main guest flow and saves screenshots of each page.
import { chromium } from 'playwright';
import { mkdirSync } from 'node:fs';

const baseUrl = process.env.BASE_URL ?? 'http://localhost:8080';
const outDir = process.env.SCREENSHOT_DIR ?? 'screenshots';
mkdirSync(outDir, { recursive: true });

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1366, height: 900 } });
const shot = (name) => page.screenshot({ path: `${outDir}/${name}.png`, fullPage: true });
const fail = (message) => { throw new Error(message); };

// 1. Catalog
await page.goto(baseUrl, { waitUntil: 'networkidle' });
const productCount = await page.locator('.product-card').count();
if (productCount === 0) fail('Home page shows no products.');
await shot('home');

// 2. Search
await page.goto(`${baseUrl}/?query=Bike&sort=price_asc`, { waitUntil: 'networkidle' });
await shot('search');

// 3. Product details
await page.goto(baseUrl, { waitUntil: 'networkidle' });
const firstName = (await page.locator('.product-title').first().innerText()).trim();
await page.locator('.product-title').first().click();
await page.waitForLoadState('networkidle');
await shot('product-details');

// 4. Guest cart: add on the home page, then open the cart in a new request.
await page.goto(baseUrl, { waitUntil: 'networkidle' });
await page.locator('.product-card form button[type=submit]').first().click();
await page.waitForLoadState('networkidle');
await page.goto(`${baseUrl}/Cart`, { waitUntil: 'networkidle' });
const cartText = await page.locator('main, body').first().innerText();
if (!cartText.includes(firstName)) fail(`Guest cart lost the item "${firstName}" between requests.`);
await shot('cart');

// 5. Assistant and account pages render (Ollama is not running in CI).
await page.goto(`${baseUrl}/Assistant`, { waitUntil: 'networkidle' });
await shot('assistant');
await page.goto(`${baseUrl}/Identity/Account/Register`, { waitUntil: 'networkidle' });
await shot('register');

await browser.close();
console.log(`Smoke test passed: ${productCount} products, guest cart kept "${firstName}".`);
