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

        // Find EDL file next to the media file.
        var edlFilePath = Path.ChangeExtension(mediaItem.Path, ".edl");
        if (!File.Exists(edlFilePath))
        {
            logger.LogDebug("EDL file {EdlFilePath} does not exist for item {ItemId}", edlFilePath, request.ItemId);
            return [];
        }
        logger.LogInformation("Found EDL file {EdlFilePath} for item {ItemId}", edlFilePath, request.ItemId);
        
        // Read EDL file.
        // For each line, add a MediaSegmentDto.
        // EDL format is:
        // start stop action
        // where start and stop are in seconds, and action is an int, 0 - cut, 3 - commercial.
        return ParseSegments(File.ReadAllLines(edlFilePath), item.Id, logger);
    }

    public static List<MediaSegmentDto> ParseSegments(string[] lines, Guid id, ILogger logger)
    {
        var segments = new List<MediaSegmentDto>();

        foreach (var line in lines)
        {
            var parts = line.Split(' ');
            if (parts.Length != 3)
            {
                logger.LogWarning("EDL line '{Line}' is not in the correct format", line);
                continue;
            }

            // Before parsing, log all three parts so we can see what we've got.
            logger.LogInformation("EDL line parts: Start='{Start}', Stop='{Stop}', Action='{Action}'", parts[0], parts[1], parts[2]);

            // Log the three parts separately, so we can see which part is invalid.
            if (!double.TryParse(parts[0], out var start))
            {
                logger.LogWarning("EDL line '{Line}' has invalid start time {Start}", line, parts[0]);
                continue;
            }
            if (!double.TryParse(parts[1], out var stop))
            {
                logger.LogWarning("EDL line '{Line}' has invalid stop time {Stop}", line, parts[1]);
                continue;
            }
            if (!int.TryParse(parts[2], out var action))
            {
                logger.LogWarning("EDL line '{Line}' has invalid action {Action}", line, parts[2]);
                continue;
            }

            segments.Add(new MediaSegmentDto
            {
                Id = Guid.NewGuid(),
                ItemId = id,
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