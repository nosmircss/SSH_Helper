---
name: Release Notes
description: Generate release notes since a given commit and update CHANGELOG.md.
category: Release
tags: [release, changelog]
---

Generate release notes for this project since a specified commit.

**Input**: The user provides a commit ID (short or full hash) and optionally a version label (e.g., `0.52.0`). If no version label is given, read the current version from the most recent `chore: Update application version to X.Y.Z` commit message or the latest git tag, and use that as the section header.

**Steps**

1. **Gather commits** — Run `git log --oneline <commit>..HEAD` to get all commits since the provided anchor. Also run `git diff-tree --no-commit-id -r --name-status <commit>..HEAD` to see which files were added, modified, or deleted.

2. **Read changed files** — For each significantly changed area (new commands, new services, modified models, new tests, UI changes, documentation, dependency changes), read the relevant source files to understand what was actually built — not just commit message summaries. Focus on:
   - New files in `Services/Scripting/Commands/` — new scripting commands
   - New files in `Services/` — new services
   - New files in `Models/` — new data models
   - Changes to `Form1.cs`, `Forms/`, `UI/` — UI features
   - Changes to `SSH_Helper.Tests/` — new test coverage
   - Changes to `SCRIPTING.md`, `README.md` — documentation updates
   - Changes to `.csproj` — dependency additions/removals

3. **Read existing CHANGELOG.md** — Understand the existing format, tone, and level of detail.

4. **Categorize changes** — Group findings into logical sections following the existing changelog pattern:
   - Major features get their own `### Heading` with detailed description, code examples where relevant, and bullet-point sub-features
   - Smaller improvements group under descriptive headings
   - Dependency changes go in a `### Dependency Changes` table (`| Package | Version | Purpose |`)
   - Documentation updates get a `### Documentation` section
   - Test coverage gets a `### Test Coverage` section listing new test classes by area
   - Use bold inline labels (`**Feature Name** —`) for items within a section

5. **Write the changelog section** — Prepend a new section to CHANGELOG.md after the `# Changelog` header:
   ```
   ## Changes Since `<short-hash>` (<version>)
   ```
   Keep the previous sections intact below. Separate sections with `---`.

**Style rules**
- Write in present tense, third person ("Scripts that don't require an SSH session are now detected...")
- Use precise technical language — name classes, methods, config keys, and file paths
- Include YAML/code examples for new scripting commands (matching existing example style)
- Tables for structured comparisons (command summaries, dependency lists)
- Bullet lists for feature breakdowns
- Bold for emphasis on key terms and sub-feature names
- No emojis
- No vague marketing language — describe what the feature does, not how great it is
- Keep the same level of detail as existing changelog sections

**Constraints**
- Do NOT invent features — only document what the commits and code actually show
- Do NOT include merge commits, fixup commits, or intermediate broken states
- Do NOT modify existing changelog sections below the new one
