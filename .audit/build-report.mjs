// Builds Enhancement_Report_v2.html from .audit/findings/*.json + .audit/mockups/*.html
// Usage: node .audit/build-report.mjs [--out Enhancement_Report_v2.html]
import { readFileSync, writeFileSync, readdirSync, existsSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const AUDIT = dirname(fileURLToPath(import.meta.url));
const ROOT = join(AUDIT, "..");
const CATS = ["SEC", "BUG", "UX", "FEAT", "ENH", "PERF", "ARCH"];
const SEVS = new Set(["critical", "high", "medium", "low"]);
const EFFS = new Set(["S", "M", "L", "XL"]);

const outArg = process.argv.indexOf("--out");
const OUT = join(ROOT, outArg > -1 ? process.argv[outArg + 1] : "Enhancement_Report_v2.html");
const FDIR = process.env.AUDIT_FINDINGS_DIR ? join(ROOT, process.env.AUDIT_FINDINGS_DIR) : join(AUDIT, "findings");
const MDIR = process.env.AUDIT_MOCKUPS_DIR ? join(ROOT, process.env.AUDIT_MOCKUPS_DIR) : join(AUDIT, "mockups");

function git(args) {
  try { return execFileSync("git", args, { cwd: ROOT, encoding: "utf8" }).trim(); } catch { return "unknown"; }
}

// ── load findings: prefer <CAT>.verified.json over <CAT>.json ──
const findings = [];
const problems = [];
for (const cat of CATS) {
  const verified = join(FDIR, `${cat}.verified.json`);
  const plain = join(FDIR, `${cat}.json`);
  const file = existsSync(verified) ? verified : existsSync(plain) ? plain : null;
  if (!file) { problems.push(`no findings file for ${cat}`); continue; }
  let items;
  try { items = JSON.parse(readFileSync(file, "utf8")); } catch (e) { problems.push(`${file}: parse error ${e.message}`); continue; }
  if (!Array.isArray(items)) { items = items.items || items.findings || []; }
  for (const f of items) {
    if (!f.id || !f.title || !f.existing_state || !f.recommendation) { problems.push(`${cat}: dropped malformed item ${f.id || "(no id)"}`); continue; }
    f.category = cat;
    if (!SEVS.has(f.severity)) { problems.push(`${f.id}: bad severity '${f.severity}' -> medium`); f.severity = "medium"; }
    if (!EFFS.has(f.effort)) { problems.push(`${f.id}: bad effort '${f.effort}' -> M`); f.effort = "M"; }
    f.area = f.area || "general";
    f.evidence = Array.isArray(f.evidence) ? f.evidence : [];
    f.user_benefit = f.user_benefit || "";
    f.mockup_idea = f.mockup_idea || "";
    f.merged_from = f.merged_from || 1;
    findings.push(f);
  }
}
const dupes = findings.map(f => f.id).filter((id, i, a) => a.indexOf(id) !== i);
if (dupes.length) problems.push(`DUPLICATE IDS: ${[...new Set(dupes)].join(", ")}`);

// ── load mockups ──
const mockups = {};
const mockDir = MDIR;
if (existsSync(mockDir)) {
  for (const f of readdirSync(mockDir).filter(f => f.endsWith(".html"))) {
    const id = f.replace(/\.html$/, "");
    if (findings.some(x => x.id === id)) mockups[id] = readFileSync(join(mockDir, f), "utf8");
    else problems.push(`mockup ${f} has no matching finding — skipped`);
  }
}

const meta = {
  generated: new Date().toISOString().slice(0, 16).replace("T", " ") + " UTC",
  branch: git(["rev-parse", "--abbrev-ref", "HEAD"]),
  commit: git(["rev-parse", "--short", "HEAD"]),
  agents: process.env.AUDIT_AGENTS || "300+",
  raw: process.env.AUDIT_RAW || findings.reduce((n, f) => n + (f.merged_from || 1), 0),
};

// escape for safe embedding inside a <script> block: no literal "</", no raw U+2028/U+2029
const LS = String.fromCharCode(0x2028), PS = String.fromCharCode(0x2029);
const safe = (o) => JSON.stringify(o)
  .replace(/</g, "\\u003c")
  .split(LS).join("\\u2028")
  .split(PS).join("\\u2029");

const template = readFileSync(join(AUDIT, "report-template.html"), "utf8");
const html = template
  .replace("__META_JSON__", () => safe(meta))
  .replace("__FINDINGS_JSON__", () => safe(findings))
  .replace("__MOCKUPS_JSON__", () => safe(mockups));

writeFileSync(OUT, html);
console.log(`OK ${OUT}`);
console.log(`  findings: ${findings.length} (${CATS.map(c => `${c}:${findings.filter(f => f.category === c).length}`).join(" ")})`);
console.log(`  mockups: ${Object.keys(mockups).length}`);
if (problems.length) { console.log(`  PROBLEMS (${problems.length}):`); problems.forEach(p => console.log(`   - ${p}`)); }
