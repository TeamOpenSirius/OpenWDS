using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.5f, 0.84f, 1f), TrackBindingType(typeof(Material)), TrackClipType(typeof(MaterialClip))]
public class MaterialTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var playable = ScriptPlayable<MaterialMixer>.Create(graph, inputCount);
        var director = go.GetComponent<PlayableDirector>();
        var mixer = playable.GetBehaviour();
        mixer.material = director.GetGenericBinding(this) as Material;
        mixer.clips = GetClips().ToArray();
        mixer.playableDirector = director;
        return playable;
    }
}
