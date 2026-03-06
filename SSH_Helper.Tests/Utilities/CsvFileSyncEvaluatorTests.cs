using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class CsvFileSyncEvaluatorTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly CsvManager _csvManager = new();

    public CsvFileSyncEvaluatorTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"CsvFileSyncEvaluatorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
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
    public void EvaluateEnvironment_WhenFingerprintMatches_ReturnsCurrent()
    {
        var filePath = WriteCsv("hosts.csv", new[]
        {
            new Dictionary<string, string> { [CsvManager.HostColumnName] = "192.168.1.10", ["port"] = "22" }
        });
        var fingerprint = CsvFileSyncEvaluator.Capture(filePath);
        var environment = CreateEnvironment(filePath, fingerprint);

        var evaluation = CsvFileSyncEvaluator.EvaluateEnvironment(environment, _csvManager);

        evaluation.Status.Should().Be(CsvFileSyncStatus.Current);
        CsvFileSyncEvaluator.Matches(fingerprint, evaluation.CurrentFingerprint).Should().BeTrue();
    }

    [Fact]
    public void EvaluateEnvironment_WhenFingerprintMissingAndSnapshotMatchesFile_ReturnsCurrent()
    {
        var filePath = WriteCsv("match.csv", new[]
        {
            new Dictionary<string, string> { [CsvManager.HostColumnName] = "192.168.1.11", ["port"] = "22" }
        });
        var environment = CreateEnvironment(filePath, null);

        var evaluation = CsvFileSyncEvaluator.EvaluateEnvironment(environment, _csvManager);

        evaluation.Status.Should().Be(CsvFileSyncStatus.Current);
        evaluation.CurrentFingerprint.Should().NotBeNull();
    }

    [Fact]
    public void EvaluateEnvironment_WhenFingerprintMissingAndSnapshotDiffers_ReturnsChangedOnDisk()
    {
        var filePath = WriteCsv("stale.csv", new[]
        {
            new Dictionary<string, string> { [CsvManager.HostColumnName] = "192.168.1.12", ["port"] = "22" }
        });
        var environment = CreateEnvironment(
            filePath,
            null,
            new List<Dictionary<string, string>>
            {
                new() { [CsvManager.HostColumnName] = "192.168.1.99", ["port"] = "2222" }
            });

        var evaluation = CsvFileSyncEvaluator.EvaluateEnvironment(environment, _csvManager);

        evaluation.Status.Should().Be(CsvFileSyncStatus.ChangedOnDisk);
    }

    [Fact]
    public void EvaluateEnvironment_WhenFileIsMissing_ReturnsMissingOnDisk()
    {
        var filePath = Path.Combine(_testDirectory, "missing.csv");
        var environment = CreateEnvironment(filePath, null);

        var evaluation = CsvFileSyncEvaluator.EvaluateEnvironment(environment, _csvManager);

        evaluation.Status.Should().Be(CsvFileSyncStatus.MissingOnDisk);
    }

    private string WriteCsv(string fileName, IReadOnlyList<Dictionary<string, string>> rows)
    {
        var filePath = Path.Combine(_testDirectory, fileName);
        var columns = new List<(string Name, string Header)>
        {
            (CsvManager.HostColumnName, CsvManager.HostColumnName),
            ("port", "port")
        };

        var csvRows = rows.Select(row => new List<string?>
        {
            row.TryGetValue(CsvManager.HostColumnName, out var host) ? host : string.Empty,
            row.TryGetValue("port", out var port) ? port : string.Empty
        });

        _csvManager.SaveToFile(filePath, columns, csvRows);
        return filePath;
    }

    private static EnvironmentConfig CreateEnvironment(
        string filePath,
        CsvFileFingerprint? fingerprint,
        List<Dictionary<string, string>>? hosts = null)
    {
        return new EnvironmentConfig
        {
            Name = "lab",
            HostColumns = new List<string> { CsvManager.HostColumnName, "port" },
            Hosts = hosts ?? new List<Dictionary<string, string>>
            {
                new() { [CsvManager.HostColumnName] = "192.168.1.11", ["port"] = "22" }
            },
            LastCsvPath = filePath,
            LastCsvFingerprint = fingerprint?.Clone()
        };
    }
}
