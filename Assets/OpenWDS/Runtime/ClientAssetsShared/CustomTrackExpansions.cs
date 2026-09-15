using UnityEngine.Timeline;

public static class CustomTrackExpansions
{
    // Current 0x5965770. Adjacent clips share their boundary with the next clip;
    // otherwise the current clip includes its end, including the final clip.
    public static bool HasClipNumber(double time, int i, TimelineClip[] clips)
    {
        var hit = clips[i].start <= time && clips[i].end >= time;
        if (i != clips.Length - 1 && clips[i + 1].start == clips[i].end)
            hit = clips[i].start <= time && clips[i].end > time;
        return hit;
    }
}
