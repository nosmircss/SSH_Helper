# M1 Environments Manual Smoke Test

## Preconditions
- Build and launch the app from current branch.
- Start with an empty or throwaway config profile.

## Scenario 1: Create and switch environments
1. In the main window, add at least one host row and set a marker value (for example `site=dev`).
2. Open `Manage` next to the environment selector and create `Dev`.
3. Change grid rows/values to represent a different target (for example `site=prod`) and create `Prod`.
4. Create a third environment `Stage`.
5. Switch between `Default`, `Dev`, `Prod`, and `Stage` from the toolbar selector.
6. Verify each environment restores its own rows/columns/selection and the title bar updates to `SSH Helper vX.Y.Z - [EnvironmentName]`.

## Scenario 2: Variable precedence
1. In one environment, set environment variable `region=us-east-1` in Manage Environments.
2. Ensure a host row does not define `region`.
3. Execute a script/command that references `region`; verify `us-east-1` is used.
4. Set host-row column `region=us-west-2` for one host.
5. Re-run execution; verify that host uses `us-west-2` (grid value overrides environment variable).

## Scenario 3: Safety rules and migration
1. Attempt to delete `Default` from Manage Environments; verify deletion is blocked with a warning.
2. Close and reopen the app; verify previously created environments persist.
3. If testing from a legacy single-environment profile, create first named environment and verify current workspace is preserved in `Default`.
