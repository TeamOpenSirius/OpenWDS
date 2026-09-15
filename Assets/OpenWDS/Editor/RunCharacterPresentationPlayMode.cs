using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CriWare;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenWDS.Editor
{
    [InitializeOnLoad]
    public static class RunCharacterPresentationPlayMode
    {
        private const string Key = "OpenWDS.CharacterPresentation";
        [Serializable] private sealed class Row
        {
            public long character;
            public string resource;
            public int condition, pixels, meshes, bones, cloth, initialMotion, standbyMotion, starActColors;
            public bool animationMoved, voiceFinished;
        }
        [Serializable] private sealed class Report
        {
            public bool passed;
            public string failure;
            public List<Row> samples = new List<Row>();
        }
        private sealed class SelectedIndexRandom : System.Random
        {
            private readonly int _index;
            public SelectedIndexRandom(int index) { _index = index; }
            public override int Next(int maxValue) => _index;
        }
        private static Report _report;
        private static PlayerUnitFixture[] _units;
        private static long[] _characters;
        private static CharacterPresentationRuntime _presentation;
        private static GameObject _background, _owner;
        private static Camera _camera;
        private static int _index, _phase;
        private static float _started;
        private static Row _row;
        private static Transform[] _bones;
        private static Quaternion[] _rotations;
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../../reverse/reports/character-presentation-playmode-validation.json"));

        static RunCharacterPresentationPlayMode()
        {
            EditorApplication.update += Poll;
            Application.logMessageReceived += (message, trace, type) =>
            {
                if (SessionState.GetInt(Key, 0) == 1 && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                    SessionState.SetString(Key + ".error", message);
            };
        }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a dedicated batch Editor.");
            SessionState.SetInt(Key, 1);
            SessionState.SetString(Key + ".error", "");
            SessionState.SetString(Key + ".start", DateTime.UtcNow.Ticks.ToString());
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        private static void Poll()
        {
            var state = SessionState.GetInt(Key, 0);
            if (state == 0) return;
            if (state == 2)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetInt(Key, 0);
                    EditorApplication.Exit(SessionState.GetBool(Key + ".passed", false) ? 0 : 1);
                }
                return;
            }
            try
            {
                var error = SessionState.GetString(Key + ".error", "");
                Require(error.Length == 0, error);
                Require((DateTime.UtcNow.Ticks - long.Parse(SessionState.GetString(Key + ".start", "0"))) /
                    (double)TimeSpan.TicksPerSecond < 300, "Character presentation timeout.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                if (_report == null) { Begin(); return; }
                if (_phase == 0)
                {
                    if (_owner != null) return;
                    if (_index == _characters.Length) { Finish(true, ""); return; }
                    Prepare(); return;
                }
                if (!_presentation.IsReady) return;
                if (_phase == 1)
                {
                    _presentation.OnStarAct(_units.First(u => u.cards.Any(c => c.characterBaseMasterId == _characters[_index])).starActEvents[0]);
                    _phase = 2; return;
                }
                if (_phase == 2)
                {
                    if (_presentation.StarActPlayCount == 0) return;
                    Require(_presentation.StarActVoicePlayCount == 1 && !string.IsNullOrEmpty(_presentation.StarActVoiceCue), "StarAct voice did not start.");
                    _row.resource = _presentation.CharacterKey;
                    _row.starActColors = _presentation.LastStarActLights?.Length ?? 0;
                    var unit = _units.First(u => u.cards.Any(c => c.characterBaseMasterId == _characters[_index]));
                    var expectedColors = unit.starActEvents[0].additionalScoreFactorPercent > 0 ? 4 : 0;
                    Require(_row.starActColors == expectedColors, "Wrong original storage-light array.");
                    var lamps = new[] { 6, 2, 1, 1, 0, 0 };
                    var best = new[] { false, false, true, false, true, false };
                    _row.condition = _index % 6 + 1;
                    _presentation.StartCoroutine(_presentation.ShowResult(_background, lamps[_index % 6], best[_index % 6]));
                    _phase = 3; return;
                }
                if (_phase == 3)
                {
                    if (_presentation.ResultPlayCount == 0) return;
                    var trigger = _presentation.Character;
                    var meshes = trigger.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    Require(meshes.Length > 0 && meshes.All(m => m.sharedMesh != null && m.sharedMesh.vertexCount > 0 && m.bones.All(b => b != null)), "Invalid character geometry.");
                    _row.meshes = meshes.Length;
                    _bones = meshes.SelectMany(m => m.bones).Distinct().ToArray();
                    _rotations = _bones.Select(b => b.localRotation).ToArray();
                    _row.bones = _bones.Length;
                    foreach (var b in trigger.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        Require(b != null, "Missing character behaviour.");
                        if (b.GetType().FullName == "MagicaCloth.MagicaBoneCloth")
                        {
                            _row.cloth++;
                            Require(b.GetType().GetMethod("VerifyData").Invoke(b, null).ToString() == "None", "Incompatible original cloth data.");
                        }
                    }
                    Require(_row.cloth > 0, "Original cloth components are missing.");
                    foreach (var material in meshes.SelectMany(m => m.sharedMaterials).Distinct())
                    {
                        Require(material.passCount == 5, "Missing recovered character GPU pass.");
                        for (var i = 0; i < 5; i++) Require(material.SetPass(i), "Character GPU pass failed: " + i);
                    }
                    _row.initialMotion = _presentation.LastMotion;
                    _started = Time.time;
                    _phase = 4; return;
                }
                for (var i = 0; i < _bones.Length; i++)
                    if (Quaternion.Angle(_rotations[i], _bones[i].localRotation) > 0.01f) _row.animationMoved = true;
                if (Time.time - _started < 1f) return;
                if (_row.pixels == 0)
                    _row.pixels = PresentationRenderingValidation.Capture(_camera,
                        _presentation.Character.GetComponentsInChildren<Renderer>(true), $"result-character-{_row.character}.png");
                if (!_presentation.VoiceFinished) return;
                _row.voiceFinished = true;
                _row.standbyMotion = _presentation.LastMotion;
                Require(_row.animationMoved, "Original character animation did not move.");
                Require(_row.standbyMotion == (_row.character == 402 || _row.character == 305 ? 4 : 1), "Incorrect post-voice motion.");
                Require(_presentation.Character.Animator.GetLayerWeight(_presentation.Character.Animator.GetLayerIndex("Mouth Layer")) == 0, "Mouth did not stop with voice.");
                _report.samples.Add(_row);
                Debug.Log("OPENWDS_CHARACTER_PRESENTATION_SAMPLE " + JsonUtility.ToJson(_row));
                UnityEngine.Object.Destroy(_owner);
                _index++;
                _phase = 0;
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
        private static void Begin()
        {
            _report = new Report();
            _units = Directory.GetFiles(Path.Combine(Application.streamingAssetsPath, "OpenWDS/TestPlayer"), "*.json")
                .OrderBy(p => p).Select(p => JsonUtility.FromJson<PlayerUnitFixture>(File.ReadAllText(p))).ToArray();
            _characters = _units.SelectMany(u => u.cards).Select(c => c.characterBaseMasterId).Distinct().OrderBy(c => c).ToArray();
            Require(_characters.Length == 11, "Unexpected current fixture cast.");
            var audio = new GameObject("PresentationAudio");
            audio.SetActive(false);
            var initializer = audio.AddComponent<CriWareInitializer>();
            initializer.dontInitializeOnAwake = true;
            initializer.initializesMana = false;
            initializer.atomConfig.acfFileName = Path.Combine(Application.streamingAssetsPath, "OpenWDS/CRI/Sirius.acf");
            audio.SetActive(true);
            initializer.Initialize();
            _background = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Prefabs/GameResultCurtainBackground"));
            _background.SetActive(true);
            _camera = _background.GetComponentInChildren<Camera>(true);
        }
        private static void Prepare()
        {
            var character = _characters[_index];
            var unit = _units.First(u => u.cards.Any(c => c.characterBaseMasterId == character));
            var partyIndex = Array.FindIndex(unit.cards, c => c.characterBaseMasterId == character);
            _row = new Row { character = character };
            _owner = new GameObject("PresentationCase" + character);
            _presentation = _owner.AddComponent<CharacterPresentationRuntime>();
            _presentation.StartCoroutine(_presentation.Prepare(unit, _camera, 0, 1f, new SelectedIndexRandom(partyIndex)));
            _row.resource = _presentation.CharacterKey;
            _phase = 1;
        }
        private static void Finish(bool passed, string failure)
        {
            if (_report == null) _report = new Report();
            _report.passed = passed;
            _report.failure = failure;
            File.WriteAllText(Output, JsonUtility.ToJson(_report, true) + "\n");
            Debug.Log($"OPENWDS_CHARACTER_PRESENTATION passed={passed} samples={_report.samples.Count} failure={failure}");
            SessionState.SetBool(Key + ".passed", passed);
            SessionState.SetInt(Key, 2);
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string failure) { if (!value) throw new InvalidOperationException(failure); }
    }
}
