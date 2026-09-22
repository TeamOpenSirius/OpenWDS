using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace OpenWDS.Runtime
{
    /// <summary>Launcher TitleTransitionFade (type 5), retained until Home is ready.</summary>
    public sealed class TitleTransitionRuntime : MonoBehaviour
    {
        private LocalAssetBundleLease _lease;
        private LocalAssetBundleLease _scripts;
        private GameObject _canvas, _view;
        private CanvasGroup _group;
        private Animator _animator;
        private readonly List<Material> _materials = new List<Material>();
        public bool IsCovering => _view != null && _view.activeInHierarchy && _group.alpha > .99f;
        public string Phase { get; private set; }
        public GameObject View => _view;

        public IEnumerator Show()
        {
            Phase = "preparing";
            yield return LocalAssetBundleLease.LoadStreaming("92e245fcb18c2affa436f260ac944f61_monoscripts.bundle",
                "OpenWDS/Menu/0.bundle", lease => _scripts = lease);
            yield return LocalAssetBundleLease.LoadStreaming("openwds-title-transition",
                "OpenWDS/Frontend/title-transition.bundle", lease => _lease = lease);
            if (_lease.Bundle == null) throw new InvalidOperationException("Title transition bundle missing");
            var prefab = _lease.Bundle.LoadAsset<GameObject>("TitleTransitionFade");
            if (prefab == null) throw new InvalidOperationException("Title transition prefab missing");
            _canvas = new GameObject("TitleTransitionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0;
            _view = Instantiate(prefab, _canvas.transform, false);
            _view.name = "TitleTransitionFade";
            foreach (var component in _view.GetComponentsInChildren<MonoBehaviour>(true))
                if (component == null) throw new InvalidOperationException("Title transition script missing");
            var adapted = new Dictionary<Material, Material>();
            Material Adapt(Material original)
            {
                if (original == null) return null;
                if (adapted.TryGetValue(original, out var previous)) return previous;
                var shader = Shader.Find(original.shader.name);
                if (shader == null || !shader.isSupported) throw new InvalidOperationException("Title UI shader missing: " + original.shader.name);
                var material = new Material(original) { shader = shader };
                _materials.Add(material);
                adapted.Add(original, material);
                return material;
            }
            foreach (var renderer in _view.GetComponentsInChildren<ParticleSystemRenderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(Adapt).ToArray();
            foreach (var graphic in _view.GetComponentsInChildren<Graphic>(true))
                graphic.material = Adapt(graphic.material);
            _group = _view.GetComponent<CanvasGroup>();
            _group.alpha = 1;
            _group.blocksRaycasts = true;
            _animator = _view.GetComponentInChildren<Animator>(true);
            _animator.gameObject.SetActive(true);
            _animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            _animator.Rebind();
            _animator.Update(0);
            Phase = "loading";
            // Native FadeInAsync 0xAFFB078 waits 1.5 s, then navigation begins.
            yield return new WaitForSecondsRealtime(1.5f);
        }

        public IEnumerator Hide()
        {
            // Native FadeOutAsync 0xAFFB540 awaits Transition_Out, then fades
            // the outer group for 0.15 s. Destination initialization precedes it.
            Phase = "out";
            _animator.SetTrigger("OutTrigger");
            float deadline = Time.realtimeSinceStartup + 5;
            while (!_animator.GetCurrentAnimatorStateInfo(0).IsName("Transition_Out") ||
                   _animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Title transition out animation");
                yield return null;
            }
            yield return _group.DOFade(0, .15f).SetUpdate(true).WaitForCompletion();
            _animator.ResetTrigger("OutTrigger");
            _group.blocksRaycasts = false;
            _view.SetActive(false);
            Phase = "complete";
        }

        private void OnDestroy()
        {
            if (_group != null) _group.DOKill();
            if (_canvas != null) { _canvas.SetActive(false); Destroy(_canvas); }
            foreach (var material in _materials) Destroy(material);
            _lease?.Dispose();
            _scripts?.Dispose();
        }
    }
}
