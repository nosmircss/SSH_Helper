using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.UI
{
    public class JobEditorValidationTests
    {
        #region ValidateName

        [Fact]
        public void ValidateName_Empty_ReturnsError()
        {
            JobEditorValidator.ValidateName("").Should().Be("Job name is required");
        }

        [Fact]
        public void ValidateName_Null_ReturnsError()
        {
            JobEditorValidator.ValidateName(null).Should().Be("Job name is required");
        }

        [Fact]
        public void ValidateName_WhitespaceOnly_ReturnsError()
        {
            JobEditorValidator.ValidateName("   ").Should().Be("Job name is required");
        }

        [Fact]
        public void ValidateName_TooLong_ReturnsError()
        {
            var name = new string('A', 101);
            JobEditorValidator.ValidateName(name).Should().Be("Job name must be 100 characters or less");
        }

        [Fact]
        public void ValidateName_ExactlyMaxLength_ReturnsNull()
        {
            var name = new string('A', 100);
            JobEditorValidator.ValidateName(name).Should().BeNull();
        }

        [Fact]
        public void ValidateName_Valid_ReturnsNull()
        {
            JobEditorValidator.ValidateName("My Valid Job").Should().BeNull();
        }

        #endregion

        #region ValidateTarget

        [Fact]
        public void ValidateTarget_Empty_ReturnsError()
        {
            JobEditorValidator.ValidateTarget("").Should().Be("Please select a target preset/folder");
        }

        [Fact]
        public void ValidateTarget_Null_ReturnsError()
        {
            JobEditorValidator.ValidateTarget(null).Should().Be("Please select a target preset/folder");
        }

        [Fact]
        public void ValidateTarget_Valid_ReturnsNull()
        {
            JobEditorValidator.ValidateTarget("MyPreset").Should().BeNull();
        }

        [Fact]
        public void ValidateTarget_CustomPreset_DoesNotRequireNamedTarget()
        {
            JobEditorValidator.ValidateTarget(JobTargetType.CustomPreset, null).Should().BeNull();
        }

        #endregion

        #region ValidateCustomPresetCommands

        [Fact]
        public void ValidateCustomPresetCommands_CustomPresetBlank_ReturnsError()
        {
            JobEditorValidator.ValidateCustomPresetCommands(JobTargetType.CustomPreset, "   ")
                .Should().Be("Custom preset content is required");
        }

        [Fact]
        public void ValidateCustomPresetCommands_CustomPresetValid_ReturnsNull()
        {
            JobEditorValidator.ValidateCustomPresetCommands(JobTargetType.CustomPreset, "echo custom")
                .Should().BeNull();
        }

        #endregion

        #region ValidateCron

        [Fact]
        public void ValidateCron_RecurringInvalid_ReturnsError()
        {
            var result = JobEditorValidator.ValidateCron(ScheduleType.Recurring, "invalid");
            result.Should().NotBeNull();
        }

        [Fact]
        public void ValidateCron_RecurringValid_ReturnsNull()
        {
            JobEditorValidator.ValidateCron(ScheduleType.Recurring, "0 * * * *").Should().BeNull();
        }

        [Fact]
        public void ValidateCron_NoneWithNull_ReturnsNull()
        {
            JobEditorValidator.ValidateCron(ScheduleType.None, null).Should().BeNull();
        }

        [Fact]
        public void ValidateCron_OneTimeWithNull_ReturnsNull()
        {
            JobEditorValidator.ValidateCron(ScheduleType.OneTime, null).Should().BeNull();
        }

        #endregion

        #region ValidateOneTimeDate

        [Fact]
        public void ValidateOneTimeDate_OneTimePastDate_ReturnsError()
        {
            var pastDate = DateTime.UtcNow.AddHours(-1);
            JobEditorValidator.ValidateOneTimeDate(ScheduleType.OneTime, pastDate)
                .Should().Be("One-time schedule must be in the future");
        }

        [Fact]
        public void ValidateOneTimeDate_OneTimeFutureDate_ReturnsNull()
        {
            var futureDate = DateTime.UtcNow.AddHours(1);
            JobEditorValidator.ValidateOneTimeDate(ScheduleType.OneTime, futureDate)
                .Should().BeNull();
        }

        [Fact]
        public void ValidateOneTimeDate_NoneWithNull_ReturnsNull()
        {
            JobEditorValidator.ValidateOneTimeDate(ScheduleType.None, null).Should().BeNull();
        }

        #endregion

        #region ValidateHosts

        [Fact]
        public void ValidateHosts_EmptyList_ReturnsError()
        {
            JobEditorValidator.ValidateHosts(new List<Dictionary<string, string>>())
                .Should().Be("At least one host with a valid IP is required");
        }

        [Fact]
        public void ValidateHosts_Null_ReturnsError()
        {
            JobEditorValidator.ValidateHosts(null)
                .Should().Be("At least one host with a valid IP is required");
        }

        [Fact]
        public void ValidateHosts_AllEmptyIps_ReturnsError()
        {
            var hosts = new List<Dictionary<string, string>>
            {
                new() { { "Host_IP", "" } },
                new() { { "Host_IP", "  " } }
            };
            JobEditorValidator.ValidateHosts(hosts)
                .Should().Be("At least one host with a valid IP is required");
        }

        [Fact]
        public void ValidateHosts_ValidList_ReturnsNull()
        {
            var hosts = new List<Dictionary<string, string>>
            {
                new() { { "Host_IP", "10.0.0.1" } }
            };
            JobEditorValidator.ValidateHosts(hosts).Should().BeNull();
        }

        #endregion

        #region ValidateStoredCredentials

        [Fact]
        public void ValidateStoredCredentials_StoredEmptyUsername_ReturnsError()
        {
            JobEditorValidator.ValidateStoredCredentials(CredentialMode.Stored, "")
                .Should().Be("Username is required for stored credentials");
        }

        [Fact]
        public void ValidateStoredCredentials_StoredNullUsername_ReturnsError()
        {
            JobEditorValidator.ValidateStoredCredentials(CredentialMode.Stored, null)
                .Should().Be("Username is required for stored credentials");
        }

        [Fact]
        public void ValidateStoredCredentials_StoredValidUsername_ReturnsNull()
        {
            JobEditorValidator.ValidateStoredCredentials(CredentialMode.Stored, "admin")
                .Should().BeNull();
        }

        [Fact]
        public void ValidateStoredCredentials_InheritFromAppEmptyUsername_ReturnsNull()
        {
            JobEditorValidator.ValidateStoredCredentials(CredentialMode.InheritFromApp, "")
                .Should().BeNull();
        }

        #endregion

        #region ValidatePerHostCredentials

        [Fact]
        public void ValidatePerHostCredentials_MissingColumns_ReturnsError()
        {
            var hosts = new List<Dictionary<string, string>>
            {
                new() { { "Host_IP", "10.0.0.1" } }
            };

            JobEditorValidator.ValidatePerHostCredentials(
                CredentialMode.PerHostColumn,
                hosts,
                new List<string> { "Host_IP", "username" })
                .Should().Be("Per-host credentials require 'username' and 'password' columns in the Hosts tab.");
        }

        [Fact]
        public void ValidatePerHostCredentials_BlankRowCredential_ReturnsRowError()
        {
            var hosts = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Host_IP", "10.0.0.1" },
                    { "username", "admin" },
                    { "password", "" }
                }
            };

            JobEditorValidator.ValidatePerHostCredentials(
                CredentialMode.PerHostColumn,
                hosts,
                new List<string> { "Host_IP", "username", "password" })
                .Should().Be("Host row 1 is missing username or password required for per-host credentials.");
        }

        [Fact]
        public void ValidatePerHostCredentials_CaseInsensitiveColumnsAndValues_ReturnsNull()
        {
            var hosts = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Host_IP", "10.0.0.1" },
                    { "Username", "admin" },
                    { "PASSWORD", "secret" }
                }
            };

            JobEditorValidator.ValidatePerHostCredentials(
                CredentialMode.PerHostColumn,
                hosts,
                new List<string> { "Host_IP", "Username", "PASSWORD" })
                .Should().BeNull();
        }

        #endregion

        #region ValidateVaultCredentials

        [Fact]
        public void ValidateVaultCredentials_VaultModeWithoutPath_ReturnsError()
        {
            JobEditorValidator.ValidateVaultCredentials(CredentialMode.Vault, "")
                .Should().Be("Vault credential mode requires a Vault path.");
        }

        [Fact]
        public void ValidateVaultCredentials_VaultModeWithPath_ReturnsNull()
        {
            JobEditorValidator.ValidateVaultCredentials(CredentialMode.Vault, "ssh/creds/router-a")
                .Should().BeNull();
        }

        #endregion

        #region ValidateTimeoutOverrides

        [Fact]
        public void ValidateTimeoutOverrides_CommandOutOfRange_ReturnsError()
        {
            JobEditorValidator.ValidateTimeoutOverrides(301, null)
                .Should().Be("Command timeout override must be between 1 and 300 seconds.");
        }

        [Fact]
        public void ValidateTimeoutOverrides_ConnectionOutOfRange_ReturnsError()
        {
            JobEditorValidator.ValidateTimeoutOverrides(null, 121)
                .Should().Be("Connection timeout override must be between 5 and 120 seconds.");
        }

        [Fact]
        public void ValidateTimeoutOverrides_ValidOverrides_ReturnsNull()
        {
            JobEditorValidator.ValidateTimeoutOverrides(45, 12).Should().BeNull();
        }

        #endregion

        #region ValidateAll

        [Fact]
        public void ValidateAll_AllValid_ReturnsNull()
        {
            var hosts = new List<Dictionary<string, string>>
            {
                new() { { "Host_IP", "10.0.0.1" } }
            };

            var result = JobEditorValidator.ValidateAll(
                "MyJob", "MyPreset",
                ScheduleType.None, null, null,
                hosts, new List<string> { "Host_IP" }, CredentialMode.InheritFromApp, null);

            result.Should().BeNull();
        }

        [Fact]
        public void ValidateAll_InvalidName_ReturnsFirstError()
        {
            var hosts = new List<Dictionary<string, string>>
            {
                new() { { "Host_IP", "10.0.0.1" } }
            };

            var result = JobEditorValidator.ValidateAll(
                "", "MyPreset",
                ScheduleType.None, null, null,
                hosts, new List<string> { "Host_IP" }, CredentialMode.InheritFromApp, null);

            result.Should().Be("Job name is required");
        }

        [Fact]
        public void ValidateAll_InvalidTarget_ReturnsTargetError()
        {
            var hosts = new List<Dictionary<string, string>>
            {
                new() { { "Host_IP", "10.0.0.1" } }
            };

            var result = JobEditorValidator.ValidateAll(
                "MyJob", "",
                ScheduleType.None, null, null,
                hosts, new List<string> { "Host_IP" }, CredentialMode.InheritFromApp, null);

            result.Should().Be("Please select a target preset/folder");
        }

        [Fact]
        public void ValidateAll_PerHostCredentials_RequiresCredentialColumns()
        {
            var hosts = new List<Dictionary<string, string>>
            {
                new() { { "Host_IP", "10.0.0.1" } }
            };

            var result = JobEditorValidator.ValidateAll(
                "MyJob", "MyPreset",
                ScheduleType.None, null, null,
                hosts, new List<string> { "Host_IP", "username" }, CredentialMode.PerHostColumn, null);

            result.Should().Be("Per-host credentials require 'username' and 'password' columns in the Hosts tab.");
        }

        [Fact]
        public void ValidateAll_CustomPresetBlankContent_ReturnsContentError()
        {
            var hosts = new List<Dictionary<string, string>>
            {
                new() { { "Host_IP", "10.0.0.1" } }
            };

            var result = JobEditorValidator.ValidateAll(
                "MyJob", null,
                ScheduleType.None, null, null,
                hosts, new List<string> { "Host_IP" }, CredentialMode.InheritFromApp, null,
                JobTargetType.CustomPreset, "  ");

            result.Should().Be("Custom preset content is required");
        }

        [Fact]
        public void ValidateAll_InvalidTimeoutOverride_ReturnsTimeoutError()
        {
            var hosts = new List<Dictionary<string, string>>
            {
                new() { { "Host_IP", "10.0.0.1" } }
            };

            var result = JobEditorValidator.ValidateAll(
                "MyJob", "MyPreset",
                ScheduleType.None, null, null,
                hosts, new List<string> { "Host_IP" }, CredentialMode.InheritFromApp, null,
                commandTimeoutOverrideSeconds: 301);

            result.Should().Be("Command timeout override must be between 1 and 300 seconds.");
        }

        [Fact]
        public void ValidateAll_VaultModeWithoutPath_ReturnsVaultPathError()
        {
            var hosts = new List<Dictionary<string, string>>
            {
                new() { { "Host_IP", "10.0.0.1" } }
            };

            var result = JobEditorValidator.ValidateAll(
                "MyJob", "MyPreset",
                ScheduleType.None, null, null,
                hosts, new List<string> { "Host_IP" }, CredentialMode.Vault, null,
                vaultCredentialPath: "  ");

            result.Should().Be("Vault credential mode requires a Vault path.");
        }

        #endregion
    }
}
