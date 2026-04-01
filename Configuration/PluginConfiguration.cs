using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Model.Plugins;

namespace EdlToMediaSegments.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        // Default mapping: Comskip writes commercials as action 0 (cut)
        EdlAction0 = MediaSegmentType.Commercial;
        EdlAction1 = MediaSegmentType.Commercial;
        EdlAction2 = MediaSegmentType.Commercial;
        EdlAction3 = MediaSegmentType.Commercial;
    }

    /// <summary>
    /// Gets or sets the segment type for EDL action 0 (Cut).
    /// </summary>
    public MediaSegmentType EdlAction0 { get; set; }

    /// <summary>
    /// Gets or sets the segment type for EDL action 1 (Mute).
    /// </summary>
    public MediaSegmentType EdlAction1 { get; set; }

    /// <summary>
    /// Gets or sets the segment type for EDL action 2 (Scene marker).
    /// </summary>
    public MediaSegmentType EdlAction2 { get; set; }

    /// <summary>
    /// Gets or sets the segment type for EDL action 3 (Commercial skip).
    /// </summary>
    public MediaSegmentType EdlAction3 { get; set; }
}
