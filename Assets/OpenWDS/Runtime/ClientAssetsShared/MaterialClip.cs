using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class MaterialClip : PlayableAsset, ITimelineClipAsset
{
    public MaterialBehaviour template = new MaterialBehaviour();
    public ClipCaps clipCaps => ClipCaps.None;
    public override Playable CreatePlayable(PlayableGraph graph, GameObject go) =>
        ScriptPlayable<MaterialBehaviour>.Create(graph, template, 0);
}
