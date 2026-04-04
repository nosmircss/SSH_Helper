## MODIFIED Requirements
### Requirement: Persistent embedded browser profile
The embedded browser mode SHALL use a shared app-owned WebView2 user-data folder under the application storage root.

The application storage root SHALL be `%LocalAppData%\\SSH_Helper` for standard builds and the executable directory for portable builds.

The embedded profile SHALL persist across runs until the operator explicitly clears it from Settings.

#### Scenario: Standard build stores embedded browser data under LocalAppData
- **WHEN** SSH Helper runs as a standard build
- **THEN** embedded browser profile data is persisted under `%LocalAppData%\\SSH_Helper`

#### Scenario: Portable build stores embedded browser data beside executable
- **WHEN** SSH Helper runs as a portable build
- **THEN** embedded browser profile data is persisted under the executable directory

#### Scenario: Embedded browser reuses prior site data
- **WHEN** an operator runs two `browser_callback_capture` steps with `browser_mode: webview2` across separate app sessions
- **THEN** the second run can reuse cookies and other site data from the first run unless the profile has been cleared
