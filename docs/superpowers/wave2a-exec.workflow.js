export const meta = {
  name: 'flow-canvas-wave2a-exec',
  description: 'Execute Wave 2a (Node & Icon redesign, 8 tasks) of the Flow Canvas plan via sequential implementer+reviewer subagents',
  phases: [
    { title: 'Task1-Icons', detail: 'Vendor Lucide inline icons (BlockIcon.tsx)' },
    { title: 'Task2-Tokens', detail: 'Node-surface + rail + per-category icon tokens' },
    { title: 'Task3-Card', detail: 'BaseBlock accent-rail card (neutral surface + icon chip + outlined badge)' },
    { title: 'Task4-Specs', detail: 'Node-redesign + icon-coverage Playwright specs' },
    { title: 'Task5-StartNode', detail: 'StartNode rail parity + optional Palette icons' },
    { title: 'Task6-BandsCore', detail: 'Branch tokens + computeBranchBands + uiSlice toggle' },
    { title: 'Task7-BandsLayer', detail: 'BranchBandsLayer ViewportPortal + Properties hex->token' },
    { title: 'Task8-Integration', detail: 'Full e2e + parity + dist gate + embedded rebuild' },
  ],
}

const REPO = 'C:/Users/nos/source/repos/nosmircss/Test/SSH_Helper'
const PLAN = 'docs/superpowers/plans/2026-05-29-flow-canvas-wave-2a-node-icon-redesign.md'

const ENV = `
ENVIRONMENT:
- Repo root: ${REPO} (Windows; Bash + Edit/Write/Read/Grep tools). Use forward-slash paths.
- FlowCanvas SPA at ${REPO}/FlowCanvas. Frontend commands run from there: \`cd ${REPO}/FlowCanvas && npm run build\` (tsc && vite build, must exit 0), \`npx playwright test --workers=1 e2e/<spec>\`, \`npm run test:e2e:parity\`, \`npm run test:e2e:dist\`. Playwright chromium is installed; dev server auto-starts.
- Wave 2a is FRONTEND-ONLY for Tasks 1-7 (no C# edits). Task 8 runs \`dotnet build SSH_Helper.sln\` from repo root (the BuildFlowCanvas target re-embeds FlowCanvas/dist; ~1-2 min, expected).
- BASELINE is GREEN (Wave 1 committed): npm run build clean; the live no-hex token-sweep gate passes; parity passes. KEEP it green.
- BADGE micro-decision is RESOLVED: ship the OUTLINED badge variant (transparent bg + mix(colors.border,40) outline). The node-redesign spec asserts the outlined variant.
- If a task step is an interactive MANUAL GUI SMOKE (launch the desktop app and click), you CANNOT automate it — instead confirm \`dotnet build SSH_Helper.sln\` succeeds, record the smoke as PENDING-HUMAN in concerns, and complete the automated steps. Do NOT report BLOCKED merely because GUI smoke cannot be automated.
`

const DECISIONS = `
CONTRACT (FLOW_CANVAS_ENHANCEMENTS.md decisions): dark-only; NO hardcoded hex/rgba outside the token layer tokens.css/tokens.ts (a LIVE Playwright gate e2e/flow-canvas-token-sweep.spec.ts enforces this AND rejects malformed var()+alpha concat). For translucency use the mix(color,pct) helper from tokens.ts (color-mix over transparent) — NEVER string-concat like \`\${var}55\` (that bug already bit Wave 1). All Wave 2a changes are VISUAL/render-only and MUST NOT change exported YAML: exportGraph.ts serializes node.data.props only; do not write styling/transient state onto node.data; parity specs (preset-parity/preset-negative/gesture-smoke/connection-guards + flow-canvas-parity) MUST stay green. INVARIANTS to preserve byte-for-byte: the accent rail is an absolutely-positioned CHILD span inside the first-child container div (NOT a wrapper/CSS border) so \`.react-flow__node > div:first-child\` stays the animated card the reduced-motion spec checks and the rail never enters the exec/heat box-shadow stack; keep exec-state precedence (running pulse / success+error glow / selection / heatmap ring), duration badge, breakpoint gutter, all four handles, child/disabled styling. StartNode stays source-only (connection-guards invariant). Framer Motion / motion stays DEFERRED to Wave 2b — add NO animation dependency in 2a. Icons are hand-vendored inline SVG (Decision #9) — no lucide-react dependency.
`

const IMPL_SCHEMA = {
  type: 'object', additionalProperties: false,
  properties: {
    status: { type: 'string', enum: ['DONE', 'DONE_WITH_CONCERNS', 'BLOCKED', 'NEEDS_CONTEXT'] },
    summary: { type: 'string' },
    filesChanged: { type: 'array', items: { type: 'string' } },
    commitSha: { type: 'string' },
    verificationPassed: { type: 'boolean' },
    verification: { type: 'string' },
    deviations: { type: 'array', items: { type: 'string' } },
    concerns: { type: 'array', items: { type: 'string' } },
  },
  required: ['status', 'summary', 'filesChanged', 'commitSha', 'verificationPassed', 'verification'],
}

const REVIEW_SCHEMA = {
  type: 'object', additionalProperties: false,
  properties: {
    specCompliant: { type: 'boolean' },
    specIssues: { type: 'array', items: { type: 'string' } },
    qualityApproved: { type: 'boolean' },
    qualityIssues: { type: 'array', items: { type: 'string' } },
    minorNotes: { type: 'array', items: { type: 'string' } },
    verdict: { type: 'string', enum: ['PASS', 'NEEDS_FIX', 'BLOCKED'] },
  },
  required: ['specCompliant', 'specIssues', 'qualityApproved', 'qualityIssues', 'verdict'],
}

const TASKS = [
  { n: 1, phase: 'Task1-Icons', title: 'Author BlockIcon.tsx (vendored stroke-icon map + fallback)' },
  { n: 2, phase: 'Task2-Tokens', title: 'Add the node + category-icon tokens to tokens.css / tokens.ts' },
  { n: 3, phase: 'Task3-Card', title: 'Rebuild BaseBlock — rail, neutral surface, icon chip, softened badge' },
  { n: 4, phase: 'Task4-Specs', title: 'Node-redesign Playwright spec + un-fixme the icon spec' },
  { n: 5, phase: 'Task5-StartNode', title: 'StartNode rail parity + optional Palette icons' },
  { n: 6, phase: 'Task6-BandsCore', title: 'Branch tokens + branchBands.ts derivation + uiSlice toggle' },
  { n: 7, phase: 'Task7-BandsLayer', title: 'BranchBandsLayer + mount + Properties hex->token harmonization' },
  { n: 8, phase: 'Task8-Integration', title: 'Full-suite verification + embedded dist rebuild' },
]

function implementerPrompt(task, isFix, reviewFeedback, priorSha) {
  if (isFix) {
    return `You are fixing review issues on Task ${task.n} ("${task.title}") of the Flow Canvas Wave 2a plan. Work was committed at ${priorSha}.\n\n` +
      `Open ${REPO}/${PLAN} and re-read the "### Task ${task.n}:" section. Address EVERY issue below: edit the code, re-run the task verification commands until green, and AMEND the task commit (\`git add <files> && git commit --amend --no-edit\`). Do NOT push.\n\n` +
      `REVIEW ISSUES TO FIX:\n${reviewFeedback}\n\n${ENV}\n${DECISIONS}\n\n` +
      `Return structured status (commitSha = \`git rev-parse HEAD\` after amend).`
  }
  return `You are an implementer executing ONE task of a vetted, self-reviewed plan. The plan has exact code blocks — use them verbatim.\n\n` +
    `STEP 0: Open ${REPO}/${PLAN}. Read the top (Goal/Architecture/File Structure/"Verified facts") for context, THEN read ONLY the "### Task ${task.n}:" section (stop at the next "### Task"). Implement EXACTLY that task — no other task.\n\n` +
    `TASK: Task ${task.n} — ${task.title}\n\n${ENV}\n${DECISIONS}\n\n` +
    `RULES:\n` +
    `- Follow the steps in order; apply given code verbatim. Where a step is TDD, do it. Ship the OUTLINED badge variant (resolved).\n` +
    `- Run EXACTLY the task Verify commands, AND always \`cd ${REPO}/FlowCanvas && npm run build\` (exit 0). For Task 8 also run \`npm run test:e2e:dist\` and \`dotnet build SSH_Helper.sln\`. Paste real output tails into \`verification\`. If something fails, FIX and re-run until green; report BLOCKED only if truly impossible.\n` +
    `- The live no-hex token-sweep gate must stay green: any new color is a --fc-* token in tokens.css; for alpha use mix()/color-mix, never \`\${var}<hex>\`. The parity specs must stay 7/7 (run \`npm run test:e2e:parity\`).\n` +
    `- Commit per the task Commit step: \`git add\` ONLY the files you changed (never \`git add -A\`/unrelated untracked files; do NOT modify .gitignore). Use the task commit message. Do NOT push.\n` +
    `- verificationPassed=true ONLY if every required build/test/spec is green. commitSha = \`git rev-parse HEAD\`.\n` +
    `Return structured status.`
}

function reviewPrompt(task, sha) {
  return `You are a strict reviewer for Task ${task.n} ("${task.title}") of the Flow Canvas Wave 2a plan. Two stages: spec-compliance FIRST, then code quality (only if spec-compliant).\n\n` +
    `1. Open ${REPO}/${PLAN}, read the "### Task ${task.n}:" section — that is the spec.\n` +
    `2. Inspect the committed change: \`cd ${REPO} && git show ${sha} --stat\` then \`git show ${sha}\`. Read touched files as needed.\n\n${ENV}\n${DECISIONS}\n\n` +
    `STAGE 1 — Spec compliance: faithful to the steps (right files/code, nothing missing/extra/out-of-scope)? Concrete specIssues. HARD checks: NO raw hex or malformed var()+alpha concat outside the token layer; no styling/transient state written onto node.data; rail is a child span (first-child div unchanged); exec/heat box-shadow stack + handles + breakpoint + duration badge preserved; StartNode still source-only; no animation dependency added.\n` +
    `STAGE 2 — Code quality (only if spec-compliant): correctness, no dead code, follows patterns, no accidental behavior/round-trip change. Blocking qualityIssues only; nits to minorNotes.\n` +
    `verdict=PASS only if specCompliant AND no blocking qualityIssues. NEEDS_FIX for spec/blocking-quality issues. BLOCKED only if task/plan is fundamentally broken. Spot-check the claimed-green verification in the diff.`
}

const results = []
let aborted = null

for (const task of TASKS) {
  phase(task.phase)
  log(`Task ${task.n}: implementing — ${task.title}`)

  let impl = await agent(implementerPrompt(task, false), { label: `impl:task${task.n}`, phase: task.phase, schema: IMPL_SCHEMA })

  if (impl.status === 'BLOCKED' || impl.status === 'NEEDS_CONTEXT') {
    aborted = { task: task.n, stage: 'implement', reason: impl.status, detail: impl.summary }
    results.push({ task: task.n, title: task.title, ok: false, impl })
    log(`Task ${task.n} ${impl.status} — aborting: ${impl.summary}`)
    break
  }

  log(`Task ${task.n}: reviewing ${impl.commitSha}`)
  let review = await agent(reviewPrompt(task, impl.commitSha), { label: `review:task${task.n}`, phase: task.phase, schema: REVIEW_SCHEMA })

  if (review.verdict === 'NEEDS_FIX') {
    const feedback = [...(review.specIssues || []).map((s) => 'SPEC: ' + s), ...(review.qualityIssues || []).map((s) => 'QUALITY: ' + s)].join('\n')
    log(`Task ${task.n}: review found issues — dispatching fix`)
    const fix = await agent(implementerPrompt(task, true, feedback, impl.commitSha), { label: `fix:task${task.n}`, phase: task.phase, schema: IMPL_SCHEMA })
    const newSha = (fix && fix.commitSha) ? fix.commitSha : impl.commitSha
    review = await agent(reviewPrompt(task, newSha), { label: `re-review:task${task.n}`, phase: task.phase, schema: REVIEW_SCHEMA })
    impl = { ...impl, commitSha: newSha, verificationPassed: fix ? fix.verificationPassed : impl.verificationPassed, filesChanged: fix?.filesChanged || impl.filesChanged, concerns: [...(impl.concerns || []), ...((fix && fix.concerns) || [])] }
  }

  const ok = review.verdict === 'PASS' && impl.verificationPassed === true
  results.push({ task: task.n, title: task.title, ok, commitSha: impl.commitSha, filesChanged: impl.filesChanged, review, deviations: impl.deviations || [], concerns: impl.concerns || [] })

  if (!ok) {
    aborted = { task: task.n, stage: 'review', reason: review.verdict, detail: [...(review.specIssues || []), ...(review.qualityIssues || [])].join('; ') || 'verification not green' }
    log(`Task ${task.n} did not pass (verdict=${review.verdict}, verifPassed=${impl.verificationPassed}) — aborting`)
    break
  }
  log(`Task ${task.n} PASS (${impl.commitSha})`)
}

return {
  wave: 'Wave 2a — Node & Icon Redesign (Tasks 1-8)',
  completed: results.filter((r) => r.ok).map((r) => `Task ${r.task}: ${r.title} (${r.commitSha})`),
  aborted,
  allPassed: !aborted && results.length === TASKS.length && results.every((r) => r.ok),
  pendingHumanSmoke: results.flatMap((r) => (r.concerns || []).filter((c) => /PENDING-HUMAN|manual smoke/i.test(c)).map((c) => `Task ${r.task}: ${c}`)),
  results,
}
