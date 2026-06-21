using System.ComponentModel.DataAnnotations;
using GraphqlDataService.Sample.GraphQL.Export;

namespace GraphqlDataService.Sample.Configuration;

public sealed class ExportConfig
{
    public const string SectionName = "Export";

    [Required]
    public string SharedStorageRoot { get; init; } = string.Empty;

    public ExportFileFormat DefaultFormat { get; init; } = ExportFileFormat.Csv;

    public bool DeleteJobFolderAfterDownload { get; init; }

    [Range(1, 10_000_000)]
    public int MaxRows { get; init; } = 100_000;

    [Range(1, 500_000_000_000)]
    public long MaxBytes { get; init; } = 50_000_000;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; init; } = 30;

    [Range(1, 100)]
    public int MaxConcurrentExports { get; init; } = 10;

    [Range(1, 300)]
    public int WorkerScanIntervalSeconds { get; init; } = 5;

    [Range(10, 3600)]
    public int LeaseSeconds { get; init; } = 120;

    [Range(1, 20)]
    public int MaxAttempts { get; init; } = 3;

    [Range(1, 30)]
    public int StatusPollingIntervalSeconds { get; init; } = 1;
}
