using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWDS.Runtime
{
    public enum BombType
    {
        Default = 1,
        Notes = 2,
        Sakura = 3,
    }

    public enum GameTapEffectType
    {
        Default = 0,
        Light = 1,
    }

    /// <summary>
    /// First executable slice of LaneEffectController: note Beam effects use the
    /// original prefab, lane coordinates and 0.925 lane pitch. Instances expire
    /// using BeamEffectController's serialized 0.5 second animation time.
    /// </summary>
    public sealed class LaneEffectRuntime
    {
        public const float SingleLaneWidth = 0.925f;

        private readonly Sirius.Game.LaneGroup _laneGroup;
        private readonly Transform _parent;
        private readonly GameObject _beamPrefab;
        private readonly GameObject[] _bombPrefabs;
        private readonly bool _isActiveKeyBeam;
        private readonly bool _isDefaultTapEffect;
        private readonly List<Sirius.Game.BeamEffectController> _beams =
            new List<Sirius.Game.BeamEffectController>(32);
        private readonly Stack<GameObject> _beamPool = new Stack<GameObject>(16);
        private readonly Stack<GameObject>[] _bombPools;
        private readonly List<TimedEffect> _bombs = new List<TimedEffect>(32);
        private readonly Dictionary<int, Sirius.Game.IHoldEffectController> _holds =
            new Dictionary<int, Sirius.Game.IHoldEffectController>(32);
        private readonly List<Sirius.Game.IHoldEffectController> _endingHolds =
            new List<Sirius.Game.IHoldEffectController>(16);
        private readonly struct TimedEffect
        {
            public readonly GameObject Instance;
            public readonly float ExpiresAt;
            public readonly int PoolIndex;

            public TimedEffect(GameObject instance, float expiresAt, int poolIndex)
            {
                Instance = instance;
                ExpiresAt = expiresAt;
                PoolIndex = poolIndex;
            }
        }

        public int ActiveBeamCount => _beams.Count;

        public LaneEffectRuntime(
            Sirius.Game.LaneGroup laneGroup,
            Transform parent,
            GameObject beamPrefab,
            GameObject[] defaultBombPrefabs)
            : this(laneGroup, parent, beamPrefab, BombType.Default,
                defaultBombPrefabs, null, null)
        {
        }

        public LaneEffectRuntime(
            Sirius.Game.LaneGroup laneGroup,
            Transform parent,
            GameObject beamPrefab,
            BombType bombType,
            GameObject[] defaultBombPrefabs,
            GameObject[] notesBombPrefabs,
            GameObject[] sakuraBombPrefabs,
            bool isActiveKeyBeam = true,
            GameTapEffectType tapEffectType =
                GameTapEffectType.Default)
        {
            _laneGroup = laneGroup ?? throw new ArgumentNullException(nameof(laneGroup));
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _beamPrefab = beamPrefab ?? throw new ArgumentNullException(nameof(beamPrefab));
            _isActiveKeyBeam = isActiveKeyBeam;
            if (tapEffectType != GameTapEffectType.Default &&
                tapEffectType != GameTapEffectType.Light)
                throw new ArgumentOutOfRangeException(
                    nameof(tapEffectType), tapEffectType,
                    "Original GameTapEffectType values are Default=0, Light=1.");
            _isDefaultTapEffect =
                tapEffectType == GameTapEffectType.Default;
            _bombPrefabs = SelectBombPrefabs(
                bombType, defaultBombPrefabs, notesBombPrefabs, sakuraBombPrefabs);
            if (_bombPrefabs.Length != 6)
                throw new ArgumentException("Six bomb prefabs are required for the selected style.");
            _bombPools = new Stack<GameObject>[_bombPrefabs.Length];
            for (var index = 0; index < _bombPools.Length; index++)
                _bombPools[index] = new Stack<GameObject>(16);
            PrewarmPools();
        }

        private void PrewarmPools()
        {
            // Retail LaneEffectSpawner and BombSpawner preload their ObjectPools.
            // Pre-create and evaluate one instance per concrete prefab before the
            // first judgment; Rent retains its existing concurrency fallback.
            if (_isActiveKeyBeam)
                Prewarm(_beamPrefab, _beamPool);
            for (var index = 0; index < _bombPrefabs.Length; index++)
                // Index 2 is the continuous HoldEffect, not a BombControllerBase
                // object and has no GameTapEffectType consumer in retail code.
                Prewarm(_bombPrefabs[index], _bombPools[index], index != 2);
        }

        private void Prewarm(
            GameObject prefab, Stack<GameObject> pool, bool applyTapEffect = false)
        {
            var instance = UnityEngine.Object.Instantiate(prefab, _parent, false);
            if (applyTapEffect)
            {
                var bomb = instance.GetComponent<Sirius.Game.IBombController>();
                if (bomb == null)
                    throw new InvalidOperationException(
                        "Recovered bomb controller is required.");
                bomb.ApplyTapEffectType(_isDefaultTapEffect);
            }
            foreach (var animator in
                     instance.GetComponentsInChildren<Animator>(true))
            {
                animator.Rebind();
                animator.Update(0f);
            }
            foreach (var particle in
                     instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Simulate(0f, true, true, true);
                particle.Stop(
                    true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            instance.SetActive(false);
            pool.Push(instance);
        }

        public void OnEffect(in InputEffectEntity effect)
        {
            if (effect.EffectType == InputEffectType.Beam)
            {
                if (_isActiveKeyBeam) SpawnBeam(effect);
                return;
            }
            if (effect.EffectType == InputEffectType.Bomb)
            {
                SpawnBomb(effect);
                return;
            }
            if (effect.EffectType == InputEffectType.HoldStart)
                StartHold(effect);
            else if (effect.EffectType == InputEffectType.HoldEnd)
                EndHold(effect.LaneId);
        }

        private void SpawnBeam(in InputEffectEntity effect)
        {
            var instance = Rent(_beamPrefab, _beamPool);
            instance.name = "Runtime_Beam_" + effect.NoteId;
            var controller = instance.GetComponent<Sirius.Game.BeamEffectController>();
            if (controller == null)
                throw new InvalidOperationException("BeamEffectController is required.");
            controller.Initialize(
                effect.Width,
                IsCritical(effect.NoteType),
                SingleLaneWidth);
            controller.transform.localPosition = GetEffectLocalPosition(
                effect.LaneId, effect.Width);
            controller.Play();
            _beams.Add(controller);
        }

        private void SpawnBomb(in InputEffectEntity effect)
        {
            var prefabIndex = GetBombPrefabIndex(effect.NoteType);
            var instance = Rent(
                _bombPrefabs[prefabIndex], _bombPools[prefabIndex]);
            instance.name = "Runtime_Bomb_" + effect.NoteId;
            instance.transform.localPosition = GetEffectLocalPosition(
                effect.LaneId, effect.Width);
            var strong = IsStrong(effect.NoteType, effect.TimingType);
            float duration;
            var bomb = instance.GetComponent<Sirius.Game.IBombController>();
            if (bomb == null)
                throw new InvalidOperationException("Recovered bomb controller is required.");
            bomb.ApplyTapEffectType(_isDefaultTapEffect);
            bomb.Initialize(effect.Width, effect.StartMilliseconds, strong);
            var isScratch = IsScratch(effect.NoteType) ||
                            effect.NoteType == NoteType.SoundPurple;
            bomb.InitializeColor(isScratch);
            bomb.Play();
            duration = bomb.AnimationTime;
            _bombs.Add(new TimedEffect(
                instance, Time.time + duration, prefabIndex));
        }

        private void StartHold(in InputEffectEntity effect)
        {
            // LaneEffectController._holds is keyed by LaneId, not by the
            // notation-note id. HoldEnd is a separate event and can carry a
            // different note id from the HoldStart that occupied this lane.
            if (_holds.ContainsKey(effect.LaneId)) return;
            var instance = Rent(_bombPrefabs[2], _bombPools[2]);
            instance.name = "Runtime_Hold_" + effect.NoteId;
            instance.transform.localPosition = GetEffectLocalPosition(
                effect.LaneId, effect.Width);
            var controller = instance.GetComponent<Sirius.Game.IHoldEffectController>();
            if (controller == null)
                throw new InvalidOperationException("HoldEffectController is required.");
            controller.Initialize(
                effect.Width, IsScratch(effect.NoteType), SingleLaneWidth);
            controller.Play();
            _holds.Add(effect.LaneId, controller);
        }

        private void EndHold(int laneId)
        {
            if (!_holds.TryGetValue(laneId, out var controller)) return;
            // Retail removes the active lane entry as soon as it schedules the
            // completion animation. Keeping it in _holds blocks the next Hold
            // on the same lane until the fade duration elapses.
            _holds.Remove(laneId);
            controller.OnHoldEnd();
            _endingHolds.Add(controller);
        }

        private Vector3 GetEffectLocalPosition(int laneId, int width)
        {
            // OnBomb and OnHoldCore both recover the first/last lane X midpoint
            // and explicitly write local Y/Z as zero before Play().
            var lastLaneId = laneId + Math.Max(1, width) - 1;
            var first = _parent.InverseTransformPoint(
                _laneGroup.GetLaneCollider(laneId).position).x;
            var last = _parent.InverseTransformPoint(
                _laneGroup.GetLaneCollider(lastLaneId).position).x;
            return new Vector3((first + last) * 0.5f, 0f, 0f);
        }

        public static bool IsCritical(NoteType noteType)
        {
            switch (noteType)
            {
                case NoteType.Critical:
                case NoteType.CriticalHoldStart:
                case NoteType.ScratchCriticalHoldStart:
                case NoteType.CriticalHold:
                case NoteType.ScratchCriticalHold:
                    return true;
                default:
                    return false;
            }
        }

        public void Reset()
        {
            foreach (var beam in _beams) if (beam != null) Return(beam.gameObject, _beamPool);
            foreach (var bomb in _bombs) if (bomb.Instance != null) Return(bomb.Instance, _bombPools[bomb.PoolIndex]);
            foreach (var hold in _holds.Values) if (hold != null) Return(hold.EffectObject, _bombPools[2]);
            foreach (var hold in _endingHolds) if (hold != null) Return(hold.EffectObject, _bombPools[2]);
            _beams.Clear(); _bombs.Clear(); _holds.Clear(); _endingHolds.Clear();
        }

        public void Tick()
        {
            for (var index = _beams.Count - 1; index >= 0; index--)
            {
                var beam = _beams[index];
                if (beam != null && !beam.TryExpire()) continue;
                _beams.RemoveAt(index);
                if (beam != null) Return(beam.gameObject, _beamPool);
            }
            for (var index = _bombs.Count - 1; index >= 0; index--)
            {
                if (Time.time < _bombs[index].ExpiresAt) continue;
                var instance = _bombs[index].Instance;
                var poolIndex = _bombs[index].PoolIndex;
                _bombs.RemoveAt(index);
                if (instance != null) Return(instance, _bombPools[poolIndex]);
            }
            for (var index = _endingHolds.Count - 1; index >= 0; index--)
            {
                var controller = _endingHolds[index];
                if (controller != null && !controller.TryExpire()) continue;
                _endingHolds.RemoveAt(index);
                if (controller != null)
                    Return(controller.EffectObject, _bombPools[2]);
            }
        }

        private GameObject Rent(GameObject prefab, Stack<GameObject> pool)
        {
            GameObject instance;
            if (pool.Count > 0)
            {
                instance = pool.Pop();
                instance.SetActive(true);
            }
            else
            {
                instance = UnityEngine.Object.Instantiate(prefab, _parent, false);
            }
            return instance;
        }

        private static void Return(GameObject instance, Stack<GameObject> pool)
        {
            foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            instance.SetActive(false);
            pool.Push(instance);
        }

        public static int GetBombPrefabIndex(NoteType noteType)
        {
            switch (noteType)
            {
                case NoteType.Critical:
                case NoteType.CriticalHoldStart:
                    return 1;
                case NoteType.Hold:
                case NoteType.CriticalHold:
                case NoteType.BlueTap:
                    return 3;
                case NoteType.Sound:
                case NoteType.SoundPurple:
                    return 5;
                case NoteType.Scratch:
                case NoteType.Flick:
                case NoteType.ScratchHoldStart:
                case NoteType.ScratchCriticalHoldStart:
                case NoteType.ScratchHold:
                case NoteType.ScratchCriticalHold:
                    return 4;
                default:
                    return 0;
            }
        }

        public static bool IsStrong(
            NoteType noteType, TimingType timingType)
        {
            return noteType == NoteType.Flick
                ? timingType > TimingType.Great
                : timingType > TimingType.Good;
        }

        private static bool IsScratch(NoteType noteType) =>
            noteType == NoteType.ScratchHoldStart ||
            noteType == NoteType.ScratchCriticalHoldStart ||
            noteType == NoteType.ScratchHold ||
            noteType == NoteType.ScratchCriticalHold;

        public static GameObject[] SelectBombPrefabs(
            BombType bombType,
            GameObject[] defaultPrefabs,
            GameObject[] notesPrefabs,
            GameObject[] sakuraPrefabs)
        {
            switch (bombType)
            {
                case BombType.Default:
                    return defaultPrefabs ??
                           throw new ArgumentNullException(nameof(defaultPrefabs));
                case BombType.Notes:
                    return notesPrefabs ??
                           throw new ArgumentNullException(nameof(notesPrefabs));
                case BombType.Sakura:
                    return sakuraPrefabs ??
                           throw new ArgumentNullException(nameof(sakuraPrefabs));
                default:
                    throw new ArgumentOutOfRangeException(nameof(bombType), bombType,
                        "Original BombType values are Default=1, Notes=2, Sakura=3.");
            }
        }
    }
}
