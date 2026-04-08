using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class QaPresetExecutionTests
{
    [Fact]
    public async Task NonRemoteQaPresets_ExecuteWithExpectedOutcomes()
    {
        var parser = new ScriptParser();
        var presets = LoadQaPresets();
        var coverageTargets = presets
            .Where(preset => IsNonRemotePreset(preset.Script.Description))
            .Where(preset => !ContainsDisallowedStepType(preset.Script))
            .OrderBy(preset => preset.Name, StringComparer.Ordinal)
            .ToList();

        coverageTargets.Should().NotBeEmpty("qa_presets.json should include non-remote presets to execute in CI");

        var failures = new List<string>();

        foreach (var preset in coverageTargets)
        {
            var expectedOutcome = GetExpectedOutcome(preset.Script.Description ?? string.Empty);
            var validationErrors = parser.Validate(preset.Script, preset.Commands);

            if (expectedOutcome == QaExpectedOutcome.IntentionalValidationFailure)
            {
                if (validationErrors.Count == 0)
                {
                    failures.Add($"{preset.Name}: expected validation failure but script validated successfully.");
                }

                continue;
            }

            if (validationErrors.Count > 0)
            {
                failures.Add($"{preset.Name}: validation failed: {string.Join(" | ", validationErrors)}");
                continue;
            }

            var executor = new ScriptExecutor();
            var context = new ScriptContext();
            var result = await executor.ExecuteAsync(preset.Script, context);

            switch (expectedOutcome)
            {
                case QaExpectedOutcome.Pass:
                    if (result.Status != ScriptExitStatus.Success)
                    {
                        failures.Add($"{preset.Name}: expected Success but got {result.Status} ({result.Message}).");
                    }

                    break;
                case QaExpectedOutcome.IntentionalFailureExit:
                    if (result.Status != ScriptExitStatus.Failure)
                    {
                        failures.Add($"{preset.Name}: expected Failure but got {result.Status} ({result.Message}).");
                    }

                    break;
                case QaExpectedOutcome.IntentionalErrorExit:
                case QaExpectedOutcome.IntentionalFailureStop:
                    if (result.Status != ScriptExitStatus.Error)
                    {
                        failures.Add($"{preset.Name}: expected Error but got {result.Status} ({result.Message}).");
                    }

                    break;
                default:
                    failures.Add($"{preset.Name}: unsupported expected outcome classification.");
                    break;
            }
        }

        failures.Should().BeEmpty();
    }

    private static bool IsNonRemotePreset(string? description)
    {
        var normalized = (description ?? string.Empty).ToLowerInvariant();

        return !normalized.Contains("ssh")
               && !normalized.Contains("internet")
               && !normalized.Contains("interactive")
               && !normalized.Contains("api")
               && !normalized.Contains("sftp")
               && !normalized.Contains("user interaction")
               && !normalized.Contains("input dialog")
               && !normalized.Contains("choose dialog")
               && !normalized.Contains("multiselect dialog")
               && !normalized.Contains("confirm dialog")
               && !normalized.Contains("file picker");
    }

    private static bool ContainsDisallowedStepType(Script script)
    {
        static bool IsDisallowed(StepType stepType)
        {
            return stepType == StepType.Send
                   || stepType == StepType.Http
                   || stepType == StepType.Webhook
                   || stepType == StepType.Ping
                   || stepType == StepType.Dns
                   || stepType == StepType.Portcheck
                   || stepType == StepType.Sftp
                   || stepType == StepType.BrowserCallbackCapture
                   || stepType == StepType.Input
                   || stepType == StepType.Choose
                   || stepType == StepType.Multiselect
                   || stepType == StepType.Confirm
                   || stepType == StepType.Interactive;
        }

        static bool ContainsInSteps(IEnumerable<ScriptStep> steps)
        {
            foreach (var step in steps)
            {
                if (IsDisallowed(step.GetStepType()))
                {
                    return true;
                }

                if (step.Then is { Count: > 0 } && ContainsInSteps(step.Then))
                {
                    return true;
                }

                if (step.Else is { Count: > 0 } && ContainsInSteps(step.Else))
                {
                    return true;
                }

                if (step.Do is { Count: > 0 } && ContainsInSteps(step.Do))
                {
                    return true;
                }

                if (step.Try is { Count: > 0 } && ContainsInSteps(step.Try))
                {
                    return true;
                }

                if (step.Catch is { Count: > 0 } && ContainsInSteps(step.Catch))
                {
                    return true;
                }

                if (step.Finally is { Count: > 0 } && ContainsInSteps(step.Finally))
                {
                    return true;
                }

                if (step.Elif is { Count: > 0 })
                {
                    foreach (var branch in step.Elif)
                    {
                        if (branch.Then is { Count: > 0 } && ContainsInSteps(branch.Then))
                        {
                            return true;
                        }
                    }
                }

                if (step.Cases is { Count: > 0 })
                {
                    foreach (var caseEntry in step.Cases)
                    {
                        if (caseEntry.Do is { Count: > 0 } && ContainsInSteps(caseEntry.Do))
                        {
                            return true;
                        }
                    }
                }

                if (step.Parallel?.Steps is { Count: > 0 } && ContainsInSteps(step.Parallel.Steps))
                {
                    return true;
                }
            }

            return false;
        }

        if (ContainsInSteps(script.Steps))
        {
            return true;
        }

        foreach (var subroutine in script.Subroutines.Values)
        {
            if (ContainsInSteps(subroutine.Steps))
            {
                return true;
            }
        }

        return false;
    }

    private static QaExpectedOutcome GetExpectedOutcome(string description)
    {
        if (description.Contains("Expected: intentional validation failure.", StringComparison.Ordinal))
        {
            return QaExpectedOutcome.IntentionalValidationFailure;
        }

        if (description.Contains("Expected: intentional failure stop.", StringComparison.Ordinal))
        {
            return QaExpectedOutcome.IntentionalFailureStop;
        }

        if (description.Contains("Expected: intentional failure exit.", StringComparison.Ordinal))
        {
            return QaExpectedOutcome.IntentionalFailureExit;
        }

        if (description.Contains("Expected: intentional error exit.", StringComparison.Ordinal))
        {
            return QaExpectedOutcome.IntentionalErrorExit;
        }

        if (description.Contains("Expected: pass", StringComparison.Ordinal))
        {
            return QaExpectedOutcome.Pass;
        }

        throw new Xunit.Sdk.XunitException($"Unsupported Expected clause: {description}");
    }

    private static IReadOnlyList<LoadedQaPreset> LoadQaPresets()
    {
        var parser = new ScriptParser();
        var repoRoot = FindRepositoryRoot();
        var qaPresetsPath = Path.Combine(repoRoot, "qa_presets.json");
        using var document = JsonDocument.Parse(File.ReadAllText(qaPresetsPath));

        document.RootElement.TryGetProperty("presets", out var presetsElement).Should().BeTrue();
        presetsElement.ValueKind.Should().Be(JsonValueKind.Object);

        var presets = new List<LoadedQaPreset>();
        foreach (var presetProperty in presetsElement.EnumerateObject())
        {
            if (!presetProperty.Value.TryGetProperty("commands", out var commandsElement) ||
                commandsElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var commands = commandsElement.GetString() ?? string.Empty;
            if (!ScriptParser.IsYamlScript(commands))
            {
                continue;
            }

            var script = parser.Parse(commands);
            presets.Add(new LoadedQaPreset(presetProperty.Name, commands, script));
        }

        presets.Should().NotBeEmpty("the QA catalog should contain YAML presets");
        return presets;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? search = new(AppContext.BaseDirectory);
        while (search != null && !File.Exists(Path.Combine(search.FullName, "qa_presets.json")))
        {
            search = search.Parent;
        }

        search.Should().NotBeNull("test should run from within the repository tree");
        return search!.FullName;
    }

    private enum QaExpectedOutcome
    {
        Pass,
        IntentionalFailureExit,
        IntentionalErrorExit,
        IntentionalFailureStop,
        IntentionalValidationFailure
    }

    private sealed record LoadedQaPreset(string Name, string Commands, Script Script);
}
