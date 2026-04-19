## Context
Teams notifications currently use a MessageCard payload in `WebhookDispatcher`, while Slack and Discord already perform channel-specific mention normalization. The scripting surface already exposes `mention:` as a string list, so replacing that with a structured Teams-only schema would create unnecessary churn across the parser, Flow Canvas, and docs.

## Goals / Non-Goals
- Goals:
  - Move Teams notify delivery to an Adaptive Card webhook envelope.
  - Support live Teams user mentions using explicit, non-ambiguous string tokens.
  - Preserve current notify shape and current behavior for non-Teams channels.
- Non-Goals:
  - Raw/custom Adaptive Card authoring.
  - Graph API lookups, display-name resolution, or profile schema changes.
  - Workflows/bot-based Teams delivery.

## Decisions
- Decision: Keep `notify.mention` as `List<string>` and add Teams-only forms `upn:<id>|<display>` and `entra:<id>|<display>`.
  - Alternatives considered: structured mention objects; rejected because it would widen the script/public surface for a narrow v1 need.
- Decision: Invalid Teams mention strings remain visible as plain text and emit runtime diagnostics.
  - Alternatives considered: fail the step; rejected because a single malformed mention should not suppress the entire notification.
- Decision: The generated Teams card stays simple: optional title block, optional mention block, and message block.
  - Alternatives considered: facts/buttons/custom card fields; rejected to keep this change aligned with the existing `notify` contract.

## Risks / Trade-offs
- Adaptive Card rendering differs from MessageCard rendering, so Teams formatting snapshots need to be re-baselined via tests.
- Teams live mentions require exact UPN or Entra Object ID input; the app will not resolve display names automatically.

## Migration Plan
No configuration migration is required. Existing Teams profiles continue to use the same stored webhook URL; only the outbound payload shape changes.
