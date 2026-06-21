namespace GraphqlDataService.Sample.GraphQL.Export;

public enum ExportFileFormat
{
    Csv,
    Excel
}

public enum ExportStatus
{
    Queued,
    Claimed,
    Running,
    Completed,
    Failed,
    Downloading,
    Downloaded,
    Expired
}

public enum ExportRequestType
{
    ColumnSelector,
    GridView
}

public enum ExportFilterOperator
{
    Equal,
    Contains,
    GreaterThanOrEqual,
    LessThanOrEqual
}

public enum ExportSortDirection
{
    Ascending,
    Descending
}

public sealed class ExportFilterInput
{
    public required string Field { get; set; }
    public ExportFilterOperator Operator { get; set; }
    public required string Value { get; set; }
}

public sealed class ExportOrderInput
{
    public required string Field { get; set; }
    public ExportSortDirection Direction { get; set; }
}

public sealed record ExportJob(
    Guid ExportId,
    ExportStatus Status,
    string DownloadUrl,
    DateTimeOffset ExpiresUtc);

public sealed record ExportJobStatusUpdate(
    Guid ExportId,
    ExportStatus Status,
    string? DownloadUrl,
    DateTimeOffset ExpiresUtc,
    string? ErrorCode);

public sealed class ExportJobRecord
{
    public int SchemaVersion { get; set; } = 1;
    public Guid ExportId { get; set; }
    public required string EntityName { get; set; }
    public ExportRequestType RequestType { get; set; }
    public ExportFileFormat Format { get; set; }
    public List<string> Columns { get; set; } = [];
    public List<ExportFilterInput> Filters { get; set; } = [];
    public List<ExportOrderInput> Order { get; set; } = [];
    public string? GridViewName { get; set; }
    public required string RequestedBy { get; set; }
    public string? TenantId { get; set; }
    public DateTimeOffset RequestedUtc { get; set; }
    public string? ServerName { get; set; }
    public ExportStatus Status { get; set; }
    public int Attempt { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public DateTimeOffset? StartedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public int? RowCount { get; set; }
    public long? FileBytes { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
