using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Sirius.Scarab
{
    public static class TimelineUtility
    {
        // GetTracks state machine 0x73e9984; recurse in the original enumeration
        // order, including repeated descendants. Do not add hierarchy guessing.
        public static IEnumerable<(PlayableDirector playableDirector, T track)> GetTracks<T>(
            PlayableDirector playableDirector, string trackName, bool recursive) where T : TrackAsset
        {
            if (playableDirector == null || playableDirector.playableAsset == null ||
                playableDirector.playableAsset.outputs == null) yield break;
            foreach (var binding in playableDirector.playableAsset.outputs)
                if ((string.IsNullOrEmpty(trackName) || binding.streamName == trackName) && binding.sourceObject is T track)
                    yield return (playableDirector, track);
            if (recursive)
                foreach (var child in GetChildPlayableDirectors(playableDirector))
                    foreach (var track in GetTracks<T>(child, trackName, true))
                        yield return track;
        }

        // 0x59864f8: only ControlPlayableAsset exposed references are followed;
        // an ordinary child Transform is not a child timeline in this method.
        public static IEnumerable<PlayableDirector> GetChildPlayableDirectors(PlayableDirector playableDirector)
        {
            foreach (var track in GetTracks<ControlTrack>(playableDirector, null, false))
                foreach (var clip in track.track.GetClips())
                    if (clip.asset is ControlPlayableAsset control)
                    {
                        var child = (GameObject)playableDirector.GetReferenceValue(control.sourceGameObject.exposedName,
                            out var valid);
                        if (valid && child != null && child.TryGetComponent<PlayableDirector>(out var director))
                        {
                            yield return director;
                            foreach (var descendant in GetChildPlayableDirectors(director)) yield return descendant;
                        }
                    }
        }

        public static void BindComponentToTrack<T>(Object component, PlayableDirector masterTimeline,
            string trackName) where T : TrackAsset
        {
            if (component == null) return;
            foreach (var track in GetTracks<T>(masterTimeline, trackName, true))
                track.playableDirector.SetGenericBinding(track.track, component);
        }
    }
}
