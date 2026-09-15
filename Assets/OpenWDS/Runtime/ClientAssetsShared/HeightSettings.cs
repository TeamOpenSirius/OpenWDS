using UnityEngine;

namespace Sirius
{
    // Current ELF: 0x5972140–0x5972678. Bone names are resolved from
    // GOT/string-literal slots in character-function-literals.json.
    public sealed class HeightSettings : MonoBehaviour
    {
        public float _bodyScale = 1f;
        public float _headScale = 1f;
        public float _shoeOffset;
        private Transform _root;

        private void Awake() => _root = gameObject.transform;

        public void Apply()
        {
            if (_root == null) _root = gameObject.transform;
            SetScale(_bodyScale, _headScale, _shoeOffset);
        }

        public void SetScale(float bodyScale, float headScale, float shoeOffset)
        {
            var body = GetBody();
            if (body != null)
            {
                body.localScale = new Vector3(bodyScale, bodyScale, bodyScale);
                body.localPosition = new Vector3(0f, bodyScale * shoeOffset, 0f);
            }
            var head = GetHead();
            if (head != null) head.localScale = new Vector3(headScale, headScale, headScale);
        }

        public void CopyFrom(HeightSettings source)
        {
            _bodyScale = source._bodyScale;
            _headScale = source._headScale;
            _shoeOffset = source._shoeOffset;
            Apply();
        }

        public void CopyFromEyePosition(HeightSettings source)
        {
            _root = gameObject.transform;
            source._root = source.transform;
            GetEyeL().localPosition = source.GetEyeL().localPosition;
            GetEyeR().localPosition = source.GetEyeR().localPosition;
        }

        private Transform GetBody() => Scarab.ModelUtility.FindTransformByName(_root, "NAJ_karada_c2");
        private Transform GetHead() => Scarab.ModelUtility.FindTransformByName(_root, "NAJ_kubi_C");
        private Transform GetEyeL() => Scarab.ModelUtility.FindTransformByName(_root, "CJ_Eye_L");
        private Transform GetEyeR() => Scarab.ModelUtility.FindTransformByName(_root, "CJ_Eye_R");
    }
}
