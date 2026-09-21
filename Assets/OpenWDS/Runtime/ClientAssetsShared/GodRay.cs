using UnityEngine;

// Original 0x595FD04-0x5960018. The uninitialized component deliberately does
// nothing, as in retail; placement starts only after its presenter calls Init.
public sealed class GodRay : MonoBehaviour
{
    private Camera _camera;
    private FlarePara _parameters;
    private float _scale = 1, _offset = 8, _rotationFactor = 20;
    private float _aspect;
    public void Init(FlarePara parameters, Camera camera)
    {
        _parameters = parameters; _camera = camera;
        _aspect = (float)Screen.width / Screen.height;
    }
    public void SetParamater(float scale, float offset, float rotation)
    {
        _scale = scale; _offset = offset; _rotationFactor = rotation;
    }
    private void LateUpdate()
    {
        if (_camera == null || _parameters == null) return;
        const float referenceAspect = 2.1666667f;
        float angle = (_parameters._rotation.value + 90) * 0.017453f;
        float width = _aspect >= referenceAspect ? Screen.height * referenceAspect : Screen.width;
        float height = _aspect >= referenceAspect ? Screen.height : Screen.width / referenceAspect;
        transform.localScale = Vector3.one * _scale;
        transform.position = _camera.ScreenToWorldPoint(new Vector3(
            (_offset * Mathf.Cos(angle) / 3 + 0.5f) * width,
            (_offset * Mathf.Sin(angle) * 0.5f + 0.5f) * height, _camera.focalLength / 20));
        transform.eulerAngles = new Vector3(0, _camera.transform.eulerAngles.y - _rotationFactor * _camera.transform.position.x, 0);
    }
}
