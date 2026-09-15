using System.Collections.Generic;
using Sirius.Animations;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Recovered GameSimulation preview slice used by OptionDialogBody.
    /// The original LaneGroup, PreviewUI, Note, camera and RenderTexture are
    /// retained; only the stripped presenter/tick worker is replaced here.
    /// </summary>
    public sealed class GameSimulationPreview : MonoBehaviour
    {
        // GameSimulationNoteObjectScheduler.Tick calls
        // NotationNoteEntity.Simulation(startTickCount, 5, 2) at
        // RVA 0xB2674C0.
        private const int SimulationNoteLane = 5;
        private const int SimulationNoteWidth = 2;
        // PreviewUI.Initialize loads this literal before applying
        // SetLocalPositionAndRotation to _bombController.transform
        // (RVA 0xB269E08). Timing/Sense remain below the PreviewUI root.
        private const float BombLocalPositionX = -0.9f;

        // Retail uses layer 18 for both gameplay and GameSimulation. In the
        // recovered single-scene setup the gameplay camera renders every layer,
        // so the two formerly separate contexts need one private render layer.
        public const int IsolatedRenderLayer = 30;

        private readonly Dictionary<Camera, int> _cameraMasks =
            new Dictionary<Camera, int>();
        private Transform _note;
        private Transform _timingPositionTransform;
        private Transform _timingScaleTransform;
        private Animator _timingAnimator;
        private Animator _senseAnimator;
        private AnimationExitTrigger _timingExitTrigger;
        private Sirius.Game.BombController _bombController;
        private Sirius.Game.LaneGroup _laneGroup;
        private double _noteSpeed;
        private double _noteOffsetValue;
        private int _noteStartOffset;
        private bool _isActiveSenseDisplay;
        private float _startedAt;
        private int _cycle = -1;
        private bool _effectPlayed;
        public int BombPlayCount { get; private set; }
        public bool IsBombActive =>
            _bombController != null && _bombController.gameObject.activeSelf;

        public void Configure(
            Camera simulationCamera,
            GameObject laneGroupPrefab,
            GameObject previewUiPrefab,
            GameObject notePrefab,
            double noteSpeed,
            double noteOffsetValue,
            int noteStartOffset,
            int timingEffectOffset,
            int timingEffectScaleType,
            bool isActiveSenseDisplay,
            int laneWidth,
            int laneAlphaValue,
            int noteHeight)
        {
            _noteSpeed = noteSpeed;
            _noteOffsetValue = noteOffsetValue;
            _noteStartOffset = noteStartOffset;
            _isActiveSenseDisplay = isActiveSenseDisplay;
            IsolateRendering(simulationCamera);

            var lane = Instantiate(laneGroupPrefab, transform, false);
            lane.name = "LaneGroup";
            ApplyLaneStart(lane.transform);
            // PreviewUI.Initialize field +0x30 is _bombController, not the
            // PreviewUI root. Only that controller is reparented beneath
            // LaneGroup.EffectParent and positioned at local X=-0.9.
            // TimingEffect and Sense retain the PreviewUI prefab/root
            // coordinate system used by the simulation camera.
            _laneGroup = lane.GetComponent<Sirius.Game.LaneGroup>();
            if (_laneGroup != null)
                _laneGroup.ApplySettings(laneWidth, laneAlphaValue);
            var effectParent = _laneGroup != null
                ? _laneGroup.LaneEffectParent
                : FindDescendant(lane.transform, "EffectParent");
            if (effectParent == null)
                throw new System.InvalidOperationException(
                    "Simulation LaneGroup EffectParent is missing.");
            var previewUi = Instantiate(previewUiPrefab, transform, false);
            previewUi.name = "PreviewUI";
            var previewController =
                previewUi.GetComponent<Sirius.GameSimulation.PreviewUI>();
            var previewBombController = previewController != null
                ? previewController.BombController
                : previewUi.GetComponentInChildren<
                    Sirius.Game.BombController>(true);
            if (previewBombController == null)
                throw new System.InvalidOperationException(
                    "Original PreviewUI NormalBombEffect is missing.");
            previewBombController.transform.SetParent(effectParent);
            previewBombController.transform.localPosition =
                new Vector3(BombLocalPositionX, 0f, 0f);
            previewBombController.transform.localRotation = Quaternion.identity;
            previewBombController.transform.localScale = Vector3.one;
            var movable = FindDescendant(lane.transform, "Movable");
            var note = Instantiate(
                notePrefab, movable != null ? movable : lane.transform, false);
            note.name = "SimulationNote";
            _note = note.transform;
            SetLayerRecursively(lane.transform, IsolatedRenderLayer);
            SetLayerRecursively(previewUi.transform, IsolatedRenderLayer);

            // TapNoteObject.Initialize selects Notes1 for the ordinary
            // non-critical simulation tap. Its presenter is stripped here.
            var notes2 = FindDescendant(note.transform, "Notes2");
            if (notes2 != null) notes2.gameObject.SetActive(false);
            _note.localScale = Vector3.one;
            // GameSimulationNoteObjectSpawner.Spawn reads NoteHeight and
            // applies GameConfig.GetNoteHeight as the note's local X rotation.
            // The settings-change callback never respawns/reinitializes this
            // note, so this is intentionally creation-only.
            _note.localRotation = Quaternion.Euler(
                OriginalGameConfig.GetNoteHeightRotationX(noteHeight),
                0f, 0f);
            var notationWidth = NotePositionCalculator.GetNoteWidth(
                SimulationNoteWidth,
                GameConfigValues.NoteWidthPerLane,
                GameConfigValues.LaneBorderWidth);
            var visualWidth =
                NoteVisualRuntime.GetTapVisualWidth(notationWidth);
            var notePosition = _note.localPosition;
            notePosition.x = NotePositionCalculator.GetNotePositionX(
                SimulationNoteLane,
                notationWidth,
                GameConfigValues.NoteWidthPerLane,
                GameConfigValues.LaneBorderWidth);
            _note.localPosition = notePosition;
            var notes1 = FindDescendant(note.transform, "Notes1");
            if (notes1 != null)
            {
                foreach (var renderer in
                         notes1.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.drawMode == SpriteDrawMode.Simple) continue;
                    var size = renderer.size;
                    size.x = visualWidth;
                    renderer.size = size;
                }
            }

            var timingEffect = FindDescendant(previewUi.transform, "TimingEffect");
            _timingPositionTransform = previewController != null &&
                                       previewController.TimingAnimator != null
                ? previewController.TimingAnimator.transform
                : timingEffect;
            _timingScaleTransform = previewController != null
                ? previewController.TimingTransform
                : timingEffect;
            _senseAnimator = previewController != null
                ? previewController.SenseAnimator
                : null;
            _bombController = previewBombController;
            _bombController.gameObject.SetActive(false);
            if (_senseAnimator != null)
                _senseAnimator.gameObject.SetActive(false);
            ApplyTimingEffectSettings(
                timingEffectOffset, timingEffectScaleType);
            if (timingEffect != null)
            {
                _timingAnimator = timingEffect.GetComponent<Animator>();
                _timingExitTrigger =
                    timingEffect.GetComponent<AnimationExitTrigger>();
                if (_timingExitTrigger == null)
                    _timingExitTrigger =
                        timingEffect.gameObject.AddComponent<AnimationExitTrigger>();
                _timingExitTrigger.Exited += StopTimingEffect;
                if (_timingAnimator != null) _timingAnimator.enabled = false;
            }
            _startedAt = Time.unscaledTime;
            var cameraController =
                simulationCamera.GetComponent<Sirius.GameSimulation.GameSimulationCamera>();
            if (cameraController != null)
                cameraController.Show();
            else
                simulationCamera.enabled = true;
        }

        public void SetSettings(
            double noteSpeed,
            double noteOffsetValue,
            int noteStartOffset,
            int timingEffectOffset,
            int timingEffectScaleType,
            bool isActiveSenseDisplay,
            int laneWidth)
        {
            _noteSpeed = noteSpeed;
            _noteOffsetValue = noteOffsetValue;
            _noteStartOffset = noteStartOffset;
            _isActiveSenseDisplay = isActiveSenseDisplay;
            _laneGroup?.ApplyLaneWidth(laneWidth);
            var lane = FindDescendant(transform, "LaneGroup");
            if (lane != null) ApplyLaneStart(lane);
            ApplyTimingEffectSettings(
                timingEffectOffset, timingEffectScaleType);
            RestartCycle();
        }

        private void ApplyTimingEffectSettings(
            int timingEffectOffset,
            int timingEffectScaleType)
        {
            // PreviewUI.Apply(GameDetailSettings), RVA 0xB26E130:
            // position belongs to _timingAnimator.transform; only scale is
            // applied to the serialized _timingTransform child.
            if (_timingPositionTransform != null)
            {
                var position = _timingPositionTransform.localPosition;
                position.y = GameHudRuntime.CalculatePositionY(
                    timingEffectOffset);
                _timingPositionTransform.localPosition = position;
            }
            if (_timingScaleTransform != null)
                _timingScaleTransform.localScale = Vector3.one *
                    GameHudRuntime.GetTimingEffectScale(
                        timingEffectScaleType);
        }

        private void Update()
        {
            if (_note == null) return;
            // PreviewUI.OnStart passes DefaultNoteStartOffset (zero) to
            // UserDataHelper.GetNoteDisplayTime. The detail-setting offset moves
            // the real chart but is deliberately not part of this demo period.
            var duration = Mathf.Max(
                0.1f,
                GameSettings.CalculateNoteDisplayTime(
                    0, _noteSpeed) /
                1000f);
            const float effectTail = 0.5f;
            var elapsed = Time.unscaledTime - _startedAt;
            var cycleLength = duration + effectTail;
            var cycle = Mathf.FloorToInt(elapsed / cycleLength);
            if (cycle != _cycle)
            {
                _cycle = cycle;
                _effectPlayed = false;
                StopTimingEffect();
                StopBombEffect();
            }
            var cycleElapsed = Mathf.Repeat(elapsed, cycleLength);
            var remaining = duration - cycleElapsed;
            var visible = remaining >= 0f;
            _note.gameObject.SetActive(visible);
            if (!visible)
            {
                // PreviewUI.OnUpdate calls PlayEffectIfNeed exactly when
                // elapsed milliseconds reach the display-time threshold.
                if (!_effectPlayed)
                {
                    _effectPlayed = true;
                    PlayHitEffect();
                }
                return;
            }
            var speedRate =
                NotePositionCalculator.CalculateSpeedRate(_noteSpeed);
            var y = NotePositionCalculator.CalculatePositionY(
                Mathf.RoundToInt(remaining * 1000f),
                0,
                speedRate,
                _noteOffsetValue,
                GameConfigValues.PositionPow3Rate,
                GameConfigValues.PositionPow1Rate);
            var noteWidth = NotePositionCalculator.GetNoteWidth(
                SimulationNoteWidth,
                GameConfigValues.NoteWidthPerLane,
                GameConfigValues.LaneBorderWidth);
            var noteX = NotePositionCalculator.GetNotePositionX(
                SimulationNoteLane,
                noteWidth,
                GameConfigValues.NoteWidthPerLane,
                GameConfigValues.LaneBorderWidth);
            _note.localPosition = new Vector3(noteX, y, 0f);
        }

        private void IsolateRendering(Camera simulationCamera)
        {
            var layerMask = 1 << IsolatedRenderLayer;
            foreach (var camera in Camera.allCameras)
            {
                if (camera == null || camera == simulationCamera) continue;
                _cameraMasks[camera] = camera.cullingMask;
                camera.cullingMask &= ~layerMask;
            }
            simulationCamera.cullingMask = layerMask;
            simulationCamera.gameObject.layer = IsolatedRenderLayer;
        }

        private void ApplyLaneStart(Transform lane)
        {
            // LaneGroup.InitializeLaneStart applies the detail offset to both
            // helpers. That presenter is stripped from the recovered prefab.
            var maskScaler =
                lane.GetComponentInChildren<Sirius.Game.LaneMaskScaler>(true);
            if (maskScaler != null)
            {
                var position = maskScaler.transform.localPosition;
                position.y = OriginalGameConfig.NoteStartPositionY +
                             OriginalGameConfig.LaneMaskOffsetY;
                maskScaler.transform.localPosition = position;
                var scale = maskScaler.transform.localScale;
                scale.y = OriginalGameConfig.GetLaneMaskScaleY(
                    _noteStartOffset);
                maskScaler.transform.localScale = scale;
            }

            var startLine =
                lane.GetComponentInChildren<Sirius.Game.LaneNoteStartLine>(true);
            if (startLine == null) return;
            var startPosition = startLine.transform.localPosition;
            startPosition.y = OriginalGameConfig.GetNoteVisiblePositionY(
                _noteStartOffset);
            startLine.transform.localPosition = startPosition;
        }

        private void PlayHitEffect()
        {
            // PreviewUI.PlayEffectIfNeed first enables _bombController. In the
            // retail presenter the demo note has already initialized it as an
            // ordinary one-lane, non-scratch, strong tap.
            _bombController.gameObject.SetActive(true);
            _bombController.Initialize(SimulationNoteWidth, 0L, true);
            _bombController.InitializeColor(false);
            _bombController.Play();
            BombPlayCount++;
            if (_senseAnimator != null)
            {
                _senseAnimator.gameObject.SetActive(_isActiveSenseDisplay);
                if (_isActiveSenseDisplay)
                {
                    _senseAnimator.enabled = true;
                    _senseAnimator.Rebind();
                    _senseAnimator.Play(
                        Animator.StringToHash("SenseMoveUp_anim"), 0, 0f);
                }
            }
            if (_timingAnimator == null) return;
            _timingAnimator.enabled = true;
            _timingAnimator.Rebind();
            _timingAnimator.Play(
                Animator.StringToHash("TimingEffect_anime"), 0, 0f);
        }

        private void StopTimingEffect()
        {
            if (_timingAnimator == null) return;
            _timingAnimator.enabled = false;
        }

        private void RestartCycle()
        {
            _startedAt = Time.unscaledTime;
            _cycle = -1;
            _effectPlayed = false;
            StopTimingEffect();
            StopBombEffect();
        }

        private void StopBombEffect()
        {
            if (_bombController == null) return;
            foreach (var particle in
                     _bombController.GetComponentsInChildren<ParticleSystem>(true))
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _bombController.gameObject.SetActive(false);
            if (_senseAnimator != null)
                _senseAnimator.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_timingExitTrigger != null)
                _timingExitTrigger.Exited -= StopTimingEffect;
            StopBombEffect();
            foreach (var pair in _cameraMasks)
            {
                if (pair.Key != null) pair.Key.cullingMask = pair.Value;
            }
            _cameraMasks.Clear();
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (var index = 0; index < root.childCount; index++)
                SetLayerRecursively(root.GetChild(index), layer);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name) return root;
            for (var index = 0; index < root.childCount; index++)
            {
                var found = FindDescendant(root.GetChild(index), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
