// Headless smoke-test of the audit report UI using FlowCanvas's playwright install.
const { createRequire } = require("module");
const path = require("path");
const ROOT = path.resolve(__dirname, "..");
const req = createRequire(path.join(ROOT, "FlowCanvas", "package.json"));
const { chromium } = req("@playwright/test");

const target = process.argv[2] || path.join(__dirname, "report-test.html");
const shot = process.argv[3] || path.join(__dirname, "report-test.png");

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  const errors = [];
  page.on("pageerror", (e) => errors.push("pageerror: " + e.message));
  page.on("dialog", (d) => { errors.push("UNEXPECTED DIALOG (xss?): " + d.message()); d.dismiss(); });
  page.on("console", (m) => { if (m.type() === "error") errors.push("console: " + m.text()); });

  await page.goto("file:///" + target.replace(/\\/g, "/"));
  const fail = (m) => { console.error("FAIL: " + m); process.exitCode = 1; };

  const cards = await page.locator(".card").count();
  console.log("cards rendered:", cards);
  if (cards < 1) fail("no cards rendered");

  // open first card, approve it
  await page.locator(".card .card-head").first().click();
  await page.locator(".card.open .dbtn.approve").first().click();
  const appCount = await page.locator("#mApp").textContent();
  if (appCount !== "1") fail("meter approved count expected 1, got " + appCount);

  // notes persistence via localStorage
  await page.locator(".card.open .notes").first().fill("test-note-xyz");
  await page.reload();
  await page.locator(".card .card-head").first().click();
  const notes = await page.locator(".card.open .notes").first().inputValue();
  if (notes !== "test-note-xyz") fail("notes did not persist, got: " + notes);
  const appAfter = await page.locator("#mApp").textContent();
  if (appAfter !== "1") fail("decision did not persist after reload");

  // generate prompt contains approved id + note
  await page.locator("#genBtn").click();
  const prompt = await page.locator("#modalText").inputValue();
  if (!prompt.includes("test-note-xyz")) fail("prompt missing notes");
  if (!/#### \w+-\d+/.test(prompt)) fail("prompt missing item heading");
  console.log("prompt length:", prompt.length);
  // verdicts tab
  await page.locator(".mtab[data-tab=verdicts]").click();
  const verd = await page.locator("#modalText").inputValue();
  if (!verd.includes('"decision": "approve"')) fail("verdicts json missing approval");
  await page.locator("#closeModal").click();

  // XSS check: the literal <script> in UX-001 text must be inert (no dialog fired) and visible as text
  await page.locator("#search").fill("xss-test");
  const visibleAfterSearch = await page.locator(".card").count();
  if (visibleAfterSearch !== 1) fail("search expected 1 card, got " + visibleAfterSearch);
  await page.locator(".card .card-head").first().click();
  const bodyText = await page.locator(".card.open .card-body").first().textContent();
  if (!bodyText.includes("alert('xss-test')")) fail("escaped script tag not shown as text");

  // mockup iframe renders
  const iframeCount = await page.locator(".card.open iframe[data-mock]").count();
  if (iframeCount !== 1) fail("mockup iframe missing");

  // filters: severity chip
  await page.locator("#search").fill("");
  await page.locator("#sevChips .chip").first().click(); // critical
  const critOnly = await page.locator(".card").count();
  console.log("critical-only cards:", critOnly);

  await page.screenshot({ path: shot, fullPage: true });
  console.log("screenshot:", shot);
  if (errors.length) { console.error("PAGE ERRORS:"); errors.forEach((e) => console.error("  " + e)); process.exitCode = 1; }
  else console.log(process.exitCode ? "RESULT: FAIL" : "RESULT: PASS");
  await browser.close();
})();
