using System;
using System.IO;
using System.Threading;
using CriWare;
using UnityEditor;
using UnityEngine;

namespace OpenWDS.Editor
{
    public static class ValidateCriAudioRuntime
    {
        public static void Run()
        {
            var root = Path.Combine(
                Application.streamingAssetsPath, "OpenWDS");
            var acf = Path.Combine(root, "CRI", "Sirius.acf");
            var acb = Path.Combine(
                root, "StandardCharts", "1", "cri", "music_1.acb.bundle");
            var commonSe = Path.Combine(root, "CRI", "GameCommonSE.acb");
            var customSe = Path.Combine(root, "CRI", "GameCustomSE_1.acb");
            var sharedSe = Path.Combine(root, "CRI", "SE.acb");
            var clearSe = Path.Combine(root, "CRI", "ClearSE.acb");
            if (!File.Exists(acf) || !File.Exists(acb))
                throw new FileNotFoundException("CRI fixture is incomplete.");

            var owner = new GameObject("CRI_Runtime_Validation");
            var initializer = owner.AddComponent<CriWareInitializer>();
            initializer.dontInitializeOnAwake = true;
            initializer.initializesMana = false;
            initializer.atomConfig.acfFileName = acf;
            CriAtomExAcb loaded = null;
            CriAtomExPlayer player = null;
            CriAtomExAcb common = null;
            CriAtomExAcb custom = null;
            CriAtomExPlayer criticalPlayer = null;
            OpenWDS.Runtime.RecoveredGameSeRuntime gameSe = null;
            OpenWDS.Runtime.RecoveredUiSeRuntime sharedSeRuntime = null;
            OpenWDS.Runtime.RecoveredGameClearSeRuntime clearSeRuntime = null;
            OpenWDS.Runtime.RecoveredGameResultSeRuntime resultSeRuntime = null;
            GameObject effectSeOwner = null;
            try
            {
                initializer.Initialize();
                loaded = CriAtomExAcb.LoadAcbFile(null, acb, null);
                if (loaded == null)
                    throw new InvalidOperationException("Music 1 ACB failed to load.");
                if (!loaded.GetCueInfo("1", out var cue))
                    throw new InvalidOperationException("Music 1 cue '1' was not found.");
                player = new CriAtomExPlayer(true);
                player.SetCue(loaded, "1");
                var playback = player.Prepare();
                Debug.Log(
                    "OPENWDS_CRI_AUDIO " +
                    $"acf={new FileInfo(acf).Length} acb={new FileInfo(acb).Length} " +
                    $"cue={cue.name} cueId={cue.id} length={cue.length} " +
                    $"tracks={cue.numTracks} waves={cue.numRelatedWaveForms} " +
                    $"preparedStatus={playback.GetStatus()}");
                playback.Resume(CriAtomEx.ResumeMode.AllPlayback);
                ValidateClockSemantics(playback);
                common = CriAtomExAcb.LoadAcbFile(null, commonSe, null);
                custom = CriAtomExAcb.LoadAcbFile(null, customSe, null);
                if (common == null || custom == null)
                    throw new InvalidOperationException("Game SE ACB failed to load.");
                Debug.Log(
                    "OPENWDS_CRI_SE common=" +
                    string.Join(",", Array.ConvertAll(
                        common.GetCueInfoList(), item => item.name)) +
                    " custom=" + string.Join(",", Array.ConvertAll(
                        custom.GetCueInfoList(), item => item.name)));
                if (!common.GetCueInfo("Basic1_critical", out var criticalCue))
                    throw new InvalidOperationException(
                        "Game SE cue is missing: Basic1_critical");
                Debug.Log(
                    "OPENWDS_CRI_CRITICAL " +
                    $"length={criticalCue.length} limits={criticalCue.numLimits} " +
                    $"tracks={criticalCue.numTracks} priority={criticalCue.priority} " +
                    $"probability={criticalCue.probability} categories=" +
                    DescribeCategories(criticalCue.categories));
                criticalPlayer = new CriAtomExPlayer();
                criticalPlayer.SetCue(common, "Basic1_critical");
                var criticalPlaybacks = new CriAtomExPlayback[7];
                for (var index = 0; index < criticalPlaybacks.Length; index++)
                {
                    criticalPlaybacks[index] = criticalPlayer.Start();
                    Thread.Sleep(85);
                }
                for (var index = 0; index < criticalPlaybacks.Length; index++)
                {
                    if (criticalPlaybacks[index].id == 0 ||
                        criticalPlaybacks[index].GetStatus() !=
                        CriAtomExPlayback.Status.Playing)
                    {
                        throw new InvalidOperationException(
                            "Dense Critical playback was dropped at index " + index);
                    }
                }
                Debug.Log(
                    "OPENWDS_CRI_CRITICAL_BURST ids=" +
                    string.Join(",", Array.ConvertAll(
                        criticalPlaybacks, item => item.id.ToString())) +
                    " statuses=" + string.Join(",", Array.ConvertAll(
                        criticalPlaybacks, item => item.GetStatus().ToString())));
                playback.Stop();
                gameSe = owner.AddComponent<OpenWDS.Runtime.RecoveredGameSeRuntime>();
                gameSe.Initialize();
                if (!OpenWDS.Runtime.RecoveredGameSeRuntime.ValidateRecoveredRules())
                    throw new InvalidOperationException(
                        "Recovered Game SE ARM64 rules failed validation.");
                Debug.Log("OPENWDS_CRI_GAME_SE runtimeCues=10 rules=valid");

                if (!File.Exists(sharedSe))
                    throw new FileNotFoundException(
                        "Shared SE fixture is missing.", sharedSe);
                sharedSeRuntime =
                    owner.AddComponent<OpenWDS.Runtime.RecoveredUiSeRuntime>();
                effectSeOwner = new GameObject("EffectSePlayer_Validation");
                var effectSe = effectSeOwner.AddComponent<Sirius.EffectSePlayer>();
                effectSe.OnSEPlay("Olivier_glass");
                if (!sharedSeRuntime.IsInitialized ||
                    sharedSeRuntime.LastCueName != "Olivier_glass" ||
                    sharedSeRuntime.CueNamePlayCount != 1 ||
                    sharedSeRuntime.LastPlayback.id ==
                    CriAtomExPlayback.invalidId)
                {
                    throw new InvalidOperationException(
                        "EffectSePlayer did not reach the shared SE player.");
                }
                Debug.Log(
                    "OPENWDS_CRI_SHARED_SE " +
                    $"cue={sharedSeRuntime.LastCueName} " +
                    $"playback={sharedSeRuntime.LastPlayback.id} " +
                    $"status={sharedSeRuntime.LastPlayback.GetStatus()} " +
                    "eventReceiver=valid");

                if (!File.Exists(clearSe))
                    throw new FileNotFoundException(
                        "Clear SE fixture is missing.", clearSe);
                clearSeRuntime = owner.AddComponent<
                    OpenWDS.Runtime.RecoveredGameClearSeRuntime>();
                clearSeRuntime.Play(
                    Sirius.Game.RecoveredBoundaryClearType.AllPerfect);
                if (!clearSeRuntime.IsInitialized ||
                    clearSeRuntime.LastCueName != "AllPerfect" ||
                    clearSeRuntime.PlayCount != 1 ||
                    clearSeRuntime.LastPlayback.id ==
                    CriAtomExPlayback.invalidId)
                {
                    throw new InvalidOperationException(
                        "GameClearSePlayer did not play the original cue.");
                }
                Debug.Log(
                    "OPENWDS_CRI_CLEAR_SE " +
                    $"bank={new FileInfo(clearSe).Length} " +
                    $"cue={clearSeRuntime.LastCueName} " +
                    $"playback={clearSeRuntime.LastPlayback.id} " +
                    $"status={clearSeRuntime.LastPlayback.GetStatus()}");

                resultSeRuntime = owner.AddComponent<
                    OpenWDS.Runtime.RecoveredGameResultSeRuntime>();
                resultSeRuntime.Configure(sharedSeRuntime);
                var beforeResultCount = sharedSeRuntime.CueNamePlayCount;
                resultSeRuntime.Begin(true);
                resultSeRuntime.Tick(float.MaxValue);
                if (resultSeRuntime.IsCounting ||
                    resultSeRuntime.PresentationCount != 1 ||
                    resultSeRuntime.CompletionCueCount != 1 ||
                    sharedSeRuntime.CueNamePlayCount != beforeResultCount + 2 ||
                    sharedSeRuntime.LastCueName != "BADGE_NEW_GOT")
                {
                    throw new InvalidOperationException(
                        "Result COUNT_UP_2/BADGE_NEW_GOT sequence is invalid.");
                }
                Debug.Log(
                    "OPENWDS_CRI_RESULT_SE countCue=COUNT_UP_2 " +
                    "completionCue=BADGE_NEW_GOT sequence=valid");
            }
            finally
            {
                if (effectSeOwner != null)
                    UnityEngine.Object.DestroyImmediate(effectSeOwner);
                if (sharedSeRuntime != null)
                    UnityEngine.Object.DestroyImmediate(sharedSeRuntime);
                if (resultSeRuntime != null)
                    UnityEngine.Object.DestroyImmediate(resultSeRuntime);
                if (clearSeRuntime != null)
                    UnityEngine.Object.DestroyImmediate(clearSeRuntime);
                if (gameSe != null) UnityEngine.Object.DestroyImmediate(gameSe);
                criticalPlayer?.Dispose();
                player?.Dispose();
                common?.Dispose();
                custom?.Dispose();
                loaded?.Dispose();
                initializer.Shutdown();
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static void ValidateClockSemantics(CriAtomExPlayback playback)
        {
            var samples = new long[4];
            for (var index = 0; index < samples.Length; index++)
            {
                Thread.Sleep(40);
                samples[index] = playback.GetTimeSyncedWithAudio();
                if (samples[index] < 0 ||
                    (index > 0 && samples[index] < samples[index - 1]))
                    throw new InvalidOperationException(
                        "CRI synchronized clock is not monotonic: " +
                        string.Join(",", samples));
            }

            playback.Pause();
            Thread.Sleep(80);
            var pausedStart = playback.GetTimeSyncedWithAudio();
            Thread.Sleep(80);
            var pausedEnd = playback.GetTimeSyncedWithAudio();
            if (pausedStart < 0 || pausedEnd < pausedStart ||
                pausedEnd - pausedStart > 20)
                throw new InvalidOperationException(
                    $"CRI pause did not freeze time: {pausedStart}->{pausedEnd}");
            playback.Resume(CriAtomEx.ResumeMode.PausedPlayback);
            Thread.Sleep(80);
            var resumed = playback.GetTimeSyncedWithAudio();
            if (resumed <= pausedEnd)
                throw new InvalidOperationException(
                    $"CRI resume did not advance time: {pausedEnd}->{resumed}");

            var sourceReads = 0;
            var cache = new OpenWDS.Runtime.RecoveredFrameCachedAudioClock();
            var first = cache.Read(100, () => { sourceReads++; return 1234; });
            var sameFrame = cache.Read(100, () => { sourceReads++; return 9999; });
            var nextFrame = cache.Read(101, () => { sourceReads++; return 1275; });
            var invalid = cache.Read(102, () => { sourceReads++; return -1; });
            if (first != 1234 || sameFrame != first || nextFrame != 1275 ||
                invalid != nextFrame || sourceReads != 3 ||
                cache.SourceReadCount != 3)
                throw new InvalidOperationException(
                    "Recovered same-frame clock cache semantics are invalid.");

            Debug.Log(
                "OPENWDS_CRI_CLOCK monotonic=" + string.Join(",", samples) +
                $" paused={pausedStart}->{pausedEnd} resumed={resumed} " +
                $"sameFrame={first}/{sameFrame} sourceReads={sourceReads} valid=True");
        }

        private static string DescribeCategories(ushort[] indices)
        {
            if (indices == null) return string.Empty;
            var items = new System.Collections.Generic.List<string>();
            foreach (var index in indices)
            {
                if (index == ushort.MaxValue) continue;
                if (CriAtomExAcf.GetCategoryInfoByIndex(index, out var category))
                    items.Add(category.name + ":" + category.numCueLimits);
            }
            return string.Join(",", items);
        }
    }
}
