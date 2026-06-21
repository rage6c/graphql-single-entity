using System.Runtime.CompilerServices;
using GraphqlDataService.Sample.Configuration;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.Converters;
using YamlDotNet.Serialization.NamingConventions;
using IOPath = System.IO.Path;

namespace GraphqlDataService.Sample.GraphQL.Export;

public sealed class YamlExportJobStore : IExportJobStore
{
    private const string JobFileName = "job.yaml";
    private const string ClaimFileName = "claim.lock";
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;
    private readonly ExportConfig _config;

    public YamlExportJobStore(IOptions<ExportConfig> options)
    {
        _config = options.Value;
        RootPath = IOPath.GetFullPath(_config.SharedStorageRoot);
        Directory.CreateDirectory(RootPath);
        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new DateTimeOffsetConverter())
            .Build();
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new DateTimeOffsetConverter())
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public string RootPath { get; }

    public async Task CreateAsync(ExportJobRecord job, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(GetJobFolder(job.ExportId));
        await WriteAsync(job, cancellationToken);
    }

    public async Task<ExportJobRecord?> ReadAsync(Guid exportId, CancellationToken cancellationToken)
    {
        var path = IOPath.Combine(GetJobFolder(exportId), JobFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var yaml = await File.ReadAllTextAsync(path, cancellationToken);
        var job = _deserializer.Deserialize<ExportJobRecord>(yaml);
        return job.SchemaVersion == 1 && job.ExportId == exportId ? job : null;
    }

    public async IAsyncEnumerable<Guid> ListAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var folder in Directory.EnumerateDirectories(RootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Guid.TryParseExact(IOPath.GetFileName(folder), "D", out var exportId))
            {
                yield return exportId;
            }

            await Task.Yield();
        }
    }

    public async Task<ExportJobRecord?> TryClaimAsync(
        Guid exportId,
        string serverName,
        CancellationToken cancellationToken)
    {
        var lockPath = IOPath.Combine(GetJobFolder(exportId), ClaimFileName);
        try
        {
            await using var claim = new FileStream(lockPath, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 1,
                Options = FileOptions.Asynchronous
            });
            await claim.WriteAsync(new byte[] { 1 }, cancellationToken);
        }
        catch (IOException)
        {
            var observedUtc = DateTimeOffset.UtcNow;
            var lockAge = observedUtc - new DateTimeOffset(File.GetLastWriteTimeUtc(lockPath), TimeSpan.Zero);
            var stale = await ReadAsync(exportId, cancellationToken);
            if (stale is null ||
                stale.Status is not (ExportStatus.Claimed or ExportStatus.Running) ||
                stale.LeaseUntilUtc is null ||
                stale.LeaseUntilUtc.Value >= observedUtc ||
                lockAge < TimeSpan.FromSeconds(_config.LeaseSeconds) ||
                stale.Attempt >= _config.MaxAttempts)
            {
                return null;
            }

            try
            {
                File.Delete(lockPath);
                await using var recoveredClaim = new FileStream(lockPath, new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    BufferSize = 1,
                    Options = FileOptions.Asynchronous
                });
                await recoveredClaim.WriteAsync(new byte[] { 1 }, cancellationToken);
            }
            catch (IOException)
            {
                return null;
            }
        }

        var job = await ReadAsync(exportId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var claimable = job is not null &&
            (job.Status == ExportStatus.Queued ||
             (job.Status is ExportStatus.Claimed or ExportStatus.Running && job.LeaseUntilUtc < now)) &&
            job.Attempt < _config.MaxAttempts;
        if (!claimable)
        {
            ReleaseClaim(exportId);
            return null;
        }

        job!.Status = ExportStatus.Claimed;
        job.ServerName = serverName;
        job.Attempt++;
        job.LeaseUntilUtc = now.AddSeconds(_config.LeaseSeconds);
        await WriteAsync(job, cancellationToken);
        return job;
    }

    public async Task WriteAsync(ExportJobRecord job, CancellationToken cancellationToken)
    {
        var folder = GetJobFolder(job.ExportId);
        Directory.CreateDirectory(folder);
        var destination = IOPath.Combine(folder, JobFileName);
        var temporary = IOPath.Combine(folder, $".{JobFileName}.{Guid.NewGuid():N}.tmp");
        var yaml = _serializer.Serialize(job);

        await using (var stream = new FileStream(temporary, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 4096,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough
        }))
        await using (var writer = new StreamWriter(stream))
        {
            await writer.WriteAsync(yaml.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporary, destination, overwrite: true);
    }

    public void ReleaseClaim(Guid exportId)
    {
        var path = IOPath.Combine(GetJobFolder(exportId), ClaimFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public string GetJobFolder(Guid exportId) =>
        IOPath.Combine(RootPath, exportId.ToString("D"));

    public string GetFilePath(Guid exportId, string fileName)
    {
        var folder = GetJobFolder(exportId);
        var path = IOPath.GetFullPath(IOPath.Combine(folder, IOPath.GetFileName(fileName)));
        if (!path.StartsWith(folder + IOPath.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Export path escaped its job folder.");
        }

        return path;
    }

    public Task DeleteAsync(Guid exportId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var folder = GetJobFolder(exportId);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        return Task.CompletedTask;
    }
}
