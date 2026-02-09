# Change: Switch SFTP Runtime Backend To SSH.NET

## Why
The current `sftp` script step depends on Rebex SFTP runtime APIs, which can require additional licensing. Operators without a Rebex SFTP license cannot use the feature even when core SSH scripting works.

## What Changes
- Replace `sftp` command runtime implementation from Rebex SFTP reflection calls to `SSH.NET` (`Renci.SshNet`) client APIs.
- Keep the script contract unchanged (`action`, `local_path`, `remote_path`, `overwrite`, `timeout`, `into`, host/credential fallbacks).
- Remove `Rebex.Sftp` package dependency and add `SSH.NET` package dependency.
- Update user documentation to reflect the new backend.

## Impact
- Affected specs: `scripting-runtime`
- Affected code: `Services/Scripting/Commands/SftpCommand.cs`, `SSH_Helper.csproj`, `SCRIPTING.md`