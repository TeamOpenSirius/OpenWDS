using System;
using System.Linq;
using Sirius.Scarab;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace OpenWDS.Editor
{
    internal static class ValidateCharacterTimeline
    {
        internal static void Run()
        {
            var host = new GameObject("timeline-material-fixture");
            var child = new GameObject("timeline-child-fixture");
            var grandchild = new GameObject("timeline-grandchild-fixture");
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var childTimeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var grandTimeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var material = new Material(Shader.Find("UI/Default"));
            try
            {
                var director = host.AddComponent<PlayableDirector>();
                var childDirector = child.AddComponent<PlayableDirector>();
                var grandDirector = grandchild.AddComponent<PlayableDirector>();
                director.playableAsset = timeline;
                childDirector.playableAsset = childTimeline;
                grandDirector.playableAsset = grandTimeline;
                var track = timeline.CreateTrack<MaterialTrack>(null, "material");
                var childTrack = childTimeline.CreateTrack<MaterialTrack>(null, "material");
                var grandTrack = grandTimeline.CreateTrack<MaterialTrack>(null, "material");
                var first = track.CreateClip<MaterialClip>();
                first.start = 0; first.duration = 1;
                var second = track.CreateClip<MaterialClip>();
                second.start = 1; second.duration = 1;
                var clips = new[] { first, second };
                Require(!CustomTrackExpansions.HasClipNumber(-0.1, 0, clips), "Before clip start");
                Require(CustomTrackExpansions.HasClipNumber(0, 0, clips), "Clip start inclusive");
                Require(!CustomTrackExpansions.HasClipNumber(1, 0, clips) &&
                    CustomTrackExpansions.HasClipNumber(1, 1, clips), "Adjacent boundary belongs to next clip");
                Require(CustomTrackExpansions.HasClipNumber(2, 1, clips) &&
                    !CustomTrackExpansions.HasClipNumber(2.01, 1, clips), "Final endpoint inclusive");
                second.start = 1.5;
                Require(CustomTrackExpansions.HasClipNumber(1, 0, clips) &&
                    !CustomTrackExpansions.HasClipNumber(1.1, 0, clips), "Gap retains exact endpoint only");
                second.start = 1;
                ((MaterialClip)first.asset).template._propertyName = "Color";
                ((MaterialClip)first.asset).template._propertyType = PropertyType.Color;
                ((MaterialClip)first.asset).template._colorField = Color.red;
                ((MaterialClip)second.asset).template._propertyName = "_Color";
                ((MaterialClip)second.asset).template._propertyType = PropertyType.Color;
                ((MaterialClip)second.asset).template._colorField = Color.green;
                director.SetGenericBinding(track, material);
                director.time = 0.5; director.Evaluate();
                Require((Vector4)material.color == (Vector4)Color.red, "Actual graph first material clip");
                director.time = 1; director.Evaluate();
                Require((Vector4)material.color == (Vector4)Color.green, "Actual graph shared boundary");
                director.Stop();
                second.start = 0.5;
                director.RebuildGraph();
                director.time = 0.75; director.Evaluate();
                Require((Vector4)material.color == (Vector4)Color.red, "Overlapping clips choose first, without blending");
                director.Stop();

                var control = timeline.CreateTrack<ControlTrack>(null, "child").CreateClip<ControlPlayableAsset>();
                ((ControlPlayableAsset)control.asset).sourceGameObject.exposedName = "child-reference";
                director.SetReferenceValue("child-reference", child);
                var nested = childTimeline.CreateTrack<ControlTrack>(null, "grandchild").CreateClip<ControlPlayableAsset>();
                ((ControlPlayableAsset)nested.asset).sourceGameObject.exposedName = "grandchild-reference";
                childDirector.SetReferenceValue("grandchild-reference", grandchild);
                // Both recursion levels are present in the original. In a chain
                // of three directors the grandchild material track occurs twice.
                var found = TimelineUtility.GetTracks<MaterialTrack>(director, "material", true).ToArray();
                Require(found.Length == 4 && found[0].track == track && found[1].track == childTrack &&
                    found[2].track == grandTrack && found[3].track == grandTrack, "Original nested enumeration order");
                TimelineUtility.BindComponentToTrack<MaterialTrack>(material, director, "material");
                Require(childDirector.GetGenericBinding(childTrack) == material &&
                    grandDirector.GetGenericBinding(grandTrack) == material, "Binding child timeline material");
                Require(!TimelineUtility.GetTracks<MaterialTrack>(director, "absent", true).Any(), "Track name filtering");
                Require(!TimelineUtility.GetTracks<MaterialTrack>(null, null, true).Any(), "Null director");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(child);
                Object.DestroyImmediate(grandchild);
                DestroyTimeline(timeline);
                DestroyTimeline(childTimeline);
                DestroyTimeline(grandTimeline);
                Object.DestroyImmediate(material);
            }
        }
        private static void DestroyTimeline(TimelineAsset timeline)
        {
            foreach (var track in timeline.GetRootTracks().ToArray())
            {
                foreach (var clip in track.GetClips()) Object.DestroyImmediate(clip.asset);
                Object.DestroyImmediate(track);
            }
            Object.DestroyImmediate(timeline);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
