namespace GraphqlDataService.Sample.Data.Entities;

public sealed class GridSchema
{
    public Guid Id { get; set; }
    public required string EntityName { get; set; }
    public required string ViewName { get; set; }
    public required string Definition { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
