## 1. Implementation
- [x] 1.1 Replace `SftpCommand` runtime to use `Renci.SshNet.SftpClient` while preserving current error-handling and capture-variable behavior.
- [x] 1.2 Preserve existing option semantics (`overwrite`, timeout seconds, host/credential fallbacks, upload/download actions).
- [x] 1.3 Update project dependencies: add `SSH.NET`, remove `Rebex.Sftp`.
- [x] 1.4 Update `SCRIPTING.md` SFTP backend documentation.

## 2. Verification
- [x] 2.1 Run targeted scripting tests covering parser and network command behaviors.
- [x] 2.2 Validate the OpenSpec change with strict mode.
