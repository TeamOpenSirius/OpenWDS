using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenWDS.Editor
{
    // Exercises ShowGameResult itself, including inactive preparation and the
    // real Animator/Spine/particle player loop. No Rebind, Animator.Update,
    // Skeleton.Animation.Apply or tween seek is used to manufacture the frames.
    [InitializeOnLoad]
    public static class RunResultPresentationPlayMode
    {
        private const string Key = "OpenWDS.ResultPresentationAudit";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly float[] CaptureTimes = { 0.05f, 0.6f, 1.5f, 2.1f, 2.5f, 3.2f, 4.5f };
        private static readonly List<Sample> Samples = new List<Sample>();
        private static GameObject _root, _background;
        private static RecoveredGameResultSeRuntime _sound;
        private static float _started;
        private static int _capture;
        private static bool _countObserved, _fadeObserved, _rankObserved;

        [Serializable]
        private sealed class Sample
        {
            public float time, leftX, alpha, entranceTime, curtainTime;
            public Vector3 curtainBoundsCenter, curtainBoundsSize;
            public int countStarts, rankCues, activeGradeParticles;
            public bool entranceState, navigation, badge;
            public string screenshot;
        }

        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public string failure;
            public List<Sample> samples;
        }

        static RunResultPresentationPlayMode()
        {
            EditorApplication.update += Poll;
            Application.logMessageReceived += (message, trace, type) =>
            {
                if (SessionState.GetInt(Key, 0) == 1 &&
                    (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                    SessionState.SetString(Key + ".error", message);
            };
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a dedicated batch Editor.");
            SessionState.SetInt(Key, 1);
            SessionState.SetString(Key + ".error", "");
            SessionState.SetString(Key + ".start", DateTime.UtcNow.Ticks.ToString());
            EditorSceneManager.OpenScene("Assets/OpenWDS/Scenes/OfflineRhythmPreview.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static string Output(string name) => Path.GetFullPath(Path.Combine(
            Application.dataPath, "../../../reverse/reports", name));

        private static void Poll()
        {
            var phase = SessionState.GetInt(Key, 0);
            if (phase == 0) return;
            if (phase == 2)
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
                if (error.Length != 0) throw new InvalidOperationException(error);
                if (DateTime.UtcNow.Subtract(new DateTime(long.Parse(SessionState.GetString(Key + ".start", "0")))).TotalSeconds > 120)
                    throw new TimeoutException("Result presentation initialization timed out.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                if (_root == null)
                {
                    var game = UnityEngine.Object.FindObjectOfType<RecoveredGameRuntime>();
                    if (game == null || !game.IsInitialized) return;
                    Begin(game);
                    return;
                }
                var sample = Observe();
                _fadeObserved |= sample.alpha > 0f && sample.alpha < 1f;
                if (sample.countStarts > 0)
                {
                    _countObserved = true;
                    Require(sample.entranceState && sample.entranceTime >= 1f && Mathf.Abs(sample.leftX - 477f) < 0.1f,
                        "Count-up began before the actual HUD entrance completed: " + JsonUtility.ToJson(sample));
                }
                Require(!sample.badge || sample.rankCues == 1, "Grade particles activated before the rank cue.");
                _rankObserved |= sample.badge && sample.activeGradeParticles > 0;
                if (_capture < CaptureTimes.Length && sample.time >= CaptureTimes[_capture])
                {
                    sample.screenshot = $"result-presentation-{_capture:00}.png";
                    Capture(Output(sample.screenshot));
                    Samples.Add(sample);
                    _capture++;
                }
                if (_capture < CaptureTimes.Length) return;
                Require(sample.navigation && sample.entranceState && Mathf.Abs(sample.leftX - 477f) < 0.1f,
                    "Settled HUD or navigation is incorrect.");
                Require(_fadeObserved && _countObserved && _rankObserved, "Fade/count-up/grade effect was not observed.");
                Require(_sound.CountStartCount == 5 && _sound.CountStopCount == 5 && _sound.RankCueCount == 1 && _sound.CompletionCueCount == 2,
                    "Result audio replay/count regression.");
                Require(Samples.Last().curtainTime >= 1f &&
                    Vector3.Distance(Samples.First().curtainBoundsSize, Samples.Last().curtainBoundsSize) > 0.1f,
                    "Curtain did not animate through its real Mecanim/Spine path.");
                Finish(true, "");
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void Begin(RecoveredGameRuntime game)
        {
            var notes = Enumerable.Range(1, 120).Select(id => new RecoveredNotationNote
                { Id = id, NoteType = (int)RecoveredNoteType.Normal }).ToArray();
            var result = new RecoveredGameResultRuntime(notes);
            var perfect = new RecoveredTimingDecision(RecoveredTimingType.PerfectStar, RecoveredTimingAssistType.None, 0);
            foreach (var note in notes) result.Collect(RecoveredInputResultEntity.Create(note, perfect));
            typeof(RecoveredGameRuntime).GetField("_gameResultRuntime", Private).SetValue(game, result);
            // ShowGameResult records results synchronously. Preserve the user's
            // existing file around this explicitly synthetic visual fixture.
            var path = (string)typeof(RecoveredLocalResultStore).GetField("_path", Private)
                .GetValue(new RecoveredLocalResultStore());
            var saved = File.Exists(path) ? File.ReadAllBytes(path) : null;
            try { typeof(RecoveredGameRuntime).GetMethod("ShowGameResult", Private).Invoke(game, null); }
            finally
            {
                if (saved != null) File.WriteAllBytes(path, saved);
                else if (File.Exists(path)) File.Delete(path);
            }
            _root = (GameObject)typeof(RecoveredGameRuntime).GetField("_gameResultInstance", Private).GetValue(game);
            _background = (GameObject)typeof(RecoveredGameRuntime).GetField("_gameResultBackgroundInstance", Private).GetValue(game);
            _sound = (RecoveredGameResultSeRuntime)typeof(RecoveredGameRuntime).GetField("_resultSe", Private).GetValue(game);
            _started = Time.time;
        }

        private static Sample Observe()
        {
            var state = _root.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0);
            var skeleton = _background.GetComponentInChildren<Spine.Unity.SkeletonMecanim>();
            var mesh = skeleton.GetComponent<MeshFilter>().sharedMesh;
            var badge = _root.transform.Find("LeftPanel/GameResultPanel/ResultPanel /HeaderPanel/NextRewardPanel/Badge");
            return new Sample
            {
                time = Time.time - _started,
                leftX = ((RectTransform)_root.transform.Find("LeftPanel")).anchoredPosition.x,
                alpha = _root.GetComponent<CanvasGroup>().alpha,
                entranceState = state.IsName("GameResult_left_in_anim"),
                entranceTime = state.normalizedTime,
                curtainTime = skeleton.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).normalizedTime,
                curtainBoundsCenter = mesh.bounds.center,
                curtainBoundsSize = mesh.bounds.size,
                countStarts = _sound.CountStartCount,
                rankCues = _sound.RankCueCount,
                navigation = _root.transform.Find("RightBotton").gameObject.activeInHierarchy,
                badge = badge.gameObject.activeInHierarchy,
                activeGradeParticles = badge.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount),
            };
        }

        private static void Capture(string path)
        {
            var camera = _background.GetComponentInChildren<Camera>();
            var canvas = _root.GetComponentInParent<Canvas>();
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            var oldDistance = canvas.planeDistance;
            var oldTarget = camera.targetTexture;
            var oldAspect = camera.aspect;
            var oldActive = RenderTexture.active;
            var target = new RenderTexture(1014, 633, 24);
            var texture = new Texture2D(1014, 633, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.aspect = 1014f / 633f;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1014, 633), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera;
                canvas.planeDistance = oldDistance;
                camera.targetTexture = oldTarget;
                camera.aspect = oldAspect;
                RenderTexture.active = oldActive;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static void Require(bool valid, string reason)
        {
            if (!valid) throw new InvalidOperationException(reason);
        }

        private static void Finish(bool passed, string failure)
        {
            SessionState.SetBool(Key + ".passed", passed);
            SessionState.SetInt(Key, 2);
            File.WriteAllText(Output("result-presentation-validation.json"),
                JsonUtility.ToJson(new Report { passed = passed, failure = failure, samples = Samples }, true));
            Debug.Log($"OPENWDS_RESULT_PRESENTATION passed={passed} {failure}");
            EditorApplication.isPlaying = false;
        }
    }
}
