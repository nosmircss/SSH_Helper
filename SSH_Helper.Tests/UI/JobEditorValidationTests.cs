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
                hosts, CredentialMode.InheritFromApp, null);

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
                hosts, CredentialMode.InheritFromApp, null);

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
                hosts, CredentialMode.InheritFromApp, null);

            result.Should().Be("Please select a target preset/folder");
        }

        #endregion
    }
}
