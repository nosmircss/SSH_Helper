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
    public void AnalyzePresets_WithWritefileRuntimeVariable_ExcludesUnderscoreRuntimeReferences()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - writefile:
                      path: "output.csv"
                      content: "test"
                      mode: overwrite
                  - print: "CSV written to {{_writefile}}"
                """
        };

        var result = analyzer.AnalyzePresets(new[] { preset });

        result.ReferencedColumns.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzePresets_WithUnderscoreOnlySimpleReferences_ExcludesUnderscoreRuntimeReferences()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = "echo {{_custom_runtime}}\r\necho ${_another_runtime}"
        };

        var result = analyzer.AnalyzePresets(new[] { preset });

        result.ReferencedColumns.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzePresets_WithPromptBuiltIn_ExcludesPromptFromMissingDependencies()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - print:
                      message: "${_prompt}"
                """
        };

        var result = analyzer.AnalyzePresets(new[] { preset });

        result.ReferencedColumns.Should().BeEmpty();
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
    public void AnalyzePresets_ChooseOptionsFromVariable_TracksOptionsSourceDependency()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - choose:
                      into: selected_interface
                      options: interface_list
                """
        };

        var result = analyzer.AnalyzePresets(new[] { preset });

        result.ReferencedColumns.Should().Contain("interface_list");
    }

    [Fact]
    public void AnalyzePresets_ExistsTypeFromVariable_TracksTypeDependency()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - exists:
                      path: "C:\\temp"
                      into: path_exists
                      type: "${expected_type}"
                """
        };

        var result = analyzer.AnalyzePresets(new[] { preset });

        result.ReferencedColumns.Should().Contain("expected_type");
    }

    [Fact]
    public void AnalyzePresets_LocalCmdInteractiveInto_DefinesOnlyExitCodeVariable()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - localcmd:
                      command: "date"
                      interactive: true
                      keep_open: true
                      into: date2
                  - print:
                      message: "exit=${date2_exit_code} stdout=${date2_stdout}"
                """
        };

        var result = analyzer.AnalyzePresets(new[] { preset });

        result.ReferencedColumns.Should().NotContain("date2_exit_code");
        result.ReferencedColumns.Should().Contain("date2_stdout");
    }

    [Fact]
    public void AnalyzePresets_LocalCmdInteractiveDetachedInto_DefinesStartupMetadataVariables()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - localcmd:
                      command: "date"
                      interactive: true
                      lifetime: detached
                      into: session
                  - print:
                      message: "pid=${session_pid} started=${session_started} err=${session_start_error} exit=${session_exit_code}"
                """
        };

        var result = analyzer.AnalyzePresets(new[] { preset });

        result.ReferencedColumns.Should().NotContain("session_pid");
        result.ReferencedColumns.Should().NotContain("session_started");
        result.ReferencedColumns.Should().NotContain("session_start_error");
        result.ReferencedColumns.Should().Contain("session_exit_code");
    }

    [Fact]
    public void AnalyzePresets_ForeachCollectionExpression_DoesNotReportFunctionCallAsMissingColumn()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                vars:
                  matched_services: []
                steps:
                  - foreach: "svc in compact(matched_services)"
                    do:
                      - print: "${svc}"
                """
        };

        var result = analyzer.AnalyzePresets(new[] { preset });

        result.ReferencedColumns.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzePresets_ForeachCollectionExpression_TracksBareVariablesInsideExpression()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - foreach: "svc in compact(split(source_services, ','))"
                    do:
                      - print: "${svc}"
                """
        };

        var result = analyzer.AnalyzePresets(new[] { preset });

        result.ReferencedColumns.Should().BeEquivalentTo("source_services");
    }

    [Fact]
    public void AnalyzePresets_SendRespond_TracksExpectAndReplyVariableDependencies()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - send:
                      command: adduser qa
                      respond:
                        - expect: "Password for ${username}:"
                          reply: "${password}"
                """
        };

        var result = analyzer.AnalyzePresets(new[] { preset });

        result.ReferencedColumns.Should().Contain("username");
        result.ReferencedColumns.Should().Contain("password");
    }

    [Fact]
    public void AnalyzePresets_InteractiveCaptureDefinesVariableForLaterReferences()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - interactive:
                      session: separate
                      command: "diagnose sniffer packet ${selected_interface} '${filter}' 4 10 a"
                      capture: sniffer_output
                  - print:
                      message: "Capture complete. Output length: ${sniffer_output.length}"
                """
        };

        var result = analyzer.AnalyzePresets(new[] { preset });

        result.ReferencedColumns.Should().BeEquivalentTo("selected_interface", "filter");
    }

    [Fact]
    public void AnalyzePresetDetails_ScriptWithSuppressionFlag_ReportsSuppression()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                suppress_missing_column_warning: true
                steps:
                  - print: "${optional_column}"
                """
        };

        var result = analyzer.AnalyzePresetDetails(preset);

        result.SuppressMissingColumnWarning.Should().BeTrue();
        result.ReferencedColumns.Should().Contain("optional_column");
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
        result.UsesInteractive.Should().BeFalse();
        result.SftpUsesDefaultHost.Should().BeFalse();
        result.SftpUsesDefaultCredentials.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeSshRequirements_BrowserCallbackCapture_FlagsSingleHostOnlyWithoutSsh()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - browser_callback_capture:
                  start_url: "https://idp.example.com/start"
                  callback_path: "/oauth_callback"
                  into: callback_data
            """);

        result.RequiresSshSession.Should().BeFalse();
        result.UsesBrowserCallbackCapture.Should().BeTrue();
        result.UsesInteractive.Should().BeFalse();
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
        result.UsesInteractive.Should().BeFalse();
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
    public void AnalyzeSshRequirements_InteractiveStep_RequiresSshAndFlagsInteractive()
    {
        var result = AnalyzeSshRequirements("""
            ---
            steps:
              - interactive:
                  session: separate
            """);

        result.RequiresSshSession.Should().BeTrue();
        result.UsesInteractive.Should().BeTrue();
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
