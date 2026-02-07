# Tasks: Add environment management

## 1. Models and persistence
- [ ] 1.1 Create `Models/EnvironmentConfig.cs`
- [ ] 1.2 Add `Environments` and `ActiveEnvironment` to `Models/AppConfiguration.cs`
- [ ] 1.3 Add configuration helper methods for environment load/save

## 2. Environment service
- [ ] 2.1 Create `Services/EnvironmentService.cs` with CRUD and switch operations
- [ ] 2.2 Add active-environment variable resolution APIs
- [ ] 2.3 Add environment-changed event contract and subscribers

## 3. UI integration
- [ ] 3.1 Add environment selector controls to `Form1.Designer.cs`
- [ ] 3.2 Implement switch flow in `Form1.cs` (dirty-state prompt, save, load, title update)
- [ ] 3.3 Create `EnvironmentDialog.cs` for create/rename/delete/duplicate and variable editing

## 4. Runtime behavior
- [ ] 4.1 Merge environment variables into host variables in `GetHostConnections()`
- [ ] 4.2 Enforce precedence: grid values > environment variables > script defaults
- [ ] 4.3 Prevent deletion of required `Default` environment

## 5. Migration and verification
- [ ] 5.1 Implement legacy config migration to default environment behavior
- [ ] 5.2 Add tests for `EnvironmentService` and migration paths
- [ ] 5.3 Add manual smoke test for multi-environment switching and variable resolution
