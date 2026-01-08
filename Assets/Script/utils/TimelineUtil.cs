using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Static utility class for timeline time access and frame conversions
/// Provides clean API for getting current timeline time, converting between time and frames, and seeking
/// Caches PlayableDirector reference for performance
/// </summary>
public static class TimelineUtil
{
    private static PlayableDirector cachedDirector;

    /// <summary>
    /// Get or find the PlayableDirector (cached for performance)
    /// Searches scene once, then reuses cached reference
    /// </summary>
    private static PlayableDirector GetDirector()
    {
        if (cachedDirector == null)
        {
            // PlayableDirector is the Unity component that controls Timeline playback
            cachedDirector = Object.FindFirstObjectByType<PlayableDirector>();
        }
        return cachedDirector;
    }

    /// <summary>
    /// Get current timeline playback time in seconds
    /// </summary>
    /// <returns>Current timeline time, or 0 if no PlayableDirector found</returns>
    public static double GetCurrentTimelineTime()
    {
        PlayableDirector director = GetDirector();
        return director != null ? director.time : 0;
    }

    /// <summary>
    /// Convert timeline time (seconds) to frame index
    /// Uses RoundToInt to avoid floating-point precision issues when time is set from frame index
    /// (e.g., frame 493: time=493/30=16.4333..., then 16.4333*30=492.999... would floor to 492)
    /// </summary>
    /// <param name="timelineTime">Time in seconds</param>
    /// <param name="frameRate">Frames per second</param>
    /// <returns>Frame index (rounded)</returns>
    public static int GetFrameIndexFromTime(double timelineTime, float frameRate)
    {
        return Mathf.RoundToInt((float)(timelineTime * frameRate));
    }

    /// <summary>
    /// Convert frame index to timeline time (seconds)
    /// </summary>
    /// <param name="frameIndex">Frame number</param>
    /// <param name="frameRate">Frames per second</param>
    /// <returns>Time in seconds</returns>
    public static double GetTimeFromFrameIndex(int frameIndex, float frameRate)
    {
        return frameRate > 0 ? frameIndex / frameRate : 0;
    }

    /// <summary>
    /// Seek timeline to specific time (seconds)
    /// Forces immediate evaluation to ensure BVH_Character updates synchronously
    /// </summary>
    /// <param name="timeInSeconds">Target time in seconds</param>
    public static void SeekToTime(double timeInSeconds)
    {
        PlayableDirector director = GetDirector();
        if (director != null)
        {
            director.time = timeInSeconds;
            director.Evaluate();
        }
    }

    /// <summary>
    /// Seek timeline to specific frame
    /// </summary>
    /// <param name="frameIndex">Target frame number</param>
    /// <param name="frameRate">Frames per second</param>
    public static void SeekToFrame(int frameIndex, float frameRate)
    {
        double timeInSeconds = GetTimeFromFrameIndex(frameIndex, frameRate);
        SeekToTime(timeInSeconds);
    }

    /// <summary>
    /// Clear cached director reference
    /// Call this if PlayableDirector is destroyed and recreated
    /// </summary>
    public static void ClearCache()
    {
        cachedDirector = null;
    }
}
