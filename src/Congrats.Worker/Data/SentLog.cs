using System.Linq;
using System.Text.Json;
using Congrats.Worker.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Congrats.Worker.Data;

public sealed class SentLog
{
    private readonly AppOptions _options;
    private readonly ILogger<SentLog> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private IReadOnlyCollection<SentLogEntry>? _cache;

    public SentLog(IOptions<AppOptions> options, ILogger<SentLog> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> AlreadySentAsync(string employeeId, OccasionType occasionType, DateOnly date, CancellationToken cancellationToken)
    {
        if (!_options.SentLog.Enabled)
        {
            return false;
        }

        var entries = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return entries.Any(entry => entry.EmployeeId == employeeId && entry.OccasionType == occasionType && entry.Date == date);
    }

    public async Task MarkSentAsync(SentLogEntry entry, CancellationToken cancellationToken)
    {
        if (!_options.SentLog.Enabled)
        {
            return;
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = (await LoadAsync(cancellationToken).ConfigureAwait(false)).ToList();
            entries.Add(entry);
            _logger.LogDebug("Marking sent log for {Employee} {Occasion} on {Date}", entry.EmployeeId, entry.OccasionType, entry.Date);

            var directory = Path.GetDirectoryName(_options.SentLog.StoragePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(_options.SentLog.StoragePath);
            await JsonSerializer.SerializeAsync(stream, entries, _serializerOptions, cancellationToken).ConfigureAwait(false);
            _cache = entries;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<IReadOnlyCollection<SentLogEntry>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_options.SentLog.StoragePath))
            {
                _cache = Array.Empty<SentLogEntry>();
                return _cache;
            }

            await using var stream = File.OpenRead(_options.SentLog.StoragePath);
            var entries = await JsonSerializer.DeserializeAsync<List<SentLogEntry>>(stream, _serializerOptions, cancellationToken).ConfigureAwait(false)
                ?? new List<SentLogEntry>();

            _cache = entries;
            return _cache;
        }
        finally
        {
            _mutex.Release();
        }
    }
}
