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
        private static readonly float[] CaptureTimes = { 0.05f, 0.6f, 1.5f, 2.1f, 2.5f, 2.8f, 3.0f, 3.2f, 6.0f };
        private static readonly List<Sample> Samples = new List<Sample>();
        private static GameObject _root, _background;
        private static GameResultSeRuntime _sound;
        private static float _started;
        private static int _capture;
        private static bool _countObserved, _fadeObserved, _rankObserved, _stageObserved;

        [Serializable]
        private sealed class Sample
        {
            public float time, leftX, alpha, entranceTime, curtainTime, stageAlpha, stageTime;
            public Vector3 curtainBoundsCenter, curtainBoundsSize;
            public float spotlightAlpha;
            public int countStarts, rankCues, usedVoices, activeGradeParticles, stageImages;
            public bool entranceState, navigation, badge;
            public string screenshot;
        }

        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public bool olivier;
            public bool failedLive;
            public bool anotherNotation;
            public bool mixedJudgements;
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

        public static void Run() => Start(false);
        public static void RunMixed() => Start(false, mixed: true);
        public static void RunOlivier() => Start(true);
        public static void RunFailed() => Start(false, true);
        public static void RunAnother() => Start(true, false, true);

        private static void Start(bool olivier, bool failed = false, bool another = false, bool mixed = false)
        {
            CreateOfflineRhythmPreviewScene.RecoverResultCurtain();
            SessionState.SetBool(Key + ".mixed", mixed);
            SessionState.SetBool(Key + ".olivier", olivier);
            SessionState.SetBool(Key + ".failed", failed);
            SessionState.SetBool(Key + ".another", another);
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a dedicated batch Editor.");
            SessionState.SetInt(Key, 1);
            SessionState.SetString(Key + ".error", "");
            SessionState.SetString(Key + ".start", DateTime.UtcNow.Ticks.ToString());
            EditorSceneManager.OpenScene("Assets/OpenWDS/Scenes/OfflineRhythmPreview.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static string Output(string name) => Path.GetFullPath(Path.Combine(
            Application.dataPath, "../../../reverse/reports",
            SessionState.GetBool(Key + ".mixed", false) ? name.Replace("result-presentation", "mixed-result-presentation") :
            SessionState.GetBool(Key + ".another", false) ? name.Replace("result-presentation", "another-result-presentation") :
            SessionState.GetBool(Key + ".failed", false) ? name.Replace("result-presentation", "failed-result-presentation") :
            SessionState.GetBool(Key + ".olivier", false) ? name.Replace("result-presentation", "olivier-result-presentation") : name));

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
                    var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
                    if (game == null || !game.IsInitialized) return;
                    if (game.CharacterPresentation != null && !game.CharacterPresentation.IsReady) return;
                    Begin(game);
                    return;
                }
                var sample = Observe();
                _fadeObserved |= sample.alpha > 0f && sample.alpha < 1f;
                _stageObserved |= sample.stageAlpha > 0.5f && sample.stageTime > 0f;
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
                var bgm = UnityEngine.Object.FindObjectOfType<GameResultSceneRuntime>().ResultBgm;
                Require(bgm.LastPlayback.GetStatus() == CriWare.CriAtomExPlayback.Status.Playing,
                    "Result BGM was displaced during count-up.");
                Require(Samples.Last().curtainTime >= 1f &&
                    Vector3.Distance(Samples.First().curtainBoundsSize, Samples.Last().curtainBoundsSize) > 0.1f,
                    "Curtain did not animate through its real Mecanim/Spine path.");
                Require(Samples.First().spotlightAlpha == 0 && Samples.Last().spotlightAlpha > .4f,
                    "Original result spotlight did not fade in.");
                Require(_stageObserved && sample.stageAlpha == 0f && sample.stageTime >= 1f, "Stage Success did not enter and fade out through its Animator.");
                if (SessionState.GetBool(Key + ".another", false))
                {
                    var rate = _root.GetComponentInChildren<Sirius.GameResult.GameResultRatePanel>(true);
                    var texts = rate.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                    Require(texts.Single(t => t.name == "RateText").text == "星章集計対象外です。" &&
                        texts.Single(t => t.name == "RateText").gameObject.activeInHierarchy &&
                        !texts.Any(t => t.name == "ThisTimeRate" && t.gameObject.activeInHierarchy),
                        "Manual Another result exclusion text/numbers");
                    var lamps = _root.transform.Find("LeftPanel/MusicInfoPanel/ClearLamps").Cast<Transform>()
                        .Where(t => t.gameObject.activeSelf).ToArray();
                    Require(lamps.Length == 1 && lamps[0].name == "LampOlivier", "Manual Another result single lamp");
                }
                else if (SessionState.GetBool(Key + ".olivier", false))
                {
                    var texts = _root.GetComponentInChildren<Sirius.GameResult.GameResultRatePanel>(true)
                        .GetComponentsInChildren<UnityEngine.UI.Text>(true);
                    Require(texts.Any(t => t.name == "ThisTimeRate" && t.text == "100.00%") &&
                        texts.Any(t => t.name == "ThisTimeRate" && t.text == SessionState.GetInt(Key + ".spPoint", 0).ToString()),
                        "Production Olivier result did not show the settled stars/percentage.");
                }
                ValidateAllPerfectParticleShader();
                Finish(true, "");
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void Begin(GameRuntime game)
        {
            // This visual sampler must observe the 0.2 s fade even when the
            // first character instantiation stalls a batch Editor frame. Bound
            // scaled animation steps; native CRI playback keeps its real clock.
            Time.maximumDeltaTime = 1f / 30f;
            var notes = Enumerable.Range(1, 120).Select(id => new NotationNote
                { Id = id, NoteType = (int)NoteType.Normal }).ToArray();
            var result = new GameResultRuntime(notes);
            var perfect = new TimingDecision(TimingType.PerfectStar, TimingAssistType.None, 0);
            foreach (var note in notes)
            {
                var timing = SessionState.GetBool(Key + ".mixed", false) && note.Id <= 5
                    ? new TimingDecision((TimingType)note.Id, TimingAssistType.None, 0)
                    : perfect;
                result.Collect(InputResultEntity.Create(note, timing));
            }
            typeof(GameRuntime).GetField("_gameResultRuntime", Private).SetValue(game, result);
            // ShowGameResult records results synchronously. Preserve the user's
            // existing file around this explicitly synthetic visual fixture.
            var another = SessionState.GetBool(Key + ".another", false);
            if (another)
            {
                var catalog = LocalMusicCatalog.FromJson(File.ReadAllText(
                    SongResourceStore.CatalogPath));
                var music = catalog.Musics.First(m => m.Id == (long)typeof(GameRuntime).GetField("_musicId", Private).GetValue(game));
                var live = new LocalLiveEntry { Id = 52, AnotherNotationId = 52, Difficulty = MusicDifficulty.Olivier, Level = 105 };
                typeof(GameRuntime).GetField("_ratingMusic", Private).SetValue(game, music);
                typeof(GameRuntime).GetField("_ratingLive", Private).SetValue(game, live);
                typeof(GameRuntime).GetField("_musicDifficulty", Private).SetValue(game, MusicDifficulty.Olivier);
            }
            var path = (string)typeof(LocalResultStore).GetField("_path", Private)
                .GetValue(new LocalResultStore(anotherNotation: another));
            var saved = File.Exists(path) ? File.ReadAllBytes(path) : null;
            try
            {
                if (SessionState.GetBool(Key + ".olivier", false) && !another)
                {
                    var catalog = LocalMusicCatalog.FromJson(File.ReadAllText(
                        SongResourceStore.CatalogPath));
                    var music = catalog.Musics.First(m => m.Id == (long)typeof(GameRuntime).GetField("_musicId", Private).GetValue(game));
                    var live = music.Lives.First(l => l.Difficulty == MusicDifficulty.Olivier);
                    typeof(GameRuntime).GetField("_ratingMusic", Private).SetValue(game, music);
                    typeof(GameRuntime).GetField("_ratingLive", Private).SetValue(game, live);
                    typeof(GameRuntime).GetField("_ratingMusics", Private).SetValue(game, new[] { music });
                    typeof(GameRuntime).GetField("_musicDifficulty", Private).SetValue(game, MusicDifficulty.Olivier);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, SettingsCrypto.EncryptUtf8("[]"));
                    typeof(GameRuntime).GetMethod("ShowGameResult", Private).Invoke(game, null);
                    Require(new LocalResultStore().GetSpPoint(music.Id) == OlivierStars.GetMaxPoint(live.Level),
                        "Production Olivier settlement did not persist the maximum A+B+C.");
                    SessionState.SetInt(Key + ".spPoint", OlivierStars.GetMaxPoint(live.Level));
                }
                else
                {
                    var failed = SessionState.GetBool(Key + ".failed", false);
                    if (failed)
                    {
                        // The isolated split cases load the same bundles as the live.
                        // Release the completed live's owner before loading those cases.
                        var splitField = typeof(GameRuntime).GetField("_splitLaneAssets", Private);
                        ((SplitLaneAssetRuntime)splitField.GetValue(game))?.Dispose();
                        splitField.SetValue(game, null);
                        ValidateDeviceFourIssues.ValidateSplitsInPlayMode();
                        game.GameHud.Life.Set(0);
                        typeof(GameRuntime).GetMethod("BeginClearPerformance", Private).Invoke(game, null);
                        var boundary = (Sirius.Game.GameResultPanel)typeof(GameRuntime)
                            .GetField("_clearAnimation", Private).GetValue(game);
                        Require(boundary.ClearType == Sirius.Game.BoundaryClearType.Failed &&
                            game.ClearSe.LastCueName == "Finish", "Failed live selected a successful clear performance.");
                        var finishAnimator = (Animator)typeof(Sirius.Game.GameResultPanel)
                            .GetField("_animator", Private).GetValue(boundary);
                        finishAnimator.Update(0f);
                        finishAnimator.Update(1.2f);
                        Require(finishAnimator.GetCurrentAnimatorStateInfo(0).IsName("ClearAnimation_finish_anim") &&
                            boundary.transform.Find("FINISH").gameObject.activeInHierarchy &&
                            !boundary.transform.Find("FAILED").gameObject.activeInHierarchy,
                            "Zero-Life must display the original FINISH animation.");
                        Capture(Output("failed-live-finish.png"),
                            (Camera)typeof(GameRuntime).GetField("_gameCamera", Private).GetValue(game),
                            boundary.GetComponentInParent<Canvas>());
                        boundary.Hide();
                        typeof(GameRuntime).GetField("_clearPerformanceStarted", Private).SetValue(game, false);
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        File.WriteAllText(path, SettingsCrypto.EncryptUtf8("[]"));
                    }
                    typeof(GameRuntime).GetMethod("ShowGameResult", Private).Invoke(game, null);
                    if (failed)
                    {
                        var musicId = (long)typeof(GameRuntime).GetField("_musicId", Private).GetValue(game);
                        var store = new LocalResultStore();
                        Require(store.GetBest(musicId, game.MusicDifficulty) == 101d &&
                            store.GetClearLamp(musicId, game.MusicDifficulty) == ClearLamp.None,
                            "Failed manual result must persist achievement without a clear lamp.");
                        Require(PlayerRating.CalculateNotationRate(20, store.GetBest(musicId, game.MusicDifficulty)) == 26.05d,
                            "Failed result achievement must still contribute rating.");
                        store.RecordResult(musicId, game.MusicDifficulty, 99d, ClearLamp.FullCombo, out _);
                        store.RecordResult(musicId, game.MusicDifficulty, 100d, ClearLamp.None, out _);
                        Require(new LocalResultStore().GetClearLamp(musicId, game.MusicDifficulty) == ClearLamp.FullCombo,
                            "A failed retry erased the historical clear lamp.");
                    }
                }
            }
            finally
            {
                if (saved != null) File.WriteAllBytes(path, saved);
                else if (File.Exists(path)) File.Delete(path);
            }
            _root = (GameObject)typeof(GameRuntime).GetField("_gameResultInstance", Private).GetValue(game);
            _background = (GameObject)typeof(GameRuntime).GetField("_gameResultBackgroundInstance", Private).GetValue(game);
            _sound = (GameResultSeRuntime)typeof(GameRuntime).GetField("_resultSe", Private).GetValue(game);
            _started = Time.time;
        }

        private static Sample Observe()
        {
            var state = _root.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0);
            var stage = _root.transform.Find("SucceseTextImagePosition/GameResultStageSuccess");
            Require(stage != null, "Stage Success subtree missing.");
            var stageImages = stage.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            Require(stageImages.Length == 12 && stageImages.All(i => i.sprite != null), "Stage Success text/sprite components did not survive saving.");
            var skeleton = _background.GetComponentInChildren<Spine.Unity.SkeletonMecanim>();
            var mesh = skeleton.GetComponent<MeshFilter>().sharedMesh;
            var badge = _root.transform.Find("LeftPanel/GameResultPanel/ResultPanel /HeaderPanel/NextRewardPanel/Badge");
            return new Sample
            {
                time = Time.time - _started,
                stageImages = stageImages.Length,
                stageAlpha = stage.Find("position").GetComponent<CanvasGroup>().alpha,
                stageTime = stage.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).normalizedTime,
                leftX = ((RectTransform)_root.transform.Find("LeftPanel")).anchoredPosition.x,
                alpha = _root.GetComponent<CanvasGroup>().alpha,
                entranceState = state.IsName("GameResult_left_in_anim"),
                entranceTime = state.normalizedTime,
                curtainTime = skeleton.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).normalizedTime,
                spotlightAlpha = _background.transform.Find("SpotLightCharacte").GetComponent<SpriteRenderer>().color.a,
                curtainBoundsCenter = mesh.bounds.center,
                curtainBoundsSize = mesh.bounds.size,
                countStarts = _sound.CountStartCount,
                usedVoices = CriWare.CriAtomExVoicePool.GetNumUsedVoices(CriWare.CriAtomExVoicePool.VoicePoolId.StandardMemory).numUsedVoices,
                rankCues = _sound.RankCueCount,
                navigation = _root.transform.Find("RightBotton").gameObject.activeInHierarchy,
                badge = badge.gameObject.activeInHierarchy,
                activeGradeParticles = badge.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount),
            };
        }

        private static void Capture(string path)
        {
            Capture(path, _background.GetComponentInChildren<Camera>(), _root.GetComponentInParent<Canvas>());
        }

        private static void Capture(string path, Camera camera, Canvas canvas)
        {
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

        private static void ValidateAllPerfectParticleShader()
        {
            var badge = _root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "BalloonBadgeRainbow");
            var original = badge.Find("BalloonBadgeEffect/Particle2_al").GetComponent<ParticleSystemRenderer>().sharedMaterial;
            Require(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original.shader)) ==
                "8abb4daa11bc8b64a8e4242f7f8984a0", "AP particle shader reference changed");
            // Render the actual AP material with a known vertex tint. The old placeholder
            // renders white; test both the native alpha-squared blend and rectangle clipping.
            var material = new Material(original);
            var target = new RenderTexture(16, 16, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(16, 16, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active;
            try
            {
                target.Create();
                RenderTexture.active = target;
                material.mainTexture = Texture2D.whiteTexture;
                material.color = Color.white;
                material.SetVector("_ClipRect", new Vector4(0, 0, .5f, 1));
                GL.Clear(true, true, Color.black);
                GL.PushMatrix();
                try
                {
                    GL.LoadOrtho();
                    Require(material.SetPass(0), "AP particle shader pass unavailable");
                    GL.Begin(GL.QUADS);
                    GL.Color(new Color(.2f, .8f, .4f, .5f));
                    GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
                    GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
                    GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
                    GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
                    GL.End();
                }
                finally { GL.PopMatrix(); }
                pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0); pixels.Apply();
                var colored = pixels.GetPixel(3, 8);
                var clipped = pixels.GetPixel(12, 8);
                Require(Mathf.Abs(colored.r - .05f) < .015f && Mathf.Abs(colored.g - .2f) < .015f &&
                    Mathf.Abs(colored.b - .1f) < .015f && clipped.r + clipped.g + clipped.b < .01f,
                    "AP particle vertex tint/blend/clipping regression: " + colored + " / " + clipped);
                Debug.Log("OPENWDS_AP_PARTICLE_COLOR passed rgb=" + colored);
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(material);
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
                JsonUtility.ToJson(new Report { passed = passed, anotherNotation = SessionState.GetBool(Key + ".another", false),
                mixedJudgements = SessionState.GetBool(Key + ".mixed", false), failedLive = SessionState.GetBool(Key + ".failed", false), olivier = SessionState.GetBool(Key + ".olivier", false), failure = failure, samples = Samples }, true));
            Debug.Log($"OPENWDS_RESULT_PRESENTATION passed={passed} {failure}");
            EditorApplication.isPlaying = false;
        }
    }
}
