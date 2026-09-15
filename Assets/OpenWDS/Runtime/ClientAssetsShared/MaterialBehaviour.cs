using System;
using UnityEngine;
using UnityEngine.Playables;

public enum PropertyType { None = 0, Float = 1, Int = 2, Vector2 = 3, Vector3 = 4, Color = 5 }

[Serializable]
public class MaterialBehaviour : PlayableBehaviour
{
    public string _propertyName;
    public PropertyType _propertyType;
    public float _floatField;
    public int _intField;
    public Vector2 _vector2Field;
    public Vector3 _vector3Field;
    public Color _colorField;
    private bool hasPropertyFlag;

    public void HasProperty(Material material)
    {
        if (string.IsNullOrEmpty(_propertyName)) return;
        var id = GetPropertyNameToId(_propertyName);
        switch (_propertyType)
        {
            case PropertyType.Float: hasPropertyFlag = material.HasFloat(id); break;
            case PropertyType.Int: hasPropertyFlag = material.HasInteger(id); break;
            case PropertyType.Vector2:
            case PropertyType.Vector3: hasPropertyFlag = material.HasVector(id); break;
            case PropertyType.Color: hasPropertyFlag = material.HasColor(id); break;
            default: hasPropertyFlag = false; break;
        }
    }
    public void SetValue(Material material)
    {
        if (!hasPropertyFlag || string.IsNullOrEmpty(_propertyName)) return;
        var id = GetPropertyNameToId(_propertyName);
        switch (_propertyType)
        {
            case PropertyType.Float: material.SetFloat(id, _floatField); break;
            case PropertyType.Int: material.SetInteger(id, _intField); break;
            case PropertyType.Vector2: material.SetVector(id, new Vector4(_vector2Field.x, _vector2Field.y, 0, 0)); break;
            case PropertyType.Vector3: material.SetVector(id, new Vector4(_vector3Field.x, _vector3Field.y, _vector3Field.z, 0)); break;
            case PropertyType.Color: material.SetVector(id, (Vector4)_colorField); break;
        }
    }
    private int GetPropertyNameToId(string name)
    {
        if ("_" != name.Substring(0, 1)) name = "_" + name;
        return Shader.PropertyToID(name);
    }
}
