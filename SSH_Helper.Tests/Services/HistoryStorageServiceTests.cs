using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public sealed class HistoryStorageServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;
    private readonly HistoryStorageService _service;

    public HistoryStorageServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"HistoryStorageTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _configPath = Path.Combine(_testDirectory, "config.json");
        _service = new HistoryStorageService(_configPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public void SaveRun_AndLoadRunPayload_PreservesFullLargePayload()
    {
        var entryId = "entry-large";
        var largeOutput = string.Concat(Enumerable.Repeat("sniffer-line-abcdefghijklmnopqrstuvwxyz0123456789\n", 12000));
        var payload = new HistoryRunPayload
        {
            Id = entryId,
            Output = largeOutput,
            HostResults = new List<HostHistoryEntry>
            {
                new()
                {
                    HostAddress = "10.0.0.1",
                    Output = largeOutput,
                    Success = true,
                    Timestamp = DateTime.UtcNow
                }
            },
            Details = CreateExecutionDetails("Large Payload Preset")
        };

        var indexEntry = new HistoryIndexEntry
        {
            Id = entryId,
            Label = "2026-02-15 10:11:12 - Large Payload Preset",
            CreatedAtUtc = DateTime.UtcNow,
            RunFileName = $"{entryId}.json"
        };

        _service.SaveRun(indexEntry, payload, maxEntries: 30);

        var index = _service.LoadIndex();
        index.Should().ContainSingle();
        index[0].HasHostResults.Should().BeTrue();
        index[0].HasDetails.Should().BeTrue();

        _service.TryLoadRunPayload(entryId, out var loaded).Should().BeTrue();
        loaded.Should().NotBeNull();
        loaded!.Output.Should().Be(largeOutput);
        loaded.HostResults.Should().ContainSingle();
        loaded.Details.Should().NotBeNull();
        loaded.Details!.InteractiveSessions.Should().ContainSingle();

        var runPath = Path.Combine(_testDirectory, "history", $"{entryId}.json");
        File.Exists(runPath).Should().BeTrue();
        File.ReadAllText(runPath).Should().Contain("sniffer-line-abcdefghijklmnopqrstuvwxyz0123456789");
    }

    [Fact]
    public void TryLoadRunPayload_WithoutDetails_SkipsDetailsPayload()
    {
        const string entryId = "entry-skip-details";
        var payload = new HistoryRunPayload
        {
            Id = entryId,
            Output = "short output",
            Details = new ExecutionDetails
            {
                PresetName = "Heavy Details",
                Commands = "show running-config",
                PresetType = "Simple",
                StartTimeUtc = DateTime.UtcNow.AddSeconds(-5),
                EndTimeUtc = DateTime.UtcNow,
                EnvironmentName = "Default",
                Username = "admin",
                CommandTimeoutSeconds = 30,
                ConnectionTimeoutSeconds = 15,
                UseConnectionPooling = false,
                RunMode = "Single preset",
                Hosts = new List<SSH_Helper.Models.HostExecutionContext>(),
                InteractiveSessions = new List<InteractiveTerminalSessionDetails>
                {
                    new()
                    {
                        SessionNumber = 1,
                        HostAddress = "10.0.0.1",
                        SessionMode = "separate",
                        EmulationMode = "full",
                        StartedAtUtc = DateTime.UtcNow.AddSeconds(-4),
                        EndedAtUtc = DateTime.UtcNow,
                        CloseReason = "completed",
                        Completed = true,
                        Transcript = string.Concat(Enumerable.Repeat("packet-line-0123456789\n", 50_000))
                    }
                }
            }
        };

        _service.SaveRun(
            new HistoryIndexEntry
            {
                Id = entryId,
                Label = "2026-02-15 15:00:00 - Skip Details",
                CreatedAtUtc = DateTime.UtcNow,
                HasDetails = true,
                RunFileName = $"{entryId}.json"
            },
            payload,
            maxEntries: 10);

        _service.TryLoadRunPayload(entryId, out var lightweightPayload, includeDetails: false).Should().BeTrue();
        lightweightPayload.Should().NotBeNull();
        lightweightPayload!.Output.Should().Be("short output");
        lightweightPayload.Details.Should().BeNull();

        _service.TryLoadRunPayload(entryId, out var fullPayload, includeDetails: true).Should().BeTrue();
        fullPayload.Should().NotBeNull();
        fullPayload!.Details.Should().NotBeNull();
        fullPayload.Details!.InteractiveSessions.Should().ContainSingle();
        fullPayload.Details.InteractiveSessions![0].Transcript.Should().Contain("packet-line-0123456789");
    }

    [Fact]
    public void TryLoadRunPayload_WithoutHostOutputs_SkipsHostOutputBodies()
    {
        const string entryId = "entry-skip-host-output";
        var largeHostOutput = string.Concat(Enumerable.Repeat("host-output-line-0123456789\n", 40_000));
        var payload = new HistoryRunPayload
        {
            Id = entryId,
            Output = "Done.",
            HostResults = new List<HostHistoryEntry>
            {
                new()
                {
                    HostAddress = "10.0.0.10",
                    Output = largeHostOutput,
                    Success = true,
                    Timestamp = DateTime.UtcNow
                }
            }
        };

        _service.SaveRun(
            new HistoryIndexEntry
            {
                Id = entryId,
                Label = "2026-02-15 15:10:00 - Skip Host Output",
                CreatedAtUtc = DateTime.UtcNow,
                HasHostResults = true,
                RunFileName = $"{entryId}.json"
            },
            payload,
            maxEntries: 10);

        _service.TryLoadRunPayload(
            entryId,
            out var lightweightPayload,
            includeDetails: false,
            includeHostOutputs: false).Should().BeTrue();
        lightweightPayload.Should().NotBeNull();
        lightweightPayload!.Output.Should().Be("Done.");
        lightweightPayload.HostResults.Should().ContainSingle();
        lightweightPayload.HostResults![0].HostAddress.Should().Be("10.0.0.10");
        lightweightPayload.HostResults[0].Success.Should().BeTrue();
        lightweightPayload.HostResults[0].Output.Should().BeEmpty();

        _service.TryLoadRunPayload(
            entryId,
            out var fullPayload,
            includeDetails: false,
            includeHostOutputs: true).Should().BeTrue();
        fullPayload.Should().NotBeNull();
        fullPayload!.HostResults.Should().ContainSingle();
        fullPayload.HostResults![0].Output.Should().Contain("host-output-line-0123456789");
    }

    [Fact]
    public void LoadIndex_DoesNotDependOnReadableRunPayload()
    {
        const string entryId = "entry-corrupt";
        var payload = new HistoryRunPayload
        {
            Id = entryId,
            Output = "ok"
        };
        var indexEntry = new HistoryIndexEntry
        {
            Id = entryId,
            Label = "2026-02-15 12:00:00 - Corrupt Run",
            CreatedAtUtc = DateTime.UtcNow,
            RunFileName = $"{entryId}.json"
        };

        _service.SaveRun(indexEntry, payload, maxEntries: 10);

        var runPath = Path.Combine(_testDirectory, "history", $"{entryId}.json");
        File.WriteAllText(runPath, "{ not-json");

        var index = _service.LoadIndex();
        index.Should().ContainSingle();
        index[0].Id.Should().Be(entryId);

        _service.TryLoadRunPayload(entryId, out var loaded).Should().BeFalse();
        loaded.Should().BeNull();
    }

    [Fact]
    public void SaveRun_EnforcesRetentionByDeletingOldestRuns()
    {
        for (int i = 0; i < 5; i++)
        {
            var id = $"entry-{i}";
            _service.SaveRun(
                new HistoryIndexEntry
                {
                    Id = id,
                    Label = $"2026-02-15 12:0{i}:00 - Entry {i}",
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(i),
                    RunFileName = $"{id}.json"
                },
                new HistoryRunPayload
                {
                    Id = id,
                    Output = $"output-{i}"
                },
                maxEntries: 3);
        }

        var index = _service.LoadIndex();
        index.Select(entry => entry.Id).Should().Equal("entry-4", "entry-3", "entry-2");
        File.Exists(Path.Combine(_testDirectory, "history", "entry-1.json")).Should().BeFalse();
        File.Exists(Path.Combine(_testDirectory, "history", "entry-0.json")).Should().BeFalse();
    }

    [Fact]
    public void DeleteRun_RemovesIndexEntryAndRunFile()
    {
        const string entryId = "entry-delete";
        _service.SaveRun(
            new HistoryIndexEntry
            {
                Id = entryId,
                Label = "2026-02-15 13:00:00 - Delete",
                CreatedAtUtc = DateTime.UtcNow,
                RunFileName = $"{entryId}.json"
            },
            new HistoryRunPayload
            {
                Id = entryId,
                Output = "delete me"
            },
            maxEntries: 10);

        _service.DeleteRun(entryId).Should().BeTrue();
        _service.LoadIndex().Should().BeEmpty();
        File.Exists(Path.Combine(_testDirectory, "history", $"{entryId}.json")).Should().BeFalse();
    }

    [Fact]
    public void DeleteAll_RemovesIndexAndAllRunFiles()
    {
        for (int i = 0; i < 3; i++)
        {
            var id = $"entry-clear-{i}";
            _service.SaveRun(
                new HistoryIndexEntry
                {
                    Id = id,
                    Label = $"2026-02-15 13:1{i}:00 - Clear {i}",
                    CreatedAtUtc = DateTime.UtcNow,
                    RunFileName = $"{id}.json"
                },
                new HistoryRunPayload
                {
                    Id = id,
                    Output = $"clear-{i}"
                },
                maxEntries: 10);
        }

        _service.DeleteAll();

        _service.LoadIndex().Should().BeEmpty();
        var runFolder = Path.Combine(_testDirectory, "history");
        if (Directory.Exists(runFolder))
        {
            Directory.GetFiles(runFolder, "*.json", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        }
    }

    [Fact]
    public void ImportLegacyHistory_ImportsEntriesWithDetailsAndHostResults()
    {
        var now = DateTime.UtcNow;
        var legacy = new List<HistoryEntry>
        {
            new()
            {
                Id = "legacy-1",
                Timestamp = "2026-02-15 14:00:00 - Legacy One",
                Output = "legacy-output-1",
                HostResults = new List<HostHistoryEntry>
                {
                    new()
                    {
                        HostAddress = "10.0.0.1",
                        Output = "host-output-1",
                        Success = true,
                        Timestamp = now
                    }
                },
                Details = CreateExecutionDetails("Legacy One")
            },
            new()
            {
                Id = "legacy-2",
                Timestamp = "2026-02-15 14:01:00 - Legacy Two",
                Output = "legacy-output-2"
            }
        };

        var imported = _service.ImportLegacyHistory(legacy, maxEntries: 10);

        imported.Should().Be(2);
        var index = _service.LoadIndex();
        index.Select(entry => entry.Id).Should().Equal("legacy-1", "legacy-2");
        index[0].HasHostResults.Should().BeTrue();
        index[0].HasDetails.Should().BeTrue();

        _service.TryLoadRunPayload("legacy-1", out var payload).Should().BeTrue();
        payload.Should().NotBeNull();
        payload!.HostResults.Should().ContainSingle();
        payload.Details.Should().NotBeNull();
        payload.Details!.PresetName.Should().Be("Legacy One");
    }

    private static ExecutionDetails CreateExecutionDetails(string presetName)
    {
        var now = DateTime.UtcNow;
        return new ExecutionDetails
        {
            PresetName = presetName,
            Commands = "show version",
            PresetType = "Simple",
            StartTimeUtc = now.AddSeconds(-30),
            EndTimeUtc = now,
            EnvironmentName = "Default",
            Username = "admin",
            CommandTimeoutSeconds = 30,
            ConnectionTimeoutSeconds = 15,
            UseConnectionPooling = true,
            RunMode = "Single preset",
            Hosts = new List<SSH_Helper.Models.HostExecutionContext>
            {
                new()
                {
                    HostAddress = "10.0.0.1",
                    Success = true,
                    TimestampUtc = now,
                    Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
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
                    StartedAtUtc = now.AddSeconds(-20),
                    EndedAtUtc = now.AddSeconds(-5),
                    CloseReason = "user_closed",
                    Completed = true,
                    Transcript = "show version"
                }
            }
        };
    }
}
