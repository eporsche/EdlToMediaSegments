using Jellyfin.Data.Enums;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model;
using MediaBrowser.Model.MediaSegments;
using Microsoft.Extensions.Logging;

namespace EdlToMediaSegments;

/// <inheritdoc />
public class EdlToMediaSegmentsProvider(
    IItemRepository itemRepository,
    ILoggerFactory loggerFactory) : IMediaSegmentProvider
{
    private readonly ILogger<EdlToMediaSegmentsProvider> logger = loggerFactory.CreateLogger<EdlToMediaSegmentsProvider>();

    /// <inheritdoc />
    public string Name => "EDL to Media Segments Provider";

    /// <inheritdoc />
    public ValueTask<bool> Supports(BaseItem item) => new(item is IHasMediaSources);

    // private MediaSegmentType? GetMediaSegmentType(string name)
    // {
    //     var mappings = Plugin.Instance?.Configuration.Patterns();

    //     foreach (var item in mappings!.Where(e => !string.IsNullOrWhiteSpace(e.Regex)))
    //     {
    //         if (!string.IsNullOrEmpty(item.Regex)
    //             && Regex.IsMatch(name, item.Regex, RegexOptions.IgnoreCase | RegexOptions.Singleline))
    //         {
    //             return item.Type;
    //         }
    //     }

    //     return null;
    // }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MediaSegmentDto>> GetMediaSegments(MediaSegmentGenerationRequest request, CancellationToken cancellationToken)
    {
        var item = itemRepository.RetrieveItem(request.ItemId);
        if (item is not IHasMediaSources mediaItem)
        {
            logger.LogDebug("Item {ItemId} is not IHasMediaSources", request.ItemId);
            return [];
        }

        // TODO: Find a way to delete existing media segments if the EDL file changes, goes away, etc.
        // If this item already has any media segments, ignore the edl file.
        var parsedFilePath = Path.ChangeExtension(mediaItem.Path, ".edlparsed");
        if (File.Exists(parsedFilePath))
        {
            logger.LogDebug("Item {ItemId} already has media segments, ignoring EDL file", request.ItemId);
            return [];
        }

        // Find EDL file next to the media file.
        var edlFilePath = Path.ChangeExtension(mediaItem.Path, ".edl");
        if (!File.Exists(edlFilePath))
        {
            logger.LogDebug("EDL file {EdlFilePath} does not exist for item {ItemId}", edlFilePath, request.ItemId);
            return [];
        }
        logger.LogInformation("Found EDL file {EdlFilePath} for item {ItemId}", edlFilePath, request.ItemId);
        // Touch the .edlparsed file to indicate we've processed this item.
        File.WriteAllText(parsedFilePath, DateTime.UtcNow.ToString("o"));

        // Read EDL file.
        // For each line, add a MediaSegmentDto.
        // EDL format is:
        // start stop action
        // where start and stop are in seconds, and action is an int, 0 - cut, 3 - commercial.
        var segments = new List<MediaSegmentDto>();

        var edlLines = File.ReadAllLines(edlFilePath);
        foreach (var line in edlLines)
        {
            var parts = line.Split(' ');
            if (parts.Length != 3)
            {
                logger.LogWarning("EDL line '{Line}' is not in the correct format", line);
                continue;
            }

            if (!int.TryParse(parts[0], out var start) || !int.TryParse(parts[1], out var stop) || !int.TryParse(parts[2], out var action))
            {
                logger.LogWarning("EDL line '{Line}' has invalid numbers", line);
                continue;
            }

            segments.Add(new MediaSegmentDto
            {
                Id = Guid.NewGuid(),
                ItemId = item.Id,
                Type = action switch
                {
                    // TODO: flesh this out
                    0 => MediaSegmentType.Commercial, // Cut
                    3 => MediaSegmentType.Commercial, // CommercialBreak
                    _ => MediaSegmentType.Unknown
                },
                StartTicks = ConvertToTicks(start),
                EndTicks = ConvertToTicks(stop)
            });
        }

        return segments;
    }

    private static long ConvertToTicks(double seconds) => (long)(seconds * TimeSpan.TicksPerSecond);
}