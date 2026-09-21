using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Original serialized volume contract and IsActive/IsTileCompatible (0x595B650).
// The retail fullscreen renderer feature is a separate consumer of this data.
[Serializable]
public sealed class FlarePara : VolumeComponent, IPostProcessComponent
{
    public BoolParameter _plareParaObject = new BoolParameter(false);
    public FloatParameter _rotation = new FloatParameter(0);
    public ColorParameter _flareColor = new ColorParameter(Color.black);
    public ClampedFloatParameter _flareScale = new ClampedFloatParameter(0, -1, 1);
    public ClampedFloatParameter _flareBlur = new ClampedFloatParameter(0.5f, 0, 1);
    public ColorParameter _paraColor = new ColorParameter(Color.white);
    public ClampedFloatParameter _paraScale = new ClampedFloatParameter(0, -1, 1);
    public ClampedFloatParameter _paraBlur = new ClampedFloatParameter(0.5f, 0, 1);
    public ClampedFloatParameter _balance = new ClampedFloatParameter(0.5f, -0.5f, 1.5f);
    public ClampedFloatParameter _roundness = new ClampedFloatParameter(0, 0, 1);
    public BoolParameter _latterBox = new BoolParameter(false);
    public BoolParameter _pillerBox = new BoolParameter(false);
    public BoolParameter _cameraAntiAilas = new BoolParameter(false);
    public bool IsActive() => _plareParaObject.value;
    public bool IsTileCompatible() => false;
}
