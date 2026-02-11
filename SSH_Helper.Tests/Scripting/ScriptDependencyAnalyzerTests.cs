using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptDependencyAnalyzerTests
{
    [Fact]
    public void AnalyzePresets_WithSimpleReferences_ReportsAllMissingCandidates()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = "echo {{foo}}\r\necho ${bar}"
        };

        var result = analyzer.AnalyzePresets(new[] { preset });

        result.ReferencedColumns.Should().BeEquivalentTo("foo", "bar");
    }

    [Fact]
    public void AnalyzePresets_WithExternallyResolvedVariables_ExcludesEnvironmentVariables()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = "echo {{foo}}\r\necho ${bar}\r\necho {{baz}}"
        };
        var externallyResolved = new[] { "FOO", "  baz  ", "", "   " };

        var result = analyzer.AnalyzePresets(new[] { preset }, externallyResolved);

        result.ReferencedColumns.Should().BeEquivalentTo("bar");
    }

    [Fact]
    public void AnalyzePresets_UpdateEnvironmentDefinesVariableForLaterSteps()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - updateenvironment:
                      variable: api_token
                      value: "${token_from_api}"
                  - print: "${api_token}"
                """
        };

        var result = analyzer.AnalyzePresets(new[] { preset });

        result.ReferencedColumns.Should().Contain("token_from_api");
        result.ReferencedColumns.Should().NotContain("api_token");
    }

    [Fact]
    public void AnalyzeSshRequirements_SendAtRoot_RequiresSshSession()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - send: "show ver"
            """);

        result.RequiresSshSession.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeSshRequirements_OnlyLocalCommands_NoSshRequired()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - http:
                  url: "https://example.com"
              - print: "done"
              - set: "x = 1"
            """);

        result.RequiresSshSession.Should().BeFalse();
        result.UsesSftp.Should().BeFalse();
        result.SftpUsesDefaultHost.Should().BeFalse();
        result.SftpUsesDefaultCredentials.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeSshRequirements_SendInIfThen_RequiresSshSession()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - if: "${condition}"
                then:
                  - send: "show ver"
            """);

        result.RequiresSshSession.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeSshRequirements_SendInForeachDo_RequiresSshSession()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - foreach: "item in items"
                do:
                  - send: "show ver"
            """);

        result.RequiresSshSession.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeSshRequirements_SendInWhileDo_RequiresSshSession()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - while: "${keep_running}"
                do:
                  - send: "show ver"
            """);

        result.RequiresSshSession.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeSshRequirements_SendInTryBlock_RequiresSshSession()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - try:
                  - send: "show ver"
            """);

        result.RequiresSshSession.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeSshRequirements_SendInCatchBlock_RequiresSshSession()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - try:
                  - print: "attempt"
                catch:
                  - send: "show ver"
            """);

        result.RequiresSshSession.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeSshRequirements_SendInElifBranch_RequiresSshSession()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - if: "${branch} == 1"
                then:
                  - print: "branch 1"
                elif:
                  - if: "${branch} == 2"
                    then:
                      - send: "show ver"
            """);

        result.RequiresSshSession.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeSshRequirements_SendInElseBlock_RequiresSshSession()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - if: "${branch} == 1"
                then:
                  - print: "branch 1"
                else:
                  - send: "show ver"
            """);

        result.RequiresSshSession.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeSshRequirements_SftpOnly_DetectsSftpNoSsh()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - sftp:
                  action: "download"
                  local_path: "C:/tmp/local.txt"
                  remote_path: "/tmp/remote.txt"
            """);

        result.RequiresSshSession.Should().BeFalse();
        result.UsesSftp.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeSshRequirements_EmptyScript_NothingRequired()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps: []
            """);

        result.RequiresSshSession.Should().BeFalse();
        result.UsesSftp.Should().BeFalse();
        result.SftpUsesDefaultHost.Should().BeFalse();
        result.SftpUsesDefaultCredentials.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeSshRequirements_SendAndSftp_BothDetected()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - send: "show ver"
              - sftp:
                  action: "download"
                  local_path: "C:/tmp/local.txt"
                  remote_path: "/tmp/remote.txt"
            """);

        result.RequiresSshSession.Should().BeTrue();
        result.UsesSftp.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeSshRequirements_DeeplyNested_Detected()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - try:
                  - foreach: "item in items"
                    do:
                      - if: "${item} == target"
                        then:
                          - send: "show ver"
            """);

        result.RequiresSshSession.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeSshRequirements_SftpWithoutHost_SftpUsesDefaultHost()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - sftp:
                  action: "download"
                  local_path: "C:/tmp/local.txt"
                  remote_path: "/tmp/remote.txt"
            """);

        result.SftpUsesDefaultHost.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeSshRequirements_SftpWithExplicitHost_NoDefaultHost()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - sftp:
                  action: "download"
                  local_path: "C:/tmp/local.txt"
                  remote_path: "/tmp/remote.txt"
                  host: "10.0.0.1"
                  username: "user"
                  password: "pass"
            """);

        result.SftpUsesDefaultHost.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeSshRequirements_SftpWithoutUsername_SftpUsesDefaultCredentials()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - sftp:
                  action: "download"
                  local_path: "C:/tmp/local.txt"
                  remote_path: "/tmp/remote.txt"
                  host: "10.0.0.1"
                  password: "pass"
            """);

        result.SftpUsesDefaultCredentials.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeSshRequirements_SftpWithoutPassword_SftpUsesDefaultCredentials()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - sftp:
                  action: "download"
                  local_path: "C:/tmp/local.txt"
                  remote_path: "/tmp/remote.txt"
                  host: "10.0.0.1"
                  username: "user"
            """);

        result.SftpUsesDefaultCredentials.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeSshRequirements_SftpWithExplicitCredentials_NoDefaultCredentials()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - sftp:
                  action: "download"
                  local_path: "C:/tmp/local.txt"
                  remote_path: "/tmp/remote.txt"
                  host: "10.0.0.1"
                  username: "user"
                  password: "pass"
            """);

        result.SftpUsesDefaultCredentials.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeSshRequirements_MultipleSftpSteps_AnyDefaultSetsFlag()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - sftp:
                  action: "download"
                  local_path: "C:/tmp/local1.txt"
                  remote_path: "/tmp/remote1.txt"
                  host: "10.0.0.1"
                  username: "user"
                  password: "pass"
              - sftp:
                  action: "download"
                  local_path: "C:/tmp/local2.txt"
                  remote_path: "/tmp/remote2.txt"
                  username: "user"
                  password: "pass"
            """);

        result.SftpUsesDefaultHost.Should().BeTrue();
    }

    private static SshRequirementResult AnalyzeSshRequirements(string scriptText)
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var parser = new ScriptParser();
        var script = parser.Parse(scriptText);
        return analyzer.AnalyzeSshRequirements(script);
    }
}
