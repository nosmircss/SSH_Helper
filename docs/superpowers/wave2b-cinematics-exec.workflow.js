export const meta = {
  name: 'flow-canvas-wave2b-cinematics',
  description: 'Execute Wave 2b Execution Cinematics (running halo / success checkmark / error shake+ripple / count-up, 7 tasks) via sequential implementer+reviewer subagents',
  phases: [
    { title: 'Task1-Token', detail: 'Add --fc-run-comet + --fc-glow-error-strong tokens (tokens.css)' },
    { title: 'Task2-CSS', detail: 'execution-cinematics.css keyframe sheet (@property + 6 keyframes + classes)' },
    { title: 'Task3-Node', detail: 'Running comet+breathing & error shake+ripple (class-driven) + cleanup (exec-pulse/spin/◌)' },
    { title: 'Task4-Check', detail: 'Success exec indicator: SVG checkmark draw-in + pop' },
    { title: 'Task5-Countup', detail: 'Live count-up duration badge (formatDuration + useRunningElapsed rAF ticker)' },
    { title: 'Task6-Specs', detail: 'flow-canvas-execution-cinematics spec + exec-state nodes in token-sweep' },
    { title: 'Task7-Integration', detail: 'Full e2e + parity + dist gate + dotnet embedded rebuild' },
  ],
}

const REPO = 'C:/Users/nos/source/repos/nosmircss/Test/SSH_Helper'
const PLAN = 'docs/superpowers/plans/2026-05-30-flow-canvas-wave-2b-execution-cinematics.md'

const ENV = `
ENVIRONMENT:
- Repo root: ${REPO} (Windows; the Bash tool is bash/git-bash — use POSIX tools like \`tail\`/\`grep\`, NOT PowerShell cmdlets like Select-Object. Use forward-slash paths).
- FlowCanvas SPA at ${REPO}/FlowCanvas. Frontend commands run from there: \`cd ${REPO}/FlowCanvas && npm run build\` (tsc && vite build, must exit 0), \`npx playwright test e2e/<spec>\`, \`npm run test:e2e:parity\` (already --workers=1), \`npm run test:e2e:dist\`. Playwright chromium is installed; the dev server auto-starts. Pipe long output through \`| tail -30\`.
- Execution Cinematics is FRONTEND-ONLY for Tasks 1-6 (no C# edits). Task 7 runs \`dotnet build SSH_Helper.sln\` from repo root (the BuildFlowCanvas target re-embeds FlowCanvas/dist; ~1-2 min, expected).
- BASELINE is GREEN (just confirmed): npm run build exit 0; live no-hex token-sweep passes; parity 22/22 under --workers=1. KEEP it green.
- KNOWN INFRA RACE (NOT a regression, do NOT report BLOCKED for it): the full parallel \`npm run test:e2e\` / \`test:e2e:dist\` show 1-2 failures/flakes in the parity-CLI specs (flow-canvas-gesture-smoke / preset-parity / connection-guards / properties-typing) because concurrent Playwright workers collide on the shared \`obj/SSH_Helper.dll\` (VBCSCompiler / Microsoft Defender lock). They ALWAYS pass serialized. Prove green with \`npm run test:e2e:parity\` (--workers=1); for the dist gate use \`npx playwright test --workers=1 --config playwright.preview.config.ts\` if the race appears.
- If a task step is an interactive MANUAL GUI SMOKE (launch the desktop app and click), you CANNOT automate it — instead confirm \`dotnet build SSH_Helper.sln\` succeeds, record the smoke as PENDING-HUMAN in concerns, and complete the automated steps. Do NOT report BLOCKED merely because GUI smoke cannot be automated.
`

const DECISIONS = `
CONTRACT (FLOW_CANVAS_ENHANCEMENTS.md decisions + the 2b Execution Cinematics design spec): dark-only; NO hardcoded hex/rgba/rgb/hsl outside the token layer tokens.css/tokens.ts (a LIVE Playwright gate e2e/flow-canvas-token-sweep.spec.ts enforces this on inline [style] attributes AND rejects malformed var()+alpha concat). For translucency use the mix(color,pct) helper from tokens.ts (color-mix over transparent) — NEVER string-concat like \`\${var}55\` (that bug bit Wave 1). The error ripple fade uses the \`transparent\` KEYWORD; the comet halo mask uses the \`black\` KEYWORD (alpha-mask shape, not a design color) — neither is hex. New colors are --fc-* tokens authored ONLY in tokens.css, and this cycle adds EXACTLY TWO: --fc-run-comet and --fc-glow-error-strong (no others).
All Wave 2b changes are VISUAL/render-only and MUST NOT change exported YAML: exportGraph.ts serializes node.data.props ONLY; do NOT write to node.data; do NOT touch exportGraph.ts / FlowCanvasBridge.cs; only READ node.data.execState + the blockTimings store Map. Parity specs (preset-parity/preset-negative/gesture-smoke/connection-guards) MUST stay 22/22 under --workers=1 — that green run is the round-trip proof.
KEY INVARIANTS: All four effects live in BaseBlock ONLY (StartNode is OUT OF SCOPE — leave it unchanged). RUNNING + ERROR are CLASS-DRIVEN (.fc-exec-running / .fc-exec-error) whose CSS animations own the card box-shadow/transform via the cascade; SUCCESS stays on the INLINE box-shadow path (\`0 0 10px var(--fc-glow-success)\`) so the Run Heatmap ring still stacks (heatTint is idle/success-only) — do NOT move success to a class. The breathing keyframe + the error ripple use \`animation-fill-mode: forwards\` so the reduced-motion blanket (which forces iteration-count:1) FREEZES them on a soft static glow instead of reverting to none — this is load-bearing, keep it. REDUCED MOTION: the comet child (.fc-run-halo) is NOT rendered (gated \`execState==='running' && !reducedMotion\`), and the live ticker does NOT tick (useRunningElapsed returns null) under reduced motion; the existing flow-canvas-reduced-motion + flow-canvas-run-timing + flow-canvas-node-redesign specs MUST stay green. CLEANUP: drop the \`◌\` spinner glyph; delete @keyframes exec-pulse; delete @keyframes spin ONLY after grep-confirming its sole animation consumer was the dropped glyph; remove the dead execGlowColors map. useRunningElapsed is a HOOK — it MUST be called BEFORE the \`if (!def)\` early return (rules of hooks). NO new npm runtime dependency — Framer Motion / motion stays DEFERRED; this cycle is fully static CSS/SVG + one rAF timer.
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
  { n: 1, phase: 'Task1-Token', title: 'Add the --fc-run-comet + --fc-glow-error-strong tokens to tokens.css' },
  { n: 2, phase: 'Task2-CSS', title: 'Create execution-cinematics.css (@property angle + 6 keyframes + helper classes)' },
  { n: 3, phase: 'Task3-Node', title: 'Running comet+breathing & error shake+ripple (class-driven) + cleanup (exec-pulse/spin/◌ glyph)' },
  { n: 4, phase: 'Task4-Check', title: 'Success exec indicator: swap ✓ for a drawn-in SVG checkmark' },
  { n: 5, phase: 'Task5-Countup', title: 'Live count-up duration badge (formatDuration + useRunningElapsed rAF ticker)' },
  { n: 6, phase: 'Task6-Specs', title: 'flow-canvas-execution-cinematics spec + exec-state nodes in token-sweep' },
  { n: 7, phase: 'Task7-Integration', title: 'Full-suite verification + embedded dist rebuild' },
]

function implementerPrompt(task, isFix, reviewFeedback, priorSha) {
  if (isFix) {
    return `You are fixing review issues on Task ${task.n} ("${task.title}") of the Flow Canvas Wave 2b Execution Cinematics plan. Work was committed at ${priorSha}.\n\n` +
      `Open ${REPO}/${PLAN} and re-read the "### Task ${task.n}:" section. Address EVERY issue below: edit the code, re-run the task verification commands until green, and AMEND the task commit (\`git add <files> && git commit --amend --no-edit\`). Do NOT push.\n\n` +
      `REVIEW ISSUES TO FIX:\n${reviewFeedback}\n\n${ENV}\n${DECISIONS}\n\n` +
      `Return structured status (commitSha = \`git rev-parse HEAD\` after amend).`
  }
  return `You are an implementer executing ONE task of a vetted, plan-checked plan. The plan has exact code blocks (under "Find:" / "Replace with:") — use them VERBATIM.\n\n` +
    `STEP 0: Open ${REPO}/${PLAN}. Read the top (Goal/Architecture/Verified facts/File Structure) for context, THEN read ONLY the "### Task ${task.n}:" section (stop at the next "### Task"). Implement EXACTLY that task — no other task. RE-READ each file right before editing it and match the "Find:" anchor exactly (it has been verified to match the live tree).\n\n` +
    `TASK: Task ${task.n} — ${task.title}\n\n${ENV}\n${DECISIONS}\n\n` +
    `RULES:\n` +
    `- Follow the steps in order; apply the given code verbatim. Where a step is a grep/verification, run it and confirm the expected result before proceeding.\n` +
    `- Run EXACTLY the task Verify commands, AND always \`cd ${REPO}/FlowCanvas && npm run build\` (exit 0). For Task 7 also run \`npm run test:e2e:dist\` (use \`npx playwright test --workers=1 --config playwright.preview.config.ts\` if the parity-CLI build-lock race appears) and \`dotnet build SSH_Helper.sln\`. Paste real output tails into \`verification\`. If something fails, FIX and re-run until green; report BLOCKED only if truly impossible.\n` +
    `- The live no-hex token-sweep gate must stay green: any new color is a --fc-* token in tokens.css (this cycle: ONLY --fc-run-comet + --fc-glow-error-strong); for alpha use mix()/color-mix, never \`\${var}<hex>\`; the CSS file uses only the \`black\` mask keyword + the \`transparent\` keyword. Parity must stay 22/22 (run \`npm run test:e2e:parity\`).\n` +
    `- Commit per the task Commit step: \`git add\` ONLY the files you changed (never \`git add -A\` of unrelated untracked files; do NOT modify .gitignore). Use the task commit message. Do NOT push. (Task 7's dist commit intentionally uses \`git add -A\` but you MUST first confirm \`git status\` shows nothing unexpected and never stage .gitignore or stray files.)\n` +
    `- verificationPassed=true ONLY if every required build/test/spec is green (the known parity-CLI parallel build-lock race is NOT a failure — prove green serialized). commitSha = \`git rev-parse HEAD\`.\n` +
    `Return structured status.`
}

function reviewPrompt(task, sha) {
  return `You are a strict reviewer for Task ${task.n} ("${task.title}") of the Flow Canvas Wave 2b Execution Cinematics plan. Two stages: spec-compliance FIRST, then code quality (only if spec-compliant).\n\n` +
    `1. Open ${REPO}/${PLAN}, read the "### Task ${task.n}:" section — that is the spec.\n` +
    `2. Inspect the committed change: \`cd ${REPO} && git show ${sha} --stat\` then \`git show ${sha}\`. Read touched files as needed.\n\n${ENV}\n${DECISIONS}\n\n` +
    `STAGE 1 — Spec compliance: faithful to the steps (right files/code, nothing missing/extra/out-of-scope)? Concrete specIssues. HARD checks: NO raw hex or malformed var()+alpha concat outside the token layer (CSS file may use only the \`black\` mask + \`transparent\` keywords); EXACTLY the two intended new tokens this cycle (no extra --fc-* added); no styling/transient state written onto node.data; exportGraph.ts / FlowCanvasBridge.cs untouched; only execState + blockTimings are READ; running/error are class-driven (.fc-exec-running/.fc-exec-error) and SUCCESS stays on the inline box-shadow path (heat ring still stacks); comet child gated \`running && !reducedMotion\` and the live ticker returns null under reduced motion; \`forwards\` is present on the breathing + ripple animations; ◌ glyph dropped + @keyframes exec-pulse deleted + @keyframes spin deleted only-if-orphaned (grep) + dead execGlowColors removed; useRunningElapsed called BEFORE the \`if (!def)\` early return (rules of hooks); NO new npm dependency (check package.json). StartNode must be UNCHANGED.\n` +
    `STAGE 2 — Code quality (only if spec-compliant): correctness, no dead code, follows patterns, no accidental behavior/round-trip change, no transient broken state left in the file. Blocking qualityIssues only; nits to minorNotes.\n` +
    `verdict=PASS only if specCompliant AND no blocking qualityIssues. NEEDS_FIX for spec/blocking-quality issues. BLOCKED only if the task/plan is fundamentally broken. Spot-check the claimed-green verification (and that any test-suite failures are only the known parity-CLI parallel race).`
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
  wave: 'Wave 2b — Execution Cinematics (running halo / success checkmark / error shake+ripple / count-up, Tasks 1-7)',
  completed: results.filter((r) => r.ok).map((r) => `Task ${r.task}: ${r.title} (${r.commitSha})`),
  aborted,
  allPassed: !aborted && results.length === TASKS.length && results.every((r) => r.ok),
  pendingHumanSmoke: results.flatMap((r) => (r.concerns || []).filter((c) => /PENDING-HUMAN|manual smoke/i.test(c)).map((c) => `Task ${r.task}: ${c}`)),
  results,
}
