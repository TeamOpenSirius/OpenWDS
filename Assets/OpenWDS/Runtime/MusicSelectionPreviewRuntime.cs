using System;
using System.Collections.Generic;
using System.IO;
using Coffee.UIExtensions;
using CriWare;
using UnityEngine;
using UnityEngine.UI;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Local counterpart of MusicStreamingPlayer + CriSpectrumParticleViewer.
    /// It plays the official preview ACB and drives the authored 38+38 spectrum
    /// bars from the CRI output analyzer.
    /// </summary>
    public sealed class MusicSelectionPreviewRuntime : MonoBehaviour
    {
        private const int BandsPerSide = 38;
        private const int ActiveBandsPerSide = 37;
        private const float BarWidth = 10f;
        private const float MinimumHeight = 10f;
        private const float MaximumHeight = 60f;

        private const int SpectrumSkip = 6;
        private readonly float[] _display = new float[ActiveBandsPerSide];
        private float[] _spectrum = new float[ActiveBandsPerSide + SpectrumSkip];
        private RectTransform[] _top = Array.Empty<RectTransform>();
        private RectTransform[] _bottom = Array.Empty<RectTransform>();
        private CriAtomExAcb _acb;
        private CriAtomExPlayer _player;
        private CriAtomExPlayback _playback;
        private CriAtomExOutputAnalyzer _analyzer;
        private long _musicId;
        private Color _color = Color.white;

        public long MusicId => _musicId;
        public bool IsPlaying =>
            _player != null &&
            _player.GetStatus() == CriAtomExPlayer.Status.Playing;
        public int SpectrumBarCount => _top.Length + _bottom.Length;

        public void Configure(Transform view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            var root = view.Find(
                "TicketMacine/MusicInformationPanel/UIParticleSpectrumViewer");
            if (root == null)
                throw new InvalidOperationException(
                    "MusicSelection spectrum viewer is missing.");
            var spectrumCanvas = root.GetComponent<Canvas>();
            if (spectrumCanvas == null)
                spectrumCanvas = root.gameObject.AddComponent<Canvas>();
            // Isolate the two stencil masks so their pop instructions cannot
            // affect later siblings on the tilted information-panel Canvas.
            spectrumCanvas.overrideSorting = true;
            spectrumCanvas.sortingOrder = 1;
            var topMask = root.Find("Top/Mask");
            var bottomMask = root.Find("Bottom/Mask");
            // The retail prefab uses the viewer's 568x616 local rect as the
            // reference frame. Top and Bottom are both anchored at local Y=0;
            // Top's authored 180-degree rotation provides the inverse edge.
            RestoreSpectrumEdge(root.Find("Top") as RectTransform);
            RestoreSpectrumEdge(root.Find("Bottom") as RectTransform);
            DisableMaskGraphic(root.Find("Top"));
            DisableMaskGraphic(root.Find("Bottom"));
            EnableParticleCanvas(topMask);
            EnableParticleCanvas(bottomMask);
            _top = BuildBars(topMask, "SpectrumNoteTop");
            _bottom = BuildBars(bottomMask, "SpectrumNoteBottom");
            if (_top.Length != BandsPerSide || _bottom.Length != BandsPerSide)
                throw new InvalidOperationException(
                    $"Expected 38+38 spectrum bars, found {_top.Length}+{_bottom.Length}.");
            MoveFirstBarToWrapEnd(_top);
            MoveFirstBarToWrapEnd(_bottom);
            ApplyColor();
            ClearBars();
        }

        private static void RestoreSpectrumEdge(RectTransform edge)
        {
            if (edge == null) return;
            var position = edge.anchoredPosition;
            position.y = 0f;
            edge.anchoredPosition = position;
        }

        private static void DisableMaskGraphic(Transform root)
        {
            var image = root != null ? root.GetComponent<Image>() : null;
            if (image != null)
            {
                // Keep the retail stencil topology, but replace the recovered
                // atlas region that renders as an opaque polygon with a plain
                // rectangular UI quad.
                image.sprite = null;
                image.overrideSprite = null;
                image.type = Image.Type.Simple;
                image.color = Color.white;
                image.enabled = true;
                image.raycastTarget = false;
            }
            var mask = root != null ? root.GetComponent<Mask>() : null;
            if (mask != null)
            {
                mask.enabled = true;
                mask.showMaskGraphic = false;
            }
            var rectMask = root != null ? root.GetComponent<RectMask2D>() : null;
            if (rectMask != null) rectMask.enabled = false;
        }

        private static void EnableParticleCanvas(
            Transform mask)
        {
            if (mask == null) return;
            var uiParticle = mask.GetComponent<UIParticle>();
            if (uiParticle != null)
            {
                // This prefab predates UIParticle 4.x's serialized
                // m_AutoScalingMode. Its old m_IgnoreCanvasScaler=0 meant no
                // transform compensation. The new package otherwise defaults
                // the absent field to Transform and drives this Mask to the
                // inverse Canvas scale (2.74 at 1136x640), displacing both the
                // authored bars and their ParticleSystem emitters.
                uiParticle.autoScalingMode = UIParticle.AutoScalingMode.None;
                uiParticle.transform.localScale = Vector3.one;
            }
            foreach (var graphic in mask.GetComponents<Graphic>())
            {
                if (graphic is Image) continue;
                // Coffee UIParticle is the retail Canvas renderer. The
                // ParticleSystemRenderer components are authored disabled and
                // must remain so; UIParticle batches their meshes inside the
                // information-panel stencil instead.
                graphic.color = Color.white;
                graphic.enabled = true;
                graphic.canvasRenderer.SetAlpha(1f);
            }
        }

        public void Play(long musicId)
        {
            if (musicId <= 0) throw new ArgumentOutOfRangeException(nameof(musicId));
            // MusicCompositeStreamingPlayer.Play ignores the current music ID.
            // A difficulty/filter refresh must not restart the preview playback.
            if (_player != null && _musicId == musicId) return;
            StopPlayer();
            _musicId = musicId;
            var relative =
                $"OpenWDS/StandardCharts/{musicId}/cri/musicpreview_{musicId}.acb.bundle";
            var path = Path.Combine(
                CriWare.Common.streamingAssetsPath, relative);
            _acb = CriAtomExAcb.LoadAcbFile(null, path, null);
            if (_acb == null)
                throw new InvalidOperationException(
                    $"CRI preview ACB failed to load: {relative}");
            var cue = musicId.ToString();
            if (!_acb.GetCueInfo(cue, out _))
                throw new InvalidOperationException(
                    $"CRI preview cue '{cue}' is missing from music {musicId}.");
            _player = new CriAtomExPlayer(true);
            _player.SetCue(_acb, cue);
            _analyzer = new CriAtomExOutputAnalyzer(
                new CriAtomExOutputAnalyzer.Config
                {
                    enableSpectrumAnalyzer = true,
                    numSpectrumAnalyzerBands = ActiveBandsPerSide + SpectrumSkip,
                });
            if (!_analyzer.AttachExPlayer(_player))
                throw new InvalidOperationException(
                    "CRI preview output analyzer could not attach to its player.");
            _playback = _player.Start();
        }

        public void SetDifficultyColor(MusicDifficulty difficulty)
        {
            // MusicSelectionInformationPanel.ChangeFrameColor parses the
            // difficulty config color and passes it directly to
            // CriSpectrumParticleViewer.SetColor.
            _color = SpectrumColor(difficulty);
            ApplyColor();
        }

        public void Stop()
        {
            StopPlayer();
        }

        private void Update()
        {
            if (_player == null) return;
            if (_player.GetStatus() == CriAtomExPlayer.Status.PlayEnd)
            {
                _player.SetStartTime(0);
                _playback = _player.Start();
            }
            _analyzer.GetSpectrumLevels(ref _spectrum);
            for (var index = 0; index < _display.Length; index++)
            {
                var spectrumIndex = index + SpectrumSkip;
                var level = spectrumIndex < _spectrum.Length
                    ? Mathf.Clamp01(Mathf.Log10(
                        Mathf.Max(1f, _spectrum[spectrumIndex])))
                    : 0f;
                var target = SpectrumHeightFromLevel(level);
                if (target - 6f > _display[index])
                    PlayParticles(index);
                _display[index] = target >= _display[index]
                    ? target
                    : Mathf.Max(
                        MinimumHeight,
                        _display[index] - 3f);
            }
            ApplyBars(_top, 0, true);
            ApplyBars(_bottom, 0, false);
        }

        private void PlayParticles(int index)
        {
            PlayParticle(_top, index);
            PlayParticle(_bottom, index);
        }

        private static void PlayParticle(RectTransform[] bars, int index)
        {
            if (index < 0 || index >= bars.Length || bars[index] == null) return;
            foreach (var particle in
                     bars[index].GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!particle.gameObject.activeSelf)
                    particle.gameObject.SetActive(true);
                particle.Play(true);
            }
        }

        private void ApplyBars(RectTransform[] bars, int offset, bool inverse)
        {
            for (var index = 0; index < bars.Length; index++)
            {
                if (bars[index] == null) continue;
                if (index >= _display.Length)
                {
                    bars[index].sizeDelta = new Vector2(BarWidth, 0f);
                    var inactiveImage = bars[index].GetComponent<Image>();
                    if (inactiveImage != null) inactiveImage.enabled = false;
                    foreach (var inactiveParticle in
                             bars[index].GetComponentsInChildren<
                                 ParticleSystem>(true))
                        inactiveParticle.Stop(true, ParticleSystemStopBehavior
                            .StopEmittingAndClear);
                    continue;
                }
                bars[index].sizeDelta =
                    new Vector2(BarWidth, _display[offset + index]);
                var image = bars[index].GetComponent<Image>();
                if (image != null)
                {
                    // The recovered rounded-rectangle Sprite points at atlas
                    // coordinates whose exported PNG layout no longer matches;
                    // it samples an opaque black atlas region. A null Sprite
                    // makes Image emit the intended solid, tintable UI quad.
                    image.sprite = null;
                    image.overrideSprite = null;
                    image.type = Image.Type.Simple;
                    image.enabled = true;
                    image.raycastTarget = false;
                }
                foreach (var particle in
                         bars[index].GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (!particle.gameObject.activeSelf)
                        particle.gameObject.SetActive(true);
                    // The authored UIParticle children use localScale=100.
                    // Retail SetEffect places the emitter at height / +/-100.
                    var shape = particle.shape;
                    var position = shape.position;
                    position.y =
                        _display[offset + index] / (inverse ? -100f : 100f);
                    shape.position = position;
                    // Coffee UIParticle owns the Canvas rendering path. The
                    // world renderer is disabled in the retail prefab too.
                    var renderer = particle.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null) renderer.enabled = false;
                }
            }
        }

        private void ClearBars()
        {
            for (var index = 0; index < _display.Length; index++)
                _display[index] = MinimumHeight;
            ApplyBars(_top, 0, true);
            ApplyBars(_bottom, 0, false);
        }

        private void ApplyColor()
        {
            foreach (var bar in EnumerateBars())
            {
                if (bar == null) continue;
                var image = bar.GetComponent<Image>();
                if (image != null)
                {
                    var imageColor = _color;
                    // CriSpectrumParticleViewer.SetColor forces only the bar
                    // Images to 0.5 alpha.
                    imageColor.a = 0.5f;
                    image.color = imageColor;
                }
                foreach (var particle in bar.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = particle.main;
                    // Particle startColor receives the same RGB while
                    // retaining the source color alpha.
                    main.startColor = _color;
                }
            }
        }

        public static float SpectrumHeight(float spectrum)
        {
            var level = Mathf.Clamp01(
                Mathf.Log10(Mathf.Max(1f, spectrum)));
            return SpectrumHeightFromLevel(level);
        }

        private static float SpectrumHeightFromLevel(float level)
        {
            var smooth = level * level * (3f - 2f * level);
            return smooth * MaximumHeight +
                   (1f - smooth) * MinimumHeight;
        }

        private IEnumerable<RectTransform> EnumerateBars()
        {
            foreach (var bar in _top) yield return bar;
            foreach (var bar in _bottom) yield return bar;
        }

        private static RectTransform[] BuildBars(Transform parent, string name)
        {
            if (parent == null) return Array.Empty<RectTransform>();
            var result = new List<RectTransform>();
            foreach (var rect in parent.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.name.StartsWith(name, StringComparison.Ordinal))
                    result.Add(rect);
            }
            if (result.Count == 1)
            {
                var template = result[0];
                for (var index = 1; index < BandsPerSide; index++)
                {
                    var clone = Instantiate(template.gameObject, parent, false);
                    clone.name = name;
                    var rect = clone.GetComponent<RectTransform>();
                    var position = rect.anchoredPosition;
                    position.x = template.anchoredPosition.x + index * 20f;
                    rect.anchoredPosition = position;
                    result.Add(rect);
                }
            }
            // CriSpectrumParticleViewer.InitEffect assigns every authored bar
            // its band position. The recovered prefab retains all 38 objects,
            // but their serialized X coordinates are all zero, so sorting by X
            // merely left every band stacked at the mask origin.
            for (var index = 0; index < result.Count; index++)
            {
                var position = result[index].anchoredPosition;
                position.x = index * 20f;
                result[index].anchoredPosition = position;
            }
            return result.ToArray();
        }

        private static void MoveFirstBarToWrapEnd(RectTransform[] bars)
        {
            if (bars == null || bars.Length == 0 || bars[0] == null) return;
            var position = bars[0].anchoredPosition;
            position.x = ActiveBandsPerSide * 20f;
            bars[0].anchoredPosition = position;
        }

        public static Color32 DifficultyFrameColor(
            MusicDifficulty difficulty)
        {
            // ColorPreset .cctor / GetDifficultyFrameColor, ARM64
            // RVA 0xA59D18C / 0xA59B4D4.
            switch (difficulty)
            {
                case MusicDifficulty.Normal:
                    // ColorPreset.get_Difficulty_Normal, RVA 0xA59B498:
                    // packed Color32 0xFFDCC300 -> RGBA (0,195,220,255).
                    return new Color32(0, 195, 220, 255);
                case MusicDifficulty.Hard:
                    return new Color32(230, 123, 9, 255);
                case MusicDifficulty.Extra:
                    return new Color32(229, 75, 100, 255);
                case MusicDifficulty.Stella:
                    return new Color32(92, 64, 183, 255);
                case MusicDifficulty.Olivier:
                    return new Color32(48, 43, 43, 200);
                default:
                    return new Color32(65, 66, 77, 225);
            }
        }

        public static Color SpectrumColor(
            MusicDifficulty difficulty)
        {
            // SetColor replaces Olivier with (0.8, 0.8, 0.9, 1);
            // all other difficulties retain the parsed config RGB/alpha.
            if (difficulty == MusicDifficulty.Olivier)
                return new Color(0.8f, 0.8f, 0.9f, 1f);
            var color = (Color)DifficultyFrameColor(difficulty);
            color.a = 1f;
            return color;
        }

        private void StopPlayer()
        {
            if (_player != null) _player.Stop();
            if (_analyzer != null)
            {
                _analyzer.DetachExPlayer();
                _analyzer.Dispose();
            }
            _player?.Dispose();
            _acb?.Dispose();
            _analyzer = null;
            _player = null;
            _acb = null;
            ClearBars();
        }

        private void OnDestroy()
        {
            StopPlayer();
        }
    }
}
