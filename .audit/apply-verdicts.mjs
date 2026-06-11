// Applies .audit/verdicts.json to .audit/findings/<CAT>.json -> <CAT>.verified.json
import { readFileSync, writeFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const AUDIT = dirname(fileURLToPath(import.meta.url));
const CATS = ["BUG", "SEC", "UX", "FEAT", "ENH", "PERF", "ARCH"];
const SEVS = new Set(["critical", "high", "medium", "low"]);

const verdicts = new Map(
  JSON.parse(readFileSync(join(AUDIT, "verdicts.json"), "utf8")).map(v => [v.id, v])
);

const refuted = [];
let kept = 0, downgraded = 0, upgraded = 0, unverified = 0;
const SEV_RANK = { critical: 0, high: 1, medium: 2, low: 3 };

for (const cat of CATS) {
  const items = JSON.parse(readFileSync(join(AUDIT, "findings", `${cat}.json`), "utf8"));
  const out = [];
  for (const f of items) {
    const v = verdicts.get(f.id);
    if (!v) { unverified++; out.push(f); continue; }
    if (v.verdict === "refuted") { refuted.push({ id: f.id, title: f.title, why: v.notes }); continue; }
    let notes = v.notes || "";
    if (v.corrected_lines) notes += (notes ? " " : "") + `[updated refs: ${v.corrected_lines}]`;
    f.verification = { verdict: v.verdict, confidence: v.confidence, notes };
    if (v.revised_severity && v.revised_severity !== "unchanged" && SEVS.has(v.revised_severity) && v.revised_severity !== f.severity) {
      if (SEV_RANK[v.revised_severity] > SEV_RANK[f.severity]) downgraded++; else upgraded++;
      f.verification.notes = `[severity ${f.severity} -> ${v.revised_severity}] ` + f.verification.notes;
      f.severity = v.revised_severity;
    }
    kept++;
    out.push(f);
  }
  writeFileSync(join(AUDIT, "findings", `${cat}.verified.json`), JSON.stringify(out, null, 1));
  console.log(`${cat}: ${items.length} -> ${out.length} (${items.length - out.length} refuted)`);
}
writeFileSync(join(AUDIT, "refuted.json"), JSON.stringify(refuted, null, 1));
console.log(`kept ${kept} verified + ${unverified} unverified, refuted ${refuted.length}, severity downgrades ${downgraded}, upgrades ${upgraded}`);
