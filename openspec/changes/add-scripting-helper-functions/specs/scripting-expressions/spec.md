## ADDED Requirements

### Requirement: Network address helper functions
Set and conditional expressions SHALL provide pure network-address helper functions for IP and URL inspection.

#### Scenario: Validate an IP address
- **WHEN** a script evaluates `is_valid_ip(Host_IP)`
- **THEN** the function returns true for a syntactically valid IPv4 or IPv6 address
- **AND** returns false for any malformed address

#### Scenario: Report IP version
- **WHEN** a script evaluates `ip_version(Host_IP)`
- **THEN** the function returns `4` for an IPv4 address and `6` for an IPv6 address
- **AND** returns an empty value for a malformed address

#### Scenario: Test CIDR membership
- **WHEN** a script evaluates `ip_in_cidr(Host_IP, "10.0.0.0/8")`
- **THEN** the function returns true when the address falls within the given CIDR range
- **AND** returns false when the address is outside the range or either argument is malformed

#### Scenario: Parse URL components
- **WHEN** a script evaluates `url_host(url)` or `url_port(url)`
- **THEN** the function returns the host or port component of the URL
- **AND** returns an empty value when the URL cannot be parsed

### Requirement: Date and time functions with an explicit time base
Expressions SHALL provide date/time functions that use an explicit, unambiguous time base and calendar-correct arithmetic.

#### Scenario: Explicit UTC and local now
- **WHEN** a script evaluates `now_utc()` or `now_local()`
- **THEN** the returned timestamp uses the requested time base
- **AND** date arithmetic and difference functions operate on a consistent base without silently mixing local and UTC time

#### Scenario: Calendar-unit date arithmetic
- **WHEN** a script evaluates `date_add(now_utc(), -3, "months")` or uses units of `week`, `month`, or `year`
- **THEN** the runtime applies calendar-correct week/month/year arithmetic

#### Scenario: Explicit parse format
- **WHEN** a script parses a date string and supplies an explicit format argument
- **THEN** the runtime parses using that format rather than guessing from a fixed format list

### Requirement: Regular expression match and capture functions
Expressions SHALL provide regex match and capture-group functions with a bounded execution timeout.

#### Scenario: Match and capture a group
- **WHEN** a script evaluates `regex_match(text, "inet (\\d+\\.\\d+\\.\\d+\\.\\d+)", 1)`
- **THEN** the function returns the first capture group of the first match
- **AND** returns an empty value when there is no match

#### Scenario: Match all occurrences
- **WHEN** a script evaluates `regex_match_all(text, pattern)`
- **THEN** the function returns a list of all matched substrings in source order

#### Scenario: Bounded evaluation
- **WHEN** any regex function evaluates a pattern
- **THEN** evaluation is subject to a bounded timeout consistent with the existing `replace`/regex functions
