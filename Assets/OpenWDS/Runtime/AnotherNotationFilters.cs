using System;
using System.Collections.Generic;

namespace OpenWDS.Runtime
{
    /// <summary>Independent special-chart filter draft; never changes ordinary selection preferences.</summary>
    public sealed class AnotherNotationFilters
    {
        public int SortMode;
        public readonly HashSet<int> Difficulty = new HashSet<int>();
        public readonly HashSet<int> ClearLamp = new HashSet<int>();
        public readonly HashSet<int> MusicVideo = new HashSet<int>();
        public readonly HashSet<int> MusicType = new HashSet<int>();
        public readonly HashSet<long> Actors = new HashSet<long>();
        public bool AllActors = true;
        public Action Applied;
        public void CopyFrom(AnotherNotationFilters source)
        {
            SortMode = source.SortMode; AllActors = source.AllActors;
            Difficulty.Clear(); Difficulty.UnionWith(source.Difficulty);
            ClearLamp.Clear(); ClearLamp.UnionWith(source.ClearLamp);
            MusicVideo.Clear(); MusicVideo.UnionWith(source.MusicVideo);
            MusicType.Clear(); MusicType.UnionWith(source.MusicType);
            Actors.Clear(); Actors.UnionWith(source.Actors);
        }
    }
}
