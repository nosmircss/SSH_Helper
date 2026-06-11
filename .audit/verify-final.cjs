// Final-report smoke test (real data): mockup iframes, filters, modal, screenshot.
const { createRequire } = require("module");
const path = require("path");
const ROOT = path.resolve(__dirname, "..");
const req = createRequire(path.join(ROOT, "FlowCanvas", "package.json"));
const { chromium } = req("@playwright/test");

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
  const errors = [];
  page.on("pageerror", (e) => errors.push("pageerror: " + e.message));
  page.on("dialog", (d) => { errors.push("UNEXPECTED DIALOG: " + d.message()); d.dismiss(); });
  await page.goto("file:///" + path.join(ROOT, "Enhancement_Report_v2.html").replace(/\\/g, "/"));
  const fail = (m) => { console.error("FAIL: " + m); process.exitCode = 1; };

  console.log("cards:", await page.locator(".card").count());

  // find a card with a mockup badge, open it, confirm iframe renders content
  const mockCard = page.locator(".card", { has: page.locator(".b-mock") }).first();
  await mockCard.locator(".card-head").click();
  const ifr = mockCard.locator("iframe[data-mock]");
  if (await ifr.count() !== 1) fail("mockup iframe missing on flagged card");
  const frameBody = await ifr.first().contentFrame().locator("body").innerText().catch(() => "");
  console.log("mockup iframe text length:", frameBody.length);
  if (frameBody.length < 20) fail("mockup iframe looks empty");

  // verifier badge present on most cards
  const verified = await page.locator(".b-ver").count();
  console.log("verification badges:", verified);
  if (verified < 300) fail("expected 300+ verification badges, got " + verified);

  // category tile filter works
  await page.locator(".stat").first().click(); // SEC
  const secCount = await page.locator(".card:visible").count();
  console.log("SEC-only cards:", secCount);
  if (secCount !== 16) fail("SEC filter expected 16, got " + secCount);
  await page.locator(".stat").first().click(); // un-filter

  // approve two specific cards, generate prompt
  await page.locator("#collapseAll").click();
  for (const i of [0, 1]) {
    const c = page.locator(".card").nth(i);
    await c.locator(".card-head").click();
    await c.locator(".dbtn.approve").click();
  }
  await page.locator("#genBtn").click();
  const prompt = await page.locator("#modalText").inputValue();
  if (!prompt.includes("## Approved (2)")) fail("prompt approved count wrong");
  console.log("prompt chars:", prompt.length);
  await page.locator("#closeModal").click();

  // clean the test decisions so the user starts fresh
  await page.evaluate(() => localStorage.removeItem("ssh-helper-audit-v2-decisions"));
  await page.reload();
  const und = await page.locator("#mUnd").textContent();
  if (und !== "313") fail("expected 313 undecided after reset, got " + und);

  await page.locator(".card .card-head").first().click();
  await page.screenshot({ path: path.join(__dirname, "report-final.png"), fullPage: false });
  console.log("screenshot saved");
  if (errors.length) { console.error("PAGE ERRORS:"); errors.forEach((e) => console.error("  " + e)); process.exitCode = 1; }
  console.log(process.exitCode ? "RESULT: FAIL" : "RESULT: PASS");
  await browser.close();
})();
