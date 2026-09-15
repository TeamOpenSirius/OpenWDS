using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class MaterialMixer : PlayableBehaviour
{
    public TimelineClip[] clips { get; set; }
    public PlayableDirector playableDirector { get; set; }
    public Material material { get; set; }
    public override void OnGraphStart(Playable playable)
    {
        if (!material) return;
        for (var i = 0; i < playable.GetInputCount(); i++)
            ((ScriptPlayable<MaterialBehaviour>)playable.GetInput(i)).GetBehaviour().HasProperty(material);
    }
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!material) return;
        var time = playableDirector.time;
        for (var i = 0; i < playable.GetInputCount(); i++)
            if (CustomTrackExpansions.HasClipNumber(time, i, clips))
            {
                ((ScriptPlayable<MaterialBehaviour>)playable.GetInput(i)).GetBehaviour().SetValue(material);
                return;
            }
    }
}
