using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class ConfigurationServiceExecutionDetailsTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;

    public ConfigurationServiceExecutionDetailsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ExecutionDetailsConfigTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _configPath = Path.Combine(_testDirectory, "config.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public void SaveAndLoad_HistoryExecutionDetails_PreservesNestedData()
    {
        var startTimeUtc = new DateTime(2026, 2, 7, 12, 0, 0, DateTimeKind.Utc);
        var endTimeUtc = startTimeUtc.AddSeconds(42);

        var service = new ConfigurationService(_configPath);
        service.Load();
        service.Update(config =>
        {
            config.SavedState = new ApplicationState
            {
                History = new List<HistoryEntry>
                {
                    new()
                    {
                        Id = "entry-1",
                        Timestamp = "2026-02-07 12:00:42 - Custom",
                        Output = "command output",
                        HostResults = new List<HostHistoryEntry>
                        {
                            new()
                            {
                                HostAddress = "10.0.0.1",
                                Output = "ok",
                                Success = true,
                                Timestamp = endTimeUtc
                            }
                        },
                        Details = new ExecutionDetails
                        {
                            PresetName = "Custom",
                            Commands = "show system status",
                            PresetType = "Simple",
                            StartTimeUtc = startTimeUtc,
                            EndTimeUtc = endTimeUtc,
                            EnvironmentName = "Prod",
                            Username = "admin",
                            CommandTimeoutSeconds = 30,
                            ConnectionTimeoutSeconds = 15,
                            UseConnectionPooling = true,
                            RunMode = "Single preset",
                            IsFolderExecution = false,
                            FolderName = string.Empty,
                            ExecutedPresetNames = new List<string> { "Custom" },
                            Hosts = new List<SSH_Helper.Models.HostExecutionContext>
                            {
                                new()
                                {
                                    HostAddress = "10.0.0.1",
                                    Success = true,
                                    TimestampUtc = endTimeUtc,
                                    Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                    {
                                        ["host"] = "10.0.0.1",
                                        ["role"] = "edge"
                                    }
                                }
                            },
                            InteractiveSessions = new List<InteractiveTerminalSessionDetails>
                            {
                                new()
                                {
                                    SessionNumber = 1,
                                    HostAddress = "10.0.0.1",
                                    SessionMode = "separate",
                                    EmulationMode = "full",
                                    StartedAtUtc = startTimeUtc.AddSeconds(5),
                                    EndedAtUtc = startTimeUtc.AddSeconds(25),
                                    CloseReason = "user_closed",
                                    Completed = true,
                                    Transcript = "show version\nFortiGate-VM64"
                                }
                            }
                        }
                    }
                }
            };
        });

        var reloaded = new ConfigurationService(_configPath).Load();
        reloaded.SavedState.Should().NotBeNull();
        reloaded.SavedState!.History.Should().ContainSingle();

        var entry = reloaded.SavedState.History[0];
        entry.Details.Should().NotBeNull();
        entry.Details!.PresetName.Should().Be("Custom");
        entry.Details.Commands.Should().Be("show system status");
        entry.Details.StartTimeUtc.Should().Be(startTimeUtc);
        entry.Details.EndTimeUtc.Should().Be(endTimeUtc);
        entry.Details.EnvironmentName.Should().Be("Prod");
        entry.Details.Username.Should().Be("admin");
        entry.Details.CommandTimeoutSeconds.Should().Be(30);
        entry.Details.ConnectionTimeoutSeconds.Should().Be(15);
        entry.Details.UseConnectionPooling.Should().BeTrue();
        entry.Details.RunMode.Should().Be("Single preset");
        entry.Details.IsFolderExecution.Should().BeFalse();
        entry.Details.ExecutedPresetNames.Should().ContainSingle().Which.Should().Be("Custom");
        entry.Details.Hosts.Should().ContainSingle();
        entry.Details.Hosts[0].HostAddress.Should().Be("10.0.0.1");
        entry.Details.Hosts[0].Variables.Should().ContainKey("role").WhoseValue.Should().Be("edge");
        entry.Details.InteractiveSessions.Should().ContainSingle();
        entry.Details.InteractiveSessions[0].HostAddress.Should().Be("10.0.0.1");
        entry.Details.InteractiveSessions[0].CloseReason.Should().Be("user_closed");
        entry.Details.InteractiveSessions[0].Completed.Should().BeTrue();
        entry.Details.InteractiveSessions[0].Transcript.Should().Contain("FortiGate-VM64");
    }

    [Fact]
    public void Save_WritesSavedStateAsCompressedPayload_AndLoadInflatesIt()
    {
        var repeatedOutput = string.Concat(Enumerable.Repeat("sniffer-line-abcdefghijklmnopqrstuvwxyz0123456789\n", 5000));
        var service = new ConfigurationService(_configPath);
        service.Load();
        service.Update(config =>
        {
            config.SavedState = new ApplicationState
            {
                History = new List<HistoryEntry>
                {
                    new()
                    {
                        Id = "entry-compressed",
                        Timestamp = "2026-02-15 11:22:33 - Sniffer",
                        Output = repeatedOutput
                    }
                }
            };
        });

        var json = File.ReadAllText(_configPath);
        var root = JObject.Parse(json);

        root["SavedState"]?.Type.Should().Be(JTokenType.Null);
        root["SavedStateCompressed"]?.Type.Should().Be(JTokenType.String);
        root["SavedStateCompressed"]!.ToString().Should().StartWith("gz64:");
        json.Should().NotContain("sniffer-line-abcdefghijklmnopqrstuvwxyz0123456789");

        var reloaded = new ConfigurationService(_configPath).Load();
        reloaded.SavedState.Should().NotBeNull();
        reloaded.SavedState!.History.Should().ContainSingle();
        reloaded.SavedState.History[0].Output.Should().Be(repeatedOutput);
    }

    [Fact]
    public void Load_MixedModernAndLegacyPresets_LoadsBothWithoutFallback()
    {
        File.WriteAllText(_configPath, """
            {
              "Username": "tester",
              "Timeout": 10,
              "Presets": {
                "ModernPreset": { "Commands": "show version", "IsScript": false },
                "LegacyPreset": "show run"
              }
            }
            """);

        var service = new ConfigurationService(_configPath);
        var config = service.Load();

        service.ConfigLoadError.Should().BeNull();
        config.Presets.Should().ContainKey("ModernPreset");
        config.Presets.Should().ContainKey("LegacyPreset");
        config.Presets["ModernPreset"].Commands.Should().Be("show version");
        config.Presets["LegacyPreset"].Commands.Should().Be("show run");
    }
}
