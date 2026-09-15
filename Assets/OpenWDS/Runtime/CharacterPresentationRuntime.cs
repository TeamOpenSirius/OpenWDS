using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CriWare;
using Sirius;
using Sirius.CharacterModel;
using Sirius.Game;
using Sirius.LiveEngine;
using UnityEngine;

namespace OpenWDS.Runtime
{
    // Local resource ownership around the 2.31.2 GameLoader/Presenter and result
    // StandbyController. Selection uses the current unit, never the probe model.
    public sealed class CharacterPresentationRuntime : MonoBehaviour
    {
        [Serializable] private sealed class BundleRow { public string id, path; }
        [Serializable] private sealed class AssetRow { public string key, internalId; public string[] bundles; }
        [Serializable] private sealed class CharacterRow { public long id, defaultCostumeId; }
        [Serializable] private sealed class VoiceBank { public long characterBaseId; public string path; }
        [Serializable] private sealed class VoiceRow { public long id, characterBaseId; public int order, condition; public string cue, motion; }
        [Serializable] private sealed class StarActRow { public long id; public int branchCondition, storageLightType; }
        [Serializable] private sealed class Index
        {
            public int schemaVersion;
            public BundleRow[] bundles;
            public AssetRow[] assets;
            public CharacterRow[] characters;
            public VoiceBank[] voices, starActVoices;
            public VoiceRow[] resultVoices;
            public StarActRow[] starActs;
            public Vector3 characterPosition;
            public Quaternion characterRotation;
        }

        private Index _index;
        private readonly Dictionary<string, AssetBundle> _bundles = new Dictionary<string, AssetBundle>();
        private AssetBundleCreateRequest _loadingBundle;
        private StarActCutInPlayer _cutIn;
        private GameObject _characterPrefab, _characterRoot;
        private CharacterObjectPerformanceTrigger _trigger;
        private CriAtomExAcb _voiceAcb;
        private CriAtomExPlayer _voicePlayer;
        private CriAtomExPlayback _voicePlayback;
        private CriAtomExAcb _starVoiceAcb;
        private CriAtomExPlayer _starVoicePlayer;
        private CriAtomExPlayback _starVoicePlayback;
        private bool _starVoiceStarted;
        private bool _voiceActive, _resultRequested;
        private float _voiceVolume;
        private readonly Queue<StarActEventFixture> _pendingActs = new Queue<StarActEventFixture>();
        public bool IsReady { get; private set; }
        public int StarActPlayCount { get; private set; }
        public int StarActVoicePlayCount { get; private set; }
        public string StarActVoiceCue { get; private set; }
        public bool StarActVoicePlaying => _starVoiceStarted && _starVoicePlayback.GetStatus() == CriAtomExPlayback.Status.Playing;
        public bool StarActVoiceFinished => _starVoiceStarted && _starVoicePlayback.GetStatus() == CriAtomExPlayback.Status.Removed;
        public int ResultPlayCount { get; private set; }
        public int VoicePlayCount { get; private set; }
        public bool VoiceFinished { get; private set; }
        public long CharacterBaseId { get; private set; }
        public string CharacterKey { get; private set; }
        public string StarActKey { get; private set; }
        public int LastMotion { get; private set; }
        public SenseLightTypes[] LastStarActLights { get; private set; }
        public StarActCutInPlayer CutIn => _cutIn;
        public CharacterObjectPerformanceTrigger Character => _trigger;

        public IEnumerator Prepare(PlayerUnitFixture unit, Camera gameCamera, int endVoiceSetting, float voiceVolume, System.Random random = null, float gameVoiceVolume = 1f)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            _voiceVolume = Mathf.Clamp01(voiceVolume);
            byte[] indexBytes = null;
            yield return StreamingAssetsRuntime.ReadBytes("OpenWDS/Presentation/index.json", b => indexBytes = b);
            _index = JsonUtility.FromJson<Index>(Encoding.UTF8.GetString(indexBytes));
            if (_index == null || _index.schemaVersion != 1) throw new InvalidOperationException("Invalid presentation resource index.");
            var leader = unit.cards.Single(c => c.position == unit.leaderPosition);
            // Actor.IsAwakingDisplay is a separate saved display preference. The
            // public fixture has an explicit normal-card preference, not inferred
            // from rarity, awakeningPhase or the character base id.
            StarActKey = "StarActCutIns/" + leader.characterMasterId.ToString(CultureInfo.InvariantCulture) + "_0";
            // GameModel.SetCharacterBaseIdForLiveUnit: Random=0, Leader=1.
            if (endVoiceSetting != 0 && endVoiceSetting != 1)
                throw new InvalidOperationException("Unsupported game-end voice selection: " + endVoiceSetting);
            CharacterBaseId = endVoiceSetting == 1 ? leader.characterBaseMasterId :
                unit.cards[(random ?? new System.Random()).Next(unit.cards.Length)].characterBaseMasterId;
            var selected = _index.characters.Single(c => c.id == CharacterBaseId);
            CharacterKey = "CharacterObjects/" + selected.defaultCostumeId.ToString(CultureInfo.InvariantCulture) +
                           CharacterBaseId.ToString(CultureInfo.InvariantCulture);
            yield return Load(StarActKey);
            yield return Load(CharacterKey);
            var sprite = FindAsset<Sprite>(StarActKey);
            if (sprite == null || sprite.rect.width <= 0 || sprite.rect.height <= 0)
                throw new InvalidOperationException("Original StarAct sprite failed to load: " + StarActKey);
            var prefab = Resources.Load<GameObject>("Prefabs/Game/StarActCutIn");
            if (prefab == null) throw new InvalidOperationException("Recovered StarAct subtree is missing.");
            _cutIn = Instantiate(prefab, transform).GetComponent<StarActCutInPlayer>();
            _cutIn.SetMainCamera(gameCamera);
            _cutIn.Initialize(sprite);
            _characterPrefab = FindAsset<GameObject>(CharacterKey);
            if (_characterPrefab == null) throw new InvalidOperationException("Original character prefab failed to load: " + CharacterKey);
            var shader = Resources.Load<Shader>("Shader/Character/ScarabCharacter");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Recovered character shader is unavailable.");
            foreach (var material in _characterPrefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct())
            {
                if (material == null || material.shader == null ||
                    (material.shader.name != "Scarab/Chara_Base_MV_Scarab" && material.shader != shader) || material.shaderKeywords.Length != 0)
                    throw new InvalidOperationException("Unrecovered character material variant: " + (material == null ? "null" : material.name));
                // Bundle assets belong to this component. Keep the original
                // MaterialParameterSetter references so its own cloning and
                // Timeline tracks still bind to the correct material instance.
                material.shader = shader;
            }
            var voice = _index.voices.Single(v => v.characterBaseId == CharacterBaseId);
            _voiceAcb = CriAtomExAcb.LoadAcbFile(null, Path.Combine(CriWare.Common.streamingAssetsPath, voice.path), null);
            if (_voiceAcb == null) throw new InvalidOperationException("Original result voice bank failed to load: " + voice.path);
            foreach (var row in _index.resultVoices.Where(v => v.characterBaseId == CharacterBaseId && !string.IsNullOrEmpty(v.cue)))
                if (!_voiceAcb.GetCueInfo(row.cue, out _)) throw new InvalidOperationException("Result voice cue missing: " + row.cue);
            _voicePlayer = new CriAtomExPlayer(true);
            _voicePlayer.SetVolume(_voiceVolume);
            // GameLoader.LoadStarActVoiceAsync selects the leader's base (the
            // ordinary fixtures have no pair-character selection). GameVoicePlayer
            // initializes its cue from GetCueInfoList().FirstOrDefault().name.
            var starVoice = _index.starActVoices.Single(v => v.characterBaseId == leader.characterBaseMasterId);
            _starVoiceAcb = CriAtomExAcb.LoadAcbFile(null, Path.Combine(CriWare.Common.streamingAssetsPath, starVoice.path), null);
            if (_starVoiceAcb == null) throw new InvalidOperationException("StarAct voice bank failed: " + starVoice.path);
            var cues = _starVoiceAcb.GetCueInfoList();
            if (cues.Length == 0 || string.IsNullOrEmpty(cues[0].name)) throw new InvalidOperationException("Empty StarAct voice bank.");
            StarActVoiceCue = cues[0].name;
            _starVoicePlayer = new CriAtomExPlayer(true);
            // GameSoundVolumeConfig.GetVolume has an additional 0.7 multiplier.
            _starVoicePlayer.SetVolume(Mathf.Clamp01(gameVoiceVolume) * 0.7f);
            _starVoicePlayer.SetCue(_starVoiceAcb, StarActVoiceCue);
            _starVoicePlayback = _starVoicePlayer.Prepare();
            if (_starVoicePlayback.id == CriAtomExPlayback.invalidId) throw new InvalidOperationException("StarAct voice prepare failed.");
            IsReady = true;
            Debug.Log($"OPENWDS_PRESENTATION_READY starAct={StarActKey} character={CharacterKey} bundles={_bundles.Count}");
        }

        private IEnumerator Load(string key)
        {
            var row = _index.assets.Single(a => a.key == key);
            foreach (var id in row.bundles)
            {
                if (_bundles.ContainsKey(id)) continue;
                var bundle = _index.bundles.Single(b => b.id == id);
                byte[] bytes = null;
                yield return StreamingAssetsRuntime.ReadBytes(bundle.path, data => bytes = data);
                var loaded = AssetBundle.LoadFromMemoryAsync(bytes);
                _loadingBundle = loaded;
                loaded.completed += operation =>
                {
                    // A scene can be retired while this async load is pending.
                    if (this == null && loaded.assetBundle != null) loaded.assetBundle.Unload(true);
                };
                yield return loaded;
                if (loaded.assetBundle == null) throw new InvalidOperationException("Presentation bundle failed to load: " + id);
                _bundles.Add(id, loaded.assetBundle);
                _loadingBundle = null;
            }
        }

        private T FindAsset<T>(string key) where T : UnityEngine.Object
        {
            var row = _index.assets.Single(a => a.key == key);
            foreach (var id in row.bundles)
            {
                var bundle = _bundles[id];
                if (bundle.GetAllAssetNames().Any(n => string.Equals(n, row.internalId, StringComparison.OrdinalIgnoreCase)))
                    return bundle.LoadAsset<T>(row.internalId);
            }
            return null;
        }

        public void OnStarAct(StarActEventFixture activation)
        {
            if (!IsReady) throw new InvalidOperationException("StarAct fired before resources were prepared.");
            _pendingActs.Enqueue(activation);
            if (_starVoiceStarted)
            {
                _starVoicePlayer.Stop();
                _starVoicePlayback = _starVoicePlayer.Prepare();
            }
            _starVoicePlayback.Resume(CriAtomEx.ResumeMode.PreparedPlayback);
            _starVoiceStarted = true;
            StarActVoicePlayCount++;
            Debug.Log("OPENWDS_STARACT_VOICE cue=" + StarActVoiceCue);
        }

        public void SetGameplayPaused(bool paused)
        {
            if (!_starVoiceStarted) return;
            if (paused) _starVoicePlayback.Pause();
            else _starVoicePlayback.Resume(CriAtomEx.ResumeMode.PausedPlayback);
        }

        private void OnApplicationPause(bool paused)
        {
            if (!_resultRequested) SetGameplayPaused(paused || GetComponent<GameRuntime>()?.IsPaused == true);
        }

        private void LateUpdate()
        {
            // Presenter consumes the fulfilled notification in LateTick. A frame
            // with multiple due events retains the last light array for its one
            // visual trigger, while every activation still contributes score.
            if (_pendingActs.Count > 0 && !_resultRequested)
            {
                StarActEventFixture activation = null;
                while (_pendingActs.Count > 0) activation = _pendingActs.Dequeue();
                var master = _index.starActs.Single(s => s.id == activation.starActMasterId);
                SenseLightTypes[] lights = null;
                if (master.branchCondition == 10 && activation.additionalScoreFactorPercent > 0)
                {
                    if (master.storageLightType >= 1 && master.storageLightType <= 4)
                        lights = new[] { (SenseLightTypes)master.storageLightType };
                    else if (master.storageLightType == 5)
                        lights = new[] { SenseLightTypes.Support, SenseLightTypes.Control, SenseLightTypes.Amplification, SenseLightTypes.Special };
                }
                LastStarActLights = lights;
                ((IStarActCutInPlayer)_cutIn).Animate(lights);
                StarActPlayCount++;
                Debug.Log($"OPENWDS_STARACT_PLAY count={StarActPlayCount} master={activation.starActMasterId} colors={lights?.Length ?? 0}");
            }
            if (!_voiceActive) return;
            var status = _voicePlayback.GetStatus();
            if (status == CriAtomExPlayback.Status.Removed)
            {
                _voiceActive = false;
                VoiceFinished = true;
                _trigger.MouthMotionStop();
                PlayMotion(CharacterBaseId == 402 || CharacterBaseId == 305 ? 4 : 1);
                Debug.Log("OPENWDS_RESULT_VOICE_FINISHED character=" + CharacterBaseId);
            }
        }

        public IEnumerator ShowResult(GameObject background, int clearLamp, bool newAchievement)
        {
            if (_resultRequested) yield break;
            _resultRequested = true;
            while (!IsReady) yield return null;
            _pendingActs.Clear();
            _starVoicePlayer?.Stop();
            _cutIn.gameObject.SetActive(false);
            _characterRoot = new GameObject("CharacterParent");
            _characterRoot.transform.SetPositionAndRotation(_index.characterPosition, _index.characterRotation);
            _characterRoot.SetActive(false);
            var instance = Instantiate(_characterPrefab, _characterRoot.transform, false);
            // GameResultStandbyController.Scale is 1.3 on every axis.
            instance.transform.localScale = Vector3.one * 1.3f;
            _trigger = instance.GetComponent<CharacterObjectPerformanceTrigger>();
            if (_trigger == null || _trigger.Animator == null) throw new InvalidOperationException("Original character performance trigger is missing.");
            if (instance.GetComponentsInChildren<MonoBehaviour>(true).Any(b => b == null))
                throw new InvalidOperationException("Original character contains missing scripts.");
            // PlayStandbyMove does not call SetDirLight (its native callers are
            // photo/theatre/spot/costume flows). Preserve the prefab's authored
            // MaterialParameterSetter configuration instead of borrowing the
            // curtain's animated light for the character shader.
            _characterRoot.SetActive(true);
            _trigger.Initialize(CharacterObjectPerformanceConst.AnimatorTypes.Home);
            var condition = clearLamp > 5 ? 1 : clearLamp > 1 ? 2 : clearLamp == 1 ? (newAchievement ? 3 : 4) : (newAchievement ? 5 : 6);
            var row = _index.resultVoices.First(v => v.characterBaseId == CharacterBaseId && v.condition == condition);
            var motion = Enum.TryParse<CharacterObjectPerformanceConst.MotionState>(row.motion, out var parsed) ? (int)parsed : 1;
            PlayMotion(motion);
            ResultPlayCount++;
            if (!string.IsNullOrEmpty(row.cue))
            {
                _trigger.MouthMotionPlay("2");
                _voicePlayer.SetCue(_voiceAcb, row.cue);
                _voicePlayback = _voicePlayer.Start();
                if (_voicePlayback.id == CriAtomExPlayback.invalidId) throw new InvalidOperationException("Result voice playback failed.");
                _voiceActive = true;
                VoicePlayCount++;
            }
            Debug.Log($"OPENWDS_RESULT_CHARACTER character={CharacterBaseId} costume={CharacterKey} condition={condition} motion={LastMotion} cue={row.cue}");
        }

        private void PlayMotion(int motion)
        {
            LastMotion = motion >= 1 && motion <= 10 ? motion : 0;
            _trigger.ResetTrigger();
            _trigger.SetMotionState((CharacterObjectPerformanceConst.MotionState)LastMotion);
            _trigger.PlayMotion();
        }

        private void OnDestroy()
        {
            _starVoicePlayer?.Dispose();
            _starVoiceAcb?.Dispose();
            _voicePlayer?.Dispose();
            _voiceAcb?.Dispose();
            if (_characterRoot != null)
            {
                _characterRoot.SetActive(false);
                Destroy(_characterRoot);
            }
            if (_cutIn != null)
            {
                _cutIn.gameObject.SetActive(false);
                Destroy(_cutIn.gameObject);
            }
            if (_loadingBundle != null && _loadingBundle.isDone && _loadingBundle.assetBundle != null)
                _loadingBundle.assetBundle.Unload(true);
            _characterPrefab = null;
            foreach (var bundle in _bundles.Values) bundle.Unload(true);
            _bundles.Clear();
        }
    }
}
