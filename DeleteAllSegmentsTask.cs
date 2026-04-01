using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace EdlToMediaSegments;

/// <summary>
/// Scheduled task that deletes all media segments from the database.
/// Appears in Jellyfin Dashboard → Scheduled Tasks with a manual Run button.
/// </summary>
public class DeleteAllSegmentsTask(
    IMediaSegmentManager mediaSegmentManager,
    ILibraryManager libraryManager,
    ILoggerFactory loggerFactory) : IScheduledTask
{
    private readonly ILogger<DeleteAllSegmentsTask> _logger = loggerFactory.CreateLogger<DeleteAllSegmentsTask>();

    /// <inheritdoc />
    public string Name => "Delete All Media Segments";

    /// <inheritdoc />
    public string Key => "DeleteAllMediaSegments";

    /// <inheritdoc />
    public string Description => "Deletes all media segments from the database. Run the Media Segment Scan task afterwards to re-generate them from EDL files.";

    /// <inheritdoc />
    public string Category => "Media Segments";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var query = new InternalItemsQuery
        {
            IsVirtualItem = false,
            Recursive = true,
            MediaTypes = [MediaType.Video, MediaType.Audio]
        };

        var items = libraryManager.GetItemList(query);
        var total = items.Count;
        var current = 0;
        var deleted = 0;

        _logger.LogInformation("Checking {Count} items for media segments to delete", total);

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (mediaSegmentManager.HasSegments(item.Id))
            {
                await mediaSegmentManager.DeleteSegmentsAsync(item.Id, cancellationToken).ConfigureAwait(false);
                deleted++;
                _logger.LogDebug("Deleted segments for {ItemName} ({ItemId})", item.Name, item.Id);
            }

            current++;
            progress.Report((double)current / total * 100);
        }

        _logger.LogInformation("Deleted media segments for {Count} items", deleted);
    }
}
