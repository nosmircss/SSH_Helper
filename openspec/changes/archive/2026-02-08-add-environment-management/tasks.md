# Tasks: Add environment management

## 1. Models and persistence
- [x] 1.1 Create `Models/EnvironmentConfig.cs`
- [x] 1.2 Add `Environments` and `ActiveEnvironment` to `Models/AppConfiguration.cs`
- [x] 1.3 Add configuration helper methods for environment load/save

## 2. Environment service
- [x] 2.1 Create `Services/EnvironmentService.cs` with CRUD and switch operations
- [x] 2.2 Add active-environment variable resolution APIs
- [x] 2.3 Add environment-changed event contract and subscribers

## 3. UI integration
- [x] 3.1 Add environment selector controls to `Form1.Designer.cs`
- [x] 3.2 Implement switch flow in `Form1.cs` (dirty-state prompt, save, load, title update)
- [x] 3.3 Create `EnvironmentDialog.cs` for create/rename/delete/duplicate and variable editing

## 4. Runtime behavior
- [x] 4.1 Merge environment variables into host variables in `GetHostConnections()`
- [x] 4.2 Enforce precedence: grid values > environment variables > script defaults
- [x] 4.3 Prevent deletion of required `Default` environment

## 5. Migration and verification
- [x] 5.1 Implement legacy config migration to default environment behavior
- [x] 5.2 Add tests for `EnvironmentService` and migration paths
- [x] 5.3 Add manual smoke test for multi-environment switching and variable resolution
