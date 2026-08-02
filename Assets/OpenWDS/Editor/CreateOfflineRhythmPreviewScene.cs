using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenWDS.Editor
{
    public static class CreateOfflineRhythmPreviewScene
    {
        private const string ScenePath = "Assets/OpenWDS/Scenes/OfflineRhythmPreview.unity";
        private const string LaneGroupPath =
            "Assets/Resources/Prefabs/Features/Game/LaneGroup.prefab";
        private const string LaneEffectPath =
            "Assets/Resources/Prefabs/Features/Game/LaneEffect.prefab";
        private const string BeamEffectPath =
            "Assets/Resources/Prefabs/BeamEffect.prefab";
        private static readonly string[] DefaultBombEffectPaths =
        {
            "Assets/Resources/Prefabs/Features/Game/Bomb/Default/NormalBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Default/CriticalBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Default/HoldEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Default/HoldBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Default/ScratchBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Default/SoundBombEffect.prefab",
        };
        private static readonly string[] NotesBombEffectPaths =
        {
            "Assets/Resources/Prefabs/Features/Game/Bomb/Notes/NormalBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Notes/CriticalBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Notes/HoldEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Notes/HoldBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Notes/ScratchBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Notes/SoundBombEffect.prefab",
        };
        private static readonly string[] SakuraBombEffectPaths =
        {
            "Assets/Resources/Prefabs/Features/Game/Bomb/Sakura/NormalBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Sakura/CriticalBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Sakura/HoldEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Sakura/HoldBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Sakura/ScratchBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Sakura/SoundBombEffect.prefab",
        };
        private const int PreviewWidth = 1920;
        private const int PreviewHeight = 1200;
        private const string HardChartSourcePath =
            "Assets/StreamingAssets/OpenWDS/StandardCharts/1/1/2.csv";
        private const string StellaChartSourcePath =
            "Assets/StreamingAssets/OpenWDS/StandardCharts/1/1/4.csv";
        private const string ScratchHoldMaterialPath =
            "Assets/Resources/Material/Game/ScratchLongNotesSprite.mat";
        private const string MusicConfigSourcePath =
            "Assets/StreamingAssets/OpenWDS/StandardCharts/1/1/music_config.csv";
        private const string HardChartTextAssetPath =
            "Assets/OpenWDS/OfflineData/Music1Hard.asset";
        private const string StellaChartTextAssetPath =
            "Assets/OpenWDS/OfflineData/Music1Stella.asset";
        private const string MusicConfigTextAssetPath =
            "Assets/OpenWDS/OfflineData/Music1Config.asset";
        private const string MusicJacketPath =
            "Assets/OpenWDS/OfflineData/Music1Jacket.png";
        private const string TestPlayerUnitPath =
            "Assets/StreamingAssets/OpenWDS/TestPlayer/stella-principal-gauge-unit.json";
        private const string TestPlayerUnitTextAssetPath =
            "Assets/OpenWDS/OfflineData/StellaPrincipalGaugeUnit.asset";
        private const string HighStarPlayerUnitPath =
            "Assets/StreamingAssets/OpenWDS/TestPlayer/stella-high-star-storage-unit.json";
        private const string HighStarPlayerUnitTextAssetPath =
            "Assets/OpenWDS/OfflineData/StellaHighStarStorageUnit.asset";
        private const string StarterPlayerUnitPath =
            "Assets/StreamingAssets/OpenWDS/TestPlayer/stella-sirius-starter-unit.json";
        private const string StarterPlayerUnitTextAssetPath =
            "Assets/OpenWDS/OfflineData/StellaSiriusStarterUnit.asset";
        private const string ResultCurtainRoot =
            "Assets/Resources/Spine";
        private const string TimingEffectPath =
            "Assets/Resources/Prefabs/Features/Game/TimingEffect.prefab";
        private const string TimingAssistEffectPath =
            "Assets/Resources/Prefabs/Features/Game/TimingAssistEffect.prefab";
        private const string ComboPath =
            "Assets/Resources/Prefabs/Features/Game/Combo.prefab";
        private const string LifePath =
            "Assets/Resources/Prefabs/Features/Game/Life.prefab";
        private const string PrincipalPath =
            "Assets/Resources/Prefabs/Features/Game/PrincipalGauge.prefab";
        private const string ScorePath =
            "Assets/Resources/Prefabs/Features/Game/Score__4ae4a764.prefab";
        private const string AchievementRatePath =
            "Assets/Resources/Prefabs/Features/Game/AchievementRate__58302b55.prefab";
        private const string SenseLightPath =
            "Assets/Resources/Prefabs/Features/Game/SenseLightPanel__9da9760c.prefab";
        private const string AdditionalScoreCutInPath =
            "Assets/Resources/Prefabs/Features/Game/AdditionalScoreCutInPanel.prefab";
        private const string ResumeCountDownPath =
            "Assets/Resources/Prefabs/Features/Game/ResumeCountDown__56595f7e.prefab";
        private const string GameIntroductionPath =
            "Assets/Resources/Prefabs/Features/Game/GameIntroduction.prefab";
        private const string ClearAnimationPath =
            "Assets/Resources/Prefabs/Features/Game/ClearAnimation.prefab";

        private static readonly string[] SenseLightSpritePaths =
        {
            "Assets/Resources/Sprite/img_illust_light_all.asset",
            "Assets/Resources/Sprite/img_illust_light_support.asset",
            "Assets/Resources/Sprite/img_illust_light_domination.asset",
            "Assets/Resources/Sprite/img_illust_light_amplification.asset",
            "Assets/Resources/Sprite/img_illust_light_special.asset",
        };

        private static readonly string[] NotePrefabPaths =
        {
            "Assets/Resources/Prefabs/Features/Game/Notes/Note.prefab",
            "Assets/Resources/Prefabs/Features/Game/Notes/HoldNote.prefab",
            "Assets/Resources/Prefabs/Features/Game/Notes/FlickNote.prefab",
            "Assets/Resources/Prefabs/Features/Game/Notes/ScratchNote.prefab",
            "Assets/Resources/Prefabs/Features/Game/Notes/ScratchHoldNote.prefab",
            "Assets/Resources/Prefabs/Features/Game/Notes/ConcurrentLineNote.prefab",
            "Assets/Resources/Prefabs/Features/Game/Notes/SoundNote.prefab",
            "Assets/Resources/Prefabs/Features/Game/Notes/SoundPurpleNote.prefab",
        };

        private static readonly string[] TimingSpritePaths =
        {
            null,
            "Assets/Resources/Sprite/txt_game_common_judgment_miss.asset",
            "Assets/Resources/Sprite/txt_game_common_judgment_bad.asset",
            "Assets/Resources/Sprite/txt_game_common_judgment_good.asset",
            "Assets/Resources/Sprite/txt_game_common_judgment_great.asset",
            "Assets/Resources/Sprite/txt_game_common_judgment_perfect.asset",
            "Assets/Resources/Sprite/txt_game_common_judgment_perfect_star.asset",
        };

        private static readonly string[] TimingAssistSpritePaths =
        {
            null,
            "Assets/Resources/Sprite/txt_game_common_judgment_slow.asset",
            "Assets/Resources/Sprite/txt_game_common_judgment_fast.asset",
        };

        private static readonly string[] SenseCutInSpritePaths =
        {
            "Assets/Resources/Sprite/img_game_common_cutin_support_left.asset",
            "Assets/Resources/Sprite/img_game_common_cutin_support_right.asset",
            "Assets/Resources/Sprite/txt_game_common_cutin_support.asset",
            "Assets/Resources/Sprite/img_game_common_cutin_domination_left.asset",
            "Assets/Resources/Sprite/img_game_common_cutin_domination_right.asset",
            "Assets/Resources/Sprite/txt_game_common_cutin_domination.asset",
            "Assets/Resources/Sprite/img_game_common_cutin_amplification_left.asset",
            "Assets/Resources/Sprite/img_game_common_cutin_amplification_right.asset",
            "Assets/Resources/Sprite/txt_game_common_cutin_amplification.asset",
            "Assets/Resources/Sprite/img_game_common_cutin_special_left.asset",
            "Assets/Resources/Sprite/img_game_common_cutin_special_right.asset",
            "Assets/Resources/Sprite/txt_game_common_cutin_special.asset",
        };

        private static readonly string[] ComboLabelPaths =
        {
            "Assets/Resources/Sprite/txt_game_txt_combo.asset",
            "Assets/Resources/Sprite/txt_game_txt_combo_fc.asset",
            "Assets/Resources/Sprite/txt_game_txt_combo_ap.asset",
        };

        private static readonly string[] RatePointPaths =
        {
            "Assets/Resources/Sprite/txt_game_txt_rate_point.asset",
            "Assets/Resources/Sprite/txt_game_txt_rate_fc_point.asset",
            "Assets/Resources/Sprite/txt_game_txt_rate_ap_point.asset",
            "Assets/Resources/Sprite/txt_game_txt_rate_nc_point.asset",
        };

        private static readonly string[] RatePercentPaths =
        {
            "Assets/Resources/Sprite/txt_game_txt_rate_percent.asset",
            "Assets/Resources/Sprite/txt_game_txt_rate_fc_percent.asset",
            "Assets/Resources/Sprite/txt_game_txt_rate_ap_percent.asset",
            "Assets/Resources/Sprite/txt_game_txt_rate_nc_percent.asset",
        };

        [Serializable]
        private sealed class PreviewReport
        {
            public string unityVersion;
            public string scenePath;
            public int configuredNotes;
            public int activeNotesAtSampleTime;
            public bool hasCamera;
            public bool hasLaneGroup;
            public bool hasNoteParent;
            public int sceneDependencies;
            public int unresolvedPlaceholderReferences;
            public bool recoveredPositionFormulaValid;
            public bool recoveredLaneFormulaValid;
            public bool recoveredSettingsDefaultsValid;
            public bool recoveredLaneSettingsMathValid;
            public bool recoveredSettingsUiBindingValid;
            public bool recoveredSettingsPersistenceDescriptorValid;
            public bool recoveredSettingsCryptoValid;
            public bool recoveredSettingsSessionValid;
            public bool recoveredSettingsStoreRoundTripValid;
            public bool recoveredSettingsPrefabsValid;
            public bool recoveredMusicSelectionFlowValid;
            public bool recoveredPauseRetireRouteValid;
            public bool recoveredStandardNotationValid;
            public bool recoveredSplitLaneSchedulerValid;
            public bool recoveredSplitLaneBundlesValid;
            public int recoveredSplitLaneCount;
            public int recoveredSplitLaneEventCount;
            public bool recoveredGameClockValid;
            public bool recoveredTapTimingValid;
            public bool recoveredTapActionValid;
            public bool recoveredHoldTimingValid;
            public bool recoveredInputOrderingValid;
            public bool recoveredHitLaneEntityValid;
            public bool recoveredLaneColliderMappingsValid;
            public bool recoveredInputLifecycleValid;
            public bool recoveredInputFireCoreValid;
            public bool recoveredAutoTouchValid;
            public bool recoveredLaneHoldLifecycleValid;
            public bool recoveredStandardHoldActionValid;
            public bool recoveredHoldFireIntegrationValid;
            public bool recoveredFlickActionValid;
            public bool recoveredScratchActionValid;
            public bool recoveredGameRuntimeValid;
            public bool recoveredPlayerInputEntryValid;
            public bool recoveredAutoTouchModeSeparationValid;
            public bool recoveredDefaultTouchRuntimeValid;
            public bool recoveredEnhancedTouchInjectionValid;
            public bool recoveredInjectedPlayerActionsValid;
            public bool recoveredPauseTouchLifecycleValid;
            public bool recoveredTouchIdReuseValid;
            public bool recoveredAllChartNoInputMissCoverageValid;
            public bool recoveredGameHudValid;
            public bool recoveredPlayerUnitValid;
            public bool recoveredHighStarPlayerUnitValid;
            public bool recoveredStarterPlayerUnitValid;
            public int recoveredPlayerUnitCardCount;
            public int recoveredPlayerUnitTotalStatus;
            public int recoveredPlayerUnitScoreNoteCount;
            public long recoveredPlayerUnitMaxScore;
            public bool recoveredGameResultRuntimeValid;
            public bool recoveredLocalResultStoreValid;
            public bool recoveredGameBackgroundValid;
            public bool recoveredGameResultPrefabValid;
            public bool recoveredGameResultBackgroundValid;
            public bool recoveredMusicJacketValid;
            public bool recoveredBoundaryPerformancesValid;
            public bool recoveredGameResultFontBundlesValid;
            public int recoveredGameResultObjectCount;
            public int recoveredGameResultUiParticleCount;
            public bool recoveredStellaHoldSoundValid;
            public bool recoveredBeamEffectValid;
            public bool recoveredBombEffectValid;
            public bool recoveredLaneEffectLifecycleValid;
            public bool recoveredEffectParentValid;
            public bool recoveredNoteVisualLifecycleValid;
            public int recoveredVisualNoteCount;
            public int recoveredVisualPeakActiveCount;
            public int recoveredVisualRecycledCount;
            public int recoveredRuntimeConsumedTapCount;
            public int recoveredRuntimeConsumedFlickCount;
            public int recoveredRuntimeConsumedHoldCount;
            public int recoveredRuntimeRemainingTapCount;
            public int recoveredRuntimeRemainingFlickCount;
            public int recoveredRuntimeRemainingHoldCount;
            public int recoveredRuntimeRemainingScratchCount;
            public int recoveredRuntimeRemainingHoldingCount;
            public int recoveredRuntimeActiveTouchHoldCount;
            public int recoveredAutoTouchScheduledEvents;
            public string recoveredSettingsCryptoCipherText;
            public int renderWidth;
            public int renderHeight;
            public bool landscapeRender;
        }
        
        
        [MenuItem("Tools/OfflineSceneCheckRun")]
        public static void Run()
        {
            var musicJacket = PrepareMusicJacket();
            var testPlayerUnitAsset = SyncTextAsset(
                TestPlayerUnitPath, TestPlayerUnitTextAssetPath);
            var highStarPlayerUnitAsset = SyncTextAsset(
                HighStarPlayerUnitPath, HighStarPlayerUnitTextAssetPath);
            var starterPlayerUnitAsset = SyncTextAsset(
                StarterPlayerUnitPath, StarterPlayerUnitTextAssetPath);
            PrepareGameResultCurtainSpine();
            BuildGameBackgroundPrefab();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single
            );
            var camera = CreateCamera();
            // GameLifetimeScope._timingEffectParent is not LaneGroup's
            // judgement EffectParent.  The original level1 component stores
            // PPtr path 156: Game/Effects, with this exact local TRS.
            var gameRoot = new GameObject("Game");
            var timingEffectParentObject = new GameObject("Effects");
            timingEffectParentObject.transform.SetParent(
                gameRoot.transform, false);
            timingEffectParentObject.transform.localPosition =
                new Vector3(0f, -0.46f, 10f);
            var timingEffectParent = timingEffectParentObject.transform;
            var laneGroup = InstantiatePrefab(
                LaneGroupPath, gameRoot.transform);
            laneGroup.name = "LaneGroup";
            laneGroup.transform.localPosition = new Vector3(0f, 0f, 12f);
            laneGroup.transform.localRotation =
                Quaternion.Euler(60f, 0f, 0f);
            laneGroup.transform.localScale = Vector3.one;
            var noteParent = FindChild(laneGroup.transform, "Movable");
            if (noteParent == null)
            {
                throw new InvalidOperationException("LaneGroup/NotePlate/Movable was not found.");
            }

            var runtimeRoot = new GameObject("RecoveredGameRuntime");
            var criRoot = new GameObject("CRIWARE");
            var criInitializer = criRoot.AddComponent<CriWare.CriWareInitializer>();
            criInitializer.initializesMana = false;
            criInitializer.atomConfig.acfFileName = "OpenWDS/CRI/Sirius.acf";
            var laneGroupController = laneGroup.GetComponent<Sirius.Game.LaneGroup>();
            var effectParent = laneGroupController.LaneEffectParent;
            if (effectParent == null)
                throw new InvalidOperationException(
                    "LaneGroup/JudgeArea/EffectParent was not found.");
            var laneEffect = InstantiatePrefab(LaneEffectPath, effectParent);
            laneEffect.name = "LaneEffect";
            laneEffect.transform.localPosition = Vector3.zero;
            var laneEffectController = laneEffect.GetComponent<Sirius.Game.LaneEffectController>();
            if (laneEffectController == null || laneEffectController.SplitEffectParent == null)
                throw new InvalidOperationException(
                    "LaneEffect/SplitEffectParent was not restored from the original prefab.");
            var musicConfigAsset = SyncTextAsset(
                MusicConfigSourcePath, MusicConfigTextAssetPath);
            var criMusic = runtimeRoot.AddComponent<RecoveredCriMusicRuntime>();
            criMusic.Configure(musicConfigAsset, false);
            var gameSe = runtimeRoot.AddComponent<RecoveredGameSeRuntime>();
            var customSettings = RecoveredGameSettings.Custom.Default();
            var detailSettings = RecoveredGameSettings.Detail.Default();
            // Scene construction validation exercises the broadest original
            // assist branch. RecoveredGameRuntime replaces this with persisted
            // settings before HUD initialization in actual Play Mode.
            detailSettings.TimingAssistSettingType = 2;
            var gameHud = runtimeRoot.AddComponent<RecoveredGameHudRuntime>();
            gameHud.Configure(
                camera,
                timingEffectParent,
                AssetDatabase.LoadAssetAtPath<GameObject>(TimingEffectPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(TimingAssistEffectPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(ComboPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(LifePath),
                AssetDatabase.LoadAssetAtPath<GameObject>(PrincipalPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(ScorePath),
                AssetDatabase.LoadAssetAtPath<GameObject>(AchievementRatePath),
                AssetDatabase.LoadAssetAtPath<GameObject>(SenseLightPath),
                LoadSprites(SenseLightSpritePaths),
                AssetDatabase.LoadAssetAtPath<GameObject>(AdditionalScoreCutInPath),
                LoadSprites(SenseCutInSpritePaths),
                LoadSprites(TimingSpritePaths),
                LoadSprites(TimingAssistSpritePaths),
                LoadComboDigits("txt_game_txt_combo_"),
                LoadComboDigits("txt_game_txt_combo_fc_"),
                LoadComboDigits("txt_game_txt_combo_ap_"),
                LoadSprites(ComboLabelPaths),
                LoadComboDigits("txt_game_txt_combo_nc_"),
                LoadSprites(RatePointPaths),
                LoadSprites(RatePercentPaths),
                detailSettings.AchievementRateSettingType,
                detailSettings.TimingAssistSettingType,
                detailSettings.IsActiveComboEffect,
                detailSettings.IsPerfectContinuous,
                detailSettings.TimingEffectOffset,
                detailSettings.TimingEffectScaleType);
            var gameRuntime = runtimeRoot.AddComponent<RecoveredGameRuntime>();
            gameRuntime.Configure(
                camera,
                laneGroupController,
                SyncTextAsset(StellaChartSourcePath, StellaChartTextAssetPath),
                musicConfigAsset,
                LoadRuntimeNotePrefabs(),
                AssetDatabase.LoadAssetAtPath<Material>(ScratchHoldMaterialPath),
                effectParent,
                laneEffectController.SplitEffectParent,
                AssetDatabase.LoadAssetAtPath<GameObject>(BeamEffectPath),
                LoadPrefabs(DefaultBombEffectPaths),
                LoadPrefabs(NotesBombEffectPaths),
                LoadPrefabs(SakuraBombEffectPaths),
                (RecoveredBombType)customSettings.BombType,
                criMusic,
                gameSe,
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/GameBackground.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/OrdinarySoloGameResult.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/GameResultCurtainBackground.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(GameIntroductionPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(ClearAnimationPath),
                "ワナビスタ！",
                "作詞：松井洋平　作曲：光増ハジメ（FirstCall）　編曲：EFFY（FirstCall）",
                RecoveredMusicDifficulty.Stella,
                musicJacket,
                testPlayerUnitAsset,
                isUnlockOlivier: false,
                localMusicCatalogAsset: AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/OpenWDS/OfflineData/LocalMusicCatalog.json"));
            var pauseRuntime = runtimeRoot.AddComponent<RecoveredGamePauseRuntime>();
            pauseRuntime.Configure(
                gameRuntime,
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/Features/Game/Pause__b53abd1e.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/Common/Dialog/Dialog.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/DialogBody/Game/PauseGameDialogBody__1ff194e7.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/Common/Dialog/Body/OptionDialogBody.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/Common/Dialog/Body/OptionInformationDialogBody.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/Common/Panels/SideMenuPanel/TextSideMenuButton.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(ResumeCountDownPath),
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Resources/Sprite/btn_common_btn_large.asset"),
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Resources/Sprite/btn_common_btn_large_red.asset"),
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Resources/Sprite/btn_common_btn_switch_filter_on.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Resources/Sprite/btn_common_btn_switch_filter_off.png"),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/Features/Game/GameSimulationCamera.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/Features/Game/LaneGroup.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/Features/GameSimulation/PreviewUI.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/Features/Game/Notes/Note.prefab"),
                AssetDatabase.LoadAssetAtPath<RenderTexture>(
                    "Assets/Resources/RenderTexture/GameRenderTexture.renderTexture"));
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Failed to save offline preview scene.");
            }
            NormalizeGeneratedYaml(ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            var splitLaneBundlesValid = ValidateSplitLaneBundlesInEditor();
            var gameResultFontBundlesValid = CaptureGameResultPreview();

            var sceneText = File.ReadAllText(Path.GetFullPath(ScenePath));
            var visualLifecycleValid = ValidateNoteVisualLifecycle(
                out var visualNoteCount,
                out var visualPeakActiveCount,
                out var visualRecycledCount);
            var report = new PreviewReport
            {
                unityVersion = Application.unityVersion,
                scenePath = ScenePath,
                configuredNotes = visualNoteCount,
                activeNotesAtSampleTime = visualPeakActiveCount,
                hasCamera = camera != null,
                hasLaneGroup = laneGroup != null,
                hasNoteParent = noteParent != null,
                sceneDependencies = AssetDatabase.GetDependencies(ScenePath, true).Length,
                unresolvedPlaceholderReferences = CountOccurrences(sceneText, "deadbeef"),
                recoveredPositionFormulaValid = ValidatePositionFormula(),
                recoveredLaneFormulaValid = ValidateLaneFormula(),
                recoveredSettingsDefaultsValid = ValidateSettingsDefaults(),
                recoveredLaneSettingsMathValid = ValidateLaneSettingsMath(),
                recoveredSettingsUiBindingValid = ValidateSettingsUiBinding(),
                recoveredSettingsPersistenceDescriptorValid =
                    ValidateSettingsPersistenceDescriptor(),
                recoveredSettingsCryptoValid = ValidateSettingsCrypto(),
                recoveredSettingsSessionValid = ValidateSettingsSession(),
                recoveredSettingsStoreRoundTripValid = ValidateSettingsStoreRoundTrip(),
                recoveredSettingsPrefabsValid = ValidateSettingsPrefabs(),
                recoveredMusicSelectionFlowValid = ValidateMusicSelectionFlow(),
                recoveredPauseRetireRouteValid = ValidatePauseRetireRoute(),
                recoveredStandardNotationValid = ValidateStandardNotation(),
                recoveredSplitLaneSchedulerValid = ValidateSplitLaneScheduler(
                    out var splitLaneCount,
                    out var splitLaneEventCount),
                recoveredSplitLaneCount = splitLaneCount,
                recoveredSplitLaneEventCount = splitLaneEventCount,
                recoveredSplitLaneBundlesValid = splitLaneBundlesValid,
                recoveredGameClockValid = ValidateGameClock(),
                recoveredTapTimingValid = ValidateTapTiming(),
                recoveredTapActionValid = ValidateTapAction(),
                recoveredHoldTimingValid = ValidateHoldTiming(),
                recoveredInputOrderingValid = ValidateInputOrdering(),
                recoveredHitLaneEntityValid = ValidateHitLaneEntity(),
                recoveredLaneColliderMappingsValid =
                    ValidateLaneColliderMappings(camera, laneGroup),
                recoveredInputLifecycleValid = ValidateInputLifecycle(),
                recoveredInputFireCoreValid = ValidateInputFireCore(),
                recoveredAutoTouchValid = ValidateAutoTouch(
                    camera,
                    laneGroup,
                    out var autoTouchScheduledEvents,
                    out var laneHoldLifecycleValid),
                recoveredLaneHoldLifecycleValid = laneHoldLifecycleValid,
                recoveredStandardHoldActionValid = ValidateStandardHoldAction(),
                recoveredHoldFireIntegrationValid = ValidateHoldFireIntegration(),
                recoveredFlickActionValid = ValidateFlickAction(),
                recoveredScratchActionValid = ValidateScratchAction(),
                recoveredGameRuntimeValid = ValidateGameRuntime(
                    out var runtimeTapCount,
                    out var runtimeFlickCount,
                    out var runtimeHoldCount,
                    out var runtimeRemainingCounts),
                recoveredPlayerInputEntryValid = ValidatePlayerInputEntry(),
                recoveredAutoTouchModeSeparationValid =
                    ValidateAutoTouchModeSeparation(camera, laneGroup),
                recoveredDefaultTouchRuntimeValid =
                    ValidateDefaultTouchRuntime(),
                recoveredEnhancedTouchInjectionValid =
                    ValidateEnhancedTouchInjection(
                        camera,
                        laneGroup,
                        out var injectedPlayerActionsValid,
                        out var pauseTouchLifecycleValid,
                        out var touchIdReuseValid),
                recoveredInjectedPlayerActionsValid =
                    injectedPlayerActionsValid,
                recoveredPauseTouchLifecycleValid =
                    pauseTouchLifecycleValid,
                recoveredTouchIdReuseValid = touchIdReuseValid,
                recoveredAllChartNoInputMissCoverageValid =
                    ValidateAllChartNoInputMissCoverage(),
                recoveredGameHudValid = ValidateGameHud(gameHud, sceneText) &&
                                        ValidatePrincipalRuntime(),
                recoveredPlayerUnitValid = ValidatePlayerUnit(
                    testPlayerUnitAsset,
                    out var playerUnitCardCount,
                    out var playerUnitTotalStatus,
                    out var playerUnitScoreNoteCount,
                    out var playerUnitMaxScore),
                recoveredHighStarPlayerUnitValid = ValidatePlayerUnit(
                    highStarPlayerUnitAsset,
                    out _,
                    out _,
                    out _,
                    out _),
                recoveredStarterPlayerUnitValid = ValidatePlayerUnit(
                    starterPlayerUnitAsset,
                    out _,
                    out _,
                    out _,
                    out _),
                recoveredPlayerUnitCardCount = playerUnitCardCount,
                recoveredPlayerUnitTotalStatus = playerUnitTotalStatus,
                recoveredPlayerUnitScoreNoteCount = playerUnitScoreNoteCount,
                recoveredPlayerUnitMaxScore = playerUnitMaxScore,
                recoveredGameResultRuntimeValid = ValidateGameResultRuntime(),
                recoveredLocalResultStoreValid = ValidateLocalResultStore(),
                recoveredGameBackgroundValid = ValidateGameBackground(),
                recoveredGameResultPrefabValid = ValidateGameResultPrefab(
                    out var gameResultObjectCount,
                    out var gameResultUiParticleCount),
                recoveredGameResultBackgroundValid = ValidateGameResultBackground(),
                recoveredMusicJacketValid = ValidateMusicJacket(musicJacket),
                recoveredBoundaryPerformancesValid =
                    ValidateBoundaryPerformances(),
                recoveredGameResultFontBundlesValid = gameResultFontBundlesValid,
                recoveredGameResultObjectCount = gameResultObjectCount,
                recoveredGameResultUiParticleCount = gameResultUiParticleCount,
                recoveredStellaHoldSoundValid = ValidateStellaHoldSoundPipeline(
                    camera, laneGroupController),
                recoveredBeamEffectValid = ValidateBeamEffect(),
                recoveredBombEffectValid = ValidateBombEffects(),
                recoveredLaneEffectLifecycleValid = ValidateLaneEffectLifecycle(
                    laneGroupController, effectParent),
                recoveredEffectParentValid =
                    effectParent == laneGroupController.LaneEffectParent &&
                    effectParent.name == "EffectParent" &&
                    laneEffect.transform.parent == effectParent,
                recoveredNoteVisualLifecycleValid = visualLifecycleValid,
                recoveredVisualNoteCount = visualNoteCount,
                recoveredVisualPeakActiveCount = visualPeakActiveCount,
                recoveredVisualRecycledCount = visualRecycledCount,
                recoveredRuntimeConsumedTapCount = runtimeTapCount,
                recoveredRuntimeConsumedFlickCount = runtimeFlickCount,
                recoveredRuntimeConsumedHoldCount = runtimeHoldCount,
                recoveredRuntimeRemainingTapCount = runtimeRemainingCounts[0],
                recoveredRuntimeRemainingFlickCount = runtimeRemainingCounts[1],
                recoveredRuntimeRemainingHoldCount = runtimeRemainingCounts[2],
                recoveredRuntimeRemainingScratchCount = runtimeRemainingCounts[3],
                recoveredRuntimeRemainingHoldingCount = runtimeRemainingCounts[4],
                recoveredRuntimeActiveTouchHoldCount = runtimeRemainingCounts[5],
                recoveredAutoTouchScheduledEvents = autoTouchScheduledEvents,
                recoveredSettingsCryptoCipherText =
                    RecoveredSettingsCrypto.EncryptUtf8("{\"x\":1}"),
                renderWidth = PreviewWidth,
                renderHeight = PreviewHeight,
                landscapeRender = PreviewWidth > PreviewHeight,
            };
            RenderPreviewWithGameBackground(camera, laneGroup);
            WriteReport(report);
            Debug.Log($"OPENWDS_OFFLINE_PREVIEW scene={ScenePath} " +
                      $"notes={report.configuredNotes} active={report.activeNotesAtSampleTime} " +
                      $"dependencies={report.sceneDependencies} " +
                      $"unresolvedRefs={report.unresolvedPlaceholderReferences}");

            if (!report.hasCamera || !report.hasLaneGroup || !report.hasNoteParent ||
                report.configuredNotes <= 0 ||
                report.activeNotesAtSampleTime == 0 ||
                report.unresolvedPlaceholderReferences != 0 ||
                !report.recoveredPositionFormulaValid ||
                !report.recoveredLaneFormulaValid ||
                !report.recoveredSettingsDefaultsValid ||
                !report.recoveredLaneSettingsMathValid ||
                !report.recoveredSettingsUiBindingValid ||
                !report.recoveredSettingsPersistenceDescriptorValid ||
                !report.recoveredSettingsCryptoValid ||
                !report.recoveredSettingsSessionValid ||
                !report.recoveredSettingsStoreRoundTripValid ||
                !report.recoveredSettingsPrefabsValid ||
                !report.recoveredMusicSelectionFlowValid ||
                !report.recoveredPauseRetireRouteValid ||
                !report.recoveredStandardNotationValid ||
                !report.recoveredSplitLaneSchedulerValid ||
                !report.recoveredSplitLaneBundlesValid ||
                !report.recoveredGameClockValid ||
                !report.recoveredTapTimingValid ||
                !report.recoveredTapActionValid ||
                !report.recoveredHoldTimingValid ||
                !report.recoveredInputOrderingValid ||
                !report.recoveredHitLaneEntityValid ||
                !report.recoveredLaneColliderMappingsValid ||
                !report.recoveredInputLifecycleValid ||
                !report.recoveredInputFireCoreValid ||
                !report.recoveredAutoTouchValid ||
                !report.recoveredLaneHoldLifecycleValid ||
                !report.recoveredStandardHoldActionValid ||
                !report.recoveredHoldFireIntegrationValid ||
                !report.recoveredFlickActionValid ||
                !report.recoveredScratchActionValid ||
                !report.recoveredGameRuntimeValid ||
                !report.recoveredPlayerInputEntryValid ||
                !report.recoveredAutoTouchModeSeparationValid ||
                !report.recoveredDefaultTouchRuntimeValid ||
                !report.recoveredEnhancedTouchInjectionValid ||
                !report.recoveredInjectedPlayerActionsValid ||
                !report.recoveredPauseTouchLifecycleValid ||
                !report.recoveredTouchIdReuseValid ||
                !report.recoveredAllChartNoInputMissCoverageValid ||
                !report.recoveredGameHudValid ||
                !report.recoveredPlayerUnitValid ||
                !report.recoveredHighStarPlayerUnitValid ||
                !report.recoveredStarterPlayerUnitValid ||
                !report.recoveredGameResultRuntimeValid ||
                !report.recoveredLocalResultStoreValid ||
                !report.recoveredGameBackgroundValid ||
                !report.recoveredGameResultPrefabValid ||
                !report.recoveredGameResultBackgroundValid ||
                !report.recoveredMusicJacketValid ||
                !report.recoveredBoundaryPerformancesValid ||
                !report.recoveredGameResultFontBundlesValid ||
                !report.recoveredStellaHoldSoundValid ||
                !report.recoveredBeamEffectValid ||
                !report.recoveredBombEffectValid ||
                !report.recoveredLaneEffectLifecycleValid ||
                !report.recoveredEffectParentValid ||
                !report.recoveredNoteVisualLifecycleValid ||
                !report.landscapeRender)
            {
                throw new InvalidOperationException("Offline rhythm preview validation failed.");
            }
            CreateLocalMusicSelectionScene.Run();
        }

        // Focused batch gate for Bomb/Hold recovery. The full scene gate also
        // captures SplitLane through Camera.Render, which can crash Unity's
        // native headless renderer before these independent checks execute.
        public static void RunBombHoldVisualValidation()
        {
            var laneGroupObject = InstantiatePrefab(LaneGroupPath, null);
            try
            {
                var laneGroup = laneGroupObject.GetComponent<Sirius.Game.LaneGroup>();
                if (laneGroup == null || laneGroup.LaneEffectParent == null)
                    throw new InvalidOperationException(
                        "LaneGroup effect references are required.");
                var bombValid = ValidateBombEffects();
                var lifecycleValid = ValidateLaneEffectLifecycle(
                    laneGroup, laneGroup.LaneEffectParent);
                Debug.Log("OPENWDS_BOMB_HOLD_VISUAL_VALIDATION bomb=" +
                          bombValid + " lifecycle=" + lifecycleValid);
                if (!bombValid || !lifecycleValid)
                    throw new InvalidOperationException(
                        "Bomb/Hold visual validation failed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(laneGroupObject);
            }
        }

        // Focused batch gate for the independent ConcurrentLine dictionary
        // lifecycle. Keep this separate from the full scene render gate so
        // display/completion regressions can be checked without Camera.Render.
        public static void RunConcurrentLineVisualValidation()
        {
            var root = new GameObject("ConcurrentLineVisualValidation");
            try
            {
                var valid = ValidateConcurrentLineCompletion(root.transform);
                Debug.Log("OPENWDS_CONCURRENT_LINE_VISUAL_VALIDATION valid=" + valid);
                if (!valid)
                    throw new InvalidOperationException(
                        "ConcurrentLine visual validation failed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static bool ValidateTapAction()
        {
            var tap = new RecoveredNotationNote
            {
                Id = 1,
                StartTickCount = 1f,
                EndTickCount = -1f,
                NoteType = (int)RecoveredNoteType.Normal,
                Lane = 3,
                Width = 2,
            };
            var clock = new RecoveredGameClock(0f, 0d);
            clock.Sync(1f, 1000, 2f, 2000);
            var manager = new RecoveredTapNoteManager(new[] { tap });
            var action = new RecoveredTapAction(clock, manager);
            var moved = new RecoveredInputEntity(
                1, 2000, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Moved);
            var began = new RecoveredInputEntity(
                1, 2000, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Began);
            if (action.TryTap(
                    moved,
                    new RecoveredHitLaneEntity(3, 0, 0, 0, 0),
                    tap).Consumed ||
                action.TryTap(
                    began,
                    new RecoveredHitLaneEntity(2, 0, 0, 0, 0),
                    tap).Consumed)
            {
                return false;
            }
            var result = action.TryTap(
                began,
                new RecoveredHitLaneEntity(4, 0, 0, 0, 0),
                tap);
            return result.Consumed && result.DeletedNote && result.LaneId == 4 &&
                   result.Timing.TimingType == RecoveredTimingType.PerfectStar &&
                   manager.Count == 0;
        }

        private static bool ValidateGameHud(
            RecoveredGameHudRuntime gameHud,
            string serializedScene)
        {
            var timingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TimingEffectPath);
            var timingAssistPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                TimingAssistEffectPath);
            var comboPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ComboPath);
            var scorePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScorePath);
            var achievementPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AchievementRatePath);
            var senseLightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                SenseLightPath);
            var cutInPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AdditionalScoreCutInPath);
            if (gameHud == null || timingPrefab == null ||
                timingAssistPrefab == null || comboPrefab == null ||
                scorePrefab == null || achievementPrefab == null ||
                senseLightPrefab == null || cutInPrefab == null ||
                timingPrefab.GetComponent<Sirius.Game.TimingEffect>() == null ||
                timingAssistPrefab.GetComponent<
                    Sirius.Game.TimingAssistEffect>() == null ||
                comboPrefab.GetComponent<Sirius.Game.UI.ComboPanel>() == null ||
                scorePrefab.GetComponent<Sirius.Game.UI.ScorePanel>() == null ||
                achievementPrefab.GetComponent<
                    Sirius.Game.UI.AchievementRatePanel>() == null ||
                cutInPrefab.GetComponent<
                    Sirius.Game.UI.AdditionalScoreCutInPanel>() == null)
                return false;
            var timingText = File.ReadAllText(Path.GetFullPath(TimingEffectPath));
            var timingAssistText = File.ReadAllText(Path.GetFullPath(
                TimingAssistEffectPath));
            var comboText = File.ReadAllText(Path.GetFullPath(ComboPath));
            var scoreText = File.ReadAllText(Path.GetFullPath(ScorePath));
            var achievementText = File.ReadAllText(Path.GetFullPath(
                AchievementRatePath));
            var senseLightText = File.ReadAllText(Path.GetFullPath(
                SenseLightPath));
            var cutInText = File.ReadAllText(Path.GetFullPath(
                AdditionalScoreCutInPath));
            if (timingText.Contains("deadbeef") ||
                timingAssistText.Contains("deadbeef") ||
                comboText.Contains("deadbeef") ||
                scoreText.Contains("deadbeef") ||
                achievementText.Contains("deadbeef") ||
                senseLightText.Contains("deadbeef") ||
                cutInText.Contains("deadbeef"))
                return false;
            const string timingParentPrefix = "  _timingParent: {fileID: ";
            var timingParentIndex = serializedScene.IndexOf(
                timingParentPrefix, StringComparison.Ordinal);
            if (timingParentIndex < 0 || serializedScene.IndexOf(
                    "  _timingParent: {fileID: 0}", StringComparison.Ordinal) >= 0 ||
                serializedScene.IndexOf(
                    "  _timingScale: 1", StringComparison.Ordinal) < 0 ||
                serializedScene.IndexOf(
                    "  _timingPositionY: ", StringComparison.Ordinal) < 0)
                return false;
            var timing = LoadSprites(TimingSpritePaths);
            var timingAssist = LoadSprites(TimingAssistSpritePaths);
            var labels = LoadSprites(ComboLabelPaths);
            var senseCutIn = LoadSprites(SenseCutInSpritePaths);
            for (var index = 1; index < timing.Length; index++)
                if (timing[index] == null) return false;
            for (var index = 1; index < timingAssist.Length; index++)
                if (timingAssist[index] == null) return false;
            foreach (var label in labels)
                if (label == null) return false;
            foreach (var sprite in senseCutIn)
                if (sprite == null) return false;
            var staticDataValid = LoadComboDigits("txt_game_txt_combo_").Length == 10 &&
                   LoadComboDigits("txt_game_txt_combo_fc_").Length == 10 &&
                   LoadComboDigits("txt_game_txt_combo_ap_").Length == 10 &&
                   LoadComboDigits("txt_game_txt_combo_nc_").Length == 10 &&
                   Mathf.Abs(RecoveredGameHudRuntime.CalculatePositionY(60) - 0.4f) <
                   0.00001f &&
                   Mathf.Abs(RecoveredGameHudRuntime.GetTimingEffectScale(0) - 0.8f) <
                   0.00001f &&
                   Mathf.Abs(RecoveredGameHudRuntime.GetTimingEffectScale(2) - 1.3f) <
                   0.00001f &&
                   RecoveredGameHudRuntime.ResolveDisplayedTiming(
                       RecoveredTimingType.PerfectStar, false) ==
                       RecoveredTimingType.Perfect &&
                   RecoveredGameHudRuntime.ResolveDisplayedTiming(
                       RecoveredTimingType.PerfectStar, true) ==
                       RecoveredTimingType.PerfectStar &&
                   !RecoveredGameHudRuntime.ShouldShowTimingAssist(
                       0, RecoveredTimingType.Great,
                       RecoveredTimingAssistType.Fast) &&
                   RecoveredGameHudRuntime.ShouldShowTimingAssist(
                       1, RecoveredTimingType.Great,
                       RecoveredTimingAssistType.Fast) &&
                   !RecoveredGameHudRuntime.ShouldShowTimingAssist(
                       1, RecoveredTimingType.Perfect,
                       RecoveredTimingAssistType.Slow) &&
                   RecoveredGameHudRuntime.ShouldShowTimingAssist(
                       2, RecoveredTimingType.Perfect,
                       RecoveredTimingAssistType.Slow) &&
                   !RecoveredGameHudRuntime.ShouldShowTimingAssist(
                       2, RecoveredTimingType.PerfectStar,
                       RecoveredTimingAssistType.Fast) &&
                   !RecoveredGameHudRuntime.ShouldShowTimingAssist(
                       2, RecoveredTimingType.Great,
                       RecoveredTimingAssistType.None) &&
                   RecoveredGameHudRuntime.ResolveComboEffectType(
                       true, Sirius.Game.UI.RecoveredComboType.AllPerfect) ==
                       Sirius.Game.UI.RecoveredComboType.AllPerfect &&
                   RecoveredGameHudRuntime.ResolveComboEffectType(
                       false, Sirius.Game.UI.RecoveredComboType.AllPerfect) ==
                       Sirius.Game.UI.RecoveredComboType.None &&
                   RecoveredGameHudRuntime.ResolveComboEffectType(
                       false, Sirius.Game.UI.RecoveredComboType.FullCombo) ==
                       Sirius.Game.UI.RecoveredComboType.None &&
                   Sirius.Game.UI.AchievementRatePanel.FormatRate(0d) ==
                       "0.0000" &&
                   Sirius.Game.UI.AchievementRatePanel.FormatRate(9.5d) ==
                       "9.5000" &&
                   Sirius.Game.UI.AchievementRatePanel.FormatRate(100.5d) ==
                       "100.5000";
            if (!staticDataValid) return false;

            var note = new RecoveredNotationNote
            {
                Id = 1, StartTickCount = 1f,
                NoteType = (int)RecoveredNoteType.Normal, Lane = 6, Width = 1,
            };
            var decision = new RecoveredTimingDecision(
                RecoveredTimingType.PerfectStar,
                RecoveredTimingAssistType.None, 0);
            var inputResult = RecoveredInputResultEntity.Create(note, decision);
            var combo = new RecoveredGameResultRuntime(new[] { note });
            combo.Collect(inputResult);
            gameHud.Initialize();
            var scoreInitiallyHidden = gameHud.ScorePanel != null &&
                                       gameHud.Score == null &&
                                       !gameHud.ScorePanel.gameObject.activeSelf;
            var achievementInitiallyOff = gameHud.AchievementRatePanel != null &&
                                          !gameHud.AchievementRatePanel.IsVisible;
            var principalInitiallyHidden = gameHud.PrincipalGauge != null &&
                                           gameHud.Principal != null &&
                                           !gameHud.PrincipalGauge.gameObject.activeSelf;
            gameHud.InitializePrincipal(new[]
            {
                new RecoveredPrincipalUnit(0, 1200),
            });
            var addedPrincipal = gameHud.ActivateSensePrincipal(
                7001,
                new Dictionary<long, int> { { 7001, 300 } },
                0);
            var principalBound = addedPrincipal == 300 &&
                                 gameHud.PrincipalGauge.gameObject.activeSelf &&
                                 gameHud.PrincipalGauge.CurrentPrincipalCount.text == "300" &&
                                 gameHud.PrincipalGauge.MaxPrincipalCount.text == "1200" &&
                                 gameHud.PrincipalGauge.Gauge.value == 300f &&
                                 gameHud.PrincipalGauge.Gauge.maxValue == 1200f;
            gameHud.InitializeScore(new RecoveredSoloScoreContext(
                0, 1d, 8000, 20, new[] { note.Id }));
            var incrementCounts = gameHud.ScorePanel.GetComponentsInChildren<
                Sirius.Game.UI.IncrementScoreCount>(true);
            var incrementInitializationHidden =
                incrementCounts.Length == 7 && incrementCounts.All(count =>
                {
                    var serialized = new SerializedObject(count);
                    var group = serialized.FindProperty("_canvasGroup")
                        ?.objectReferenceValue as CanvasGroup;
                    return group != null && group.alpha < 0.01f;
                });
            gameHud.AchievementRatePanel.Initialize(
                new[]
                {
                    LoadComboDigits("txt_game_txt_combo_"),
                    LoadComboDigits("txt_game_txt_combo_fc_"),
                    LoadComboDigits("txt_game_txt_combo_ap_"),
                    LoadComboDigits("txt_game_txt_combo_nc_"),
                },
                LoadSprites(RatePointPaths),
                LoadSprites(RatePercentPaths),
                Sirius.Game.UI.RecoveredAchievementRateSettingType.Add,
                false,
                false,
                false);
            gameHud.ProcessFrame(new[] { inputResult }, combo);
            var effect = gameHud.LastTimingEffect;
            var renderer = effect != null
                ? effect.GetComponentInChildren<SpriteRenderer>(true) : null;
            var expectedCutInScreenPosition =
                Sirius.Game.UI.AdditionalScoreCutInPanel
                    .CalculateParentScreenPosition(
                        Camera.main, gameHud.TimingParent);
            var baseValid = gameHud.TimingPoolCount == 1 &&
                   gameHud.TimingAssistPoolCount == 1 &&
                   effect != null &&
                   effect.transform.localPosition == Vector3.zero &&
                   Mathf.Approximately(
                       gameHud.TimingParent.localPosition.y,
                       RecoveredGameHudRuntime.CalculatePositionY(60)) &&
                   gameHud.AdditionalScoreCutInPanel != null &&
                   Vector3.Distance(
                       gameHud.AdditionalScoreCutInPanel.ParentScreenPosition,
                       expectedCutInScreenPosition) < 0.01f &&
                   gameHud.ComboPanel != null && gameHud.ComboPanel.ComboCount == 1 &&
                   gameHud.SenseLightPanel != null &&
                   scoreInitiallyHidden && achievementInitiallyOff &&
                   gameHud.Score != null && gameHud.Score.Count == 96000L &&
                   gameHud.ScorePanel.gameObject.activeSelf &&
                   incrementInitializationHidden &&
                   !gameHud.ScorePanel.IsAutoPlayVisible &&
                   gameHud.ScorePanel.ScoreCount.text == "96000" &&
                   gameHud.ScorePanel.ZeroText.text == "000000" &&
                   gameHud.AchievementRatePanel.IsVisible &&
                   !gameHud.AchievementRatePanel.IsAnimationEnabled &&
                   gameHud.AchievementRatePanel.IsApStarVisible &&
                   Math.Abs(gameHud.AchievementRatePanel.Rate - 101d) < 0.000001d &&
                   gameHud.AchievementRatePanel.FormattedRate == "101.0000" &&
                   ValidateAchievementRateSlots(
                       gameHud.AchievementRatePanel.RateImages) &&
                   gameHud.LifeGauge != null && gameHud.Life != null &&
                   principalInitiallyHidden && principalBound &&
                   gameHud.Life.Value == RecoveredLifeRuntime.DefaultValue &&
                   RecoveredLifeRuntime.CalculateDecrement(
                       RecoveredNoteType.Normal, RecoveredTimingType.Miss) == 80 &&
                   RecoveredLifeRuntime.CalculateDecrement(
                       RecoveredNoteType.Hold, RecoveredTimingType.Miss) == 50 &&
                   RecoveredLifeRuntime.CalculateDecrement(
                       RecoveredNoteType.HoldEighth, RecoveredTimingType.Miss) == 30 &&
                   RecoveredLifeRuntime.CalculateDecrement(
                       RecoveredNoteType.Sound, RecoveredTimingType.Bad) == 0 &&
                   renderer != null && renderer.sprite != null &&
                   renderer.sprite.name == "txt_game_common_judgment_perfect_star";
            if (!baseValid) return false;

            var assistNote = new RecoveredNotationNote
            {
                Id = 2, StartTickCount = 2f,
                NoteType = (int)RecoveredNoteType.Normal, Lane = 6, Width = 1,
            };
            var assistDecision = new RecoveredTimingDecision(
                RecoveredTimingType.Great,
                RecoveredTimingAssistType.Fast, -60);
            var assistResult = RecoveredInputResultEntity.Create(
                assistNote, assistDecision);
            var assistCombo = new RecoveredGameResultRuntime(
                new[] { assistNote });
            assistCombo.Collect(assistResult);
            gameHud.ProcessFrame(new[] { assistResult }, assistCombo);
            var assistEffect = gameHud.LastTimingAssistEffect;
            return assistEffect != null &&
                   assistEffect.CurrentSprite != null &&
                   assistEffect.CurrentSprite.name ==
                       "txt_game_common_judgment_fast" &&
                   Mathf.Approximately(
                       assistEffect.transform.localPosition.y,
                       0f) &&
                   Mathf.Approximately(
                       gameHud.TimingParent.localPosition.y,
                       RecoveredGameHudRuntime.CalculatePositionY(60)) &&
                   Mathf.Approximately(
                       assistEffect.transform.localScale.x, 1f);
        }

        private static bool ValidatePrincipalRuntime()
        {
            var principal = new RecoveredPrincipalRuntime();
            principal.Initialize(new[]
            {
                new RecoveredPrincipalUnit(2, 1000),
                new RecoveredPrincipalUnit(5, 750),
            });
            if (principal.DefaultOrder != 2 || principal.UnitCount != 2 ||
                principal.GetCurrentPrincipal(2) != 0 ||
                principal.GetMaxPrincipal(2) != 1000 ||
                RecoveredPrincipalRuntime.CalculateAddingPrincipal(400, 25d) != 500)
                return false;
            principal.AddNonTargetBuff(25d, 2);
            principal.AddPrincipalGaugeUpBuff(1000, 50d, 2);
            if (principal.GetAddingPrincipal(400, 2) != 700 ||
                principal.ActivateSense(
                    7001,
                    new Dictionary<long, int> { { 7001, 400 } },
                    2) != 700 ||
                principal.GetCurrentPrincipal(2) != 700)
                return false;
            principal.TickBuffs(999);
            if (principal.GetAddingPrincipal(400, 2) != 700)
                return false;
            principal.ExtendBuffDuration(1, 2);
            principal.TickBuffs(2);
            if (principal.GetAddingPrincipal(400, 2) != 500 ||
                !principal.RemoveNonTargetBuff(25d, 2) ||
                principal.GetAddingPrincipal(400, 2) != 400)
                return false;
            principal.Reset();
            principal.Add(400, 2);
            if (principal.ApplyEffect(
                    RecoveredPrincipalEffectType.Gain, 100d, 2, false) ||
                principal.GetCurrentPrincipal(2) != 400)
                return false;
            principal.ApplyEffect(RecoveredPrincipalEffectType.Bonus, 25d, 2, true);
            principal.ApplyEffect(RecoveredPrincipalEffectType.LimitUp, 250d, 2, true);
            principal.ApplyEffect(
                RecoveredPrincipalEffectType.GainPercentageOfLimit, 10d, 2, true);
            principal.ApplyEffect(RecoveredPrincipalEffectType.Gain, 700d, 2, true);
            if (principal.GetCurrentPrincipal(2) != 1250 ||
                principal.GetMaxPrincipal(2) != 1250)
                return false;
            principal.Reset();
            return principal.GetCurrentPrincipal(2) == 0 &&
                   principal.GetMaxPrincipal(2) == 1000 &&
                   principal.GetCurrentPrincipal(5) == 0 &&
                   principal.GetMaxPrincipal(5) == 750;
        }

        private static bool ValidateGameRuntime(
            out int tapCount,
            out int flickCount,
            out int holdCount,
            out int[] remainingCounts)
        {
            tapCount = 0;
            flickCount = 0;
            holdCount = 0;
            remainingCounts = new int[6];
            var chartPath = Path.Combine(
                Application.dataPath, "StreamingAssets", "OpenWDS",
                "StandardCharts", "1", "1", "2.csv");
            var notation = RecoveredStandardNotation.Parse(File.ReadAllText(chartPath));
            var runtime = new RecoveredInputHandlerRuntime(
                notation,
                3.019f,
                note => new Vector2(note.Lane, 0f),
                position => new RecoveredHitLaneEntity(
                    Mathf.RoundToInt(position.x), 0, 0, 0, 0));

            var frames = new SortedSet<long>();
            var activeHoldEffects = new Dictionary<int, int>();
            var autoTimingCounts = new Dictionary<RecoveredTimingType, int>();
            var resultRuntime = new RecoveredGameResultRuntime(notation);
            var nonPerfectAutoResults = new List<string>();
            var holdStarts = 0;
            var holdEnds = 0;
            var holdStartedBeforeTail = false;
            frames.Add(0);
            foreach (var input in runtime.ScheduledEvents)
            {
                frames.Add(input.Milliseconds);
            }
            foreach (var note in notation)
            {
                if (note.EndMilliseconds >= 0) frames.Add(note.EndMilliseconds);
            }
            // The runtime now restores NotationNoteManager's per-frame MISS
            // expiration. Sparse event-only stepping can incorrectly expire
            // HoldEighth pulses between two synthetic frames; exercise the same
            // cadence as gameplay in addition to exact event boundaries.
            for (long milliseconds = 0; milliseconds <= 61000; milliseconds += 16)
                frames.Add(milliseconds);
            foreach (var milliseconds in frames)
            {
                runtime.Tick(milliseconds, milliseconds + 100000);
                foreach (var result in runtime.InputResults)
                {
                    resultRuntime.Collect(result);
                    if (!autoTimingCounts.ContainsKey(result.TimingType))
                        autoTimingCounts[result.TimingType] = 0;
                    autoTimingCounts[result.TimingType]++;
                    if (result.TimingType != RecoveredTimingType.PerfectStar)
                        nonPerfectAutoResults.Add(result.NoteId + ":" +
                            result.NoteType + "@" + milliseconds + "=" +
                            result.TimingType);
                }
                AuditHoldEvents(runtime.HoldEvents, activeHoldEffects,
                    ref holdStarts, ref holdEnds);
                foreach (var holdEvent in runtime.HoldEvents)
                {
                    if (holdEvent.EffectType !=
                        RecoveredInputEffectType.HoldStart)
                        continue;
                    var body = notation.FirstOrDefault(
                        note => note.Id == holdEvent.NoteId &&
                                note.NoteType == (int)holdEvent.NoteType);
                    if (body != null &&
                        milliseconds < body.EndMilliseconds)
                    {
                        holdStartedBeforeTail = true;
                    }
                }
            }
            runtime.Tick(90000, 190000);
            foreach (var result in runtime.InputResults)
            {
                resultRuntime.Collect(result);
                if (!autoTimingCounts.ContainsKey(result.TimingType))
                    autoTimingCounts[result.TimingType] = 0;
                autoTimingCounts[result.TimingType]++;
                if (result.TimingType != RecoveredTimingType.PerfectStar)
                    nonPerfectAutoResults.Add(result.NoteId + ":" +
                        result.NoteType + "@90000=" + result.TimingType);
            }
            AuditHoldEvents(runtime.HoldEvents, activeHoldEffects,
                ref holdStarts, ref holdEnds);
            var naturallyBalanced = activeHoldEffects.Count == 0 &&
                                    holdStarts == holdEnds;
            var activeAssignments = new List<string>();
            foreach (var pair in runtime.ActiveTouchHolds)
                activeAssignments.Add(pair.Key + "->" + pair.Value.Id);
            Debug.Log("OPENWDS_HOLD_EVENT_AUDIT starts=" + holdStarts +
                      " ends=" + holdEnds +
                      " activeBeforeComplete=" + activeHoldEffects.Count +
                      " laneHoldsBeforeComplete=" + runtime.ActiveTouchHoldCount +
                      " ids=" + string.Join(",", activeHoldEffects.Keys) +
                      " assignments=" + string.Join(",", activeAssignments));
            var timingAudit = new List<string>();
            foreach (var pair in autoTimingCounts)
                timingAudit.Add(pair.Key + "=" + pair.Value);
            Debug.Log("OPENWDS_AUTO_JUDGE_TIMINGS " + string.Join(",", timingAudit));
            Debug.Log("OPENWDS_AUTO_JUDGE_NONPERFECT " +
                      string.Join(",", nonPerfectAutoResults));
            var collectedTimingAudit = new List<string>();
            foreach (var pair in resultRuntime.TimingCounts)
                collectedTimingAudit.Add(pair.Key + "=" + pair.Value);
            Debug.Log("OPENWDS_AUTO_JUDGE_COLLECTED_TIMINGS " +
                      string.Join(",", collectedTimingAudit) +
                      " maxCombo=" + resultRuntime.MaxCombo +
                      " perfectStar=" + resultRuntime.IsPerfectStar);
            runtime.CompleteGame();

            tapCount = runtime.ConsumedTapCount;
            flickCount = runtime.ConsumedFlickCount;
            holdCount = runtime.ConsumedHoldCount;
            remainingCounts[0] = runtime.RemainingTapCount;
            remainingCounts[1] = runtime.RemainingFlickCount;
            remainingCounts[2] = runtime.RemainingHoldCount;
            remainingCounts[3] = runtime.RemainingScratchCount;
            remainingCounts[4] = runtime.RemainingHoldingCount;
            remainingCounts[5] = runtime.ActiveTouchHoldCount;
            return runtime.ScheduledEventCount > 0 &&
                   runtime.ProcessedFrameCount == frames.Count + 1 &&
                   runtime.IsGameCompleted &&
                   runtime.PlayableQueuesEmpty &&
                   runtime.RemainingHoldingCount == 0 &&
                   runtime.ActiveTouchHoldCount == 0 &&
                   naturallyBalanced &&
                   holdStartedBeforeTail &&
                   resultRuntime.TimingCounts[RecoveredTimingType.Bad] == 0 &&
                   resultRuntime.TimingCounts[RecoveredTimingType.Miss] == 0 &&
                   resultRuntime.IsPerfectStar &&
                   tapCount == 133 && flickCount == 3 && holdCount > 0;
        }

        private static bool ValidatePlayerInputEntry()
        {
            var notation = new[]
            {
                new RecoveredNotationNote
                {
                    Id = 0,
                    StartTickCount = 0.9f,
                    EndTickCount = -1f,
                    NoteType = (int)RecoveredNoteType.Flick,
                    Lane = 1,
                    Width = 2,
                },
                new RecoveredNotationNote
                {
                    Id = 1,
                    StartTickCount = 1f,
                    EndTickCount = -1f,
                    NoteType = (int)RecoveredNoteType.Normal,
                    Lane = 6,
                    Width = 2,
                },
            };
            var runtime = new RecoveredInputHandlerRuntime(
                notation,
                0f,
                note => new Vector2(note.Lane, 0f),
                position => new RecoveredHitLaneEntity(
                    Mathf.RoundToInt(position.x), 0, 0, 0, 0));
            var playerTouches = new[]
            {
                // Reuse the Flick note ID deliberately: AutoTouch's identity
                // guard must not apply to device-derived player touches.
                new RecoveredInputEntity(
                    0,
                    5000,
                    new Vector2(6f, 0f),
                    new Vector2(6f, 0f),
                    Vector2.zero,
                    RecoveredTouchPhase.Began),
            };

            runtime.TickPlayer(1000, 5000, playerTouches);
            Debug.Log(
                "OPENWDS_PLAYER_INPUT_ENTRY " +
                $"tap={runtime.ConsumedTapCount} results={runtime.InputResults.Count} " +
                $"remainingTap={runtime.RemainingTapCount} " +
                $"remainingFlick={runtime.RemainingFlickCount} " +
                $"miss={runtime.PublishedMissCount}");
            return runtime.ConsumedTapCount == 1 &&
                   runtime.InputResults.Count == 2 &&
                   runtime.InputResults[0].NoteId == 1 &&
                   runtime.InputResults[0].TimingType ==
                   RecoveredTimingType.PerfectStar &&
                   runtime.RemainingTapCount == 0 &&
                   runtime.RemainingFlickCount == 0 &&
                   runtime.InputResults[1].NoteId == 0 &&
                   runtime.InputResults[1].TimingType ==
                   RecoveredTimingType.Miss &&
                   !runtime.InputResults[1].IsInput;
        }

        private static bool ValidateAutoTouchModeSeparation(
            Camera camera,
            GameObject laneGroupObject)
        {
            var laneGroup =
                laneGroupObject.GetComponent<Sirius.Game.LaneGroup>();
            if (laneGroup == null) return false;
            var faithful = CollectMagicLastNoteAutoTimings(
                camera,
                laneGroup,
                out var faithfulPerfectStar,
                out var faithfulGood);
            Debug.Log(
                "OPENWDS_AUTO_JUDGE_FAITHFUL " +
                $"perfectStar={faithfulPerfectStar} good={faithfulGood}");
            return faithful == 1216 &&
                   faithfulPerfectStar == 1213 &&
                   faithfulGood == 3;
        }

        private static int CollectMagicLastNoteAutoTimings(
            Camera camera,
            Sirius.Game.LaneGroup laneGroup,
            out int perfectStar,
            out int good)
        {
            perfectStar = 0;
            good = 0;
            var chartPath = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                "OpenWDS",
                "StandardCharts",
                "7",
                "1",
                "4.csv");
            var configPath = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                "OpenWDS",
                "StandardCharts",
                "7",
                "1",
                "music_config.csv");
            var notation = RecoveredStandardNotation.Parse(
                File.ReadAllText(chartPath));
            var config = RecoveredStandardNotation.ParseMusicConfig(
                File.ReadAllText(configPath));
            var managers = laneGroup.ColliderManagers;
            managers.InitializeMappings();
            var raycaster = new RecoveredLaneRaycaster(
                camera,
                managers.MainColliders,
                managers.SubLeftInnerColliders,
                managers.SubRightInnerColliders,
                managers.SubLeftOuterColliders,
                managers.SubRightOuterColliders);
            var runtime = new RecoveredInputHandlerRuntime(
                notation,
                config.DelayStartSeconds,
                note =>
                {
                    var screen = camera.WorldToScreenPoint(
                        laneGroup.GetLaneCollider(note.Lane).position);
                    return new Vector2(screen.x, screen.y);
                },
                position =>
                {
                    var input = new RecoveredInputEntity(
                        0,
                        0,
                        position,
                        position,
                        Vector2.zero,
                        RecoveredTouchPhase.Stationary);
                    var inputs = new[] { input };
                    var hits = new List<RecoveredHitLaneEntity>(1);
                    var beganHits = new List<RecoveredHitLaneEntity>(1);
                    raycaster.Raycast(inputs, hits, beganHits);
                    return hits[0];
                });
            var frames = new SortedSet<long> { 0L };
            foreach (var input in runtime.ScheduledEvents)
                frames.Add(input.Milliseconds);
            foreach (var note in notation)
            {
                if (note.EndMilliseconds >= 0)
                    frames.Add(note.EndMilliseconds);
            }
            var lastMilliseconds = notation.Max(note =>
                Math.Max(note.StartMilliseconds, note.EndMilliseconds));
            for (long milliseconds = 0;
                 milliseconds <= lastMilliseconds + 200;
                 milliseconds += 16)
            {
                frames.Add(milliseconds);
            }

            var total = 0;
            var nonAutoPerfect = new List<string>();
            var chainTrace = new List<string>();
            foreach (var milliseconds in frames)
            {
                runtime.Tick(milliseconds, milliseconds + 200000L);
                if (milliseconds >= 67800 && milliseconds <= 70000 &&
                    (runtime.InputResults.Count > 0 ||
                     runtime.HoldEvents.Count > 0))
                {
                    var assignments = string.Join(
                        "+",
                        runtime.ActiveTouchHolds.Select(
                            pair => pair.Key + ">" + pair.Value.Id));
                    chainTrace.Add(
                        milliseconds + ":R=" +
                        string.Join(
                            "+",
                            runtime.InputResults.Select(
                                value => value.NoteId + "/" +
                                         (int)value.NoteType + "/" +
                                         value.TimingType)) +
                        ":E=" +
                        string.Join(
                            "+",
                            runtime.HoldEvents.Select(
                                value => value.NoteId + "/" +
                                         (int)value.EffectType)) +
                        ":A=" + assignments);
                }
                foreach (var result in runtime.InputResults)
                {
                    total++;
                    if (result.TimingType == RecoveredTimingType.PerfectStar)
                        perfectStar++;
                    else if (result.TimingType == RecoveredTimingType.Good)
                        good++;
                    else
                        nonAutoPerfect.Add(
                            result.NoteId + ":" + (int)result.NoteType +
                            "@" + milliseconds + "=" + result.TimingType);
                }
            }
            Debug.Log(
                "OPENWDS_MAGIC_LAST_NOTE_CHAIN_TRACE " +
                string.Join(",", chainTrace));
            Debug.Log(
                "OPENWDS_MAGIC_LAST_NOTE_QUEUE " +
                $"total={total} " +
                $"resolved={runtime.ResolvedNoteCount}/" +
                $"{runtime.ScheduledNoteCount} miss={runtime.PublishedMissCount} " +
                $"tap={runtime.RemainingTapCount} " +
                $"flick={runtime.RemainingFlickCount} " +
                $"hold={runtime.RemainingHoldCount} " +
                $"scratch={runtime.RemainingScratchCount} " +
                $"holding={runtime.RemainingHoldingCount} " +
                $"active={runtime.ActiveTouchHoldCount} " +
                $"other={string.Join(",", nonAutoPerfect)}");
            if (nonAutoPerfect.Count > 0)
            {
                var missSchedule = new List<string>();
                foreach (var resultText in nonAutoPerfect)
                {
                    var separator = resultText.IndexOf(':');
                    if (separator <= 0 ||
                        !int.TryParse(
                            resultText.Substring(0, separator), out var noteId))
                        continue;
                    foreach (var scheduled in runtime.ScheduledEvents)
                    {
                        if (scheduled.TouchId != noteId) continue;
                        var hit = raycaster.RaycastPoint(
                            scheduled.ScreenPosition);
                        missSchedule.Add(
                            noteId + ":" + scheduled.Milliseconds + ":" +
                            (int)scheduled.Phase + ":" +
                            hit.HitMainLaneId + "/" +
                            hit.HitSubLeftInnerLaneId + "/" +
                            hit.HitSubRightInnerLaneId + "/" +
                            hit.HitSubLeftOuterLaneId + "/" +
                            hit.HitSubRightOuterLaneId);
                    }
                }
                Debug.Log(
                    "OPENWDS_MAGIC_LAST_NOTE_MISS_SCHEDULE " +
                    string.Join(",", missSchedule));
            }
            return total;
        }

        private static bool ValidateDefaultTouchRuntime()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var manifest = File.ReadAllText(
                Path.Combine(projectRoot, "Packages", "manifest.json"));
            var projectSettings = File.ReadAllText(
                Path.Combine(
                    projectRoot,
                    "ProjectSettings",
                    "ProjectSettings.asset"));
            return typeof(RecoveredDefaultTouchRuntime) != null &&
                   manifest.Contains("\"com.unity.inputsystem\": \"1.14.2\"") &&
                   projectSettings.Contains("activeInputHandler: 2") &&
                   (int)UnityEngine.InputSystem.TouchPhase.Began ==
                   (int)RecoveredTouchPhase.Began &&
                   (int)UnityEngine.InputSystem.TouchPhase.Stationary ==
                   (int)RecoveredTouchPhase.Stationary;
        }

        private static bool ValidateAllChartNoInputMissCoverage()
        {
            var chartRoot = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                "OpenWDS",
                "StandardCharts");
            var chartPaths = Directory.GetFiles(
                chartRoot, "*.csv", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path) != "music_config.csv")
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var emptyTouches = Array.Empty<RecoveredInputEntity>();
            var totalExpected = 0;
            var totalMiss = 0;
            var failures = new List<string>();
            foreach (var chartPath in chartPaths)
            {
                var notation = RecoveredStandardNotation.Parse(
                    File.ReadAllText(chartPath));
                var expected = notation.Count(note =>
                    note.NoteType != (int)RecoveredNoteType.None);
                var chartEnd = notation.Length == 0
                    ? 0L
                    : notation.Max(note =>
                        Math.Max(
                            note.StartMilliseconds,
                            note.EndMilliseconds));
                var runtime = new RecoveredInputHandlerRuntime(
                    notation,
                    0f,
                    _ => Vector2.zero,
                    _ => default);
                runtime.TickPlayer(
                    chartEnd + 2000L,
                    chartEnd + 2000L,
                    emptyTouches);
                var queuesEmpty =
                    runtime.RemainingTapCount == 0 &&
                    runtime.RemainingFlickCount == 0 &&
                    runtime.RemainingHoldCount == 0 &&
                    runtime.RemainingScratchCount == 0 &&
                    runtime.RemainingHoldingCount == 0 &&
                    runtime.ActiveTouchHoldCount == 0;
                if (runtime.PublishedMissCount != expected ||
                    runtime.ResolvedNoteCount != expected ||
                    runtime.InputResults.Count != expected ||
                    !queuesEmpty)
                {
                    failures.Add(
                        Path.GetRelativePath(chartRoot, chartPath) +
                        ":expected=" + expected +
                        "/miss=" + runtime.PublishedMissCount +
                        "/resolved=" + runtime.ResolvedNoteCount +
                        "/results=" + runtime.InputResults.Count +
                        "/queues=" + runtime.RemainingTapCount + "/" +
                        runtime.RemainingFlickCount + "/" +
                        runtime.RemainingHoldCount + "/" +
                        runtime.RemainingScratchCount + "/" +
                        runtime.RemainingHoldingCount + "/" +
                        runtime.ActiveTouchHoldCount);
                }
                totalExpected += expected;
                totalMiss += runtime.PublishedMissCount;
                runtime.Dispose();
            }
            Debug.Log(
                "OPENWDS_ALL_CHART_NO_INPUT_MISS charts=" +
                chartPaths.Length + " expected=" + totalExpected +
                " miss=" + totalMiss + " failures=" +
                string.Join(",", failures));
            return chartPaths.Length == 50 &&
                   totalExpected == totalMiss &&
                   failures.Count == 0;
        }

        private static bool ValidateEnhancedTouchInjection(
            Camera camera,
            GameObject laneGroupObject,
            out bool playerActionsValid,
            out bool pauseTouchLifecycleValid,
            out bool touchIdReuseValid)
        {
            playerActionsValid = false;
            pauseTouchLifecycleValid = false;
            touchIdReuseValid = false;
            var enabledHere = !EnhancedTouchSupport.enabled;
            Touchscreen screen = null;
            try
            {
                if (enabledHere) EnhancedTouchSupport.Enable();
                screen = InputSystem.AddDevice<Touchscreen>();
                var laneGroup =
                    laneGroupObject.GetComponent<Sirius.Game.LaneGroup>();
                if (laneGroup == null) return false;
                var lane2Position = GetInjectedLaneScreenPosition(
                    camera, laneGroup, 2);
                var lane4Position = GetInjectedLaneScreenPosition(
                    camera, laneGroup, 4);
                var lane8Position = GetInjectedLaneScreenPosition(
                    camera, laneGroup, 8);
                var movedLane4Position =
                    lane4Position + new Vector2(60f, 0f);
                var provider = new RecoveredDefaultTouchProvider();
                var touches = new List<RecoveredInputEntity>(16);
                var baseTime = InputState.currentTime;

                QueueInjectedTouch(
                    screen,
                    101,
                    UnityEngine.InputSystem.TouchPhase.Began,
                    lane4Position,
                    Vector2.zero,
                    baseTime);
                InputSystem.Update();
                provider.GetTouches(touches, 1000L, _ => 1000L);
                var beganValid = ContainsInjectedTouch(
                    touches,
                    101,
                    RecoveredTouchPhase.Began,
                    lane4Position,
                    Vector2.zero);
                var beganInput = beganValid
                    ? touches[FindInjectedTouch(
                        touches, 101, RecoveredTouchPhase.Began)]
                    : default;

                touches.Clear();
                InputSystem.Update();
                provider.GetTouches(touches, 1016L, _ => 1016L);
                var stationaryValid = ContainsInjectedTouch(
                    touches,
                    101,
                    RecoveredTouchPhase.Stationary,
                    lane4Position,
                    Vector2.zero,
                    1016L);
                var stationaryInput = stationaryValid
                    ? touches[FindInjectedTouch(
                        touches, 101, RecoveredTouchPhase.Stationary)]
                    : default;

                touches.Clear();
                QueueInjectedTouch(
                    screen,
                    101,
                    UnityEngine.InputSystem.TouchPhase.Moved,
                    movedLane4Position,
                    new Vector2(60f, 0f),
                    baseTime + 0.02d);
                InputSystem.Update();
                provider.GetTouches(touches, 1020L, _ => 1020L);
                var movedValid = ContainsInjectedTouch(
                    touches,
                    101,
                    RecoveredTouchPhase.Moved,
                    movedLane4Position,
                    new Vector2(60f, 0f));
                var movedInput = movedValid
                    ? touches[FindInjectedTouch(
                        touches, 101, RecoveredTouchPhase.Moved)]
                    : default;

                touches.Clear();
                QueueInjectedTouch(
                    screen,
                    202,
                    UnityEngine.InputSystem.TouchPhase.Began,
                    lane2Position,
                    Vector2.zero,
                    baseTime + 0.04d);
                QueueInjectedTouch(
                    screen,
                    203,
                    UnityEngine.InputSystem.TouchPhase.Began,
                    lane8Position,
                    Vector2.zero,
                    baseTime + 0.04d);
                InputSystem.Update();
                provider.GetTouches(touches, 1040L, _ => 1040L);
                var multiTouchValid =
                    ContainsInjectedTouch(
                        touches,
                        202,
                        RecoveredTouchPhase.Began,
                        lane2Position,
                        Vector2.zero) &&
                    ContainsInjectedTouch(
                        touches,
                        203,
                        RecoveredTouchPhase.Began,
                        lane8Position,
                        Vector2.zero);
                var multiInputs = new List<RecoveredInputEntity>(2);
                foreach (var touch in touches)
                {
                    if ((touch.TouchId == 202 || touch.TouchId == 203) &&
                        touch.Phase == RecoveredTouchPhase.Began)
                    {
                        multiInputs.Add(touch);
                    }
                }

                QueueInjectedTouch(
                    screen,
                    101,
                    UnityEngine.InputSystem.TouchPhase.Ended,
                    movedLane4Position,
                    Vector2.zero,
                    baseTime + 0.05d);
                QueueInjectedTouch(
                    screen,
                    202,
                    UnityEngine.InputSystem.TouchPhase.Ended,
                    lane2Position,
                    Vector2.zero,
                    baseTime + 0.05d);
                QueueInjectedTouch(
                    screen,
                    203,
                    UnityEngine.InputSystem.TouchPhase.Ended,
                    lane8Position,
                    Vector2.zero,
                    baseTime + 0.05d);
                InputSystem.Update();
                touches.Clear();
                provider.GetTouches(touches, 1050L, _ => 1050L);
                var endedIndex = FindInjectedTouch(
                    touches, 101, RecoveredTouchPhase.Ended);
                var endedValid = endedIndex >= 0;
                var endedInput = endedValid
                    ? touches[endedIndex]
                    : default;

                provider.Reset();
                touches.Clear();
                QueueInjectedTouch(
                    screen,
                    301,
                    UnityEngine.InputSystem.TouchPhase.Began,
                    new Vector2(500f, 120f),
                    Vector2.zero,
                    baseTime + 0.10d);
                InputSystem.Update();
                provider.GetTouches(touches, 1100L, _ => 1100L);
                var historyBeganValid = FindInjectedTouch(
                    touches, 301, RecoveredTouchPhase.Began) >= 0;

                touches.Clear();
                QueueInjectedTouch(
                    screen,
                    301,
                    UnityEngine.InputSystem.TouchPhase.Moved,
                    new Vector2(530f, 120f),
                    new Vector2(30f, 0f),
                    baseTime + 0.11d);
                InputSystem.Update();
                QueueInjectedTouch(
                    screen,
                    301,
                    UnityEngine.InputSystem.TouchPhase.Moved,
                    new Vector2(560f, 120f),
                    new Vector2(30f, 0f),
                    baseTime + 0.12d);
                InputSystem.Update();
                provider.GetTouches(touches, 1120L, _ => 1120L);
                var movedPositions = new List<float>(2);
                foreach (var touch in touches)
                {
                    if (touch.TouchId == 301 &&
                        touch.Phase == RecoveredTouchPhase.Moved)
                    {
                        movedPositions.Add(touch.ScreenPosition.x);
                    }
                }
                var historyValid = historyBeganValid &&
                                   movedPositions.Count == 2 &&
                                   movedPositions[0] == 530f &&
                                   movedPositions[1] == 560f;

                // InputHandler removes LaneHold assignments when ticking stops.
                // During the countdown the old finger may end and a new finger
                // may begin; DefaultTouch.Reset must expose that active touch
                // on resume so the body can be reacquired.
                provider.Reset();
                QueueInjectedTouch(
                    screen,
                    301,
                    UnityEngine.InputSystem.TouchPhase.Ended,
                    new Vector2(560f, 120f),
                    Vector2.zero,
                    baseTime + 0.13d);
                InputSystem.Update();
                QueueInjectedTouch(
                    screen,
                    302,
                    UnityEngine.InputSystem.TouchPhase.Began,
                    lane4Position,
                    Vector2.zero,
                    baseTime + 0.14d);
                InputSystem.Update();
                provider.Reset();
                touches.Clear();
                provider.GetTouches(touches, 1130L, _ => 1130L);
                var countdownTouchReplayed = ContainsInjectedTouch(
                    touches,
                    302,
                    RecoveredTouchPhase.Began,
                    lane4Position,
                    Vector2.zero);

                QueueInjectedTouch(
                    screen,
                    302,
                    UnityEngine.InputSystem.TouchPhase.Moved,
                    movedLane4Position,
                    new Vector2(30f, 0f),
                    baseTime + 0.15d);
                InputSystem.Update();
                touches.Clear();
                provider.GetTouches(touches, 1140L, _ => 1140L);
                var countdownTouchMoved = ContainsInjectedTouch(
                    touches,
                    302,
                    RecoveredTouchPhase.Moved,
                    movedLane4Position,
                    new Vector2(30f, 0f));

                QueueInjectedTouch(
                    screen,
                    302,
                    UnityEngine.InputSystem.TouchPhase.Ended,
                    movedLane4Position,
                    Vector2.zero,
                    baseTime + 0.16d);
                InputSystem.Update();
                touches.Clear();
                provider.GetTouches(touches, 1150L, _ => 1150L);
                var countdownTouchEnded = ContainsInjectedTouch(
                    touches,
                    302,
                    RecoveredTouchPhase.Ended,
                    movedLane4Position,
                    Vector2.zero);
                pauseTouchLifecycleValid =
                    countdownTouchReplayed &&
                    countdownTouchMoved &&
                    countdownTouchEnded;

                QueueInjectedTouch(
                    screen,
                    301,
                    UnityEngine.InputSystem.TouchPhase.Began,
                    lane4Position,
                    Vector2.zero,
                    baseTime + 0.17d);
                InputSystem.Update();
                touches.Clear();
                provider.GetTouches(touches, 1160L, _ => 1160L);
                touchIdReuseValid = ContainsInjectedTouch(
                    touches,
                    301,
                    RecoveredTouchPhase.Began,
                    lane4Position,
                    Vector2.zero);
                playerActionsValid =
                    beganValid && stationaryValid && movedValid &&
                    multiTouchValid && endedValid &&
                    ValidateInjectedPlayerActions(
                        camera,
                        laneGroup,
                        beganInput,
                        stationaryInput,
                        movedInput,
                        endedInput,
                        multiInputs);

                Debug.Log(
                    "OPENWDS_ENHANCED_TOUCH_INJECTION " +
                    $"began={beganValid} stationary={stationaryValid} " +
                    $"moved={movedValid} multi={multiTouchValid} " +
                    $"history={historyValid} pauseTouch=" +
                    $"{pauseTouchLifecycleValid} reuse={touchIdReuseValid} " +
                    $"actions={playerActionsValid} " +
                    $"events={FormatInjectedTouches(touches)}");
                return beganValid && stationaryValid && movedValid &&
                       multiTouchValid && endedValid && historyValid &&
                       pauseTouchLifecycleValid && touchIdReuseValid;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "OPENWDS_ENHANCED_TOUCH_INJECTION_ERROR " + exception);
                playerActionsValid = false;
                pauseTouchLifecycleValid = false;
                touchIdReuseValid = false;
                return false;
            }
            finally
            {
                if (screen != null)
                {
                    InputSystem.RemoveDevice(screen);
                    InputSystem.Update();
                }
                if (enabledHere && EnhancedTouchSupport.enabled)
                    EnhancedTouchSupport.Disable();
            }
        }

        private static bool ValidateInjectedPlayerActions(
            Camera camera,
            Sirius.Game.LaneGroup laneGroup,
            RecoveredInputEntity began,
            RecoveredInputEntity stationary,
            RecoveredInputEntity moved,
            RecoveredInputEntity ended,
            List<RecoveredInputEntity> multiInputs)
        {
            var managers = laneGroup.ColliderManagers;
            managers.InitializeMappings();
            var raycaster = new RecoveredLaneRaycaster(
                camera,
                managers.MainColliders,
                managers.SubLeftInnerColliders,
                managers.SubRightInnerColliders,
                managers.SubLeftOuterColliders,
                managers.SubRightOuterColliders);
            Func<Vector2, RecoveredHitLaneEntity> resolveLane =
                position =>
                {
                    var input = new RecoveredInputEntity(
                        0,
                        0,
                        position,
                        position,
                        Vector2.zero,
                        RecoveredTouchPhase.Stationary);
                    var inputs = new[] { input };
                    var hits = new List<RecoveredHitLaneEntity>(1);
                    var beganHits = new List<RecoveredHitLaneEntity>(1);
                    raycaster.Raycast(inputs, hits, beganHits);
                    return hits[0];
                };
            Func<RecoveredNotationNote, Vector2> screenPosition =
                note => GetInjectedLaneScreenPosition(
                    camera, laneGroup, note.Lane);

            var tapNotation = new[]
            {
                CreateInjectedNotation(
                    1, 1.04f, -1f, RecoveredNoteType.Normal, 2),
                CreateInjectedNotation(
                    2, 1.04f, -1f, RecoveredNoteType.Critical, 8),
            };
            var tapRuntime = new RecoveredInputHandlerRuntime(
                tapNotation, 0f, screenPosition, resolveLane);
            tapRuntime.TickPlayer(1040L, 1040L, multiInputs);
            var tapValid = tapRuntime.ConsumedTapCount == 2 &&
                           tapRuntime.InputResults.Count == 2 &&
                           tapRuntime.RemainingTapCount == 0;

            var flickNotation = new[]
            {
                CreateInjectedNotation(
                    3, 1.02f, -1f, RecoveredNoteType.Flick, 4),
            };
            var flickRuntime = new RecoveredInputHandlerRuntime(
                flickNotation,
                0f,
                screenPosition,
                resolveLane,
                captureFlickDiagnostics: true);
            flickRuntime.TickPlayer(1000L, 1000L, new[] { began });
            flickRuntime.TickPlayer(1020L, 1020L, new[] { moved });
            var flickValid = flickRuntime.ConsumedFlickCount == 1 &&
                             flickRuntime.InputResults.Count == 1 &&
                             flickRuntime.InputResults[0].TimingType ==
                             RecoveredTimingType.PerfectStar &&
                             flickRuntime.RemainingFlickCount == 0;

            var holdNotation = new[]
            {
                CreateInjectedNotation(
                    4, 0.9f, 1.016f, RecoveredNoteType.Hold, 4),
            };
            var holdRuntime = new RecoveredInputHandlerRuntime(
                holdNotation, 0f, screenPosition, resolveLane);
            var holdStartInput = new RecoveredInputEntity(
                stationary.TouchId,
                1000L,
                stationary.StartScreenPosition,
                stationary.ScreenPosition,
                stationary.DeltaPosition,
                RecoveredTouchPhase.Stationary);
            var holdEndInput = new RecoveredInputEntity(
                stationary.TouchId,
                1016L,
                stationary.StartScreenPosition,
                stationary.ScreenPosition,
                stationary.DeltaPosition,
                RecoveredTouchPhase.Stationary);
            holdRuntime.TickPlayer(1000L, 1000L, new[] { holdStartInput });
            var holdStarted = holdRuntime.ConsumedHoldCount == 0 &&
                              holdRuntime.ActiveTouchHoldCount == 1;
            var holdConsumedAfterStart = holdRuntime.ConsumedHoldCount;
            var holdActiveAfterStart = holdRuntime.ActiveTouchHoldCount;
            holdRuntime.TickPlayer(1016L, 1016L, new[] { holdEndInput });
            var holdJudged = holdRuntime.ConsumedHoldCount == 1 &&
                             holdRuntime.ActiveTouchHoldCount == 1;
            var holdConsumedAtEnd = holdRuntime.ConsumedHoldCount;
            var holdActiveAtEnd = holdRuntime.ActiveTouchHoldCount;
            var holdRemainingAtEnd = holdRuntime.RemainingHoldCount;
            holdRuntime.TickPlayer(1050L, 1050L, new[] { ended });
            var holdValid = holdStarted &&
                            holdJudged &&
                            holdRuntime.ActiveTouchHoldCount == 0;

            var scratchNotation = new[]
            {
                CreateInjectedNotation(
                    5, 0.9f, 1.02f, RecoveredNoteType.ScratchHold, 4, 3),
            };
            var scratchRuntime = new RecoveredInputHandlerRuntime(
                scratchNotation, 0f, screenPosition, resolveLane);
            scratchRuntime.TickPlayer(1020L, 1020L, new[] { moved });
            var scratchValid = scratchRuntime.RemainingScratchCount == 0 &&
                               scratchRuntime.InputResults.Count == 1 &&
                               scratchRuntime.InputResults[0].TimingType ==
                               RecoveredTimingType.PerfectStar;

            Debug.Log(
                "OPENWDS_INJECTED_PLAYER_ACTIONS " +
                $"tap={tapValid} flick={flickValid} " +
                $"hold={holdValid} scratch={scratchValid} " +
                $"holdState={holdStarted}/{holdJudged}/" +
                $"{holdConsumedAfterStart}/{holdActiveAfterStart}/" +
                $"{holdConsumedAtEnd}/{holdActiveAtEnd}/" +
                $"{holdRemainingAtEnd}/{holdRuntime.ActiveTouchHoldCount} " +
                $"flickConsumed={flickRuntime.ConsumedFlickCount} " +
                $"flickRemaining={flickRuntime.RemainingFlickCount} " +
                $"began={began.StartScreenPosition}/{began.ScreenPosition}/" +
                $"{began.DeltaPosition}@{began.Milliseconds} " +
                $"moved={moved.StartScreenPosition}/{moved.ScreenPosition}/" +
                $"{moved.DeltaPosition}@{moved.Milliseconds} " +
                $"diagnostic={flickRuntime.GetFlickInputDiagnostic(3)}");
            return tapValid && flickValid && holdValid && scratchValid;
        }

        private static RecoveredNotationNote CreateInjectedNotation(
            int id,
            float startSeconds,
            float endSeconds,
            RecoveredNoteType noteType,
            int lane,
            int width = 1)
        {
            return new RecoveredNotationNote
            {
                Id = id,
                StartTickCount = startSeconds,
                EndTickCount = endSeconds,
                NoteType = (int)noteType,
                Lane = lane,
                Width = width,
            };
        }

        private static Vector2 GetInjectedLaneScreenPosition(
            Camera camera,
            Sirius.Game.LaneGroup laneGroup,
            int lane)
        {
            var screen = camera.WorldToScreenPoint(
                laneGroup.GetLaneCollider(lane).position);
            return new Vector2(screen.x, screen.y);
        }

        private static void QueueInjectedTouch(
            Touchscreen screen,
            int touchId,
            UnityEngine.InputSystem.TouchPhase phase,
            Vector2 position,
            Vector2 delta,
            double time)
        {
            InputSystem.QueueStateEvent(
                screen,
                new TouchState
                {
                    touchId = touchId,
                    phase = phase,
                    position = position,
                    delta = delta,
                    pressure = 1f,
                },
                time);
        }

        private static bool ContainsInjectedTouch(
            List<RecoveredInputEntity> touches,
            int touchId,
            RecoveredTouchPhase phase,
            Vector2 position,
            Vector2 delta,
            long? milliseconds = null)
        {
            var index = FindInjectedTouch(touches, touchId, phase);
            if (index < 0) return false;
            var touch = touches[index];
            return touch.ScreenPosition == position &&
                   touch.DeltaPosition == delta &&
                   (!milliseconds.HasValue ||
                    touch.Milliseconds == milliseconds.Value);
        }

        private static int FindInjectedTouch(
            List<RecoveredInputEntity> touches,
            int touchId,
            RecoveredTouchPhase phase)
        {
            for (var index = 0; index < touches.Count; index++)
            {
                if (touches[index].TouchId == touchId &&
                    touches[index].Phase == phase)
                {
                    return index;
                }
            }
            return -1;
        }

        private static string FormatInjectedTouches(
            List<RecoveredInputEntity> touches)
        {
            var values = new List<string>(touches.Count);
            foreach (var touch in touches)
            {
                values.Add(
                    $"{touch.TouchId}:{touch.Phase}@{touch.Milliseconds}" +
                    $"[{touch.ScreenPosition.x},{touch.DeltaPosition.x}]");
            }
            return string.Join(",", values);
        }

        private static bool ValidateGameBackground()
        {
            const string prefabPath =
                "Assets/Resources/Prefabs/GameBackground.prefab";
            const string texturePath =
                "Assets/Resources/Texture2D/ingame_bg.png";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Debug.Log("OPENWDS_GAME_BACKGROUND prefab=" + (prefab != null) +
                      " texture=" + (texture != null ? texture.width + "x" + texture.height : "null") +
                      " active=" + (prefab != null && prefab.activeSelf) +
                      " transforms=" + (prefab != null ? prefab.GetComponentsInChildren<Transform>(true).Length : -1) +
                      " sprites=" + (prefab != null ? prefab.GetComponentsInChildren<SpriteRenderer>(true).Length : -1) +
                      " animator=" + (prefab != null && prefab.GetComponent<Animator>() != null) +
                      " sorting=" + (prefab != null && prefab.GetComponent<UnityEngine.Rendering.SortingGroup>() != null) +
                      " shadow=" + (prefab != null && prefab.GetComponent<Sirius.GameBackgroundShadow>() != null) +
                      " jacket=" + (prefab != null && prefab.GetComponentInChildren<Sirius.Game.GameBackgroundJacket>(true) != null) +
                      " fitter=" + (prefab != null && prefab.GetComponentInChildren<Sirius.Screens.SpriteRendererScreenFitter>(true) != null));
            if (prefab == null || texture == null ||
                texture.width != 1920 || texture.height != 1180 ||
                !prefab.activeSelf ||
                prefab.GetComponentsInChildren<Transform>(true).Length != 8 ||
                prefab.GetComponentsInChildren<SpriteRenderer>(true).Length != 6 ||
                prefab.GetComponent<Animator>() == null ||
                prefab.GetComponent<UnityEngine.Rendering.SortingGroup>() == null ||
                prefab.GetComponent<Sirius.GameBackgroundShadow>() == null ||
                prefab.GetComponentInChildren<Sirius.Game.GameBackgroundJacket>(true) == null ||
                prefab.GetComponentInChildren<
                    Sirius.Screens.SpriteRendererScreenFitter>(true) == null)
            {
                return false;
            }
            var sample = prefab.transform.Find("sample");
            var background = sample != null ? sample.GetComponent<SpriteRenderer>() : null;
            if (background == null || background.sprite == null ||
                background.sprite.name != "ingame_bg")
                return false;
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                if (component == null) return false;
            return true;
        }

        private static Sprite PrepareMusicJacket()
        {
            var importer = AssetImporter.GetAtPath(MusicJacketPath) as TextureImporter;
            if (importer == null)
                throw new FileNotFoundException(
                    "Music 1 jacket is missing; run tools/sync_music_jacket.py");
            var changed = importer.textureType != TextureImporterType.Sprite ||
                          importer.spriteImportMode != SpriteImportMode.Single;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            if (changed) importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MusicJacketPath);
            if (sprite == null || sprite.texture == null ||
                sprite.texture.width != 800 || sprite.texture.height != 800)
                throw new InvalidDataException("Music 1 jacket import is not an 800x800 Sprite");
            return sprite;
        }

        private static bool ValidateMusicJacket(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null ||
                sprite.texture.width != 800 || sprite.texture.height != 800)
                return false;
            var background = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/GameBackground.prefab");
            var result = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/OrdinarySoloGameResult.prefab");
            return background != null && result != null &&
                   background.GetComponentInChildren<
                       Sirius.Game.GameBackgroundJacket>(true) != null &&
                   result.transform.Find("LeftPanel/MusicInfoPanel/FocusMask/JacketImage") != null;
        }

        private static bool ValidateBoundaryPerformances()
        {
            var introduction =
                AssetDatabase.LoadAssetAtPath<GameObject>(GameIntroductionPath);
            var clear =
                AssetDatabase.LoadAssetAtPath<GameObject>(ClearAnimationPath);
            if (introduction == null || clear == null ||
                introduction.GetComponent<
                    Sirius.Game.GameIntroductionAnimationController>() == null ||
                introduction.GetComponent<
                    Sirius.Animations.AnimationExitTrigger>() == null ||
                introduction.GetComponent<Sirius.EffectSePlayer>() == null ||
                clear.GetComponent<Sirius.Game.GameResultPanel>() == null)
                return false;
            if (CountMissingScripts(introduction.transform) != 0 ||
                CountMissingScripts(clear.transform) != 0)
                return false;
            var introductionSprites = introduction
                .GetComponentsInChildren<Image>(true)
                .Count(image => image.sprite != null);
            var clearSprites = clear.GetComponentsInChildren<Image>(true)
                .Count(image => image.sprite != null);
            var introductionText = File.ReadAllText(
                Path.GetFullPath(GameIntroductionPath));
            var clearText = File.ReadAllText(
                Path.GetFullPath(ClearAnimationPath));
            const string additiveShaderPath =
                "Assets/Resources/Shader/UI_Additive.shader";
            const string alphaShaderPath =
                "Assets/Resources/Shader/UIParticle_AlphaBlend.shader";
            const string ellipseShaderPath =
                "Assets/Resources/Shader/UI_EllipseAddtive.shader";
            const string grayscaleShaderPath =
                "Assets/Resources/Shader/OMSB_ImageGrayscale.shader";
            var additiveShader = AssetDatabase.LoadAssetAtPath<Shader>(
                additiveShaderPath);
            var alphaShader = AssetDatabase.LoadAssetAtPath<Shader>(
                alphaShaderPath);
            var ellipseShader = AssetDatabase.LoadAssetAtPath<Shader>(
                ellipseShaderPath);
            var grayscaleShader = AssetDatabase.LoadAssetAtPath<Shader>(
                grayscaleShaderPath);
            var additiveShaderText = File.ReadAllText(
                Path.GetFullPath(additiveShaderPath));
            var alphaShaderText = File.ReadAllText(
                Path.GetFullPath(alphaShaderPath));
            var ellipseShaderText = File.ReadAllText(
                Path.GetFullPath(ellipseShaderPath));
            var grayscaleShaderText = File.ReadAllText(
                Path.GetFullPath(grayscaleShaderPath));
            return introductionSprites >= 80 && clearSprites >= 60 &&
                   !introductionText.Contains("deadbeef") &&
                   !clearText.Contains("deadbeef") &&
                   additiveShader != null && additiveShader.isSupported &&
                   additiveShader.name == "UI/Additive" &&
                   alphaShader != null && alphaShader.isSupported &&
                   alphaShader.name == "UIParticle/AlphaBlend" &&
                   ellipseShader != null && ellipseShader.isSupported &&
                   ellipseShader.name == "UI/EllipseAddtive" &&
                   !additiveShaderText.Contains("DummyShaderTextExporter") &&
                   additiveShaderText.Contains("Blend One One") &&
                   !alphaShaderText.Contains("DummyShaderTextExporter") &&
                   alphaShaderText.Contains(
                       "Blend SrcAlpha OneMinusSrcAlpha") &&
                   !ellipseShaderText.Contains("DummyShaderTextExporter") &&
                   ellipseShaderText.Contains("Blend SrcAlpha One") &&
                   grayscaleShader != null && grayscaleShader.isSupported &&
                   grayscaleShader.name == "OMSB/ImageGrayscale" &&
                   !grayscaleShaderText.Contains("DummyShaderTextExporter") &&
                   grayscaleShaderText.Contains(
                       "Blend One OneMinusSrcAlpha") &&
                   grayscaleShaderText.Contains(
                       "fixed3(0.30, 0.59, 0.11)") &&
                   Mathf.Approximately(
                       Sirius.Game.GameIntroductionAnimationController
                           .GetAuthoredDuration(false),
                       6.0833335f) &&
                   Mathf.Approximately(
                       Sirius.Game.GameIntroductionAnimationController
                           .GetAuthoredDuration(true),
                       15.083333f) &&
                   Sirius.Game.GameResultPanel.GetTrigger(
                       Sirius.Game.RecoveredBoundaryClearType.Clear) ==
                       "ToClear" &&
                   Sirius.Game.GameResultPanel.GetTrigger(
                       Sirius.Game.RecoveredBoundaryClearType.FullCombo) ==
                       "ToFullCombo" &&
                   Sirius.Game.GameResultPanel.GetTrigger(
                       Sirius.Game.RecoveredBoundaryClearType.AllPerfect) ==
                       "ToAllPerfect" &&
                   Sirius.Game.GameResultPanel.GetTrigger(
                       Sirius.Game.RecoveredBoundaryClearType.Failed) ==
                       "ToFailed" &&
                   Mathf.Approximately(
                       Sirius.Game.GameResultPanel.GetAuthoredDuration(
                           Sirius.Game.RecoveredBoundaryClearType.Failed),
                       4.733333f) &&
                   Mathf.Approximately(
                       Sirius.Game.GameResultPanel.GetAuthoredDuration(
                           Sirius.Game.RecoveredBoundaryClearType.Clear),
                       6.45f) &&
                   Mathf.Approximately(
                       Sirius.Game.GameResultPanel.GetAuthoredDuration(
                           Sirius.Game.RecoveredBoundaryClearType.AllPerfect),
                       9.5f);
        }

        private static int CountMissingScripts(Transform root)
        {
            var count = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                count += GameObjectUtility
                    .GetMonoBehavioursWithMissingScriptCount(
                        transform.gameObject);
            return count;
        }

        private static void PrepareGameResultCurtainSpine()
        {
            const string atlasTextPath = ResultCurtainRoot + "/curtain_Albedo.atlas_0.txt";
            const string skeletonPath = ResultCurtainRoot + "/curtain_Albedo.skel_0.bytes";
            const string texturePath = ResultCurtainRoot + "/curtain_Albedo.png";
            const string materialPath = ResultCurtainRoot + "/curtain_Albedo_Spine.mat";
            const string spineShaderPath =
                "Assets/Spine/Runtime/spine-unity/Shaders/Spine-Skeleton.shader";
            const string atlasAssetPath = ResultCurtainRoot + "/curtain_Albedo_Atlas.asset";
            const string skeletonAssetPath = ResultCurtainRoot + "/curtain_Albedo_SkeletonData.asset";
            const string prefabPath =
                "Assets/Resources/Prefabs/GameResultCurtainBackground.prefab";

            var atlasText = AssetDatabase.LoadAssetAtPath<TextAsset>(atlasTextPath);
            var skeletonText = AssetDatabase.LoadAssetAtPath<TextAsset>(skeletonPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (atlasText == null || skeletonText == null || texture == null ||
                texture.width != 2048 || texture.height != 2048)
                throw new FileNotFoundException(
                    "GameResult curtain Spine inputs are missing; rerun " +
                    "tools/sync_game_result_background.py");

            // The original Spine material consumes premultiplied alpha
            // (_StraightAlphaInput=0). Unity's Alpha Is Transparency importer
            // processing is only appropriate for straight-alpha textures and
            // makes spine-unity warn on every editor startup.
            var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (textureImporter == null)
                throw new InvalidOperationException("Curtain texture importer is missing");
            if (textureImporter.alphaIsTransparency)
            {
                textureImporter.alphaIsTransparency = false;
                textureImporter.SaveAndReimport();
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(spineShaderPath);
            if (shader == null || shader.name != "Spine/Skeleton")
                throw new InvalidOperationException(
                    "Installed Spine/Skeleton shader asset is missing");
            if (material == null)
            {
                material = new Material(shader) { name = "curtain_Albedo" };
                material.mainTexture = texture;
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.name = "curtain_Albedo";
                material.shader = shader;
                material.mainTexture = texture;
                EditorUtility.SetDirty(material);
            }

            var atlasAsset = AssetDatabase.LoadAssetAtPath<Spine.Unity.SpineAtlasAsset>(
                atlasAssetPath);
            if (atlasAsset == null)
            {
                atlasAsset = ScriptableObject.CreateInstance<Spine.Unity.SpineAtlasAsset>();
                atlasAsset.name = "curtain_Albedo_Atlas";
                AssetDatabase.CreateAsset(atlasAsset, atlasAssetPath);
            }
            atlasAsset.atlasFile = atlasText;
            atlasAsset.materials = new[] { material };
            atlasAsset.Clear();
            EditorUtility.SetDirty(atlasAsset);

            var skeletonAsset = AssetDatabase.LoadAssetAtPath<Spine.Unity.SkeletonDataAsset>(
                skeletonAssetPath);
            if (skeletonAsset == null)
            {
                skeletonAsset = ScriptableObject.CreateInstance<Spine.Unity.SkeletonDataAsset>();
                skeletonAsset.name = "curtain_Albedo_SkeletonData";
                AssetDatabase.CreateAsset(skeletonAsset, skeletonAssetPath);
            }
            skeletonAsset.skeletonJSON = skeletonText;
            skeletonAsset.atlasAssets = new Spine.Unity.AtlasAssetBase[] { atlasAsset };
            skeletonAsset.scale = 0.01f;
            skeletonAsset.Clear();
            if (skeletonAsset.GetSkeletonData(false) == null)
                throw new InvalidDataException("Original curtain_result Spine binary cannot be read");
            EditorUtility.SetDirty(skeletonAsset);

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var skeleton = root.GetComponentInChildren<Spine.Unity.SkeletonMecanim>(true);
                if (skeleton == null)
                    throw new InvalidOperationException("Curtain SkeletonMecanim is missing");
                skeleton.skeletonDataAsset = skeletonAsset;
                skeleton.Initialize(true, false);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        private static void BuildGameBackgroundPrefab()
        {
            const string prefabPath =
                "Assets/Resources/Prefabs/GameBackground.prefab";
            const string backgroundSpritePath =
                "Assets/Resources/Sprite/ingame_bg.asset";
            const string shadowSpritePath =
                "Assets/Resources/Sprite/img_game_common_bg_shadow.asset";
            const string controllerPath =
                "Assets/Resources/AnimatorController/NoLifeBGShadow_animator.controller";
            var backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(backgroundSpritePath);
            var shadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(shadowSpritePath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (backgroundSprite == null || shadowSprite == null || controller == null)
                throw new FileNotFoundException(
                    "Game background dependencies are missing; run tools/sync_game_background.py");

            var root = new GameObject("Background");
            try
            {
                root.layer = 18;

                var sample = CreateBackgroundChild(root.transform, "sample", 18,
                    new Vector3(0f, 0f, 12f), Vector3.one);
                var sampleRenderer = sample.AddComponent<SpriteRenderer>();
                sampleRenderer.sprite = backgroundSprite;
                // Source child order 0 inside SortingGroup -10, folded to the
                // same effective order for the extracted standalone hierarchy.
                sampleRenderer.sortingOrder = -10;
                sample.AddComponent<Sirius.Screens.SpriteRendererScreenFitter>();

                var jacket = CreateBackgroundChild(sample.transform, "Jacket", 18,
                    new Vector3(0f, 2f, 0f), new Vector3(0.6f, 0.6f, 1f));
                var jacketRenderer = jacket.AddComponent<SpriteRenderer>();
                jacketRenderer.sortingOrder = -11;
                jacket.AddComponent<Sirius.Game.GameBackgroundJacket>();

                var shadow = CreateBackgroundChild(root.transform, "BGShadow", 18,
                    Vector3.zero, Vector3.one);
                CreateBackgroundShadow(shadow.transform, "TopLeft", shadowSprite,
                    new Vector3(1.21875f, 1.33333f, 1f), SpriteSortPoint.Pivot);
                CreateBackgroundShadow(shadow.transform, "TopRight", shadowSprite,
                    new Vector3(-1.21875f, 1.33333f, 1f), SpriteSortPoint.Center);
                CreateBackgroundShadow(shadow.transform, "BottomLeft", shadowSprite,
                    new Vector3(1.21875f, -1.33333f, 1f), SpriteSortPoint.Center);
                CreateBackgroundShadow(shadow.transform, "BottomRight", shadowSprite,
                    new Vector3(-1.21875f, -1.33333f, 1f), SpriteSortPoint.Center);

                root.AddComponent<Sirius.GameBackgroundShadow>();
                root.AddComponent<Sirius.Game.GameBackgroundBrightnessChanger>();
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                var sorting = root.AddComponent<UnityEngine.Rendering.SortingGroup>();
                sorting.sortingOrder = -10;

                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (saved == null)
                    throw new InvalidOperationException("Failed to save GameBackground prefab.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            NormalizeGeneratedYaml(prefabPath, 3287776065410756070L);
        }

        private static void NormalizeGeneratedYaml(
            string assetPath,
            long stableWarningFileId = 0L)
        {
            var fullPath = Path.GetFullPath(assetPath);
            var text = File.ReadAllText(fullPath);
            text = Regex.Replace(text, @"[ \t]+(?=\r?$)", string.Empty,
                RegexOptions.Multiline);
            if (stableWarningFileId != 0L)
            {
                text = Regex.Replace(
                    text,
                    @"--- !u!1029 &-?\d+(?=\r?\nDefaultAsset:)",
                    $"--- !u!1029 &{stableWarningFileId}");
            }
            File.WriteAllText(fullPath, text);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static GameObject CreateBackgroundChild(
            Transform parent, string name, int layer, Vector3 position, Vector3 scale)
        {
            var child = new GameObject(name);
            child.layer = layer;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = scale;
            return child;
        }

        private static void CreateBackgroundShadow(
            Transform parent, string name, Sprite sprite, Vector3 scale,
            SpriteSortPoint sortPoint)
        {
            var child = CreateBackgroundChild(
                parent, name, 18, new Vector3(0f, 0f, 12f), scale);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -9;
            renderer.spriteSortPoint = sortPoint;
        }

        private static bool ValidateGameResultRuntime()
        {
            var rateNotation = new[]
            {
                new RecoveredNotationNote
                {
                    Id = 1, StartTickCount = 1f, NoteType =
                        (int)RecoveredNoteType.Normal, Lane = 4, Width = 1,
                },
                new RecoveredNotationNote
                {
                    Id = 2, StartTickCount = 2f, NoteType =
                        (int)RecoveredNoteType.Normal, Lane = 5, Width = 1,
                },
            };
            var rateResult = new RecoveredGameResultRuntime(rateNotation);
            var addInitial =
                rateResult.GetDisplayedAchievementRate(2);
            var subtractInitial =
                rateResult.GetDisplayedAchievementRate(1);
            rateResult.Collect(RecoveredInputResultEntity.Create(
                rateNotation[0], new RecoveredTimingDecision(
                    RecoveredTimingType.Perfect,
                    RecoveredTimingAssistType.None, 0)));
            var addAfterPerfect =
                rateResult.GetDisplayedAchievementRate(2);
            var subtractAfterPerfect =
                rateResult.GetDisplayedAchievementRate(1);
            rateResult.Collect(RecoveredInputResultEntity.Create(
                rateNotation[1], new RecoveredTimingDecision(
                    RecoveredTimingType.PerfectStar,
                    RecoveredTimingAssistType.None, 0)));
            var addCompleted =
                rateResult.GetDisplayedAchievementRate(2);
            var subtractCompleted =
                rateResult.GetDisplayedAchievementRate(1);

            var notation = new[]
            {
                new RecoveredNotationNote
                {
                    Id = 10, StartTickCount = 1f, NoteType =
                        (int)RecoveredNoteType.Sound, Lane = 4, Width = 1,
                },
                new RecoveredNotationNote
                {
                    Id = 11, StartTickCount = 1f, NoteType =
                        (int)RecoveredNoteType.HoldEighth, Lane = 4, Width = 1,
                },
                new RecoveredNotationNote
                {
                    Id = 12, StartTickCount = 2f, NoteType =
                        (int)RecoveredNoteType.Normal, Lane = 5, Width = 1,
                },
                new RecoveredNotationNote
                {
                    Id = 13, StartTickCount = 3f, NoteType =
                        (int)RecoveredNoteType.Critical, Lane = 6, Width = 1,
                },
            };
            var result = new RecoveredGameResultRuntime(notation);
            result.Collect(RecoveredInputResultEntity.Create(
                notation[0], new RecoveredTimingDecision(
                    RecoveredTimingType.PerfectStar,
                    RecoveredTimingAssistType.None, 0)));
            result.Collect(RecoveredInputResultEntity.Create(
                notation[1], new RecoveredTimingDecision(
                    RecoveredTimingType.PerfectStar,
                    RecoveredTimingAssistType.None, 0)));
            result.Collect(RecoveredInputResultEntity.Create(
                notation[2], new RecoveredTimingDecision(
                    RecoveredTimingType.PerfectStar,
                    RecoveredTimingAssistType.Slow, 1)));
            result.Collect(RecoveredInputResultEntity.Create(
                notation[3], new RecoveredTimingDecision(
                    RecoveredTimingType.Great,
                    RecoveredTimingAssistType.Fast, -1)));

            var fullComboBeforeGood = result.IsFullCombo &&
                                      !result.IsAllPerfect &&
                                      result.Count == 3 &&
                                      result.MaxCombo == 3 &&
                                      !result.IsPerfectStar &&
                                      Math.Abs(result.AchievementRate -
                                               (101d + 101d + 80d) / 3d) < 0.000001d;
            result.Collect(RecoveredInputResultEntity.Create(
                notation[2], new RecoveredTimingDecision(
                    RecoveredTimingType.Good,
                    RecoveredTimingAssistType.None, 0)));
            var score = new RecoveredSoloScoreRuntime(1.25d, 8000, 20, 4);
            var firstScore = score.Collect(RecoveredTimingType.PerfectStar);
            score.Collect(RecoveredTimingType.Perfect);
            score.Collect(RecoveredTimingType.Great);
            var finalScore = score.Collect(RecoveredTimingType.Good);
            var expectedMaxScore = (long)(1.25d * (8000 * 10) * 1.2d);
            var idleRuntime = new RecoveredInputHandlerRuntime(
                rateNotation,
                0f,
                note => new Vector2(note.Lane, 0f),
                position => default);
            idleRuntime.TickPlayer(2200L, 2200L,
                Array.Empty<RecoveredInputEntity>());
            var idleResult = new RecoveredGameResultRuntime(rateNotation);
            foreach (var inputResult in idleRuntime.InputResults)
                idleResult.Collect(inputResult);
            var hiddenPerfectStarView =
                Sirius.GameResult.RecoveredGameResultViewData.FromRuntime(
                    rateResult, 1.5d, 5d, false,
                    shouldShowPerfectStar: false);
            var graphFixture = new Dictionary<int, int>
            {
                { 2, 1 }, { -1, 2 }, { 0, 3 }, { -3, 4 }, { 1, 5 },
            };
            var positiveBuckets =
                Sirius.GameResult.TimingGraphPanel.OrderPositiveBuckets(
                    graphFixture);
            var negativeBuckets =
                Sirius.GameResult.TimingGraphPanel.OrderNegativeBuckets(
                    graphFixture);

            return rateResult.AchievementRateNoteCount == 2 &&
                   Math.Abs(addInitial) < 0.000001d &&
                   Math.Abs(subtractInitial - 101d) < 0.000001d &&
                   Math.Abs(addAfterPerfect - 50d) < 0.000001d &&
                   Math.Abs(subtractAfterPerfect - 100.5d) < 0.000001d &&
                   Math.Abs(addCompleted - 100.5d) < 0.000001d &&
                   Math.Abs(subtractCompleted - 100.5d) < 0.000001d &&
                   result.DuplicatedSoundNoteCount == 1 &&
                   result.IsIgnoredDuplicateNote(10) &&
                   result.TimingCounts[RecoveredTimingType.PerfectStar] == 2 &&
                   result.TimingCounts[RecoveredTimingType.Great] == 1 &&
                   result.TimingCounts[RecoveredTimingType.Good] == 1 &&
                   result.InputDiffSummary.Count == 50 &&
                   !result.InputDiffSummary.ContainsKey(0) &&
                   result.InputDiffSummary[-25] == 0 &&
                   result.InputDiffSummary[25] == 0 &&
                   result.InputDiffSummary[1] == 1 &&
                   result.InputDiffSummary[-1] == 1 &&
                   Math.Abs(result.AchievementRate - 83d) < 0.000001d &&
                   Math.Abs(result.CalculateRecommendationTiming(0.25d, 5d) -
                            0.25d) < 0.000001d &&
                   Math.Abs(RecoveredGameResultRuntime.CalculateRecommendationTiming(
                                new Dictionary<int, int> { { 0, 1 } }, 0d, 5d) +
                            0.15d) < 0.000001d &&
                   Math.Abs(RecoveredGameResultRuntime.CalculateRecommendationTiming(
                                new Dictionary<int, int> { { 100, 1 } }, 1d, 5d) -
                            (-2d)) < 0.000001d &&
                   score.MaxScore == expectedMaxScore &&
                   score.BaseRate == 80000 &&
                   Math.Abs(score.BuffRate - 1.2d) < 0.000001d &&
                   firstScore == (long)decimal.Floor(
                       expectedMaxScore *
                       (decimal.Floor((1.01m / 4m) * 10000m) / 10000m)) &&
                   finalScore >= 0 &&
                   score.Count == (long)decimal.Floor(
                       expectedMaxScore *
                       (decimal.Floor(((1.01m + 1m + 0.8m + 0.5m) / 4m) *
                                      10000m) / 10000m)) &&
                   fullComboBeforeGood && result.Count == 0 &&
                   result.MaxCombo == 3 && result.FailedCount == 1 &&
                   !result.IsFullCombo && !result.IsAllPerfect &&
                   idleRuntime.InputResults.Count == 2 &&
                   idleRuntime.PublishedMissCount == 2 &&
                   idleResult.TimingCounts[RecoveredTimingType.Miss] == 2 &&
                   idleResult.CollectedCount == 2 &&
                   !idleResult.IsAllPerfect &&
                   !idleResult.IsFullCombo &&
                   !idleRuntime.InputResults[0].IsInput &&
                   hiddenPerfectStarView.GetDisplayedTimingCount(
                       RecoveredTimingType.Perfect) == 2 &&
                   Math.Abs(hiddenPerfectStarView.CurrentRecommendationTiming -
                            1.5d) < 0.000001d &&
                   !hiddenPerfectStarView.ShouldShowPerfectStar &&
                   positiveBuckets.Length == 2 &&
                   positiveBuckets[0].Key == 1 &&
                   positiveBuckets[1].Key == 2 &&
                   negativeBuckets.Length == 2 &&
                   negativeBuckets[0].Key == -3 &&
                   negativeBuckets[1].Key == -1;
        }

        private static bool ValidateLocalResultStore()
        {
            var root = Path.Combine(
                Path.GetTempPath(), "openwds-result-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new RecoveredLocalResultStore(root);
                var firstNew = store.RecordResult(
                    7,
                    RecoveredMusicDifficulty.Stella,
                    84.956d,
                    RecoveredClearLamp.Clear,
                    out var firstPrevious);
                var secondNew = store.RecordResult(
                    7,
                    RecoveredMusicDifficulty.Stella,
                    83d,
                    RecoveredClearLamp.AllPerfect,
                    out var secondPrevious);
                var reloaded = new RecoveredLocalResultStore(root);
                return firstNew &&
                       Math.Abs(firstPrevious) < 0.000001d &&
                       !secondNew &&
                       Math.Abs(secondPrevious - 84.956d) < 0.000001d &&
                       Math.Abs(reloaded.GetBest(
                           7, RecoveredMusicDifficulty.Stella) - 84.956d) <
                       0.000001d &&
                       reloaded.GetClearLamp(
                           7, RecoveredMusicDifficulty.Stella) ==
                       RecoveredClearLamp.AllPerfect &&
                       reloaded.GetClearLamp(
                           7, RecoveredMusicDifficulty.Hard) ==
                       RecoveredClearLamp.None;
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static bool ValidatePlayerUnit(
            TextAsset asset,
            out int cardCount,
            out int totalStatus,
            out int scoreNoteCount,
            out long maxScore)
        {
            cardCount = 0;
            totalStatus = 0;
            scoreNoteCount = 0;
            maxScore = 0;
            if (asset == null) return false;

            var fixture = RecoveredPlayerUnitFixture.Parse(asset);
            var chart = AssetDatabase.LoadAssetAtPath<TextAsset>(
                StellaChartTextAssetPath);
            if (chart == null) return false;
            var notation = RecoveredStandardNotation.Parse(chart.text);
            var resultRuntime = new RecoveredGameResultRuntime(notation);
            var noteIds = resultRuntime.GetScoreNoteIds(notation);
            var context = fixture.CreateScoreContext(noteIds);
            var score = new RecoveredSoloScoreRuntime(
                context.DifficultyAutoCoefficient,
                context.TotalStatus,
                context.BaseScorePercentage,
                context.TotalNotesCount);

            cardCount = fixture.cards.Length;
            totalStatus = fixture.totalStatus;
            scoreNoteCount = context.TotalNotesCount;
            maxScore = score.MaxScore;
            var life = new RecoveredLifeRuntime();
            var lifeChanged = life.Add(fixture.GetInitialLifeAddition());
            var senseScore = new RecoveredSenseScoreRuntime(fixture);
            var starActScore = new RecoveredStarActScoreRuntime(fixture);
            var principal = new RecoveredPrincipalRuntime();
            principal.Initialize(new[]
            {
                new RecoveredPrincipalUnit(
                    fixture.order, fixture.initialMaxPrincipal),
            });
            var principalAddingMap = new Dictionary<long, int>();
            foreach (var senseEvent in fixture.senseEvents)
                principalAddingMap[senseEvent.senseMasterId] =
                    senseEvent.acquirableGauge;
            senseScore.Tick(
                long.MaxValue,
                (senseEvent, addedScore, actorIndex) =>
                {
                    starActScore.OnSenseActivated(senseEvent);
                    principal.ActivateSense(
                        senseEvent.senseMasterId,
                        principalAddingMap,
                        fixture.order);
                });
            var commonValid = cardCount == 5 &&
                              Math.Abs(
                                  context.DifficultyAutoCoefficient - 1.05d) <
                              0.000001d &&
                              context.BaseScorePercentage == 0 &&
                              scoreNoteCount == 656 &&
                              resultRuntime.DuplicatedSoundNoteCount == 8 &&
                              fixture.senseNotationMasterId == 1 &&
                              fixture.senseEvents.Length == 8 &&
                              senseScore.ActivationCount == 8 &&
                              fixture.initialMaxPrincipal == 1000 &&
                              principal.GetMaxPrincipal(fixture.order) == 1000 &&
                              fixture.starActEvents.Length == 1 &&
                              starActScore.ActivationCount == 1 &&
                              RecoveredPartyStatusRuntime.CalculateCharacterStatus(
                                  53, 0, 0, 25, 0, 0, 0f) == 13 &&
                              RecoveredPartyStatusRuntime.CalculatePartySlotStatus(
                                  100, 10f, 3f, 20f, 4f) == 137;
            if (!commonValid) return false;

            if (fixture.profile == "starter")
            {
                return totalStatus == 294 && fixture.startEffects.Length == 1 &&
                       fixture.GetStarActSenseLightCount() == 3 &&
                       fixture.GetInitialLifeAddition() == 120 && lifeChanged &&
                       principal.GetCurrentPrincipal(fixture.order) == 0 &&
                       life.Value == 1120 && life.MaxValue == 1000 &&
                       maxScore == 3087L && senseScore.Count == 1228L &&
                       fixture.starActEvents[0].timingSeconds == 40 &&
                       fixture.starActEvents[0].triggeringSenseEventId == 1401 &&
                       fixture.starActEvents[0].scoreFactorPercent == 120 &&
                       fixture.starActEvents[0].branchId == 0 &&
                       starActScore.Count == 352L &&
                       starActScore.GetLightCount(1) == 0 &&
                       starActScore.GetLightCount(2) == 0 &&
                       starActScore.GetLightCount(3) == 2 &&
                       starActScore.GetLightCount(4) == 3;
            }

            if (fixture.profile == "high-star-storage")
            {
                var allFourStar = Array.TrueForAll(
                    fixture.cards, card => card.rarity == 4);
                return allFourStar && totalStatus == 533 &&
                       fixture.GetStarActSenseLightCount() == 7 &&
                       fixture.characterTotalStatus == 424 &&
                       fixture.startEffects.Length == 0 &&
                       fixture.GetInitialLifeAddition() == 0 && !lifeChanged &&
                       principal.GetCurrentPrincipal(fixture.order) == 0 &&
                       life.Value == 1000 && life.MaxValue == 1000 &&
                       maxScore == 5596L && senseScore.Count == 5167L &&
                       fixture.starActEvents[0].timingSeconds == 80 &&
                       fixture.starActEvents[0].triggeringSenseEventId == 1202 &&
                       fixture.starActEvents[0].scoreFactorPercent == 970 &&
                       fixture.starActEvents[0].storageLightCount == 2 &&
                       fixture.starActEvents[0].branchId == 14123102 &&
                       fixture.starActEvents[0].branchEffectMasterId == 19027 &&
                       fixture.starActEvents[0].additionalScoreFactorPercent == 700 &&
                       starActScore.Count == 8901L &&
                       starActScore.GetLightCount(1) == 1 &&
                       starActScore.GetLightCount(2) == 0 &&
                       starActScore.GetLightCount(3) == 0 &&
                       starActScore.GetLightCount(4) == 2;
            }

            if (fixture.profile == "principal-gauge")
            {
                var allFourStar = Array.TrueForAll(
                    fixture.cards, card => card.rarity == 4);
                return allFourStar && totalStatus == 533 &&
                       fixture.GetStarActSenseLightCount() == 7 &&
                       fixture.characterTotalStatus == 425 &&
                       fixture.startEffects.Length == 0 &&
                       fixture.GetInitialLifeAddition() == 0 && !lifeChanged &&
                       life.Value == 1000 && life.MaxValue == 1000 &&
                       maxScore == 5596L && senseScore.Count == 1556L &&
                       principal.GetCurrentPrincipal(fixture.order) == 840 &&
                       fixture.starActEvents[0].timingSeconds == 85 &&
                       fixture.starActEvents[0].triggeringSenseEventId == 1102 &&
                       fixture.starActEvents[0].scoreFactorPercent == 970 &&
                       fixture.starActEvents[0].storageLightCount == 1 &&
                       fixture.starActEvents[0].branchId == 0 &&
                       starActScore.Count == 5170L &&
                       starActScore.GetLightCount(1) == 1 &&
                       starActScore.GetLightCount(2) == 0 &&
                       starActScore.GetLightCount(3) == 0 &&
                       starActScore.GetLightCount(4) == 0;
            }

            return false;
        }

        private static bool ValidateGameResultPrefab(
            out int objectCount,
            out int uiParticleCount)
        {
            const string path =
                "Assets/Resources/Prefabs/OrdinarySoloGameResult.prefab";
            objectCount = 0;
            uiParticleCount = 0;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return false;

            objectCount = prefab.GetComponentsInChildren<Transform>(true).Length;
            uiParticleCount = prefab.GetComponentsInChildren<
                Coffee.UIExtensions.UIParticle>(true).Length;
            var components = prefab.GetComponentsInChildren<Component>(true);
            foreach (var component in components)
            {
                if (component == null) return false;
            }

            var source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "Resources/Prefabs/OrdinarySoloGameResult.prefab"));
            if (source.Contains("deadbeef") ||
                source.Contains("6c71a908b1ef91d472e0cb136cd2354c"))
            {
                return false;
            }

            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponentInChildren<
                    Sirius.GameResult.GameResultPanel>(true);
                var counts = new Dictionary<RecoveredTimingType, int>
                {
                    { RecoveredTimingType.Miss, 1 },
                    { RecoveredTimingType.Bad, 2 },
                    { RecoveredTimingType.Good, 3 },
                    { RecoveredTimingType.Great, 4 },
                    { RecoveredTimingType.Perfect, 5 },
                    { RecoveredTimingType.PerfectStar, 6 },
                };
                var summary = new Dictionary<int, int>
                {
                    { -6, 2 }, { 0, 4 }, { 7, 3 },
                };
                panel?.Initialize(new Sirius.GameResult.RecoveredGameResultViewData(
                    19, 87.25d, 91.5d, 0.1d, -0.2d, 5d,
                    false, true, false, false, counts, summary));
                var combo = panel != null
                    ? new SerializedObject(panel.ComboPanel)
                    : null;
                var comboText = combo?.FindProperty("_maxComboText")
                    ?.objectReferenceValue as UnityEngine.UI.Text;
                var badges = combo?.FindProperty("_badgeEffectObjects");
                var graph = panel != null
                    ? new SerializedObject(panel.TimingGraphPanel)
                    : null;
                var fast = graph?.FindProperty("_fast")
                    ?.objectReferenceValue as UnityEngine.UI.Text;
                var slow = graph?.FindProperty("_slow")
                    ?.objectReferenceValue as UnityEngine.UI.Text;
                var rate = panel != null
                    ? new SerializedObject(panel.RatePanel)
                    : null;
                var noRate = rate?.FindProperty("_noRateText")
                    ?.objectReferenceValue as UnityEngine.UI.Text;
                var notationLabel = rate?.FindProperty("_notationRateText")
                    ?.objectReferenceValue as UnityEngine.UI.Text;
                var playerLabel = rate?.FindProperty("_playerRateText")
                    ?.objectReferenceValue as UnityEngine.UI.Text;
                var notationRate = notationLabel != null
                    ? notationLabel.transform.parent?.parent
                        ?.Find("RatePanel/ThisTimeRate")
                        ?.GetComponent<UnityEngine.UI.Text>()
                    : null;
                var playerRate = playerLabel != null
                    ? playerLabel.transform.parent?.parent
                        ?.Find("RatePanel/ThisTimeRate")
                        ?.GetComponent<UnityEngine.UI.Text>()
                    : null;
                var allPerfectBadge = badges != null && badges.arraySize > 1
                    ? badges.GetArrayElementAtIndex(0).objectReferenceValue as GameObject
                    : null;
                var fullComboBadge = badges != null && badges.arraySize > 1
                    ? badges.GetArrayElementAtIndex(1).objectReferenceValue as GameObject
                    : null;
                var bindingValid = panel != null && comboText != null &&
                    comboText.text == "19" && badges != null &&
                    allPerfectBadge != null && !allPerfectBadge.activeSelf &&
                    fullComboBadge != null && fullComboBadge.activeSelf &&
                    fast != null && fast.text == "2" &&
                    slow != null && slow.text == "3" &&
                    noRate != null && !noRate.gameObject.activeSelf &&
                    notationRate != null && notationRate.gameObject.activeSelf &&
                    notationRate.text == "0.00" &&
                    playerRate != null && playerRate.gameObject.activeSelf &&
                    playerRate.text == "0.00";
                panel?.RatePanel.Initialize(
                    new Sirius.GameResult.RecoveredGameResultViewData(
                        19, 100.8d, 100.8d, 0.1d, -0.2d, 5d,
                        false, true, false, false, counts, summary,
                        bestEverNotationRate: 33.87d,
                        thisTimeNotationRate: 33.87d,
                        beforePlayerRate: 999.99d,
                        afterPlayerRate: 1000d,
                        isNewNotationRate: true,
                        isNewPlayerRate: true));
                var playerRateArrow = playerRate != null
                    ? playerRate.transform.parent?.Find("PreviousRate/Arrow")
                    : null;
                var playerPreviousRate = playerRate != null
                    ? playerRate.transform.parent?.Find("PreviousRate")
                        ?.GetComponent<UnityEngine.UI.Text>()
                    : null;
                var rateColorValid =
                    notationRate != null &&
                    notationRate.text == "33.87" &&
                    ((Color32)notationRate.color).Equals(
                        new Color32(83, 84, 94, 255)) &&
                    playerRate != null &&
                    playerRate.text == "1000.00" &&
                    ((Color32)playerRate.color).Equals(
                        new Color32(255, 131, 236, 255)) &&
                    playerRateArrow != null &&
                    playerRateArrow.gameObject.activeInHierarchy &&
                    playerPreviousRate != null &&
                    playerPreviousRate.gameObject.activeInHierarchy &&
                    playerPreviousRate.text == "999.99";
                Debug.Log(string.Format(
                    "OPENWDS_GAME_RESULT_RATE_DETAIL text={0} color={1} " +
                    "arrowFound={2} arrowActive={3}",
                    playerRate != null ? playerRate.text : "null",
                    playerRate != null
                        ? ((Color32)playerRate.color).ToString()
                        : "null",
                    playerRateArrow != null,
                    playerRateArrow != null &&
                    playerRateArrow.gameObject.activeSelf));

                var rootGroup = instance.GetComponent<CanvasGroup>();
                var resultNavigation = instance.transform.Find("RightBotton");
                var resultNext = resultNavigation?.Find("NextButton")
                    ?.GetComponent<Button>();
                var resultReplay = resultNavigation?.Find("InGameButton")
                    ?.GetComponent<Button>();
                var resultNavigationValid = resultNext != null &&
                    resultReplay != null &&
                    resultNavigation.Find("BackButton") == null &&
                    resultNext.GetComponentInChildren<Text>(true)?.text == "次へ" &&
                    resultReplay.GetComponentInChildren<Text>(true)?.text ==
                        "もう一度遊ぶ" &&
                    resultNext.transition == Selectable.Transition.Animation &&
                    resultReplay.transition == Selectable.Transition.Animation &&
                    resultNext.GetComponent<Animator>()?.runtimeAnimatorController
                        ?.name == "ButtonScale" &&
                    resultReplay.GetComponent<Animator>()?.runtimeAnimatorController
                        ?.name == "ButtonScale";
                var musicInfo = instance.transform.Find("LeftPanel/MusicInfoPanel");
                var autoMessage = instance.transform.Find(
                    "LeftPanel/GameResultPanel/AutoHiddenPanel")
                    ?.GetComponentInChildren<UnityEngine.UI.Text>(true);
                var numericFontsValid = true;
                foreach (var label in instance.GetComponentsInChildren<
                             TMPro.TextMeshProUGUI>(true))
                {
                    // The reported missing glyphs are the six four-digit timing
                    // counters. Decorative TMP labels may intentionally use a
                    // material preset with another texture and are out of scope.
                    if (!label.text.Contains("000")) continue;
                    if (label.font == null || !label.font.HasCharacter('0') ||
                        label.fontSharedMaterial == null ||
                        label.fontSharedMaterial.mainTexture != label.font.atlasTexture)
                    {
                        Debug.Log("OPENWDS_GAME_RESULT_FONT_FAIL " +
                                  label.transform.name + " text=" + label.text +
                                  " font=" + (label.font != null ? label.font.name : "null") +
                                  " material=" + (label.fontSharedMaterial != null
                                      ? label.fontSharedMaterial.name : "null"));
                        numericFontsValid = false;
                        break;
                    }
                }
                var countPageValid = panel != null &&
                    panel.CurrentPanelType == Sirius.GameResult.GameResultPanel.PanelType.TimingCount &&
                    panel.ComboPanel.GetComponent<CanvasGroup>().alpha == 1f &&
                    panel.AveragePanel.GetComponent<CanvasGroup>().alpha == 0f &&
                    panel.TimingGraphPanel.GetComponent<CanvasGroup>().alpha == 0f;
                panel?.ChangePanel();
                var graphPageValid = panel != null &&
                    panel.CurrentPanelType == Sirius.GameResult.GameResultPanel.PanelType.TimingGraph &&
                    panel.ComboPanel.GetComponent<CanvasGroup>().alpha == 0f &&
                    panel.AveragePanel.GetComponent<CanvasGroup>().alpha == 1f &&
                    panel.TimingGraphPanel.GetComponent<CanvasGroup>().alpha == 1f;
                panel?.Initialize(new Sirius.GameResult.RecoveredGameResultViewData(
                    19, 87.25d, 91.5d, 0.1d, -0.2d, 5d,
                    true, true, false, false, counts, summary));
                var panelSerialized = panel != null ? new SerializedObject(panel) : null;
                var resultCanvas = panelSerialized?.FindProperty("_resultPanelCanvasGroup")
                    ?.objectReferenceValue as CanvasGroup;
                var autoCanvas = panelSerialized?.FindProperty("_autoHiddenPanelCanvasGroup")
                    ?.objectReferenceValue as CanvasGroup;
                var autoPresentationValid = resultCanvas != null && autoCanvas != null &&
                                            resultCanvas.alpha == 0f &&
                                            autoCanvas.alpha == 1f;

                Debug.Log(string.Format(
                    "OPENWDS_GAME_RESULT binding={0} countPage={1} graphPage={2} " +
                    "autoPresentation={3} rootAlpha={4} musicInfo={5} " +
                    "autoMessage={6} fonts={7} rateColor={8} objects={9} " +
                    "uiParticles={10} navigation={11}",
                    bindingValid,
                    countPageValid,
                    graphPageValid,
                    autoPresentationValid,
                    rootGroup != null ? rootGroup.alpha : -1f,
                    musicInfo != null,
                    autoMessage != null,
                    numericFontsValid,
                    rateColorValid,
                    objectCount,
                    uiParticleCount,
                    resultNavigationValid));

                return bindingValid && countPageValid && graphPageValid &&
                       autoPresentationValid &&
                       rootGroup != null && rootGroup.alpha == 1f &&
                       musicInfo != null && autoMessage != null &&
                       autoMessage.text.Contains("オートプレイのため") &&
                       numericFontsValid && rateColorValid &&
                       resultNavigationValid &&
                       objectCount == 407 &&
                       uiParticleCount == 29 &&
                       prefab.GetComponentsInChildren<Sirius.GameResult.GameResultPanel>(
                           true).Length == 1 &&
                       prefab.GetComponentsInChildren<
                           Sirius.GameResult.GameResultAveragePanel>(true).Length == 1 &&
                       prefab.GetComponentsInChildren<Sirius.GameResult.InputResultPanel>(
                           true).Length == 1 &&
                       prefab.GetComponentsInChildren<
                           Sirius.GameResult.GameResultComboPanel>(true).Length == 1 &&
                       prefab.GetComponentsInChildren<Sirius.GameResult.TimingGraphPanel>(
                           true).Length == 1 &&
                       prefab.GetComponentsInChildren<
                           Sirius.GameResult.GameResultRatePanel>(true).Length == 1 &&
                       prefab.GetComponentsInChildren<
                           Sirius.GameResult.GameResultTimingPanel>(true).Length == 1;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static bool ValidateGameResultBackground()
        {
            const string path =
                "Assets/Resources/Prefabs/GameResultCurtainBackground.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null ||
                prefab.GetComponentsInChildren<Transform>(true).Length != 8 ||
                prefab.GetComponentsInChildren<Camera>(true).Length != 1 ||
                prefab.GetComponentsInChildren<ParticleSystem>(true).Length != 2 ||
                prefab.GetComponentsInChildren<MeshRenderer>(true).Length != 1)
            {
                return false;
            }
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                if (component == null) return false;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
                if (material == null || material.shader == null) return false;
            foreach (var particleRenderer in prefab.GetComponentsInChildren<
                         ParticleSystemRenderer>(true))
            {
                var particleMaterial = particleRenderer.sharedMaterial;
                if (particleMaterial == null || particleMaterial.shader == null ||
                    particleMaterial.shader.name != "UI/Additive" ||
                    particleMaterial.renderQueue != 3000)
                    return false;
            }
            if (prefab.GetComponentInChildren<Spine.Unity.SkeletonMecanim>(true) == null ||
                prefab.GetComponentInChildren<
                    UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(true) == null)
                return false;
            var authoredSkeleton = prefab.GetComponentInChildren<
                Spine.Unity.SkeletonMecanim>(true);
            if (authoredSkeleton.skeletonDataAsset == null ||
                authoredSkeleton.skeletonDataAsset.GetSkeletonData(false) == null)
                return false;
            var curtainData = authoredSkeleton.skeletonDataAsset.GetSkeletonData(false);
            var closeAnimation = curtainData.FindAnimation("close");
            if (closeAnimation == null)
                return false;
            var attachmentReport = new System.Text.StringBuilder();
            foreach (var skin in curtainData.Skins)
            foreach (var entry in skin.Attachments)
            {
                string attachmentPath = null;
                if (entry.Attachment is Spine.RegionAttachment regionAttachment)
                    attachmentPath = regionAttachment.Path;
                else if (entry.Attachment is Spine.MeshAttachment meshAttachment)
                    attachmentPath = meshAttachment.Path;
                if (attachmentPath != null)
                    attachmentReport.Append(skin.Name).Append(':')
                        .Append(entry.Name).Append("->").Append(attachmentPath)
                        .Append(',');
            }
            Debug.Log("OPENWDS_RESULT_CURTAIN_ATTACHMENTS " + attachmentReport);
            var poseSkeleton = new Spine.Skeleton(curtainData);
            var poseVertices = new float[2048];
            var poseReport = new System.Text.StringBuilder();
            var poseTimes = new[] { 0f, closeAnimation.Duration * 0.5f,
                closeAnimation.Duration };
            foreach (var poseTime in poseTimes)
            {
                poseSkeleton.SetToSetupPose();
                closeAnimation.Apply(poseSkeleton, 0f, poseTime, false, null, 1f,
                    Spine.MixBlend.Setup, Spine.MixDirection.In);
                poseSkeleton.UpdateWorldTransform();
                poseSkeleton.GetBounds(out var poseX, out var poseY,
                    out var poseWidth, out var poseHeight, ref poseVertices);
                poseReport.Append(" t=").Append(poseTime.ToString("0.###"))
                    .Append(" bounds=").Append(poseX.ToString("0.###"))
                    .Append(',').Append(poseY.ToString("0.###"))
                    .Append(',').Append(poseWidth.ToString("0.###"))
                    .Append(',').Append(poseHeight.ToString("0.###"));
            }
            Debug.Log("OPENWDS_RESULT_CURTAIN_POSES duration=" +
                      closeAnimation.Duration.ToString("0.###") + poseReport);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.SetActive(true);
                var camera = instance.GetComponentInChildren<Camera>(true);
                var skeleton = instance.GetComponentInChildren<
                    Spine.Unity.SkeletonMecanim>(true);
                skeleton?.Initialize(true, false);
                if (skeleton != null)
                {
                    skeleton.UpdateTiming = Spine.Unity.UpdateTiming.ManualUpdate;
                    skeleton.Skeleton.SetToSetupPose();
                    closeAnimation.Apply(skeleton.Skeleton, 0f,
                        closeAnimation.Duration, false, null, 1f,
                        Spine.MixBlend.Setup, Spine.MixDirection.In);
                    skeleton.Skeleton.UpdateWorldTransform();
                    var slotReport = new System.Text.StringBuilder();
                    foreach (var slot in skeleton.Skeleton.DrawOrder)
                    {
                        float[] world = null;
                        if (slot.Attachment is Spine.RegionAttachment region)
                        {
                            world = new float[8];
                            region.ComputeWorldVertices(slot, world, 0, 2);
                        }
                        else if (slot.Attachment is Spine.VertexAttachment vertex)
                        {
                            world = new float[vertex.WorldVerticesLength];
                            vertex.ComputeWorldVertices(slot, world);
                        }
                        if (world == null || world.Length < 2) continue;
                        var slotMinX = float.PositiveInfinity;
                        var slotMaxX = float.NegativeInfinity;
                        for (var value = 0; value + 1 < world.Length; value += 2)
                        {
                            slotMinX = Math.Min(slotMinX, world[value]);
                            slotMaxX = Math.Max(slotMaxX, world[value]);
                        }
                        slotReport.Append(slot.Data.Name).Append(':')
                            .Append(slot.Attachment.Name).Append('=')
                            .Append(slotMinX.ToString("0.###")).Append("..")
                            .Append(slotMaxX.ToString("0.###")).Append(',');
                    }
                    Debug.Log("OPENWDS_RESULT_CURTAIN_SLOTS " + slotReport);
                }
                skeleton?.LateUpdate();
                var mesh = skeleton != null
                    ? skeleton.GetComponent<MeshFilter>()?.sharedMesh
                    : null;
                if (!instance.activeInHierarchy || camera == null ||
                    !camera.enabled || !camera.gameObject.activeInHierarchy ||
                    skeleton == null || !skeleton.valid || mesh == null ||
                    mesh.vertexCount <= 0)
                    return false;

                var curtainRenderer = skeleton.GetComponent<MeshRenderer>();
                var curtainMaterial = curtainRenderer != null
                    ? curtainRenderer.sharedMaterial
                    : null;
                var meshUv = mesh.uv;
                var meshColors = mesh.colors32;
                var meshVertices = mesh.vertices;
                var minU = float.PositiveInfinity;
                var maxU = float.NegativeInfinity;
                foreach (var uv in meshUv)
                {
                    minU = Math.Min(minU, uv.x);
                    maxU = Math.Max(maxU, uv.x);
                }
                var minAlpha = 255;
                var maxAlpha = 0;
                var negativeRgb = 255;
                var positiveRgb = 255;
                var viewportVertices = 0;
                var minVertexX = float.PositiveInfinity;
                var maxVertexX = float.NegativeInfinity;
                var negativeMinU = float.PositiveInfinity;
                var negativeMaxU = float.NegativeInfinity;
                var positiveMinU = float.PositiveInfinity;
                var positiveMaxU = float.NegativeInfinity;
                var negativeMinV = float.PositiveInfinity;
                var negativeMaxV = float.NegativeInfinity;
                var positiveMinV = float.PositiveInfinity;
                var positiveMaxV = float.NegativeInfinity;
                for (var vertexIndex = 0; vertexIndex < meshVertices.Length;
                     vertexIndex++)
                {
                    var vertex = meshVertices[vertexIndex];
                    minVertexX = Math.Min(minVertexX, vertex.x);
                    maxVertexX = Math.Max(maxVertexX, vertex.x);
                    if (vertexIndex < meshUv.Length && vertex.x < 0f)
                    {
                        negativeMinU = Math.Min(negativeMinU, meshUv[vertexIndex].x);
                        negativeMaxU = Math.Max(negativeMaxU, meshUv[vertexIndex].x);
                        negativeMinV = Math.Min(negativeMinV, meshUv[vertexIndex].y);
                        negativeMaxV = Math.Max(negativeMaxV, meshUv[vertexIndex].y);
                    }
                    else if (vertexIndex < meshUv.Length)
                    {
                        positiveMinU = Math.Min(positiveMinU, meshUv[vertexIndex].x);
                        positiveMaxU = Math.Max(positiveMaxU, meshUv[vertexIndex].x);
                        positiveMinV = Math.Min(positiveMinV, meshUv[vertexIndex].y);
                        positiveMaxV = Math.Max(positiveMaxV, meshUv[vertexIndex].y);
                    }
                    var viewport = camera.WorldToViewportPoint(
                        skeleton.transform.TransformPoint(vertex));
                    if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f &&
                        viewport.y >= 0f && viewport.y <= 1f)
                        viewportVertices++;
                }
                foreach (var color in meshColors)
                {
                    minAlpha = Math.Min(minAlpha, color.a);
                    maxAlpha = Math.Max(maxAlpha, color.a);
                }
                for (var colorIndex = 0; colorIndex < meshColors.Length &&
                     colorIndex < meshVertices.Length; colorIndex++)
                {
                    var color = meshColors[colorIndex];
                    var minimum = Math.Min(color.r, Math.Min(color.g, color.b));
                    if (meshVertices[colorIndex].x < 0f)
                        negativeRgb = Math.Min(negativeRgb, minimum);
                    else
                        positiveRgb = Math.Min(positiveRgb, minimum);
                }
                Debug.Log("OPENWDS_RESULT_CURTAIN_MESH renderer=" +
                          (curtainRenderer != null && curtainRenderer.enabled) +
                          " material=" + (curtainMaterial != null
                              ? curtainMaterial.name : "null") +
                          " shader=" + (curtainMaterial != null &&
                              curtainMaterial.shader != null
                              ? curtainMaterial.shader.name : "null") +
                          " texture=" + (curtainMaterial != null &&
                              curtainMaterial.mainTexture != null
                              ? curtainMaterial.mainTexture.name : "null") +
                          " subMeshes=" + mesh.subMeshCount +
                          " bounds=" + curtainRenderer.bounds.center + "/" +
                          curtainRenderer.bounds.size +
                          " vertex0=" + (meshVertices.Length > 0
                              ? meshVertices[0].ToString() : "none") +
                          " viewport0=" + (meshVertices.Length > 0
                              ? camera.WorldToViewportPoint(
                                  skeleton.transform.TransformPoint(meshVertices[0])).ToString()
                              : "none") +
                          " vertexX=" + minVertexX.ToString("0.###") + ".." +
                          maxVertexX.ToString("0.###") +
                          " negativeU=" + negativeMinU.ToString("0.###") + ".." +
                          negativeMaxU.ToString("0.###") +
                          " negativeV=" + negativeMinV.ToString("0.###") + ".." +
                          negativeMaxV.ToString("0.###") +
                          " positiveU=" + positiveMinU.ToString("0.###") + ".." +
                          positiveMaxU.ToString("0.###") +
                          " positiveV=" + positiveMinV.ToString("0.###") + ".." +
                          positiveMaxV.ToString("0.###") +
                          " viewportVertices=" + viewportVertices +
                          " uv=" + minU.ToString("0.###") + ".." +
                          maxU.ToString("0.###") + " alpha=" + minAlpha +
                          ".." + maxAlpha + " layer=" + skeleton.gameObject.layer +
                          " rgbNegative=" + negativeRgb +
                          " rgbPositive=" + positiveRgb +
                          " culling=" + camera.cullingMask);

                // Validate the authored GameResult camera and z=-14 curtain as
                // a rendered pair. A mesh-only check missed the case where the
                // preview's later depth-0 game camera erased this depth-(-1)
                // camera's output.
                var target = new RenderTexture(256, 144, 24,
                    RenderTextureFormat.ARGB32);
                var pixels = new Texture2D(256, 144, TextureFormat.RGBA32, false);
                var previousTarget = camera.targetTexture;
                var previousActive = RenderTexture.active;
                var previousFlags = camera.clearFlags;
                var previousColor = camera.backgroundColor;
                try
                {
                    camera.targetTexture = target;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = Color.clear;
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 256, 144), 0, 0);
                    pixels.Apply(false, false);
                    var visiblePixels = 0;
                    var leftPixels = 0;
                    var rightPixels = 0;
                    var renderedPixels = pixels.GetPixels32();
                    for (var index = 0; index < renderedPixels.Length; index++)
                    {
                        var pixel = renderedPixels[index];
                        if (pixel.a > 2 && (pixel.r > 2 || pixel.g > 2 || pixel.b > 2))
                        {
                            visiblePixels++;
                            if (index % 256 < 128) leftPixels++;
                            else rightPixels++;
                        }
                    }
                    Debug.Log("OPENWDS_RESULT_CURTAIN_RENDER pixels=" +
                              visiblePixels + " left=" + leftPixels +
                              " right=" + rightPixels +
                              " cameraDepth=" + camera.depth +
                              " cameraZ=" + camera.transform.position.z +
                              " curtainZ=" + skeleton.transform.position.z +
                              " vertices=" + mesh.vertexCount);
                    return camera.depth == -1f &&
                           Math.Abs(camera.transform.position.z - 1.95f) < 0.001f &&
                           Math.Abs(skeleton.transform.position.z - (-14f)) < 0.001f &&
                           leftPixels > 100 && rightPixels > 100;
                }
                finally
                {
                    camera.targetTexture = previousTarget;
                    camera.clearFlags = previousFlags;
                    camera.backgroundColor = previousColor;
                    RenderTexture.active = previousActive;
                    UnityEngine.Object.DestroyImmediate(pixels);
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static bool CaptureGameResultPreview()
        {
            const int width = 1323;
            const int height = 721;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/OrdinarySoloGameResult.prefab");
            if (prefab == null) return false;
            var cameraObject = new GameObject("GameResultCaptureCamera", typeof(Camera));
            var captureCamera = cameraObject.GetComponent<Camera>();
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = Color.black;
            captureCamera.nearClipPlane = 0.1f;
            captureCamera.farClipPlane = 10f;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            captureCamera.targetTexture = target;
            var canvasObject = new GameObject(
                "GameResultCaptureCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = captureCamera;
            canvas.planeDistance = 1f;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var instance = UnityEngine.Object.Instantiate(prefab, canvasObject.transform);
            var instanceRect = instance.transform as RectTransform;
            if (instanceRect != null)
            {
                instanceRect.anchorMin = Vector2.zero;
                instanceRect.anchorMax = Vector2.one;
                instanceRect.offsetMin = Vector2.zero;
                instanceRect.offsetMax = Vector2.zero;
                instanceRect.localScale = Vector3.one;
            }
            var fonts = new Sirius.GameResult.RecoveredGameResultFontRuntime();
            var applied = fonts.Apply(instance);
            var fontBundlesValid = fonts.BundleCount == 5 &&
                                   fonts.FontCount == 2 && applied >= 14;
            Debug.Log(string.Format(
                "OPENWDS_RESULT_FONT_BUNDLES bundles={0} fonts={1} applied={2} assets={3}",
                fonts.BundleCount, fonts.FontCount, applied, fonts.AssetSummary));
            var panel = instance.GetComponentInChildren<
                Sirius.GameResult.GameResultPanel>(true);
            var counts = new Dictionary<RecoveredTimingType, int>
            {
                { RecoveredTimingType.PerfectStar, 1592 },
                { RecoveredTimingType.Perfect, 63 },
                { RecoveredTimingType.Great, 9 },
                { RecoveredTimingType.Good, 0 },
                { RecoveredTimingType.Bad, 0 },
                { RecoveredTimingType.Miss, 1 },
            };
            panel?.Initialize(new Sirius.GameResult.RecoveredGameResultViewData(
                1370, 100.7879d, 100.1393d, 0d, 0d, 5d,
                true, false, false, false, counts,
                new Dictionary<int, int>(), "ワナビスタ！",
                RecoveredMusicDifficulty.Stella));
            var slideAnimator = instance.GetComponent<Animator>();
            if (slideAnimator != null)
            {
                slideAnimator.Rebind();
                slideAnimator.Update(0f);
                slideAnimator.SetTrigger(Animator.StringToHash("Next"));
                // GameResult_left_in_anim ends at 1.9166666 seconds. Step the
                // original controller beyond that point to make this capture
                // exercise the same ShowAsync path as runtime.
                for (var frame = 0; frame < 120; frame++)
                    slideAnimator.Update(1f / 60f);
            }
            Canvas.ForceUpdateCanvases();
            var leftPanel = instance.transform.Find("LeftPanel") as RectTransform;
            var leftPanelLayoutValid = leftPanel != null &&
                Mathf.Abs(leftPanel.pivot.x - 0.5f) < 0.0001f &&
                Mathf.Abs(leftPanel.anchoredPosition.x - 477f) < 0.01f;
            Debug.Log(string.Format(
                "OPENWDS_RESULT_LAYOUT pivotX={0} positionX={1} animator={2} valid={3}",
                leftPanel != null ? leftPanel.pivot.x : -1f,
                leftPanel != null ? leftPanel.anchoredPosition.x : -1f,
                slideAnimator != null,
                leftPanelLayoutValid));
            LogGameResultNumericTextState(instance);
            captureCamera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            var projectRoot = Directory.GetParent(Application.dataPath).Parent.Parent.FullName;
            var output = Path.Combine(projectRoot, "reverse/reports/game-result-preview.png");
            File.WriteAllBytes(output, texture.EncodeToPNG());
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(texture);
            captureCamera.targetTexture = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            fonts.Dispose();
            UnityEngine.Object.DestroyImmediate(canvasObject);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            return fontBundlesValid && leftPanelLayoutValid;
        }

        private static void LogGameResultNumericTextState(GameObject instance)
        {
            var entries = new List<string>();
            foreach (var label in instance.GetComponentsInChildren<
                         TMPro.TextMeshProUGUI>(true))
            {
                if (label.text == null || !label.gameObject.name.Contains("Count")) continue;
                label.ForceMeshUpdate(true, true);
                var parentAlpha = 1f;
                for (var current = label.transform; current != null;
                     current = current.parent)
                {
                    var group = current.GetComponent<CanvasGroup>();
                    if (group != null) parentAlpha *= group.alpha;
                }
                var rect = label.rectTransform;
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                var textInfo = label.textInfo;
                var mesh = label.canvasRenderer != null
                    ? label.canvasRenderer.GetMesh() : null;
                entries.Add(string.Format(
                    "{0}[text={1},active={2},enabled={3},colorA={4:F2}," +
                    "canvasA={5:F2},rect=({6:F1},{7:F1},{8:F1},{9:F1})," +
                    "font={10},material={11},chars={12},verts={13}," +
                    "rgb=({14:F2},{15:F2},{16:F2}),cull={17}," +
                    "world=({18:F1},{19:F1})-({20:F1},{21:F1})," +
                    "meshBounds=({22:F1},{23:F1},{24:F1},{25:F1})," +
                    "materials={26},path={27}]",
                    label.gameObject.name,
                    label.text.Replace("<", "{").Replace(">", "}"),
                    label.gameObject.activeInHierarchy,
                    label.enabled,
                    label.color.a,
                    parentAlpha,
                    rect.anchoredPosition.x,
                    rect.anchoredPosition.y,
                    rect.rect.width,
                    rect.rect.height,
                    label.font != null ? label.font.name : "null",
                    label.fontSharedMaterial != null
                        ? label.fontSharedMaterial.name : "null",
                    textInfo != null ? textInfo.characterCount : -1,
                    mesh != null ? mesh.vertexCount : -1,
                    label.color.r,
                    label.color.g,
                    label.color.b,
                    label.canvasRenderer != null && label.canvasRenderer.cull,
                    corners[0].x,
                    corners[0].y,
                    corners[2].x,
                    corners[2].y,
                    mesh != null ? mesh.bounds.min.x : -1f,
                    mesh != null ? mesh.bounds.min.y : -1f,
                    mesh != null ? mesh.bounds.max.x : -1f,
                    mesh != null ? mesh.bounds.max.y : -1f,
                    label.canvasRenderer != null
                        ? label.canvasRenderer.materialCount : -1,
                    GetTransformPath(label.transform, instance.transform)));
            }
            Debug.Log("OPENWDS_RESULT_NUMERICS " + string.Join(";", entries.ToArray()));
        }

        private static string GetTransformPath(Transform transform, Transform root)
        {
            var names = new List<string>();
            for (var current = transform; current != null; current = current.parent)
            {
                names.Add(current.name + "#" + current.GetSiblingIndex());
                if (current == root) break;
            }
            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static bool ValidateSplitLaneBundlesInEditor()
        {
            var root = Path.Combine(
                Application.streamingAssetsPath, "OpenWDS/SplitLane/1.96.0");
            if (!Directory.Exists(root))
            {
                Debug.Log("OPENWDS_SPLIT_BUNDLES missing");
                return false;
            }

            var files = Directory.GetFiles(root, "*.bundle", SearchOption.AllDirectories);
            Array.Sort(files, (left, right) =>
            {
                var leftMain = left.Contains("game_splitlane_assets_spliteffects") ? 1 : 0;
                var rightMain = right.Contains("game_splitlane_assets_spliteffects") ? 1 : 0;
                var compare = leftMain.CompareTo(rightMain);
                return compare != 0 ? compare : string.CompareOrdinal(left, right);
            });
            var bundles = new List<AssetBundle>();
            var prefabs = new List<string>();
            var validPrefabs = 0;
            var splitPreviewCaptured = false;
            foreach (var file in files)
            {
                var bundle = AssetBundle.LoadFromFile(file);
                if (bundle == null) continue;
                bundles.Add(bundle);
                if (!file.Contains("game_splitlane_assets_spliteffects"))
                {
                    // Direct loading has no Addressables dependency provider.
                    // Materialize dependency assets before resolving cross-bundle
                    // PPtrs stored by the main SplitEffect prefab.
                    bundle.LoadAllAssets<UnityEngine.Object>();
                    continue;
                }
                foreach (var prefab in bundle.LoadAllAssets<GameObject>())
                {
                    var laneEffectController = UnityEngine.Object.FindObjectOfType<
                        Sirius.Game.LaneEffectController>();
                    var splitEffectParent = laneEffectController != null
                        ? laneEffectController.SplitEffectParent
                        : null;
                    var instance = splitEffectParent != null
                        ? UnityEngine.Object.Instantiate(
                            prefab, splitEffectParent, false)
                        : UnityEngine.Object.Instantiate(prefab);
                    var splitEffectParentValid = splitEffectParent != null &&
                        instance.transform.parent == splitEffectParent &&
                        splitEffectParent.name == "SplitEffectParent";
                    var controller = instance.GetComponentInChildren<
                        Sirius.Game.SplitEffectController>(true);
                    var elementSlots = -1;
                    var elementReferences = -1;
                    if (controller != null)
                    {
                        var serialized = new SerializedObject(controller);
                        var elements = serialized.FindProperty("_splitEffectElements");
                        if (elements != null && elements.isArray)
                        {
                            elementSlots = elements.arraySize;
                            elementReferences = 0;
                            for (var index = 0; index < elements.arraySize; index++)
                                if (elements.GetArrayElementAtIndex(index)
                                        .objectReferenceValue != null)
                                    elementReferences++;
                        }
                    }
                    var childCountBefore = instance.GetComponentsInChildren<
                        Transform>(true).Length;
                    if (controller != null)
                    {
                        var minimumOpacity =
                            RecoveredGameSettings.MinimumSplitEffectLineOpacity;
                        var maximumOpacity =
                            RecoveredGameSettings.MaximumSplitEffectLineOpacity;
                        controller.Initialize(
                            false, 100, in minimumOpacity, in maximumOpacity);
                        controller.OnFadeIn(3, 3);
                    }
                    var settingBehaviorValid = ValidateSplitEffectSettings(
                        prefab, splitEffectParent);
                    var compatibleShaderReplacements =
                        RecoveredSplitLaneAssetRuntime.ApplyCompatibleShaders(instance);
                    var spawnedChildren = instance.GetComponentsInChildren<
                        Transform>(true).Length - childCountBefore;
                    var missing = 0;
                    foreach (var component in instance.GetComponentsInChildren<Component>(true))
                        if (component == null) missing++;
                    var particles = instance.GetComponentsInChildren<
                        ParticleSystem>(true).Length;
                    var elementParentCount = 0;
                    foreach (var element in instance.GetComponentsInChildren<
                                 Sirius.Game.SplitEffectElement>(true))
                        if (element.transform.parent != null &&
                            element.transform.parent.GetComponent<SpriteRenderer>() != null)
                            elementParentCount++;
                    var elementParentsValid = elementParentCount == 7;
                    var splitPositionsValid = false;
                    var linePositions = new List<string>();
                    var spriteRenderers = instance.GetComponentsInChildren<
                        SpriteRenderer>(true);
                    if (spriteRenderers.Length == 7)
                    {
                        var expected = new[] { -5.55f, -1.85f, 1.85f, 5.55f };
                        splitPositionsValid = true;
                        for (var index = 0; index < spriteRenderers.Length; index++)
                        {
                            var x = spriteRenderers[index].transform.localPosition.x;
                            linePositions.Add(x.ToString("0.###",
                                System.Globalization.CultureInfo.InvariantCulture));
                            if (index < expected.Length &&
                                Mathf.Abs(x - expected[index]) > 0.0001f)
                                splitPositionsValid = false;
                        }
                    }
                    var unsupportedShaders = new HashSet<string>();
                    var lineShadersValid = true;
                    foreach (var renderer in instance.GetComponentsInChildren<
                                 Renderer>(true))
                    {
                        foreach (var material in renderer.sharedMaterials)
                        {
                            if (material == null || material.shader == null ||
                                material.shader.isSupported) continue;
                            unsupportedShaders.Add(material.shader.name);
                        }
                    }
                    foreach (var renderer in spriteRenderers)
                    {
                        var material = renderer.sharedMaterial;
                        if (material == null || material.shader == null ||
                            material.shader.name !=
                            "OpenWDS/Recovered/SplitEffect/SplitEffectSyuriken")
                            lineShadersValid = false;
                    }
                    if (!splitPreviewCaptured && prefab.name == "10170" &&
                        Camera.main != null && unsupportedShaders.Count == 0)
                    {
                        foreach (var animator in instance.GetComponentsInChildren<
                                     Animator>(true))
                            animator.Update(1f);
                        LogSplitLaneParticleState(instance, "afterPlay");
                        foreach (var particle in instance.GetComponentsInChildren<
                                     ParticleSystem>(true))
                            particle.Simulate(0.1f, false, false, true);
                        LogSplitLaneParticleState(instance, "afterSimulate");
                        splitPreviewCaptured = CaptureSplitLanePreview(
                            Camera.main, instance);
                    }
                    prefabs.Add(string.Format(
                        "{0}[anim={1},sprites={2},particles={3},missing={4}," +
                        "controller={5},slots={6},refs={7},spawned={8}," +
                        "positions={9},positionValid={10},shaderReplacements={11}," +
                        "elementParents={12},elementParentsValid={13}," +
                        "unsupportedShaders={14},splitParentValid={15}," +
                        "lineShadersValid={16},settingBehaviorValid={17}]",
                        prefab.name,
                        instance.GetComponentsInChildren<Animator>(true).Length,
                        instance.GetComponentsInChildren<SpriteRenderer>(true).Length,
                        particles,
                        missing,
                        controller != null,
                        elementSlots,
                        elementReferences,
                        spawnedChildren,
                        string.Join("/", linePositions.ToArray()),
                        splitPositionsValid,
                        compatibleShaderReplacements,
                        elementParentCount,
                        elementParentsValid,
                        string.Join("/", new List<string>(unsupportedShaders).ToArray()),
                        splitEffectParentValid,
                        lineShadersValid,
                        settingBehaviorValid));
                    if (instance.GetComponentsInChildren<Animator>(true).Length == 1 &&
                        instance.GetComponentsInChildren<SpriteRenderer>(true).Length == 7 &&
                        controller != null && elementSlots > 0 &&
                        elementReferences == elementSlots && spawnedChildren > 0 &&
                        splitPositionsValid && elementParentsValid &&
                        splitEffectParentValid && lineShadersValid && missing == 0 &&
                        unsupportedShaders.Count == 0 && settingBehaviorValid)
                        validPrefabs++;
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
            Debug.Log(string.Format(
                "OPENWDS_SPLIT_BUNDLES files={0} loaded={1} prefabs={2}",
                files.Length,
                bundles.Count,
                string.Join(",", prefabs.ToArray())));
            foreach (var bundle in bundles) bundle.Unload(false);
            return files.Length == 14 && bundles.Count == 14 && validPrefabs == 3 &&
                   splitPreviewCaptured;
        }

        private static bool ValidateSplitEffectSettings(
            GameObject prefab, Transform parent)
        {
            if (prefab == null || parent == null) return false;
            if (RecoveredSplitLaneAssetRuntime.IsLightSetting(
                    (int)RecoveredSpritEffectSettingType.Rich) ||
                RecoveredSplitLaneAssetRuntime.IsLightSetting(
                    (int)RecoveredSpritEffectSettingType.Normal) ||
                !RecoveredSplitLaneAssetRuntime.IsLightSetting(
                    (int)RecoveredSpritEffectSettingType.Light))
                return false;

            var normal = UnityEngine.Object.Instantiate(prefab, parent, false);
            var globalLight = UnityEngine.Object.Instantiate(prefab, parent, false);
            var dim = UnityEngine.Object.Instantiate(prefab, parent, false);
            try
            {
                var minimumOpacity =
                    RecoveredGameSettings.MinimumSplitEffectLineOpacity;
                var maximumOpacity =
                    RecoveredGameSettings.MaximumSplitEffectLineOpacity;
                var normalController = normal.GetComponent<
                    Sirius.Game.SplitEffectController>();
                var lightController = globalLight.GetComponent<
                    Sirius.Game.SplitEffectController>();
                var dimController = dim.GetComponent<
                    Sirius.Game.SplitEffectController>();
                if (normalController == null || lightController == null ||
                    dimController == null)
                    return false;

                normalController.Initialize(
                    false, 100, in minimumOpacity, in maximumOpacity);
                normalController.OnFadeIn(3, (int)RecoveredSplitLaneType.Full);
                var fullParticles = CountPlayingParticles(normal);
                normalController.Clear();
                normalController.OnFadeIn(3, (int)RecoveredSplitLaneType.BothEnds);
                var bothEndParticles = CountPlayingParticles(normal);
                normalController.Clear();
                normalController.OnFadeIn(3, (int)RecoveredSplitLaneType.Light);
                var chartLightParticles = CountPlayingParticles(normal);
                normalController.Clear();
                normalController.OnFadeIn(3, (int)RecoveredSplitLaneType.Ignore);
                var ignoreParticles = CountPlayingParticles(normal);

                lightController.Initialize(
                    true, 100, in minimumOpacity, in maximumOpacity);
                lightController.OnFadeIn(3, (int)RecoveredSplitLaneType.Full);
                var globalLightParticles = CountPlayingParticles(globalLight);

                dimController.Initialize(
                    false, 10, in minimumOpacity, in maximumOpacity);
                var opacityValid = true;
                var dimElements = dim.GetComponentsInChildren<
                    Sirius.Game.SplitEffectElement>(true);
                var checkedLines = 0;
                foreach (var element in dimElements)
                {
                    if (element.transform.parent == null) continue;
                    var line = element.transform.parent.GetComponent<SpriteRenderer>();
                    if (line == null) continue;
                    var source = element.LineColor;
                    var actual = line.color;
                    opacityValid &= Mathf.Approximately(actual.r, source.r * 0.1f) &&
                                    Mathf.Approximately(actual.g, source.g * 0.1f) &&
                                    Mathf.Approximately(actual.b, source.b * 0.1f) &&
                                    Mathf.Approximately(actual.a, source.a);
                    checkedLines++;
                }

                return fullParticles > 0 && bothEndParticles > 0 &&
                       bothEndParticles < fullParticles &&
                       chartLightParticles == 0 && ignoreParticles == 0 &&
                       globalLightParticles == 0 && checkedLines == 7 &&
                       opacityValid;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(normal);
                UnityEngine.Object.DestroyImmediate(globalLight);
                UnityEngine.Object.DestroyImmediate(dim);
            }
        }

        private static int CountPlayingParticles(GameObject root)
        {
            var count = 0;
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
                if (particle.isPlaying) count++;
            return count;
        }

        private static void LogSplitLaneParticleState(
            GameObject instance, string phase)
        {
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            var active = 0;
            var playing = 0;
            var particles = 0;
            var enabledRenderers = 0;
            var materialStates = new HashSet<string>();
            foreach (var system in systems)
            {
                if (system.gameObject.activeInHierarchy) active++;
                if (system.isPlaying) playing++;
                particles += system.particleCount;
                var renderer = system.GetComponent<ParticleSystemRenderer>();
                if (renderer == null) continue;
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                    enabledRenderers++;
                var material = renderer.sharedMaterial;
                var texture = material != null && material.HasProperty("_MainTex")
                    ? material.GetTexture("_MainTex") : null;
                materialStates.Add(string.Format(
                    "{0}:{1}:tex={2}:layer={3}",
                    material != null ? material.name : "null",
                    material != null && material.shader != null
                        ? material.shader.name : "null",
                    texture != null ? texture.name : "null",
                    renderer.gameObject.layer));
            }
            Debug.Log(string.Format(
                "OPENWDS_SPLIT_PARTICLES phase={0} systems={1} active={2} " +
                "playing={3} particles={4} enabledRenderers={5} materials={6}",
                phase, systems.Length, active, playing, particles, enabledRenderers,
                string.Join("/", new List<string>(materialStates).ToArray())));
        }

        private static bool CaptureSplitLanePreview(Camera camera, GameObject instance)
        {
            var target = new RenderTexture(
                PreviewWidth, PreviewHeight, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(
                PreviewWidth, PreviewHeight, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)PreviewWidth / PreviewHeight;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(
                    new Rect(0, 0, PreviewWidth, PreviewHeight), 0, 0);
                image.Apply();
                var outputPath = Path.Combine(
                    GetWorkspaceRoot(), "reverse", "reports",
                    "split-lane-preview.png");
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                var png = image.EncodeToPNG();
                File.WriteAllBytes(outputPath, png);
                Debug.Log(string.Format(
                    "OPENWDS_SPLIT_PREVIEW instance={0} bytes={1} output={2}",
                    instance.name, png.Length, outputPath));
                return png.Length > 0;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void AuditHoldEvents(
            IReadOnlyList<RecoveredInputEffectEntity> events,
            Dictionary<int, int> active,
            ref int starts,
            ref int ends)
        {
            foreach (var effect in events)
            {
                if (effect.EffectType == RecoveredInputEffectType.HoldStart)
                {
                    starts++;
                    active[effect.NoteId] = effect.LaneId;
                }
                else if (effect.EffectType == RecoveredInputEffectType.HoldEnd)
                {
                    ends++;
                    active.Remove(effect.NoteId);
                }
            }
        }

        private static bool ValidateStellaHoldSoundPipeline(
            Camera camera,
            Sirius.Game.LaneGroup laneGroup)
        {
            var notation = RecoveredStandardNotation.Parse(
                File.ReadAllText(StellaChartSourcePath));
            var managers = laneGroup.ColliderManagers;
            managers.InitializeMappings();
            Physics.SyncTransforms();
            var raycaster = new RecoveredLaneRaycaster(
                camera,
                managers.MainColliders,
                managers.SubLeftInnerColliders,
                managers.SubRightInnerColliders,
                managers.SubLeftOuterColliders,
                managers.SubRightOuterColliders);
            var runtime = new RecoveredInputHandlerRuntime(
                notation,
                3.019f,
                note =>
                {
                    var screen = camera.WorldToScreenPoint(
                        laneGroup.GetLaneCollider(note.Lane).position);
                    return new Vector2(screen.x, screen.y);
                },
                position =>
                    ResolveLaneAtScreenPosition(raycaster, position));
            var active = new Dictionary<int, int>();
            var starts = 0;
            var ends = 0;
            var soundResults = 0;
            var criticalResults = 0;
            var scratchResults = 0;
            var denseCriticalExpected = new HashSet<int>();
            var directScratchExpected = new HashSet<int>();
            var holdBodiesExpected = new HashSet<int>();
            var holdBodiesStarted = new HashSet<int>();
            var holdBodiesEnded = new HashSet<int>();
            var denseCriticalRaycast = new List<string>();
            foreach (var note in notation)
            {
                if (note.NoteType == (int)RecoveredNoteType.Hold ||
                    note.NoteType == (int)RecoveredNoteType.CriticalHold ||
                    note.NoteType == (int)RecoveredNoteType.ScratchHold ||
                    note.NoteType == (int)RecoveredNoteType.ScratchCriticalHold)
                {
                    holdBodiesExpected.Add(note.Id);
                }
                if (note.NoteType == (int)RecoveredNoteType.Critical &&
                    note.StartMilliseconds >= 23898 &&
                    note.StartMilliseconds <= 24407)
                {
                    denseCriticalExpected.Add(note.Id);
                    var screen = camera.WorldToScreenPoint(
                        laneGroup.GetLaneCollider(note.Lane).position);
                    var hit = ResolveLaneAtScreenPosition(
                        raycaster, new Vector2(screen.x, screen.y));
                    denseCriticalRaycast.Add(
                        note.Id + ":" + note.Lane + "-" + note.EndLane +
                        "->" + hit.HitMainLaneId + "/" +
                        hit.HitSubLeftInnerLaneId + "/" +
                        hit.HitSubRightInnerLaneId + "/" +
                        hit.HitSubLeftOuterLaneId + "/" +
                        hit.HitSubRightOuterLaneId);
                }
                if (note.NoteType == (int)RecoveredNoteType.Scratch)
                    directScratchExpected.Add(note.Id);
            }
            var denseCriticalPublished = new HashSet<int>();
            var directScratchPublished = new HashSet<int>();
            var denseCriticalTiming = new List<string>();
            var directScratchTiming = new List<string>();
            var denseWindowResults = new List<string>();
            var lateTapResults = new List<string>();
            var timingValid = true;
            var holdOwnershipValid = true;
            // Drive this integration gate at a real frame cadence. Flick
            // Began(note-1) and Moved(note) are intentionally fetched in one
            // AutoTouch frame; stepping at every scheduled millisecond splits
            // a single synthetic touch into an impossible >1000 FPS sequence.
            for (long milliseconds = 0; milliseconds <= 90000; milliseconds += 16)
            {
                runtime.Tick(milliseconds, milliseconds + 100000);
                foreach (var effect in runtime.HoldEvents)
                {
                    if (effect.EffectType == RecoveredInputEffectType.HoldStart)
                        holdBodiesStarted.Add(effect.NoteId);
                    else if (effect.EffectType == RecoveredInputEffectType.HoldEnd)
                        holdBodiesEnded.Add(effect.NoteId);
                }
                AuditHoldEvents(runtime.HoldEvents, active, ref starts, ref ends);
                holdOwnershipValid &= HoldOwnershipMatches(
                    active, runtime.ActiveTouchHolds);
                foreach (var result in runtime.InputResults)
                {
                    if ((result.NoteType == RecoveredNoteType.Normal ||
                         result.NoteType == RecoveredNoteType.Critical ||
                         result.NoteType == RecoveredNoteType.HoldStart ||
                         result.NoteType == RecoveredNoteType.CriticalHoldStart ||
                         result.NoteType == RecoveredNoteType.ScratchHoldStart ||
                         result.NoteType == RecoveredNoteType.ScratchCriticalHoldStart ||
                         result.NoteType == RecoveredNoteType.BlueTap) &&
                        milliseconds - result.StartMilliseconds >= 16)
                    {
                        lateTapResults.Add(
                            result.NoteId + ":" + (int)result.NoteType + ":" +
                            result.StartMilliseconds + "@" + milliseconds);
                    }
                    if (milliseconds >= 23872 && milliseconds <= 25536)
                    {
                        denseWindowResults.Add(
                            result.NoteId + ":" + (int)result.NoteType + ":" +
                            result.StartMilliseconds + "@" + milliseconds);
                    }
                    if (result.NoteType == RecoveredNoteType.Sound ||
                        result.NoteType == RecoveredNoteType.SoundPurple)
                    {
                        soundResults++;
                    }
                    if (result.NoteType == RecoveredNoteType.Critical)
                    {
                        criticalResults++;
                        if (denseCriticalExpected.Contains(result.NoteId))
                        {
                            denseCriticalPublished.Add(result.NoteId);
                            denseCriticalTiming.Add(
                                result.NoteId + ":" + result.StartMilliseconds +
                                "@" + milliseconds);
                            timingValid &= milliseconds >= result.StartMilliseconds &&
                                           milliseconds - result.StartMilliseconds < 16;
                        }
                    }
                    if (result.NoteType == RecoveredNoteType.Scratch)
                    {
                        scratchResults++;
                        directScratchPublished.Add(result.NoteId);
                        directScratchTiming.Add(
                            result.NoteId + ":" + result.StartMilliseconds +
                            "@" + milliseconds);
                        timingValid &= milliseconds >= result.StartMilliseconds &&
                                       milliseconds - result.StartMilliseconds < 16;
                    }
                }
            }
            runtime.Tick(90000, 190000);
            foreach (var effect in runtime.HoldEvents)
            {
                if (effect.EffectType == RecoveredInputEffectType.HoldStart)
                    holdBodiesStarted.Add(effect.NoteId);
                else if (effect.EffectType == RecoveredInputEffectType.HoldEnd)
                    holdBodiesEnded.Add(effect.NoteId);
            }
            AuditHoldEvents(runtime.HoldEvents, active, ref starts, ref ends);
            holdOwnershipValid &= HoldOwnershipMatches(
                active, runtime.ActiveTouchHolds);
            foreach (var result in runtime.InputResults)
            {
                if (result.NoteType == RecoveredNoteType.Sound ||
                    result.NoteType == RecoveredNoteType.SoundPurple)
                {
                    soundResults++;
                }
                if (result.NoteType == RecoveredNoteType.Critical)
                    criticalResults++;
                if (result.NoteType == RecoveredNoteType.Scratch)
                    scratchResults++;
            }
            var assignments = new List<string>();
            foreach (var pair in runtime.ActiveTouchHolds)
                assignments.Add(pair.Key + "->" + pair.Value.Id);
            var staleDetails = new List<string>();
            foreach (var pair in runtime.ActiveTouchHolds)
            {
                var note = pair.Value;
                var scheduledTrace = new List<string>();
                foreach (var scheduled in runtime.ScheduledEvents)
                {
                    if (scheduled.TouchId != note.Id) continue;
                    var scheduledHit = ResolveLaneAtScreenPosition(
                        raycaster, scheduled.ScreenPosition);
                    scheduledTrace.Add(
                        scheduled.Milliseconds + ":" + (int)scheduled.Phase +
                        ":" + scheduledHit.HitMainLaneId + "/" +
                        scheduledHit.HitSubLeftInnerLaneId + "/" +
                        scheduledHit.HitSubRightInnerLaneId + "/" +
                        scheduledHit.HitSubLeftOuterLaneId + "/" +
                        scheduledHit.HitSubRightOuterLaneId);
                }
                staleDetails.Add(
                    "touch=" + pair.Key + "/note=" + note.Id +
                    "/type=" + note.NoteType + "/lane=" + note.Lane +
                    "-" + note.EndLane + "/time=" + note.StartMilliseconds +
                    "-" + note.EndMilliseconds + "/scheduled=" +
                    string.Join("+", scheduledTrace));
            }
            Debug.Log("OPENWDS_STELLA_HOLD_SOUND starts=" + starts +
                      " ends=" + ends + " active=" + active.Count +
                      " soundResults=" + soundResults +
                      " criticalResults=" + criticalResults +
                      " scratchResults=" + scratchResults +
                      " ids=" + string.Join(",", active.Keys) +
                      " assignments=" + string.Join(",", assignments) +
                      " details=" + string.Join(",", staleDetails));
            Debug.Log("OPENWDS_STELLA_HOLD_COVERAGE expected=" +
                      holdBodiesExpected.Count + " started=" +
                      holdBodiesStarted.Count + " ended=" +
                      holdBodiesEnded.Count + " missingStart=" +
                      string.Join(",", holdBodiesExpected.Except(holdBodiesStarted)) +
                      " missingEnd=" +
                      string.Join(",", holdBodiesExpected.Except(holdBodiesEnded)) +
                      " unexpectedStart=" +
                      string.Join(",", holdBodiesStarted.Except(holdBodiesExpected)) +
                      " unexpectedEnd=" +
                      string.Join(",", holdBodiesEnded.Except(holdBodiesExpected)));
            Debug.Log("OPENWDS_HOLD_SE_OWNERSHIP valid=" + holdOwnershipValid);
            Debug.Log("OPENWDS_STELLA_NOTE_TIMING critical=" +
                      string.Join(",", denseCriticalTiming) +
                      " scratch=" + string.Join(",", directScratchTiming) +
                      " timingValid=" + timingValid);
            Debug.Log("OPENWDS_STELLA_CRITICAL_RAYCAST " +
                      string.Join(",", denseCriticalRaycast));
            Debug.Log("OPENWDS_STELLA_DENSE_WINDOW " +
                      string.Join(",", denseWindowResults));
            Debug.Log("OPENWDS_STELLA_LATE_TAPS " +
                      string.Join(",", lateTapResults));
            return notation.Length == 671 &&
                   holdBodiesStarted.SetEquals(holdBodiesExpected) &&
                   holdBodiesEnded.SetEquals(holdBodiesExpected) &&
                   starts == holdBodiesExpected.Count &&
                   ends == holdBodiesExpected.Count &&
                   holdOwnershipValid &&
                   active.Count == 0 && runtime.ActiveTouchHoldCount == 0 &&
                   soundResults == 14 && criticalResults == 54 &&
                   scratchResults == 5 && denseCriticalExpected.Count == 7 &&
                   denseCriticalPublished.SetEquals(denseCriticalExpected) &&
                   directScratchPublished.SetEquals(directScratchExpected) &&
                   timingValid;
        }

        private static bool HoldOwnershipMatches(
            IReadOnlyDictionary<int, int> eventOwnership,
            IReadOnlyDictionary<int, RecoveredNotationNote> activeTouchHolds)
        {
            var activeNoteIds = new HashSet<int>();
            foreach (var pair in activeTouchHolds)
                if (pair.Value != null) activeNoteIds.Add(pair.Value.Id);
            if (eventOwnership.Count != activeNoteIds.Count) return false;
            foreach (var noteId in eventOwnership.Keys)
                if (!activeNoteIds.Contains(noteId)) return false;
            return true;
        }

        private static RecoveredHitLaneEntity ResolveLaneAtScreenPosition(
            RecoveredLaneRaycaster raycaster,
            Vector2 position)
        {
            var input = new RecoveredInputEntity(
                0, 0, position, position, Vector2.zero,
                RecoveredTouchPhase.Stationary);
            var hits = new List<RecoveredHitLaneEntity>(1);
            var beganHits = new List<RecoveredHitLaneEntity>(1);
            raycaster.Raycast(new[] { input }, hits, beganHits);
            return hits[0];
        }

        private static bool ValidateBeamEffect()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BeamEffectPath);
            if (prefab == null) return false;
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var controller = instance.GetComponent<Sirius.Game.BeamEffectController>();
                if (controller == null) return false;
                controller.Initialize(
                    4, false, RecoveredLaneEffectRuntime.SingleLaneWidth);
                var square = FindChild(instance.transform, "BeamSquare")
                    ?.GetComponent<ParticleSystem>();
                var left = FindChild(instance.transform, "BeamFrameLB")
                    ?.GetComponent<ParticleSystem>();
                var right = FindChild(instance.transform, "BeamFrameRB")
                    ?.GetComponent<ParticleSystem>();
                return square != null && left != null && right != null &&
                       Mathf.Approximately(square.main.startSizeXMultiplier, 3.7f) &&
                       Mathf.Approximately(left.shape.position.x, -1.85f) &&
                       Mathf.Approximately(right.shape.position.x, 1.85f) &&
                       RecoveredLaneEffectRuntime.IsCritical(
                           RecoveredNoteType.ScratchCriticalHold) &&
                       !RecoveredLaneEffectRuntime.IsCritical(RecoveredNoteType.ScratchHold);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static bool ValidateLaneEffectLifecycle(
            Sirius.Game.LaneGroup laneGroup,
            Transform effectParent)
        {
            var runtime = new RecoveredLaneEffectRuntime(
                laneGroup,
                effectParent,
                AssetDatabase.LoadAssetAtPath<GameObject>(BeamEffectPath),
                LoadPrefabs(DefaultBombEffectPaths));
            var beamDisabledRuntime = new RecoveredLaneEffectRuntime(
                laneGroup,
                effectParent,
                AssetDatabase.LoadAssetAtPath<GameObject>(BeamEffectPath),
                RecoveredBombType.Default,
                LoadPrefabs(DefaultBombEffectPaths),
                null,
                null,
                false);
            var start = new RecoveredNotationNote
            {
                Id = 9100,
                StartTickCount = 1f,
                EndTickCount = 2f,
                NoteType = (int)RecoveredNoteType.Hold,
                Lane = 3,
                Width = 2,
            };
            var endWithDifferentNoteId = new RecoveredNotationNote
            {
                Id = 9101,
                StartTickCount = 2f,
                EndTickCount = -1f,
                NoteType = (int)RecoveredNoteType.Hold,
                Lane = 3,
                Width = 2,
            };
            var shifted = new RecoveredNotationNote
            {
                Id = 9102,
                StartTickCount = 2f,
                EndTickCount = 3f,
                NoteType = (int)RecoveredNoteType.ScratchHold,
                Lane = 8,
                Width = 3,
            };
            beamDisabledRuntime.OnEffect(RecoveredInputEffectEntity.OnBeam(
                start, RecoveredTimingType.PerfectStar));
            if (beamDisabledRuntime.ActiveBeamCount != 0 ||
                FindChild(effectParent, "Runtime_Beam_9100") != null)
                return false;
            runtime.OnEffect(RecoveredInputEffectEntity.OnHoldStart(start));
            var instance = FindChild(effectParent, "Runtime_Hold_9100");
            if (instance == null) return false;
            Transform shiftedInstance = null;
            try
            {
                var firstX = effectParent.InverseTransformPoint(
                    laneGroup.GetLaneCollider(3).position).x;
                var lastX = effectParent.InverseTransformPoint(
                    laneGroup.GetLaneCollider(4).position).x;
                if (!Mathf.Approximately(instance.localPosition.x,
                        (firstX + lastX) * 0.5f) ||
                    !Mathf.Approximately(instance.localPosition.y, 0f) ||
                    !Mathf.Approximately(instance.localPosition.z, 0f))
                {
                    return false;
                }

                runtime.OnEffect(RecoveredInputEffectEntity.OnHoldEnd(
                    endWithDifferentNoteId));
                foreach (var particle in
                         instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (particle.main.loop) return false;
                }
                runtime.OnEffect(RecoveredInputEffectEntity.OnHoldStart(shifted));
                shiftedInstance = FindChild(effectParent, "Runtime_Hold_9102");
                if (shiftedInstance == null || shiftedInstance == instance)
                    return false;
                var shiftedFirstX = effectParent.InverseTransformPoint(
                    laneGroup.GetLaneCollider(8).position).x;
                var shiftedLastX = effectParent.InverseTransformPoint(
                    laneGroup.GetLaneCollider(10).position).x;
                return Mathf.Approximately(
                           shiftedInstance.localPosition.x,
                           (shiftedFirstX + shiftedLastX) * 0.5f) &&
                       Mathf.Approximately(shiftedInstance.localPosition.y, 0f) &&
                       Mathf.Approximately(shiftedInstance.localPosition.z, 0f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
                if (shiftedInstance != null)
                    UnityEngine.Object.DestroyImmediate(shiftedInstance.gameObject);
            }
        }

        private static bool ValidateBombEffects()
        {
            var defaultPrefabs = LoadPrefabs(DefaultBombEffectPaths);
            var notesPrefabs = LoadPrefabs(NotesBombEffectPaths);
            var sakuraPrefabs = LoadPrefabs(SakuraBombEffectPaths);
            if (RecoveredLaneEffectRuntime.SelectBombPrefabs(
                    RecoveredBombType.Default, defaultPrefabs, notesPrefabs,
                    sakuraPrefabs) != defaultPrefabs ||
                RecoveredLaneEffectRuntime.SelectBombPrefabs(
                    RecoveredBombType.Notes, defaultPrefabs, notesPrefabs,
                    sakuraPrefabs) != notesPrefabs ||
                RecoveredLaneEffectRuntime.SelectBombPrefabs(
                    RecoveredBombType.Sakura, defaultPrefabs, notesPrefabs,
                    sakuraPrefabs) != sakuraPrefabs ||
                (int)RecoveredBombType.Default != 1 ||
                (int)RecoveredBombType.Notes != 2 ||
                (int)RecoveredBombType.Sakura != 3 ||
                (int)RecoveredGameTapEffectType.Default != 0 ||
                (int)RecoveredGameTapEffectType.Light != 1)
                return false;
            if (RecoveredLaneEffectRuntime.GetBombPrefabIndex(
                    RecoveredNoteType.Normal) != 0 ||
                RecoveredLaneEffectRuntime.GetBombPrefabIndex(
                    RecoveredNoteType.Critical) != 1 ||
                RecoveredLaneEffectRuntime.GetBombPrefabIndex(
                    RecoveredNoteType.Hold) != 3 ||
                RecoveredLaneEffectRuntime.GetBombPrefabIndex(
                    RecoveredNoteType.Flick) != 4 ||
                RecoveredLaneEffectRuntime.GetBombPrefabIndex(
                    RecoveredNoteType.SoundPurple) != 5 ||
                !RecoveredLaneEffectRuntime.IsStrong(
                    RecoveredNoteType.Normal, RecoveredTimingType.Great) ||
                RecoveredLaneEffectRuntime.IsStrong(
                    RecoveredNoteType.Flick, RecoveredTimingType.Great))
            {
                return false;
            }

            var bombObject = UnityEngine.Object.Instantiate(defaultPrefabs[0]);
            var holdObject = UnityEngine.Object.Instantiate(defaultPrefabs[2]);
            try
            {
                var bomb = bombObject.GetComponent<Sirius.Game.BombController>();
                var hold = holdObject.GetComponent<Sirius.Game.HoldEffectController>();
                if (bomb == null || hold == null) return false;
                bomb.Initialize(4, 1000, true);
                var bombSerialized = new SerializedObject(bomb);
                var square = bombSerialized.FindProperty("_bombSquare")
                    .objectReferenceValue as ParticleSystem;
                var boxes = bombSerialized.FindProperty("_bombBoxes");
                var pillers = bombSerialized.FindProperty("_bombPillers");
                if (square == null || boxes.arraySize != 4 || pillers.arraySize != 4 ||
                    !Mathf.Approximately(square.main.startSizeX.constantMin, 2.9f) ||
                    !Mathf.Approximately(square.main.startSizeX.constantMax, 3.7f))
                {
                    return false;
                }
                var firstBox = boxes.GetArrayElementAtIndex(0)
                    .objectReferenceValue as ParticleSystem;
                var lastPiller = pillers.GetArrayElementAtIndex(3)
                    .objectReferenceValue as ParticleSystem;
                if (firstBox == null || lastPiller == null ||
                    !Mathf.Approximately(firstBox.main.startSizeXMultiplier, 3.7f) ||
                    !Mathf.Approximately(firstBox.main.startSizeYMultiplier, 3f) ||
                    !Mathf.Approximately(lastPiller.shape.position.x, 1.85f) ||
                    !Mathf.Approximately(lastPiller.shape.position.y, -0.31f))
                {
                    return false;
                }

                var holdSerialized = new SerializedObject(hold);
                var holdSquare = holdSerialized.FindProperty("_bombSquare")
                    .objectReferenceValue as ParticleSystem;
                var holdBoxes = holdSerialized.FindProperty("_bombBoxes");
                if (holdBoxes == null || holdBoxes.arraySize != 4) return false;
                var holdSideLeft = holdBoxes.GetArrayElementAtIndex(2)
                    .objectReferenceValue as ParticleSystem;
                var holdSideRight = holdBoxes.GetArrayElementAtIndex(3)
                    .objectReferenceValue as ParticleSystem;
                var authoredSideLeftSize = holdSideLeft != null
                    ? holdSideLeft.main.startSizeXMultiplier : 0f;
                var authoredSideRightSize = holdSideRight != null
                    ? holdSideRight.main.startSizeXMultiplier : 0f;
                // Re-run after capturing the authored endpoint sizes. Retail
                // changes only the first two box widths.
                hold.Initialize(4, false, RecoveredLaneEffectRuntime.SingleLaneWidth);
                var holdParticle = holdSerialized.FindProperty("_bombParticle")
                    .objectReferenceValue as ParticleSystem;
                var holdStar = holdSerialized.FindProperty("_bombStar")
                    .objectReferenceValue as ParticleSystem;
                if (holdSquare == null || holdParticle == null || holdStar == null ||
                    !Mathf.Approximately(holdSquare.main.startSizeX.constantMin, 2.9f) ||
                    !Mathf.Approximately(holdSquare.main.startSizeX.constantMax, 3.7f) ||
                    holdSideLeft == null || holdSideRight == null ||
                    !Mathf.Approximately(
                        holdSideLeft.main.startSizeXMultiplier,
                        authoredSideLeftSize) ||
                    !Mathf.Approximately(
                        holdSideRight.main.startSizeXMultiplier,
                        authoredSideRightSize) ||
                    !Mathf.Approximately(holdParticle.shape.scale.x, 3.7f) ||
                    !Mathf.Approximately(
                        holdParticle.emission.rateOverTime.constant, 120f) ||
                    !Mathf.Approximately(holdStar.shape.scale.x, 3.3f) ||
                    !Mathf.Approximately(holdStar.emission.rateOverTime.constant, 40f))
                {
                    return false;
                }
                hold.OnHoldEnd();
                foreach (var particle in hold.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (particle.main.loop) return false;
                }
                return ValidateAlternateBombStyle(notesPrefabs, true) &&
                       ValidateAlternateBombStyle(sakuraPrefabs, false) &&
                       ValidateTapEffectModes(
                           defaultPrefabs, notesPrefabs, sakuraPrefabs);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bombObject);
                UnityEngine.Object.DestroyImmediate(holdObject);
            }
        }

        private static bool ValidateTapEffectModes(
            GameObject[] defaultPrefabs,
            GameObject[] notesPrefabs,
            GameObject[] sakuraPrefabs)
        {
            var defaultBomb = UnityEngine.Object.Instantiate(defaultPrefabs[0]);
            var notesBomb = UnityEngine.Object.Instantiate(notesPrefabs[0]);
            var sakuraBomb = UnityEngine.Object.Instantiate(sakuraPrefabs[0]);
            var scratchBomb = UnityEngine.Object.Instantiate(defaultPrefabs[4]);
            var soundBomb = UnityEngine.Object.Instantiate(defaultPrefabs[5]);
            try
            {
                var defaultController = defaultBomb.GetComponent<
                    Sirius.Game.IRecoveredBombController>();
                var notesController = notesBomb.GetComponent<
                    Sirius.Game.IRecoveredBombController>();
                var sakuraController = sakuraBomb.GetComponent<
                    Sirius.Game.IRecoveredBombController>();
                var scratchController = scratchBomb.GetComponent<
                    Sirius.Game.IRecoveredBombController>();
                var soundController = soundBomb.GetComponent<
                    Sirius.Game.IRecoveredBombController>();
                if (defaultController == null || notesController == null ||
                    sakuraController == null || scratchController == null ||
                    soundController == null)
                    return false;

                var defaultSerialized = new SerializedObject(
                    defaultBomb.GetComponent<Sirius.Game.BombController>());
                var notesSerialized = new SerializedObject(
                    notesBomb.GetComponent<Sirius.Game.BombNotesController>());
                var sakuraSerialized = new SerializedObject(
                    sakuraBomb.GetComponent<Sirius.Game.BombSakuraController>());
                var defaultSquareBefore = ParticleActive(
                    defaultSerialized, "_bombSquare");
                var defaultFlareBefore = ParticleActive(
                    defaultSerialized, "_bombFlare");
                var notesCircleBefore = ParticleActive(
                    notesSerialized, "_bombCircle");
                var notesRoteBefore = ParticleActive(
                    notesSerialized, "_bombRote");
                var sakuraFlowerBefore = ParticleActive(
                    sakuraSerialized, "_bombFlower");
                var soundActiveBefore = soundBomb.GetComponentsInChildren<
                    ParticleSystem>(true).Count(x => x.gameObject.activeSelf);
                defaultController.ApplyTapEffectType(false);
                notesController.ApplyTapEffectType(false);
                sakuraController.ApplyTapEffectType(false);
                scratchController.ApplyTapEffectType(false);
                soundController.ApplyTapEffectType(false);

                if (ParticleActive(defaultSerialized, "_bombSquare") !=
                        defaultSquareBefore ||
                    ParticleActive(defaultSerialized, "_bombFlare") !=
                        defaultFlareBefore ||
                    !ParticleArrayInactive(defaultSerialized, "_bombBoxes") ||
                    !ParticleArrayInactive(defaultSerialized, "_bombPillers") ||
                    !ParticleInactive(defaultSerialized, "_bombParticle") ||
                    !ParticleInactive(defaultSerialized, "_bombStar") ||
                    !ParticleInactive(defaultSerialized, "_bombStarCenter"))
                {
                    Debug.Log("OPENWDS_TAP_EFFECT_LIGHT invalid=Default");
                    return false;
                }

                if (ParticleActive(notesSerialized, "_bombCircle") !=
                        notesCircleBefore ||
                    ParticleActive(notesSerialized, "_bombRote") !=
                        notesRoteBefore ||
                    !ParticleInactive(notesSerialized, "_bombLight") ||
                    !ParticleArrayInactive(notesSerialized, "_bombLights") ||
                    !ParticleInactive(notesSerialized, "_bombNotes") ||
                    !ParticleInactive(notesSerialized, "_bombLines") ||
                    !ParticleInactive(notesSerialized, "_bombRings1") ||
                    !ParticleInactive(notesSerialized, "_bombRings2"))
                {
                    Debug.Log("OPENWDS_TAP_EFFECT_LIGHT invalid=Notes");
                    return false;
                }

                if (ParticleActive(sakuraSerialized, "_bombFlower") !=
                        sakuraFlowerBefore ||
                    !ParticleInactive(sakuraSerialized, "_bombSmoke") ||
                    !ParticleArrayInactive(sakuraSerialized, "_bombLights") ||
                    !ParticleInactive(sakuraSerialized, "_bombPetals") ||
                    !ParticleInactive(sakuraSerialized, "_bombLeafs") ||
                    !ParticleInactive(sakuraSerialized, "_bombTrail") ||
                    !ParticleInactive(sakuraSerialized, "_bombLine1") ||
                    !ParticleInactive(sakuraSerialized, "_bombLine2"))
                {
                    Debug.Log("OPENWDS_TAP_EFFECT_LIGHT invalid=Sakura");
                    return false;
                }

                var scratch = scratchBomb.GetComponent<
                    Sirius.Game.ScratchBombController>();
                var scratchSerialized = new SerializedObject(scratch);
                var slash = scratchSerialized.FindProperty("_slashEffectTransform")
                    ?.objectReferenceValue as Transform;
                var flare = scratchSerialized.FindProperty("_scratchBombFlare")
                    ?.objectReferenceValue as Transform;
                if (slash == null || flare == null)
                {
                    Debug.Log("OPENWDS_TAP_EFFECT_LIGHT invalid=ScratchReferences");
                    return false;
                }
                var disabledScratchRenderers = 0;
                foreach (var renderer in slash.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.gameObject == flare.gameObject)
                        continue;
                    if (renderer.enabled)
                    {
                        Debug.Log("OPENWDS_TAP_EFFECT_LIGHT invalid=ScratchRenderer");
                        return false;
                    }
                    disabledScratchRenderers++;
                }
                scratchController.Initialize(4, 1000, true);
                var flareRenderer = flare.GetComponent<Renderer>();
                if (!slash.gameObject.activeSelf || !flare.gameObject.activeSelf ||
                    flareRenderer == null || !flareRenderer.enabled)
                {
                    Debug.Log("OPENWDS_TAP_EFFECT_LIGHT invalid=ScratchStrongFlare");
                    return false;
                }

                var soundActiveAfter = soundBomb.GetComponentsInChildren<
                    ParticleSystem>(true).Count(x => x.gameObject.activeSelf);
                return disabledScratchRenderers > 0 &&
                       soundActiveBefore == soundActiveAfter;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(defaultBomb);
                UnityEngine.Object.DestroyImmediate(notesBomb);
                UnityEngine.Object.DestroyImmediate(sakuraBomb);
                UnityEngine.Object.DestroyImmediate(scratchBomb);
                UnityEngine.Object.DestroyImmediate(soundBomb);
            }
        }

        private static bool ParticleActive(SerializedObject owner, string field)
        {
            var particle = owner.FindProperty(field)?.objectReferenceValue as
                ParticleSystem;
            return particle != null && particle.gameObject.activeSelf;
        }

        private static bool ParticleInactive(SerializedObject owner, string field)
        {
            var particle = owner.FindProperty(field)?.objectReferenceValue as
                ParticleSystem;
            // Retail SetUpLightParticles null-checks every serialized reference.
            return particle == null || !particle.gameObject.activeSelf;
        }

        private static bool ParticleArrayInactive(
            SerializedObject owner, string field)
        {
            var particles = owner.FindProperty(field);
            if (particles == null || !particles.isArray || particles.arraySize == 0)
                return true;
            for (var index = 0; index < particles.arraySize; index++)
            {
                var particle = particles.GetArrayElementAtIndex(index)
                    .objectReferenceValue as ParticleSystem;
                if (particle != null && particle.gameObject.activeSelf) return false;
            }
            return true;
        }

        private static bool ValidateAlternateBombStyle(
            GameObject[] prefabs, bool isNotes)
        {
            var bombObject = UnityEngine.Object.Instantiate(prefabs[0]);
            var holdObject = UnityEngine.Object.Instantiate(prefabs[2]);
            var scratchObject = UnityEngine.Object.Instantiate(prefabs[4]);
            try
            {
                var bomb = bombObject.GetComponent<Sirius.Game.IRecoveredBombController>();
                var hold = holdObject.GetComponent<Sirius.Game.IRecoveredHoldEffectController>();
                var scratch = scratchObject.GetComponent<Sirius.Game.IRecoveredBombController>();
                if (bomb == null || hold == null || scratch == null) return false;
                bomb.Initialize(4, 1000, true);
                scratch.Initialize(4, 1000, false);
                hold.Initialize(4, true, RecoveredLaneEffectRuntime.SingleLaneWidth);

                var bombBehaviour = isNotes
                    ? (MonoBehaviour)bombObject.GetComponent<Sirius.Game.BombNotesController>()
                    : bombObject.GetComponent<Sirius.Game.BombSakuraController>();
                var holdBehaviour = isNotes
                    ? (MonoBehaviour)holdObject.GetComponent<Sirius.Game.HoldNotesEffectController>()
                    : holdObject.GetComponent<Sirius.Game.HoldSakuraEffectController>();
                var scratchBehaviour = isNotes
                    ? (MonoBehaviour)scratchObject.GetComponent<Sirius.Game.ScratchBombNotesController>()
                    : scratchObject.GetComponent<Sirius.Game.ScratchBombSakuraController>();
                if (bombBehaviour == null || holdBehaviour == null ||
                    scratchBehaviour == null) return false;
                var serialized = new SerializedObject(bombBehaviour);
                var primaryName = isNotes ? "_bombCircle" : "_bombFlower";
                var primary = serialized.FindProperty(primaryName)
                    ?.objectReferenceValue as ParticleSystem;
                if (primary == null ||
                    !Mathf.Approximately(primary.main.startSize.constantMin, 1f) ||
                    !Mathf.Approximately(primary.main.startSize.constantMax, 2f) ||
                    !Mathf.Approximately(primary.shape.scale.x, 3.7f) ||
                    !Mathf.Approximately(primary.shape.scale.y, 0.63f) ||
                    !Mathf.Approximately(
                        primary.emission.rateOverTimeMultiplier, 120f))
                    return false;

                var scratchSerialized = new SerializedObject(scratchBehaviour);
                var scratchTransform = scratchSerialized.FindProperty("_slashEffectTransform")
                    ?.objectReferenceValue as Transform;
                if (scratchTransform == null || scratchTransform.gameObject.activeSelf)
                    return false;
                hold.OnHoldEnd();
                var holdSerialized = new SerializedObject(holdBehaviour);
                var holdPrimary = holdSerialized.FindProperty(primaryName)
                    ?.objectReferenceValue as ParticleSystem;
                return holdPrimary != null &&
                       Mathf.Approximately(
                           holdPrimary.emission.rateOverTimeMultiplier, 20f) &&
                       !holdPrimary.main.loop;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bombObject);
                UnityEngine.Object.DestroyImmediate(holdObject);
                UnityEngine.Object.DestroyImmediate(scratchObject);
            }
        }

        private static bool ValidateNoteVisualLifecycle(
            out int visualNoteCount,
            out int peakActiveCount,
            out int recycledCount)
        {
            visualNoteCount = 0;
            peakActiveCount = 0;
            recycledCount = 0;
            var root = new GameObject("VisualLifecycleValidation");
            try
            {
                var chartPath = Path.Combine(
                    Application.dataPath, "StreamingAssets", "OpenWDS",
                    "StandardCharts", "1", "1", "2.csv");
                var notation = RecoveredStandardNotation.Parse(File.ReadAllText(chartPath));
                var runtime = new RecoveredNoteVisualRuntime(
                    notation, root.transform, LoadRuntimeNotePrefabs(), 5d);
                var concurrentDisabled = new RecoveredNoteVisualRuntime(
                    notation, root.transform, LoadRuntimeNotePrefabs(), 5d,
                    0d, null, 8, false);
                if (runtime.MoveMilliseconds != 1483 ||
                    runtime.TotalConcurrentLineCount != 28 ||
                    concurrentDisabled.TotalConcurrentLineCount != 0 ||
                    RecoveredNoteVisualRuntime.GetPrefabIndex(83) != 0 ||
                    RecoveredNoteVisualRuntime.GetPrefabIndex(101) != 1 ||
                    RecoveredNoteVisualRuntime.GetPrefabIndex(31) != 6 ||
                    RecoveredNoteVisualRuntime.GetArrowActiveCount(3, false) != 5 ||
                    RecoveredNoteVisualRuntime.GetArrowActiveCount(4, false) != 5 ||
                    RecoveredNoteVisualRuntime.GetArrowActiveCount(4, true) != 11 ||
                    RecoveredNoteVisualRuntime.GetArrowActiveCount(6, true) != 18 ||
                    RecoveredNoteVisualRuntime.GetArrowAnimationStateName(false, 5) !=
                        "ScratchNotesArrow_flick5_anim" ||
                    RecoveredNoteVisualRuntime.GetArrowAnimationStateName(true, 11) !=
                        "ScratchNotesArrow_jump11_anim" ||
                    !Mathf.Approximately(
                        RecoveredNoteVisualRuntime.GetArrowInterval(3), 0.35f) ||
                    !Mathf.Approximately(
                        RecoveredNoteVisualRuntime.GetArrowInterval(4), 0.36f) ||
                    !Mathf.Approximately(
                        RecoveredOriginalGameConfig.GetLaneMaskScaleY(0), 12.5f) ||
                    !Mathf.Approximately(
                        RecoveredOriginalGameConfig.GetLaneMaskScaleY(100), 790f))
                {
                    return false;
                }
                var fourLaneWidth = RecoveredNotePositionCalculator.GetNoteWidth(
                    4,
                    RecoveredGameConfigValues.NoteWidthPerLane,
                    RecoveredGameConfigValues.LaneBorderWidth);
                if (!Mathf.Approximately(
                        RecoveredNoteVisualRuntime.GetTapVisualWidth(fourLaneWidth),
                        fourLaneWidth - 0.15f) ||
                    !Mathf.Approximately(
                        RecoveredNoteVisualRuntime.GetHoldLineWidth(fourLaneWidth),
                        fourLaneWidth - 0.15f + 0.1f))
                {
                    return false;
                }
                if (!Mathf.Approximately(
                        RecoveredOriginalGameConfig.GetNoteHeightRotationX(1), 6f) ||
                    !Mathf.Approximately(
                        RecoveredOriginalGameConfig.GetNoteHeightRotationX(8), -15f) ||
                    !Mathf.Approximately(
                        RecoveredOriginalGameConfig.GetNoteHeightRotationX(10), -21f))
                {
                    return false;
                }
                var holdVisualValid = ValidateHoldVisualState(root.transform);
                var scratchHoldEndValid = ValidateScratchHoldEndPosition(root.transform);
                var arrowVisualValid = ValidateArrowVisualState(root.transform);
                var jumpArrowVisualValid = ValidateJumpScratchArrowVisualState(root.transform);
                var concurrentCompletionValid =
                    ValidateConcurrentLineCompletion(root.transform);
                Debug.Log("OPENWDS_NOTE_VISUAL_COMPONENTS hold=" + holdVisualValid +
                          " scratchHoldEnd=" + scratchHoldEndValid +
                          " arrow=" + arrowVisualValid +
                          " jumpArrow=" + jumpArrowVisualValid +
                          " concurrentCompletion=" + concurrentCompletionValid);
                if (!holdVisualValid || !scratchHoldEndValid || !arrowVisualValid ||
                    !jumpArrowVisualValid || !concurrentCompletionValid) return false;

                runtime.Tick(0);
                peakActiveCount = runtime.PeakActiveCount;
                long lastMilliseconds = 0;
                foreach (var note in notation)
                {
                    lastMilliseconds = Math.Max(
                        lastMilliseconds,
                        Math.Max(note.StartMilliseconds, note.EndMilliseconds));
                }
                runtime.Tick(lastMilliseconds + runtime.MoveMilliseconds + 1);
                visualNoteCount = runtime.TotalVisualNoteCount;
                recycledCount = runtime.RecycledCount;
                Debug.Log("OPENWDS_NOTE_VISUAL_COUNTS total=" + visualNoteCount +
                          " peak=" + peakActiveCount +
                          " spawned=" + runtime.SpawnedCount +
                          " recycled=" + recycledCount +
                          " active=" + runtime.ActiveCount +
                          " concurrent=" + runtime.ActiveConcurrentLineCount);
                return visualNoteCount > 0 && peakActiveCount > 0 &&
                       runtime.SpawnedCount == visualNoteCount &&
                       recycledCount == visualNoteCount && runtime.ActiveCount == 0 &&
                       runtime.ActiveConcurrentLineCount == 0;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static bool ValidateConcurrentLineCompletion(Transform parent)
        {
            var first = new RecoveredNotationNote
            {
                Id = 990001,
                NoteType = (int)RecoveredNoteType.Normal,
                Lane = 2,
                Width = 1,
                StartTickCount = 1f,
                EndTickCount = -0.001f,
            };
            var firstPair = new RecoveredNotationNote
            {
                Id = 990002,
                NoteType = (int)RecoveredNoteType.Critical,
                Lane = 8,
                Width = 1,
                StartTickCount = 1f,
                EndTickCount = -0.001f,
            };
            var missed = new RecoveredNotationNote
            {
                Id = 990003,
                NoteType = (int)RecoveredNoteType.Normal,
                Lane = 3,
                Width = 1,
                StartTickCount = 2f,
                EndTickCount = -0.001f,
            };
            var missedPair = new RecoveredNotationNote
            {
                Id = 990004,
                NoteType = (int)RecoveredNoteType.Critical,
                Lane = 9,
                Width = 1,
                StartTickCount = 2f,
                EndTickCount = -0.001f,
            };
            var holdStart = new RecoveredNotationNote
            {
                Id = 990005,
                NoteType = (int)RecoveredNoteType.HoldStart,
                Lane = 1,
                Width = 1,
                StartTickCount = 4f,
                EndTickCount = -0.001f,
            };
            var holdStartPair = new RecoveredNotationNote
            {
                Id = 990006,
                NoteType = (int)RecoveredNoteType.CriticalHoldStart,
                Lane = 10,
                Width = 1,
                StartTickCount = 4f,
                EndTickCount = -0.001f,
            };
            var holdBody = new RecoveredNotationNote
            {
                Id = 990007,
                NoteType = (int)RecoveredNoteType.Hold,
                Lane = 2,
                Width = 1,
                StartTickCount = 5f,
                EndTickCount = 6f,
            };
            var holdBodyPair = new RecoveredNotationNote
            {
                Id = 990008,
                NoteType = (int)RecoveredNoteType.ScratchHold,
                Lane = 9,
                Width = 1,
                StartTickCount = 5f,
                EndTickCount = 6f,
            };
            var runtime = new RecoveredNoteVisualRuntime(
                new[]
                {
                    first, firstPair, missed, missedPair,
                    holdStart, holdStartPair, holdBody, holdBodyPair,
                },
                parent, LoadRuntimeNotePrefabs(), 5d);
            runtime.Tick(1000L);
            if (runtime.TotalConcurrentLineCount != 4 ||
                runtime.ActiveConcurrentLineCount != 2)
            {
                return false;
            }

            runtime.OnInputResults(new[]
            {
                RecoveredInputResultEntity.Create(
                    first,
                    new RecoveredTimingDecision(
                        RecoveredTimingType.PerfectStar,
                        RecoveredTimingAssistType.None,
                        0L)),
            });
            if (runtime.ActiveConcurrentLineCount != 1 ||
                runtime.CompletedConcurrentLineCount != 1)
            {
                return false;
            }

            runtime.OnInputResults(new[]
            {
                RecoveredInputResultEntity.OnMiss(missed),
            });
            if (runtime.ActiveConcurrentLineCount != 1 ||
                runtime.CompletedConcurrentLineCount != 1)
            {
                return false;
            }

            // HoldStart has EndMilliseconds=-1 in notation. Retail still clears
            // its concurrent line with StartMilliseconds, not by the broad
            // numeric test NoteType > Flick that the old recovery used.
            runtime.Tick(4000L);
            runtime.OnInputResults(new[]
            {
                RecoveredInputResultEntity.Create(
                    holdStart,
                    new RecoveredTimingDecision(
                        RecoveredTimingType.PerfectStar,
                        RecoveredTimingAssistType.None,
                        0L)),
            });
            if (runtime.ActiveConcurrentLineCount != 0 ||
                runtime.CompletedConcurrentLineCount != 2)
            {
                return false;
            }

            // Hold/ScratchHold bodies are grouped and completed by EndMilliseconds.
            runtime.Tick(6000L);
            runtime.OnInputResults(new[]
            {
                RecoveredInputResultEntity.Create(
                    holdBody,
                    new RecoveredTimingDecision(
                        RecoveredTimingType.PerfectStar,
                        RecoveredTimingAssistType.None,
                        0L)),
            });
            return runtime.ActiveConcurrentLineCount == 0 &&
                   runtime.CompletedConcurrentLineCount == 3;
        }

        private static bool ValidateHoldVisualState(Transform parent)
        {
            var note = new RecoveredNotationNote
            {
                Id = 987654,
                NoteType = (int)RecoveredNoteType.Hold,
                Lane = 3,
                Width = 2,
                StartTickCount = 1f,
                EndTickCount = 2f,
            };
            var runtime = new RecoveredNoteVisualRuntime(
                new[] { note }, parent, LoadRuntimeNotePrefabs(), 5d);
            var spawnMilliseconds = note.StartMilliseconds - runtime.MoveMilliseconds;
            runtime.Tick(spawnMilliseconds);
            var instance = parent.Find("Runtime_987654_100");
            var end = instance != null ? instance.Find("Note") : null;
            if (instance == null || end == null ||
                Quaternion.Angle(instance.localRotation, Quaternion.identity) > 0.01f ||
                !Mathf.Approximately(
                    Mathf.DeltaAngle(end.localEulerAngles.x, -15f), 0f))
            {
                runtime.Reset();
                return false;
            }
            SpriteRenderer line = null;
            foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.transform.parent == instance &&
                    renderer.drawMode != SpriteDrawMode.Simple)
                {
                    line = renderer;
                    break;
                }
            }
            var block = new MaterialPropertyBlock();
            if (line == null)
            {
                runtime.Reset();
                return false;
            }
            var initializedEndY = end.localPosition.y;
            runtime.Tick(0);
            var endExpected = RecoveredNotePositionCalculator.CalculatePositionY(
                note.EndMilliseconds,
                0,
                RecoveredNotePositionCalculator.CalculateSpeedRate(5d),
                0d,
                RecoveredGameConfigValues.PositionPow3Rate,
                RecoveredGameConfigValues.PositionPow1Rate);
            var endFollowsHeight = !Mathf.Approximately(
                                       end.localPosition.y, initializedEndY) &&
                                   Mathf.Approximately(
                                       instance.localPosition.y + end.localPosition.y,
                                       endExpected) &&
                                   Mathf.Approximately(
                                       end.localPosition.y, line.size.y);
            if (!endFollowsHeight)
            {
                Debug.LogError($"OPENWDS_HOLD_END spawn={spawnMilliseconds} " +
                               $"endY={initializedEndY}->{end.localPosition.y} " +
                               $"worldY={instance.localPosition.y + end.localPosition.y} " +
                               $"expected={endExpected} lineY={line.size.y}");
                runtime.Reset();
                return false;
            }
            line.GetPropertyBlock(block);
            if (block.GetInteger(Shader.PropertyToID("IsTouchMask")) != 0 ||
                block.GetInteger(Shader.PropertyToID("_IsScratch")) != 0)
            {
                runtime.Reset();
                return false;
            }
            runtime.Tick(1125);
            var gray = RecoveredOriginalGameConfig.HoldNoteGrayOutColor;
            var isGray = Approximately(line.color, gray);
            var handled = runtime.OnHold(note.Id, true);
            line.GetPropertyBlock(block);
            var isTouched = block.GetInteger(Shader.PropertyToID("IsTouchMask")) == 1 &&
                            Approximately(line.color, Color.white);
            runtime.Reset();
            return isGray && handled && isTouched;
        }

        private static bool ValidateArrowVisualState(Transform parent)
        {
            var note = new RecoveredNotationNote
            {
                Id = 987655,
                NoteType = (int)RecoveredNoteType.Flick,
                Lane = 3,
                Width = 4,
                StartTickCount = 1f,
                EndTickCount = -1f,
            };
            var runtime = new RecoveredNoteVisualRuntime(
                new[] { note }, parent, LoadRuntimeNotePrefabs(), 5d);
            runtime.Tick(0);
            var instance = parent.Find("Runtime_987655_50");
            var animator = instance != null
                ? instance.GetComponentInChildren<Animator>(true)
                : null;
            var left = instance != null
                ? FindDescendant(instance, "NotesLeft")
                : null;
            var right = instance != null
                ? FindDescendant(instance, "NotesRight")
                : null;
            if (animator == null || left == null || right == null ||
                !left.gameObject.activeSelf || !right.gameObject.activeSelf ||
                left.childCount < 6)
            {
                Debug.LogError($"OPENWDS_ARROW_BIND animator={animator != null} " +
                               $"left={left != null} right={right != null} " +
                               $"leftActive={left?.gameObject.activeSelf} " +
                               $"rightActive={right?.gameObject.activeSelf} " +
                               $"children={left?.childCount ?? -1}");
                runtime.Reset();
                return false;
            }
            animator.Update(0f);
            var expectedState = Animator.StringToHash(
                "ScratchNotesArrow_flick5_anim");
            var actualState = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            if (actualState != expectedState)
            {
                Debug.LogError($"OPENWDS_ARROW_STATE expected={expectedState} actual={actualState}");
                runtime.Reset();
                return false;
            }
            var first = left.GetChild(0).GetComponent<SpriteRenderer>();
            var sixth = left.GetChild(5).GetComponent<SpriteRenderer>();
            var material = first != null ? first.sharedMaterial : null;
            if (first == null || sixth == null || material == null ||
                material.shader == null || material.shader.name != "Effect/Alpha" ||
                material.renderQueue != (int)UnityEngine.Rendering.RenderQueue.Transparent ||
                !first.enabled || sixth.enabled ||
                !Mathf.Approximately(left.GetChild(1).localPosition.x, 0.36f))
            {
                Debug.LogError($"OPENWDS_ARROW_RENDER first={first != null} " +
                               $"sixth={sixth != null} firstEnabled={first?.enabled} " +
                               $"sixthEnabled={sixth?.enabled} " +
                               $"shader={material?.shader?.name} queue={material?.renderQueue} " +
                               $"secondX={left.GetChild(1).localPosition.x}");
                runtime.Reset();
                return false;
            }
            var alphaBefore = new float[5];
            for (var index = 0; index < alphaBefore.Length; index++)
                alphaBefore[index] = left.GetChild(index).GetComponent<SpriteRenderer>().color.a;
            animator.Update(0.1f);
            var alphaAfter = new float[5];
            var changed = false;
            for (var index = 0; index < alphaAfter.Length; index++)
            {
                alphaAfter[index] = left.GetChild(index).GetComponent<SpriteRenderer>().color.a;
                changed |= !Mathf.Approximately(alphaBefore[index], alphaAfter[index]);
            }
            Debug.Log($"OPENWDS_ARROW_SAMPLE state={actualState} " +
                      $"alphaBefore={string.Join(",", alphaBefore)} " +
                      $"alphaAfter={string.Join(",", alphaAfter)} " +
                      $"secondX={left.GetChild(1).localPosition.x}");
            runtime.Reset();
            return changed && alphaBefore[0] < 1f;
        }

        private static bool ValidateScratchHoldEndPosition(Transform parent)
        {
            var note = new RecoveredNotationNote
            {
                Id = 987656,
                NoteType = (int)RecoveredNoteType.ScratchHold,
                Lane = 3,
                Width = 4,
                StartTickCount = 1f,
                EndTickCount = 2f,
            };
            var runtime = new RecoveredNoteVisualRuntime(
                new[] { note }, parent, LoadRuntimeNotePrefabs(), 5d);
            runtime.Tick(0);
            var instance = parent.Find("Runtime_987656_110");
            var end = instance != null ? instance.Find("ScratchNote") : null;
            if (instance == null || end == null)
            {
                runtime.Reset();
                return false;
            }
            var firstLocalHeight = end.localPosition.y;
            var firstExpected = RecoveredNotePositionCalculator.CalculatePositionY(
                note.EndMilliseconds,
                0,
                RecoveredNotePositionCalculator.CalculateSpeedRate(5d),
                0d,
                RecoveredGameConfigValues.PositionPow3Rate,
                RecoveredGameConfigValues.PositionPow1Rate);
            var firstValid = Mathf.Approximately(
                instance.localPosition.y + end.localPosition.y,
                firstExpected);
            runtime.Tick(500);
            var secondExpected = RecoveredNotePositionCalculator.CalculatePositionY(
                note.EndMilliseconds,
                500,
                RecoveredNotePositionCalculator.CalculateSpeedRate(5d),
                0d,
                RecoveredGameConfigValues.PositionPow3Rate,
                RecoveredGameConfigValues.PositionPow1Rate);
            var secondValid = Mathf.Approximately(
                instance.localPosition.y + end.localPosition.y,
                secondExpected);
            var followsHeight = !Mathf.Approximately(firstLocalHeight, end.localPosition.y);
            runtime.Reset();
            return firstValid && secondValid && followsHeight;
        }

        private static bool ValidateJumpScratchArrowVisualState(Transform parent)
        {
            var note = new RecoveredNotationNote
            {
                Id = 987657,
                NoteType = (int)RecoveredNoteType.ScratchHold,
                Lane = 3,
                Width = 3,
                StartTickCount = 1f,
                EndTickCount = 2f,
                GimmickType = 1,
                GimmickValue = -6,
            };
            var runtime = new RecoveredNoteVisualRuntime(
                new[] { note }, parent, LoadRuntimeNotePrefabs(), 5d,
                0d, AssetDatabase.LoadAssetAtPath<Material>(ScratchHoldMaterialPath));
            runtime.Tick(0);
            var instance = parent.Find("Runtime_987657_110");
            var left = instance != null ? FindDescendant(instance, "NotesLeft") : null;
            var right = instance != null ? FindDescendant(instance, "NotesRight") : null;
            var animator = instance != null
                ? instance.GetComponentInChildren<Animator>(true)
                : null;
            var enabledCount = 0;
            if (left != null)
            {
                for (var index = 0; index < left.childCount; index++)
                {
                    var renderer = left.GetChild(index).GetComponent<SpriteRenderer>();
                    if (renderer != null && renderer.enabled) enabledCount++;
                }
            }
            if (animator != null) animator.Update(0f);
            var actualState = animator != null
                ? animator.GetCurrentAnimatorStateInfo(0).shortNameHash
                : 0;
            var end = instance != null ? instance.Find("ScratchNote") : null;
            var endVisualWidth = float.NaN;
            if (end != null)
            {
                foreach (var renderer in end.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.drawMode == SpriteDrawMode.Simple) continue;
                    endVisualWidth = renderer.size.x;
                    break;
                }
            }
            var opposite = new RecoveredNotationNote
            {
                NoteType = (int)RecoveredNoteType.ScratchHold,
                Lane = 3,
                Width = 3,
                GimmickType = 1,
                GimmickValue = 6,
            };
            var oppositeEndX =
                RecoveredNoteVisualRuntime.GetJumpScratchEndOffsetX(opposite);
            var jumpLaneCount = Mathf.Abs(note.GimmickValue);
            var initialEndX = end != null ? end.localPosition.x : float.NaN;
            var expectedState = Animator.StringToHash(
                "ScratchNotesArrow_jump18_anim");
            var valid = left != null && right != null &&
                        left.gameObject.activeSelf && !right.gameObject.activeSelf &&
                        enabledCount == 18 && actualState == expectedState &&
                        end != null && Mathf.Approximately(
                            end.localPosition.x, -1.3875f) &&
                        Mathf.Approximately(oppositeEndX, 1.3875f) &&
                        Mathf.Approximately(endVisualWidth, 5.39f);

            // Reuse the exact same pooled prefab in both directions. The
            // original ScratchHoldNoteObject.Set resets _endScratch X before
            // the spawner optionally invokes SetJumpScratch.
            runtime.Reset();
            note.GimmickType = 0;
            note.GimmickValue = 0;
            runtime.Tick(0);
            instance = parent.Find("Runtime_987657_110");
            end = instance != null ? instance.Find("ScratchNote") : null;
            var plainAfterLeftX = end != null ? end.localPosition.x : float.NaN;

            runtime.Reset();
            note.GimmickType = 1;
            note.GimmickValue = 6;
            runtime.Tick(0);
            instance = parent.Find("Runtime_987657_110");
            end = instance != null ? instance.Find("ScratchNote") : null;
            var rightX = end != null ? end.localPosition.x : float.NaN;

            runtime.Reset();
            note.GimmickType = 0;
            note.GimmickValue = 0;
            runtime.Tick(0);
            instance = parent.Find("Runtime_987657_110");
            end = instance != null ? instance.Find("ScratchNote") : null;
            var plainAfterRightX = end != null ? end.localPosition.x : float.NaN;
            valid &= Mathf.Approximately(plainAfterLeftX, 0f) &&
                     Mathf.Approximately(rightX, 1.3875f) &&
                     Mathf.Approximately(plainAfterRightX, 0f);
            Debug.Log("OPENWDS_JUMP_SCRATCH_ARROW laneCount=" +
                      jumpLaneCount + " width=" + note.Width +
                      " enabled=" + enabledCount + " state=" + actualState +
                      " endX=" + initialEndX +
                      " oppositeEndX=" + oppositeEndX +
                      " endVisualWidth=" + endVisualWidth +
                      " pooledX=" + plainAfterLeftX + "/" + rightX + "/" +
                      plainAfterRightX +
                      " valid=" + valid);
            runtime.Reset();
            return valid;
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            if (parent == null) return null;
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child.name == name) return child;
                var found = FindDescendant(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static bool Approximately(Color left, Color right)
        {
            return Mathf.Approximately(left.r, right.r) &&
                   Mathf.Approximately(left.g, right.g) &&
                   Mathf.Approximately(left.b, right.b) &&
                   Mathf.Approximately(left.a, right.a);
        }

        private static bool ValidateInputOrdering()
        {
            var phases = new[]
            {
                RecoveredTouchPhase.Began,
                RecoveredTouchPhase.Moved,
                RecoveredTouchPhase.Stationary,
                RecoveredTouchPhase.Ended,
                RecoveredTouchPhase.Canceled,
                RecoveredTouchPhase.None,
            };
            for (var index = 0; index < phases.Length - 1; index++)
            {
                if (RecoveredInputOrdering.CompareTouchPhase(phases[index], phases[index + 1]) >= 0)
                {
                    return false;
                }
            }

            var inputs = new List<RecoveredInputEntity>
            {
                new RecoveredInputEntity(1, 30, Vector2.zero, Vector2.zero, Vector2.zero,
                    RecoveredTouchPhase.Began),
                new RecoveredInputEntity(2, 10, Vector2.zero, Vector2.zero, Vector2.zero,
                    RecoveredTouchPhase.Canceled),
                new RecoveredInputEntity(3, 20, Vector2.zero, Vector2.zero, Vector2.zero,
                    RecoveredTouchPhase.Moved),
            };
            RecoveredInputOrdering.SortLikeInputHandler(inputs);
            return inputs[0].Milliseconds == 10 &&
                   inputs[1].Milliseconds == 20 &&
                   inputs[2].Milliseconds == 30 &&
                   new RecoveredInputEntity(7, 1, Vector2.zero, Vector2.zero, Vector2.zero,
                       RecoveredTouchPhase.Began).Equals(
                       new RecoveredInputEntity(7, 2, Vector2.one, Vector2.one, Vector2.one,
                           RecoveredTouchPhase.Ended));
        }

        private static bool ValidateHitLaneEntity()
        {
            var hit = new RecoveredHitLaneEntity(0, 2, 3, 4, 5);
            var sameUniqueId = new RecoveredHitLaneEntity(0, 2, 3, 4, 5);
            var mainHit = new RecoveredHitLaneEntity(6, 2, 3, 4, 5);
            return hit.Exists &&
                   hit.GetLaneIdOrDefault() == 2 &&
                   hit.GetHashCode() == 2030405 &&
                   hit.Equals(sameUniqueId) &&
                   mainHit.GetLaneIdOrDefault() == 6 &&
                   !default(RecoveredHitLaneEntity).Exists;
        }

        private static bool ValidateInputLifecycle()
        {
            var flickInputs = new RecoveredFlickInputManager();
            var laneHits = new RecoveredLaneHitManager();
            var began = new RecoveredInputEntity(
                9, 100, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Began);
            var moved = new RecoveredInputEntity(
                9, 130, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Moved);
            var ended = new RecoveredInputEntity(
                9, 160, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Ended);

            if (!flickInputs.TrySetBeganTime(in began, out var beganMilliseconds) ||
                beganMilliseconds != 100 ||
                !flickInputs.TrySetBeganTime(in moved, out beganMilliseconds) ||
                beganMilliseconds != 100)
            {
                return false;
            }
            laneHits.Set(9, 4);
            if (laneHits.IsChangedHitLaneId(4, 9) ||
                !laneHits.IsChangedHitLaneId(5, 9) ||
                !RecoveredInputLifecycle.IsEnded(RecoveredTouchPhase.Canceled))
            {
                return false;
            }

            var removedHoldTouchId = 0;
            RecoveredInputLifecycle.FinalizeEndedTouches(
                new[] { ended },
                flickInputs,
                laneHits,
                touchId => removedHoldTouchId = touchId);
            return flickInputs.Count == 0 &&
                   laneHits.Count == 0 &&
                   removedHoldTouchId == 9;
        }

        private static bool ValidateInputFireCore()
        {
            var early = new RecoveredNotationNote { StartTickCount = 1f, NoteType = 10 };
            var late = new RecoveredNotationNote { StartTickCount = 2f, NoteType = 50 };
            var candidates = RecoveredInputFireCore.CreateSortedCandidates(
                new[] { late },
                new[] { early });
            if (candidates[0] != early ||
                RecoveredInputFireCore.GetCandidateAction(80) != RecoveredCandidateAction.Tap ||
                RecoveredInputFireCore.GetCandidateAction(50) != RecoveredCandidateAction.Flick ||
                RecoveredInputFireCore.GetCandidateAction(900) != RecoveredCandidateAction.Ignore)
            {
                return false;
            }

            var touches = new List<RecoveredInputEntity>
            {
                new RecoveredInputEntity(12, 1000, Vector2.zero, Vector2.zero,
                    Vector2.zero, RecoveredTouchPhase.Began),
            };
            var hitLanes = new List<RecoveredHitLaneEntity>
            {
                new RecoveredHitLaneEntity(3, 0, 0, 0, 0),
            };
            var beganHitLanes = new List<RecoveredHitLaneEntity>(hitLanes);
            var laneHits = new RecoveredLaneHitManager();
            var endedTouches = new List<RecoveredInputEntity>();
            RecoveredInputFireCore.ConsumeTapAndFlickPass(
                touches,
                hitLanes,
                beganHitLanes,
                candidates,
                laneHits,
                (input, current, began, note) =>
                    new RecoveredCandidateDecision(note == early, current.GetLaneIdOrDefault()),
                endedTouches);
            return touches.Count == 0 &&
                   hitLanes.Count == 0 &&
                   candidates.Count == 1 &&
                   candidates[0] == late &&
                   laneHits.Count == 1;
        }

        private static bool ValidateAutoTouch(
            Camera camera,
            GameObject laneGroupObject,
            out int scheduledEvents,
            out bool laneHoldLifecycleValid)
        {
            scheduledEvents = 0;
            laneHoldLifecycleValid = false;
            var laneGroup = laneGroupObject.GetComponent<Sirius.Game.LaneGroup>();
            if (laneGroup == null || laneGroup.LaneCount != 12)
            {
                return false;
            }
            var chartRoot = Path.Combine(
                Application.dataPath, "StreamingAssets", "OpenWDS",
                "StandardCharts", "1", "1");
            var notation = RecoveredStandardNotation.Parse(
                File.ReadAllText(Path.Combine(chartRoot, "1.csv")));
            var clock = new RecoveredGameClock(3.019f, 0d);
            var autoTouch = new RecoveredAutoTouch(
                clock,
                notation,
                note =>
                {
                    var world = laneGroup.GetLaneCollider(note.Lane).position;
                    var screen = camera.WorldToScreenPoint(world);
                    return new Vector2(screen.x, screen.y);
                });
            scheduledEvents = autoTouch.ScheduledEventCount;
            if (scheduledEvents != 142)
            {
                return false;
            }

            RecoveredNotationNote firstHoldStart = null;
            RecoveredNotationNote firstHoldBody = null;
            foreach (var note in notation)
            {
                if (note.NoteType == (int)RecoveredNoteType.HoldStart &&
                    note.Lane == 7 && note.Width == 4 &&
                    note.StartMilliseconds == 3050)
                {
                    firstHoldStart = note;
                }
                else if (note.NoteType == (int)RecoveredNoteType.Hold &&
                         note.Lane == 7 && note.Width == 4 &&
                         note.StartMilliseconds == 3050)
                {
                    firstHoldBody = note;
                }
            }
            if (firstHoldStart == null || firstHoldBody == null)
            {
                return false;
            }
            var holdManager = new RecoveredLaneHoldManager();
            var holdEvents = new List<RecoveredInputEffectEntity>();

            clock.Sync(6.07f, 6070, 10f, 10000); // chart time 3051 ms
            var touches = new List<RecoveredInputEntity>();
            autoTouch.GetTouches(touches);
            if (touches.Count != 6 || autoTouch.ActiveHoldingCount != 1)
            {
                return false;
            }
            var foundConvertedHoldBegin = false;
            foreach (var touch in touches)
            {
                if (touch.Phase == RecoveredTouchPhase.Began &&
                    touch.Milliseconds == 9999 &&
                    touch.TouchId == firstHoldBody.Id)
                {
                    foundConvertedHoldBegin = true;
                    holdManager.Set(touch.TouchId, firstHoldBody);
                }
            }
            holdManager.GetHoldEvents(holdEvents);
            if (holdEvents.Count != 1 ||
                holdEvents[0].NoteId != firstHoldBody.Id ||
                holdEvents[0].StartMilliseconds != firstHoldBody.StartMilliseconds ||
                holdEvents[0].LaneId != 7 || holdEvents[0].Width != 4 ||
                holdEvents[0].NoteType != RecoveredNoteType.Hold ||
                holdEvents[0].TimingType != RecoveredTimingType.None ||
                holdEvents[0].EffectType != RecoveredInputEffectType.HoldStart)
            {
                return false;
            }
            holdEvents.Clear();
            touches.Clear();
            autoTouch.GetTouches(touches);
            if (!foundConvertedHoldBegin || touches.Count != 1 ||
                touches[0].Phase != RecoveredTouchPhase.Stationary)
            {
                return false;
            }
            holdManager.Set(touches[0].TouchId, firstHoldBody);
            holdManager.GetHoldEvents(holdEvents);
            if (holdEvents.Count != 0 || holdManager.Count != 1)
            {
                return false;
            }

            clock.Sync(8.104f, 8104, 12f, 12000); // chart time 5085 ms
            touches.Clear();
            autoTouch.GetTouches(touches);
            if (touches.Count != 1 ||
                touches[0].Phase != RecoveredTouchPhase.Ended ||
                touches[0].TouchId != firstHoldBody.Id ||
                autoTouch.ActiveHoldingCount != 0)
            {
                return false;
            }
            var timing = RecoveredHoldTimingDecider.DecideEnd(
                clock, touches[0].Milliseconds, firstHoldBody);
            holdManager.Remove(touches[0].TouchId);
            holdManager.GetHoldEvents(holdEvents);
            laneHoldLifecycleValid =
                timing.TimingType == RecoveredTimingType.PerfectStar &&
                timing.TimingAssistType == RecoveredTimingAssistType.None &&
                holdManager.Count == 0 &&
                holdEvents.Count == 1 &&
                holdEvents[0].NoteId == firstHoldBody.Id &&
                holdEvents[0].EffectType == RecoveredInputEffectType.HoldEnd &&
                ValidateSharedHoldOccupancy(firstHoldBody);
            return laneHoldLifecycleValid;
        }

        private static bool ValidateSharedHoldOccupancy(RecoveredNotationNote hold)
        {
            var manager = new RecoveredLaneHoldManager();
            var events = new List<RecoveredInputEffectEntity>();
            manager.Set(101, hold);
            manager.Set(102, hold);
            manager.GetHoldEvents(events);
            if (events.Count != 1 ||
                events[0].EffectType != RecoveredInputEffectType.HoldStart ||
                manager.Count != 2 || !manager.Exists(hold, 101))
            {
                return false;
            }

            events.Clear();
            manager.Remove(101);
            manager.GetHoldEvents(events);
            if (events.Count != 0 || manager.Count != 1)
            {
                return false;
            }

            manager.Remove(102);
            manager.GetHoldEvents(events);
            return events.Count == 1 &&
                   events[0].EffectType == RecoveredInputEffectType.HoldEnd &&
                   manager.Count == 0;
        }

        private static bool ValidateStandardHoldAction()
        {
            var chartPath = Path.Combine(
                Application.dataPath, "StreamingAssets", "OpenWDS",
                "StandardCharts", "1", "1", "1.csv");
            var notation = RecoveredStandardNotation.Parse(File.ReadAllText(chartPath));
            var manager = new RecoveredStandardHoldNoteManager(notation);
            if (manager.HoldNoteCount != 24 ||
                manager.HoldingNoteCount != 102 ||
                manager.UncompletedHoldStartNoteCount != 24)
            {
                return false;
            }

            var hitLane = new RecoveredHitLaneEntity(7, 0, 0, 0, 0);
            var tooEarlyAction = new RecoveredStandardHoldAction(manager);
            var stationary = new RecoveredInputEntity(
                5, 0, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Stationary);
            // AutoTouch sends Stationary on every rendered frame. Sweep the
            // complete early GOOD window so a discrete test cannot accidentally
            // jump from -126 ms straight to the exact end again.
            for (var musicMilliseconds = 4958L;
                 musicMilliseconds < 5084L;
                 musicMilliseconds++)
            {
                if (tooEarlyAction.TryHold(
                        stationary, hitLane, musicMilliseconds).Consumed)
                    return false;
            }

            // isHolding=false first acquires the body against StartMilliseconds;
            // a later call with LaneHold's current body judges EndMilliseconds.
            var action = new RecoveredStandardHoldAction(manager);
            var acquired = action.TryHold(stationary, hitLane, 3050);
            if (!acquired.Assigned || acquired.Consumed ||
                acquired.HoldNote == null)
            {
                return false;
            }
            var result = action.TryHold(
                stationary, hitLane, 5084, acquired.HoldNote);
            if (!result.Consumed || result.LaneId != 7 ||
                result.HoldNote == null || result.HoldStartNote == null ||
                result.HoldNote.NoteType != (int)RecoveredNoteType.Hold ||
                result.HoldNote.StartMilliseconds != 3050 ||
                result.HoldNote.EndMilliseconds != 5084 ||
                result.HoldStartNote.StartMilliseconds != 3050 ||
                result.Timing.TimingType != RecoveredTimingType.PerfectStar ||
                result.DeletedHoldingNoteCount != 10 ||
                manager.HoldNoteCount != 23 ||
                manager.HoldingNoteCount != 92 ||
                manager.UncompletedHoldStartNoteCount != 23 ||
                manager.IsHoldStartNoteNotCompleted(result.HoldStartNote))
            {
                return false;
            }

            // InputAction.OnHoldEnd uses HoldTimingDecider before the endpoint.
            // End-126 is outside the window; End-125 is the inclusive GOOD
            // boundary and End-60 is the PERFECT_STAR boundary.
            var releaseManager = new RecoveredStandardHoldNoteManager(notation);
            var releaseAction = new RecoveredStandardHoldAction(releaseManager);
            var ended = new RecoveredInputEntity(
                5, 0, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Ended);
            var releaseAcquired =
                releaseAction.TryHold(stationary, hitLane, 3050);
            if (!releaseAcquired.Assigned || releaseAcquired.Consumed ||
                releaseAction.TryHold(
                    ended, hitLane, 4958, releaseAcquired.HoldNote).Consumed)
                return false;

            var graceManager = new RecoveredStandardHoldNoteManager(notation);
            var graceAction = new RecoveredStandardHoldAction(graceManager);
            var graceAcquired = graceAction.TryHold(stationary, hitLane, 4500);
            var releaseResult = graceAction.TryHold(
                ended, hitLane, 4959, graceAcquired.HoldNote);
            if (!releaseResult.Consumed ||
                releaseResult.Timing.TimingType != RecoveredTimingType.Good ||
                graceManager.HoldNoteCount != 23 ||
                graceManager.HoldingNoteCount != 92 ||
                graceManager.UncompletedHoldStartNoteCount != 23)
            {
                return false;
            }

            var sound = new RecoveredNotationNote
            {
                Id = 9200, StartTickCount = 1f, EndTickCount = -1f,
                NoteType = (int)RecoveredNoteType.Sound, Lane = 7, Width = 1,
            };
            var purple = new RecoveredNotationNote
            {
                Id = 9201, StartTickCount = 1f, EndTickCount = -1f,
                NoteType = (int)RecoveredNoteType.SoundPurple, Lane = 7, Width = 1,
            };
            var eighth = new RecoveredNotationNote
            {
                Id = 9202, StartTickCount = 1f, EndTickCount = -1f,
                NoteType = (int)RecoveredNoteType.HoldEighth, Lane = 7, Width = 1,
            };
            var holdingManager = new RecoveredStandardHoldNoteManager(
                new[] { sound, purple, eighth });
            var unified = new RecoveredUnifiedHoldAction(
                holdingManager,
                new RecoveredScratchNoteManager(
                    new[] { sound, purple, eighth }));
            var holdingOnly = unified.TryHold(stationary, hitLane, 1000);
            return !holdingOnly.Consumed &&
                   unified.HoldingResults.Count == 3 &&
                   unified.HoldingResults[0] == sound &&
                   unified.HoldingResults[1] == purple &&
                   unified.HoldingResults[2] == eighth &&
                   holdingManager.HoldingNoteCount == 0;
        }

        private static bool ValidateHoldFireIntegration()
        {
            var chartPath = Path.Combine(
                Application.dataPath, "StreamingAssets", "OpenWDS",
                "StandardCharts", "1", "1", "1.csv");
            var notation = RecoveredStandardNotation.Parse(File.ReadAllText(chartPath));
            var noteManager = new RecoveredStandardHoldNoteManager(notation);
            var action = new RecoveredStandardHoldAction(noteManager);
            var input = new RecoveredInputEntity(
                77, 0, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Stationary);
            var touches = new List<RecoveredInputEntity> { input };
            var hitLanes = new List<RecoveredHitLaneEntity>
            {
                new RecoveredHitLaneEntity(7, 0, 0, 0, 0),
            };
            var laneHits = new RecoveredLaneHitManager();
            var laneHolds = new RecoveredLaneHoldManager();
            var assigned = RecoveredInputFireCore.ConsumeHoldPass(
                touches,
                hitLanes,
                laneHits,
                laneHolds,
                (currentInput, currentHitLane) =>
                    action.TryHold(currentInput, currentHitLane, 3050));
            var consumed = RecoveredInputFireCore.ConsumeHoldPass(
                touches,
                hitLanes,
                laneHits,
                laneHolds,
                (currentInput, currentHitLane) =>
                    action.TryHold(
                        currentInput, currentHitLane, 5084,
                        laneHolds.TryGet(
                            currentInput.TouchId, out var currentHold)
                            ? currentHold
                            : null));
            var effects = new List<RecoveredInputEffectEntity>();
            laneHolds.GetHoldEvents(effects);
            var ordinaryLifecycleValid =
                   assigned == 0 && consumed == 1 &&
                   touches.Count == 1 && hitLanes.Count == 1 &&
                   laneHits.Count == 1 && laneHolds.Count == 1 &&
                   effects.Count == 1 &&
                   effects[0].StartMilliseconds == 3050 &&
                   effects[0].LaneId == 7 && effects[0].Width == 4 &&
                   effects[0].EffectType == RecoveredInputEffectType.HoldStart;
            if (!ordinaryLifecycleValid) return false;

            // A released body remains assignable for its complete duration.
            // Replacing the finger emits End/Start so NoteView restores its
            // judgment-line crop, and End-125 is accepted as an early GOOD.
            var reconnectStart = new RecoveredNotationNote
            {
                Id = 9900,
                StartTickCount = 1f,
                EndTickCount = -1f,
                NoteType = (int)RecoveredNoteType.HoldStart,
                Lane = 4,
                Width = 2,
            };
            var reconnectBody = new RecoveredNotationNote
            {
                Id = 9901,
                StartTickCount = 1f,
                EndTickCount = 2f,
                NoteType = (int)RecoveredNoteType.Hold,
                Lane = 4,
                Width = 2,
            };
            var reconnectManager = new RecoveredStandardHoldNoteManager(
                new[] { reconnectStart, reconnectBody });
            var reconnectAction = new RecoveredStandardHoldAction(
                reconnectManager);
            var reconnectHolds = new RecoveredLaneHoldManager();
            var reconnectHits = new RecoveredLaneHitManager();
            var reconnectLane = new RecoveredHitLaneEntity(4, 0, 0, 0, 0);
            var firstTouch = new RecoveredInputEntity(
                201, 1200, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Stationary);
            var secondTouch = new RecoveredInputEntity(
                202, 1500, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Stationary);
            var firstInputs = new List<RecoveredInputEntity> { firstTouch };
            var secondInputs = new List<RecoveredInputEntity> { secondTouch };
            var reconnectLanes = new List<RecoveredHitLaneEntity> {
                reconnectLane
            };
            RecoveredInputFireCore.ConsumeHoldPass(
                firstInputs, reconnectLanes, reconnectHits, reconnectHolds,
                (currentInput, currentLane) =>
                    reconnectAction.TryHold(currentInput, currentLane, 1200));
            effects.Clear();
            reconnectHolds.GetHoldEvents(effects);
            var firstStartValid = effects.Count == 1 &&
                effects[0].NoteId == reconnectBody.Id &&
                effects[0].EffectType == RecoveredInputEffectType.HoldStart;
            reconnectHolds.Remove(firstTouch.TouchId);
            effects.Clear();
            reconnectHolds.GetHoldEvents(effects);
            var liftEndValid = effects.Count == 1 &&
                effects[0].NoteId == reconnectBody.Id &&
                effects[0].EffectType == RecoveredInputEffectType.HoldEnd;
            RecoveredInputFireCore.ConsumeHoldPass(
                secondInputs, reconnectLanes, reconnectHits, reconnectHolds,
                (currentInput, currentLane) =>
                    reconnectAction.TryHold(currentInput, currentLane, 1500));
            effects.Clear();
            reconnectHolds.GetHoldEvents(effects);
            var replacementStartValid = effects.Count == 1 &&
                effects[0].NoteId == reconnectBody.Id &&
                effects[0].EffectType == RecoveredInputEffectType.HoldStart &&
                reconnectHolds.TryGet(secondTouch.TouchId, out var reassigned) &&
                reassigned == reconnectBody;
            var earlyEnd = new RecoveredInputEntity(
                202, 1875, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Ended);
            secondInputs[0] = earlyEnd;
            var earlyConsumed = RecoveredInputFireCore.ConsumeHoldPass(
                secondInputs, reconnectLanes, reconnectHits, reconnectHolds,
                (currentInput, currentLane) =>
                    reconnectAction.TryHold(
                        currentInput, currentLane, 1875,
                        reconnectHolds.TryGet(
                            currentInput.TouchId, out var current)
                            ? current
                            : null));
            if (!firstStartValid || !liftEndValid ||
                !replacementStartValid || earlyConsumed != 1 ||
                reconnectManager.HoldNoteCount != 0)
            {
                return false;
            }

            // Exercise the public pause transition used by GameRuntime:
            // RemoveAll publishes HoldEnd, then a countdown touch is accepted
            // as a fresh HoldStart and can use the same early tail window.
            var pauseRuntime = new RecoveredInputHandlerRuntime(
                new[] { reconnectBody },
                0f,
                note => Vector2.zero,
                position => reconnectLane);
            pauseRuntime.TickPlayer(1200, 1200, firstInputs);
            var pauseInitialStart = pauseRuntime.ActiveTouchHoldCount == 1 &&
                pauseRuntime.HoldEvents.Count == 1 &&
                pauseRuntime.HoldEvents[0].EffectType ==
                    RecoveredInputEffectType.HoldStart;
            pauseRuntime.ReleaseHoldLanesForPause();
            var pauseReleased = pauseRuntime.ActiveTouchHoldCount == 0 &&
                pauseRuntime.HoldEvents.Count == 1 &&
                pauseRuntime.HoldEvents[0].EffectType ==
                    RecoveredInputEffectType.HoldEnd;
            pauseRuntime.TickPlayer(
                1500, 1500, new[] { secondTouch });
            var pauseReacquired = pauseRuntime.ActiveTouchHoldCount == 1 &&
                pauseRuntime.HoldEvents.Count == 1 &&
                pauseRuntime.HoldEvents[0].EffectType ==
                    RecoveredInputEffectType.HoldStart;
            pauseRuntime.TickPlayer(1875, 1875, new[] { earlyEnd });
            var pauseTailValid = pauseRuntime.InputResults.Count == 1 &&
                pauseRuntime.InputResults[0].TimingType ==
                    RecoveredTimingType.Good &&
                pauseRuntime.RemainingHoldCount == 0;
            pauseRuntime.Dispose();
            return pauseInitialStart && pauseReleased &&
                   pauseReacquired && pauseTailValid;
        }

        private static bool ValidateFlickAction()
        {
            var chartPath = Path.Combine(
                Application.dataPath, "StreamingAssets", "OpenWDS",
                "StandardCharts", "1", "1", "2.csv");
            var notation = RecoveredStandardNotation.Parse(File.ReadAllText(chartPath));
            RecoveredNotationNote firstFlick = null;
            foreach (var note in notation)
            {
                if (note.NoteType == (int)RecoveredNoteType.Flick)
                {
                    firstFlick = note;
                    break;
                }
            }
            if (firstFlick == null || firstFlick.StartMilliseconds != 68050 ||
                firstFlick.Lane != 1 || firstFlick.Width != 3)
            {
                return false;
            }

            var atMinus70 = RecoveredFlickTimingDecider.DecideMusicTime(67980, 68050);
            var atPlus70 = RecoveredFlickTimingDecider.DecideMusicTime(68120, 68050);
            var atPlus71 = RecoveredFlickTimingDecider.DecideMusicTime(68121, 68050);
            var atPlus100 = RecoveredFlickTimingDecider.DecideMusicTime(68150, 68050);
            var outside = RecoveredFlickTimingDecider.DecideMusicTime(68151, 68050);
            if (atMinus70.TimingType != RecoveredTimingType.PerfectStar ||
                atPlus70.TimingType != RecoveredTimingType.PerfectStar ||
                atPlus71.TimingType != RecoveredTimingType.Great ||
                atPlus100.TimingType != RecoveredTimingType.Great ||
                outside.TimingType != RecoveredTimingType.None ||
                atMinus70.TimingAssistType != RecoveredTimingAssistType.None ||
                atMinus70.DiffMilliseconds != 0)
            {
                return false;
            }

            var clock = new RecoveredGameClock(3.019f, 0d);
            clock.Sync(71.069f, 71069, 100f, 100000); // music time 68050 ms
            var flickInputs = new RecoveredFlickInputManager();
            var flickNotes = new RecoveredFlickNoteManager(notation);
            if (flickNotes.Count != 3 ||
                !RecoveredFlickTimingDecider.IsBeganRangeTarget(
                    clock, firstFlick, 99900) ||
                RecoveredFlickTimingDecider.IsBeganRangeTarget(
                    clock, firstFlick, 100100))
            {
                return false;
            }
            var action = new RecoveredFlickAction(
                clock, flickInputs, flickNotes);
            var began = new RecoveredInputEntity(
                91, 100000, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Began);
            if (action.TryFlick(began, firstFlick).Consumed || flickInputs.Count != 1)
            {
                return false;
            }
            var insufficient = new RecoveredInputEntity(
                91, 100000, Vector2.zero, Vector2.zero, new Vector2(5f, 5f),
                RecoveredTouchPhase.Moved);
            if (action.TryFlick(insufficient, firstFlick).Consumed ||
                flickNotes.Count != 3)
            {
                return false;
            }
            var moved = new RecoveredInputEntity(
                91, 100000, Vector2.zero, Vector2.zero, new Vector2(6f, 5f),
                RecoveredTouchPhase.Moved);
            var movedResult = action.TryFlick(moved, firstFlick);
            if (!movedResult.Consumed || !movedResult.DeletedNote ||
                movedResult.CompletionReason != RecoveredFlickCompletionReason.Moved ||
                movedResult.Timing.TimingType != RecoveredTimingType.PerfectStar ||
                flickNotes.Count != 2 || flickInputs.Count != 0)
            {
                return false;
            }

            var releaseInputs = new RecoveredFlickInputManager();
            var releaseNotes = new RecoveredFlickNoteManager(notation);
            var releaseAction = new RecoveredFlickAction(
                clock, releaseInputs, releaseNotes);
            releaseAction.TryFlick(began, firstFlick);
            var ended = new RecoveredInputEntity(
                91, 100050, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Ended);
            var releasedResult = releaseAction.TryFlick(ended, firstFlick);
            if (!releasedResult.Consumed ||
                releasedResult.CompletionReason != RecoveredFlickCompletionReason.Released ||
                releasedResult.Timing.TimingType != RecoveredTimingType.Great ||
                releaseNotes.Count != 2 || releaseInputs.Count != 0)
            {
                return false;
            }

            var expiredInputs = new RecoveredFlickInputManager();
            var expiredNotes = new RecoveredFlickNoteManager(notation);
            var expiredAction = new RecoveredFlickAction(
                clock, expiredInputs, expiredNotes);
            expiredAction.TryFlick(began, firstFlick);
            clock.Sync(71.149f, 71149, 100.08f, 100080); // both timelines +80 ms
            var stationary = new RecoveredInputEntity(
                91, 100080, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Stationary);
            var expiredResult = expiredAction.TryFlick(stationary, firstFlick);
            if (!expiredResult.Consumed || !expiredResult.DeletedNote ||
                expiredResult.CompletionReason != RecoveredFlickCompletionReason.Expired ||
                expiredResult.Timing.TimingType != RecoveredTimingType.Great ||
                expiredNotes.Count != 2 || expiredInputs.Count != 0)
            {
                return false;
            }

            // Feed the original AutoTouch Flick pair through the recovered action.
            var autoClock = new RecoveredGameClock(3.019f, 0d);
            autoClock.Sync(71.069f, 71069, 100f, 100000);
            var autoTouch = new RecoveredAutoTouch(
                autoClock,
                new[] { firstFlick },
                _ => new Vector2(320f, 180f));
            var autoInputs = new List<RecoveredInputEntity>();
            autoTouch.GetTouches(autoInputs);
            if (autoTouch.ScheduledEventCount != 2 || autoInputs.Count != 2 ||
                autoInputs[0].Phase != RecoveredTouchPhase.Began ||
                autoInputs[0].Milliseconds != 99999 ||
                autoInputs[1].Phase != RecoveredTouchPhase.Moved ||
                autoInputs[1].Milliseconds != 100000 ||
                autoInputs[1].DeltaPosition != new Vector2(50f, 50f) ||
                autoTouch.ActiveHoldingCount != 0)
            {
                return false;
            }
            var autoFlickInputs = new RecoveredFlickInputManager();
            var autoFlickNotes = new RecoveredFlickNoteManager(new[] { firstFlick });
            var autoAction = new RecoveredFlickAction(
                autoClock, autoFlickInputs, autoFlickNotes);
            var autoBeginResult = autoAction.TryFlick(autoInputs[0], firstFlick);
            var autoMovedResult = autoAction.TryFlick(autoInputs[1], firstFlick);
            return !autoBeginResult.Consumed && autoMovedResult.Consumed &&
                   autoMovedResult.DeletedNote &&
                   autoMovedResult.CompletionReason == RecoveredFlickCompletionReason.Moved &&
                   autoMovedResult.Timing.TimingType == RecoveredTimingType.PerfectStar &&
                   autoFlickNotes.Count == 0 && autoFlickInputs.Count == 0 &&
                   RecoveredOriginalGameConfig.FlickDistance == 50f &&
                   RecoveredOriginalGameConfig.FlickExpiredMilliseconds == 80L;
        }

        private static bool ValidateScratchAction()
        {
            var chartPath = Path.Combine(
                Application.dataPath, "StreamingAssets", "OpenWDS",
                "StandardCharts", "1", "1", "2.csv");
            var notation = RecoveredStandardNotation.Parse(File.ReadAllText(chartPath));
            RecoveredNotationNote firstScratchHold = null;
            foreach (var note in notation)
            {
                if (note.NoteType == (int)RecoveredNoteType.ScratchHold)
                {
                    firstScratchHold = note;
                    break;
                }
            }
            if (firstScratchHold == null ||
                firstScratchHold.StartMilliseconds != 762 ||
                firstScratchHold.EndMilliseconds != 1016 ||
                firstScratchHold.Lane != 5 || firstScratchHold.Width != 4)
            {
                return false;
            }

            var earlyBoundary = RecoveredScratchTimingDecider.DecideMusicTime(
                966, firstScratchHold);
            var justBoundary = RecoveredScratchTimingDecider.DecideMusicTime(
                1066, firstScratchHold);
            var tooEarly = RecoveredScratchTimingDecider.DecideMusicTime(
                965, firstScratchHold);
            if (earlyBoundary.TimingType != RecoveredTimingType.PerfectStar ||
                earlyBoundary.DiffMilliseconds != -50 ||
                justBoundary.TimingType != RecoveredTimingType.Great ||
                justBoundary.DiffMilliseconds != 50 ||
                tooEarly.TimingType != RecoveredTimingType.None)
            {
                return false;
            }

            var manager = new RecoveredScratchNoteManager(notation);
            if (manager.Count != 46 ||
                !manager.IsIncludedLane(5, firstScratchHold) ||
                !manager.IsIncludedLane(8, firstScratchHold) ||
                manager.IsIncludedLane(4, firstScratchHold) ||
                manager.IsIncludedLane(9, firstScratchHold))
            {
                return false;
            }

            RecoveredNotationNote leftJump = null;
            RecoveredNotationNote rightJump = null;
            foreach (var note in notation)
            {
                if (!RecoveredJumpScratch.IsJumpScratch(note)) continue;
                if (note.GimmickValue < 0 && leftJump == null) leftJump = note;
                if (note.GimmickValue > 0 && rightJump == null) rightJump = note;
            }
            if (leftJump == null || rightJump == null) return false;
            var leftRange = RecoveredJumpScratch.GetLaneRange(leftJump);
            var rightRange = RecoveredJumpScratch.GetLaneRange(rightJump);
            if (leftJump.StartMilliseconds != 3050 ||
                leftRange.StartLane != 7 || leftRange.EndLane != 12 ||
                rightJump.StartMilliseconds != 3559 ||
                rightRange.StartLane != 7 || rightRange.EndLane != 12 ||
                !manager.IsIncludedLane(7, leftJump, true) ||
                !manager.IsIncludedLane(12, rightJump, true) ||
                manager.IsIncludedLane(6, leftJump, true) ||
                manager.IsIncludedLane(7, firstScratchHold, true) ||
                !RecoveredJumpScratch.IsConnectedNext(leftJump, rightJump) ||
                !RecoveredJumpScratch.IsConnectedPrevious(rightJump, leftJump) ||
                !RecoveredJumpScratch.ExistsConnectedNext(notation, leftJump) ||
                !RecoveredJumpScratch.ExistsConnectedPrevious(notation, rightJump) ||
                RecoveredJumpScratch.ExistsConnectedPrevious(notation, leftJump) ||
                RecoveredJumpScratch.IsConnectedNext(firstScratchHold, leftJump))
            {
                return false;
            }

            if (!manager.CanDeleteAutomatically(firstScratchHold) ||
                !manager.AddPendingDecide(firstScratchHold) ||
                manager.AddPendingDecide(firstScratchHold) ||
                manager.PendingDecideCount != 1 ||
                manager.CanDeleteAutomatically(firstScratchHold))
            {
                return false;
            }
            manager.ResetPendingDecide();
            if (manager.PendingDecideCount != 0 ||
                !manager.CanDeleteAutomatically(firstScratchHold))
            {
                return false;
            }

            var action = new RecoveredScratchAction(manager);
            var insufficient = new RecoveredInputEntity(
                301, 0, Vector2.zero, Vector2.zero, new Vector2(5f, 5f),
                RecoveredTouchPhase.Moved);
            var beforeClockRange = action.TryScratch(
                insufficient, firstScratchHold, 5, 1016, false, 965);
            var pending = action.TryScratch(
                insufficient, firstScratchHold, 5, 1016, false, 966);
            if (beforeClockRange.Consumed || beforeClockRange.AddedPendingDecide ||
                pending.Consumed || !pending.AddedPendingDecide ||
                manager.PendingDecideCount != 1 ||
                !RecoveredScratchTimingDecider.IsLessThanMinTiming(
                    firstScratchHold, 965) ||
                !RecoveredScratchTimingDecider.IsMinTimingToJustTiming(
                    firstScratchHold, 966) ||
                !RecoveredScratchTimingDecider.IsJustTimingToMaxTiming(
                    firstScratchHold, 1066))
            {
                return false;
            }
            manager.ResetPendingDecide();
            var moved = new RecoveredInputEntity(
                301, 0, Vector2.zero, Vector2.zero, new Vector2(6f, 5f),
                RecoveredTouchPhase.Moved);
            var result = action.TryScratch(moved, firstScratchHold, 8, 1016);
            if (!result.Consumed || !result.DeletedNote ||
                result.Timing.TimingType != RecoveredTimingType.PerfectStar ||
                manager.Count != 45)
            {
                return false;
            }

            // The late branch is deliberately phase/distance independent and
            // publishes the fixed GREAT/None/0 decision used by TryScratchCore.
            var lateManager = new RecoveredScratchNoteManager(
                new[] { firstScratchHold });
            var lateAction = new RecoveredScratchAction(lateManager);
            var endedWithoutMovement = new RecoveredInputEntity(
                302, 0, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Ended);
            var lateResult = lateAction.TryScratch(
                endedWithoutMovement, firstScratchHold, 5, 1066);
            if (!lateResult.Consumed || !lateResult.DeletedNote ||
                lateResult.AddedPendingDecide ||
                lateResult.Timing.TimingType != RecoveredTimingType.Great ||
                lateResult.Timing.TimingAssistType !=
                    RecoveredTimingAssistType.None ||
                lateResult.Timing.DiffMilliseconds != 0 ||
                lateManager.Count != 0)
            {
                return false;
            }

            // AutoTouch.Initialize's real Jump-Scratch chain: the head emits a
            // Start-time Began, every 110/111 emits Moved at its own End, and
            // the terminal note adds Ended at End + 1 ms.
            var jumpClock = new RecoveredGameClock(0f, 0d);
            var jumpAutoTouch = new RecoveredAutoTouch(
                jumpClock,
                notation,
                note => new Vector2(note.Lane, 0f));
            var chainInputs = new List<RecoveredInputEntity>();
            foreach (var input in jumpAutoTouch.ScheduledEvents)
            {
                if (input.TouchId == leftJump.Id) chainInputs.Add(input);
            }
            if (chainInputs.Count != 6 ||
                chainInputs[0].Phase != RecoveredTouchPhase.Began ||
                chainInputs[0].Milliseconds != 3050 ||
                chainInputs[0].ScreenPosition.x != 9f ||
                chainInputs[1].Phase != RecoveredTouchPhase.Moved ||
                chainInputs[1].Milliseconds != 3559 ||
                chainInputs[1].ScreenPosition.x != 9f ||
                chainInputs[2].Phase != RecoveredTouchPhase.Moved ||
                chainInputs[2].Milliseconds != 4067 ||
                chainInputs[2].ScreenPosition.x != 12f ||
                chainInputs[3].Phase != RecoveredTouchPhase.Moved ||
                chainInputs[3].Milliseconds != 4576 ||
                chainInputs[3].ScreenPosition.x != 7f ||
                chainInputs[4].Phase != RecoveredTouchPhase.Moved ||
                chainInputs[4].Milliseconds != 5084 ||
                chainInputs[4].ScreenPosition.x != 12f ||
                chainInputs[5].Phase != RecoveredTouchPhase.Ended ||
                chainInputs[5].Milliseconds != 5085 ||
                RecoveredJumpScratch.GetConnectedFirst(notation, rightJump) != leftJump ||
                RecoveredJumpScratch.GetDestinationLane(leftJump) != 7 ||
                RecoveredJumpScratch.GetDestinationLane(rightJump) != 12)
            {
                return false;
            }

            var jumpManager = new RecoveredScratchNoteManager(notation);
            var jumpAction = new RecoveredScratchAction(jumpManager);
            if (!jumpAction.TryScratch(
                    chainInputs[2], rightJump, 12, rightJump.EndMilliseconds,
                    true).Consumed)
            {
                return false;
            }

            // Feed the chain head's original End-time AutoTouch movement through
            // the complete Hold pass. It must judge the first segment at its own
            // tail instead of waiting for the next connected segment.
            var unifiedHoldNotes = new RecoveredStandardHoldNoteManager(notation);
            var unifiedScratchNotes = new RecoveredScratchNoteManager(notation);
            var unifiedAction = new RecoveredUnifiedHoldAction(
                unifiedHoldNotes, unifiedScratchNotes);
            var unifiedTouches = new List<RecoveredInputEntity> { chainInputs[1] };
            var unifiedHitLanes = new List<RecoveredHitLaneEntity>
            {
                new RecoveredHitLaneEntity(9, 0, 0, 0, 0),
            };
            var unifiedLaneHits = new RecoveredLaneHitManager();
            var unifiedLaneHolds = new RecoveredLaneHoldManager();
            RecoveredHoldActionResult unifiedResult = default;
            var unifiedConsumed = RecoveredInputFireCore.ConsumeHoldPass(
                unifiedTouches,
                unifiedHitLanes,
                unifiedLaneHits,
                unifiedLaneHolds,
                (currentInput, currentHitLane) =>
                {
                    unifiedResult = unifiedAction.TryHold(
                        currentInput, currentHitLane, 3559);
                    return unifiedResult;
                });
            var unifiedEffects = new List<RecoveredInputEffectEntity>();
            unifiedLaneHolds.GetHoldEvents(unifiedEffects);
            Debug.Log("OPENWDS_JUMP_HEAD_AUTOPLAY consumed=" + unifiedConsumed +
                      " result=" + unifiedResult.Consumed +
                      " note=" + (unifiedResult.HoldNote != null
                          ? unifiedResult.HoldNote.Id.ToString() : "null") +
                      " assignment=" + (unifiedResult.AssignmentNote != null
                          ? unifiedResult.AssignmentNote.Id.ToString() : "null") +
                      " deleted=" + unifiedResult.DeletedScratchNoteCount +
                      " lane=" + unifiedResult.LaneId +
                      " timing=" + unifiedResult.Timing.TimingType +
                      " remaining=" + unifiedScratchNotes.Count +
                      " laneHits=" + unifiedLaneHits.Count +
                      " laneHolds=" + unifiedLaneHolds.Count +
                      " effects=" + unifiedEffects.Count);
            if (unifiedConsumed != 1 || !unifiedResult.Consumed ||
                unifiedResult.HoldNote != leftJump ||
                unifiedResult.AssignmentNote != rightJump ||
                unifiedResult.DeletedScratchNoteCount != 0 ||
                unifiedResult.LaneId != 9 ||
                unifiedResult.Timing.TimingType != RecoveredTimingType.PerfectStar ||
                unifiedScratchNotes.Count != 45 ||
                unifiedLaneHits.Count != 1 || unifiedLaneHolds.Count != 1 ||
                unifiedEffects.Count != 1 ||
                unifiedEffects[0].NoteId != rightJump.Id ||
                unifiedEffects[0].EffectType != RecoveredInputEffectType.HoldStart)
            {
                return false;
            }

            RecoveredNotationNote thirdJump = null;
            RecoveredNotationNote terminalJump = null;
            foreach (var candidate in notation)
            {
                if (RecoveredJumpScratch.IsConnectedNext(rightJump, candidate))
                    thirdJump = candidate;
            }
            if (thirdJump != null)
            {
                foreach (var candidate in notation)
                {
                    if (RecoveredJumpScratch.IsConnectedNext(thirdJump, candidate))
                        terminalJump = candidate;
                }
            }
            if (thirdJump == null || terminalJump == null) return false;

            var chainManager = new RecoveredScratchNoteManager(
                new[] { leftJump, rightJump, thirdJump, terminalJump });
            var chainAction = new RecoveredUnifiedHoldAction(
                new RecoveredStandardHoldNoteManager(
                    new[] { leftJump, rightJump, thirdJump, terminalJump }),
                chainManager);
            var chainHeadResult = chainAction.TryHold(
                chainInputs[1],
                new RecoveredHitLaneEntity(9, 0, 0, 0, 0),
                chainInputs[1].Milliseconds);
            var connectedResult = chainAction.TryHold(
                chainInputs[2],
                new RecoveredHitLaneEntity(12, 0, 0, 0, 0),
                chainInputs[2].Milliseconds);
            var thirdResult = chainAction.TryHold(
                chainInputs[3],
                new RecoveredHitLaneEntity(7, 0, 0, 0, 0),
                chainInputs[3].Milliseconds);
            var terminalResult = chainAction.TryHold(
                chainInputs[4],
                new RecoveredHitLaneEntity(12, 0, 0, 0, 0),
                chainInputs[4].Milliseconds);
            var releaseResult = chainAction.TryHold(
                chainInputs[5],
                new RecoveredHitLaneEntity(12, 0, 0, 0, 0),
                chainInputs[5].Milliseconds);
            Debug.Log("OPENWDS_JUMP_CHAIN_AUTOPLAY head=" +
                      chainHeadResult.Consumed + "/" + chainHeadResult.HoldNote?.Id +
                      "/" + chainHeadResult.Timing.TimingType +
                      "/" + chainHeadResult.DeletedScratchNoteCount +
                      " second=" + connectedResult.Consumed + "/" +
                      connectedResult.HoldNote?.Id + "/" +
                      connectedResult.Timing.TimingType + "/" +
                      connectedResult.DeletedScratchNoteCount +
                      " third=" + thirdResult.Consumed + "/" +
                      thirdResult.HoldNote?.Id + "/" +
                      thirdResult.Timing.TimingType + "/" +
                      thirdResult.DeletedScratchNoteCount +
                      " terminal=" + terminalResult.Consumed + "/" +
                      terminalResult.HoldNote?.Id + "/" +
                      terminalResult.Timing.TimingType + "/" +
                      terminalResult.DeletedScratchNoteCount +
                      " release=" + releaseResult.Consumed +
                      " remaining=" + chainManager.Count);
            if (!chainHeadResult.Consumed ||
                chainHeadResult.HoldNote != leftJump ||
                chainHeadResult.Timing.TimingType !=
                    RecoveredTimingType.PerfectStar ||
                chainHeadResult.DeletedScratchNoteCount != 0 ||
                !connectedResult.Consumed ||
                connectedResult.HoldNote != rightJump ||
                connectedResult.Timing.TimingType !=
                    RecoveredTimingType.PerfectStar ||
                connectedResult.DeletedScratchNoteCount != 0 ||
                !thirdResult.Consumed || thirdResult.HoldNote != thirdJump ||
                thirdResult.Timing.TimingType !=
                    RecoveredTimingType.PerfectStar ||
                thirdResult.DeletedScratchNoteCount != 0 ||
                !terminalResult.Consumed ||
                terminalResult.HoldNote != terminalJump ||
                terminalResult.Timing.TimingType !=
                    RecoveredTimingType.PerfectStar ||
                releaseResult.Consumed || chainManager.Count != 0 ||
                chainManager.Contains(leftJump) ||
                chainManager.Contains(rightJump) ||
                chainManager.Contains(thirdJump) ||
                chainManager.Contains(terminalJump))
            {
                return false;
            }

            // Type 40 has a distinct StartMilliseconds target and one-event
            // AutoTouch schedule. No standard chart currently contains type 40,
            // so this fixture only exercises values proven by Initialize.
            var scratch = new RecoveredNotationNote
            {
                Id = 999,
                StartTickCount = 2f,
                EndTickCount = -1f,
                NoteType = (int)RecoveredNoteType.Scratch,
                Lane = 3,
                Width = 2,
            };
            var autoClock = new RecoveredGameClock(0f, 0d);
            autoClock.Sync(2f, 2000, 2f, 2000);
            var autoTouch = new RecoveredAutoTouch(
                autoClock, new[] { scratch }, _ => new Vector2(320f, 180f));
            var inputs = new List<RecoveredInputEntity>();
            autoTouch.GetTouches(inputs);
            return autoTouch.ScheduledEventCount == 1 && inputs.Count == 1 &&
                   inputs[0].Phase == RecoveredTouchPhase.Moved &&
                   inputs[0].Milliseconds == 2000 &&
                   inputs[0].DeltaPosition == new Vector2(50f, 50f) &&
                   RecoveredScratchTimingDecider.GetTargetMilliseconds(scratch) == 2000 &&
                   RecoveredOriginalGameConfig.ScratchDistance == 50f;
        }

        private static bool ValidateLaneColliderMappings(Camera camera, GameObject laneGroup)
        {
            var managers = laneGroup.GetComponentInChildren<Sirius.Game.LaneColliderManagers>(true);
            if (managers == null)
            {
                return false;
            }
            managers.InitializeMappings();
            if (managers.MainColliders.Count != 12 ||
                managers.SubLeftInnerColliders.Count != 12 ||
                managers.SubRightInnerColliders.Count != 12 ||
                managers.SubLeftOuterColliders.Count != 12 ||
                managers.SubRightOuterColliders.Count != 12)
            {
                return false;
            }

            var mainId = FindColliderId(managers.MainColliders, 1);
            var leftId = FindColliderId(managers.SubLeftInnerColliders, 2);
            var rightId = FindColliderId(managers.SubRightOuterColliders, 3);
            var raycaster = new RecoveredLaneRaycaster(
                camera,
                managers.MainColliders,
                managers.SubLeftInnerColliders,
                managers.SubRightInnerColliders,
                managers.SubLeftOuterColliders,
                managers.SubRightOuterColliders);
            try
            {
                var hit = raycaster.ResolveHitInstanceIds(mainId, leftId, rightId);
                if (!hit.Exists ||
                    hit.HitMainLaneId != 1 ||
                    hit.HitSubLeftInnerLaneId != 2 ||
                    hit.HitSubRightOuterLaneId != 3)
                {
                    return false;
                }

                var group = laneGroup.GetComponent<Sirius.Game.LaneGroup>();
                if (group == null) return false;
                var inputs = new List<RecoveredInputEntity>(6);
                var expected = new List<RecoveredHitLaneEntity>(6);
                for (var lane = 1; lane <= 11; lane += 2)
                {
                    var screen = camera.WorldToScreenPoint(
                        group.GetLaneCollider(lane).position);
                    var position = new Vector2(screen.x, screen.y);
                    inputs.Add(new RecoveredInputEntity(
                        lane, 0, position, position, Vector2.zero,
                        RecoveredTouchPhase.Moved));
                    expected.Add(raycaster.RaycastPoint(position));
                }
                var batched = new List<RecoveredHitLaneEntity>(inputs.Count);
                var beganBatched =
                    new List<RecoveredHitLaneEntity>(inputs.Count);
                raycaster.Raycast(inputs, batched, beganBatched);
                return batched.Count == expected.Count &&
                       !batched.Where(
                           (value, index) => !value.Equals(expected[index])).Any() &&
                       beganBatched.Count == expected.Count &&
                       !beganBatched.Where(
                           (value, index) => !value.Equals(expected[index])).Any();
            }
            finally
            {
                raycaster.Dispose();
            }
        }

        private static int FindColliderId(IReadOnlyDictionary<int, int> colliders, int laneId)
        {
            foreach (var pair in colliders)
            {
                if (pair.Value == laneId)
                {
                    return pair.Key;
                }
            }
            return 0;
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.02f, 0.04f, 1f);
            camera.fieldOfView = RecoveredGameConfigValues.CameraFieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private static void RenderPreviewWithGameBackground(
            Camera camera, GameObject laneGroup)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/GameBackground.prefab");
            if (prefab == null)
                throw new FileNotFoundException("GameBackground prefab is required for preview.");
            var instance = UnityEngine.Object.Instantiate(prefab);
            var laneWasActive = laneGroup.activeSelf;
            try
            {
                laneGroup.SetActive(true);
                var backgroundSorting = instance.GetComponent<
                    UnityEngine.Rendering.SortingGroup>();
                var sample = instance.transform.Find("sample");
                if (sample != null)
                {
                    sample.gameObject.SendMessage(
                        "Awake", SendMessageOptions.DontRequireReceiver);
                    sample.gameObject.SendMessage(
                        "Adjust", SendMessageOptions.DontRequireReceiver);
                }
                var shadow = instance.GetComponent<Sirius.GameBackgroundShadow>();
                if (shadow != null) shadow.Initialize();
                var backgroundRenderer = sample != null
                    ? sample.GetComponent<SpriteRenderer>() : null;
                var laneTransform = FindChild(laneGroup.transform, "BG_Lane");
                var laneObject = laneTransform != null ? laneTransform.gameObject : null;
                var laneRenderer = laneObject != null
                    ? laneObject.GetComponent<SpriteRenderer>() : null;
                Debug.Log("OPENWDS_BACKGROUND_SORT bgOrder=" +
                          (backgroundRenderer != null ? backgroundRenderer.sortingOrder : -9999) +
                          " bgQueue=" + (backgroundRenderer != null && backgroundRenderer.sharedMaterial != null
                              ? backgroundRenderer.sharedMaterial.renderQueue : -9999) +
                          " groupOrder=" + (backgroundSorting != null ? backgroundSorting.sortingOrder : -9999) +
                          " laneActive=" + (laneObject != null && laneObject.activeInHierarchy) +
                          " laneEnabled=" + (laneRenderer != null && laneRenderer.enabled) +
                          " laneOrder=" + (laneRenderer != null ? laneRenderer.sortingOrder : -9999) +
                          " laneQueue=" + (laneRenderer != null && laneRenderer.sharedMaterial != null
                              ? laneRenderer.sharedMaterial.renderQueue : -9999));
                RenderPreview(camera);
            }
            finally
            {
                laneGroup.SetActive(laneWasActive);
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static bool ValidateAchievementRateSlots(Image[] images)
        {
            if (images == null || images.Length != 9) return false;
            var expectedWidths = new[]
            {
                32f, 20f, 20f, 40f, 40f, 16f, 40f, 40f, 40f,
            };
            var expectedDigits = new[] { -1, 0, 0, 0, 0, -1, 1, 0, 1 };
            for (var index = 0; index < images.Length; index++)
            {
                var image = images[index];
                var layout = image != null
                    ? image.GetComponent<LayoutElement>() : null;
                if (image == null || image.sprite == null || layout == null ||
                    !Mathf.Approximately(
                        layout.preferredWidth, expectedWidths[index]))
                    return false;
                if (index == 0 &&
                    !image.sprite.name.EndsWith("_percent",
                        StringComparison.Ordinal))
                    return false;
                if (index == 5 &&
                    !image.sprite.name.EndsWith("_point",
                        StringComparison.Ordinal))
                    return false;
                if (expectedDigits[index] >= 0 &&
                    !image.sprite.name.EndsWith(
                        "_" + expectedDigits[index],
                        StringComparison.Ordinal))
                    return false;
                if (expectedDigits[index] >= 0 &&
                    (!Mathf.Approximately(image.sprite.rect.width, 118f) ||
                     !Mathf.Approximately(image.sprite.rect.height, 148f)))
                    return false;
            }
            return true;
        }

        private static GameObject[] LoadRuntimeNotePrefabs()
        {
            // Original asset order includes the dedicated ConcurrentLine prefab
            // between ScratchHold and the two Sound note prefabs.
            var prefabs = new GameObject[NotePrefabPaths.Length];
            for (var index = 0; index < NotePrefabPaths.Length; index++)
            {
                prefabs[index] = AssetDatabase.LoadAssetAtPath<GameObject>(NotePrefabPaths[index]);
                if (prefabs[index] == null)
                    throw new FileNotFoundException(
                        "Required prefab was not found.", NotePrefabPaths[index]);
            }
            return prefabs;
        }

        private static GameObject[] LoadPrefabs(string[] paths)
        {
            var prefabs = new GameObject[paths.Length];
            for (var index = 0; index < paths.Length; index++)
            {
                prefabs[index] = AssetDatabase.LoadAssetAtPath<GameObject>(paths[index]);
                if (prefabs[index] == null)
                    throw new FileNotFoundException("Required prefab was not found.", paths[index]);
            }
            return prefabs;
        }

        private static Sprite[] LoadSprites(string[] paths)
        {
            var sprites = new Sprite[paths.Length];
            for (var index = 0; index < paths.Length; index++)
            {
                if (string.IsNullOrEmpty(paths[index])) continue;
                sprites[index] = AssetDatabase.LoadAssetAtPath<Sprite>(paths[index]);
                if (sprites[index] == null)
                    throw new FileNotFoundException(
                        "Required GameConfig sprite was not found.", paths[index]);
            }
            return sprites;
        }

        private static Sprite[] LoadComboDigits(string prefix)
        {
            var paths = new string[10];
            for (var digit = 0; digit < paths.Length; digit++)
                paths[digit] = "Assets/Resources/Sprite/" + prefix + digit + ".asset";
            return LoadSprites(paths);
        }

        private static GameObject InstantiatePrefab(string path, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new FileNotFoundException("Required prefab was not found.", path);
            }
            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Failed to instantiate " + path);
            }
            return instance;
        }

        private static TextAsset SyncTextAsset(string sourcePath, string assetPath)
        {
            var sourceText = File.ReadAllText(Path.GetFullPath(sourcePath));
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (asset == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                asset = new TextAsset(sourceText);
                AssetDatabase.CreateAsset(asset, assetPath);
                return asset;
            }

            var refreshed = new TextAsset(sourceText);
            EditorUtility.CopySerialized(refreshed, asset);
            UnityEngine.Object.DestroyImmediate(refreshed);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var offset = 0;
            while ((offset = text.IndexOf(value, offset, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                offset += value.Length;
            }
            return count;
        }

        private static bool ValidatePositionFormula()
        {
            return Mathf.Approximately(
                       RecoveredNotePositionCalculator.CalculateSpeedRate(10d),
                       6f) &&
                   Mathf.Approximately(
                       RecoveredNotePositionCalculator.CalculatePositionY(
                           1000,
                           0,
                           1f,
                           0d,
                           2d,
                           3d),
                       5f) &&
                   Mathf.Approximately(
                       RecoveredNotePositionCalculator.CalculateMoveSeconds(
                           5d, 5000, 3, 4.45f),
                       1.4833333f);
        }

        private static bool ValidateLaneFormula()
        {
            const float noteWidthPerLane = 0.85f;
            const float laneBorderWidth = 0.075f;
            var oneLaneWidth = RecoveredNotePositionCalculator.GetNoteWidth(
                1,
                noteWidthPerLane,
                laneBorderWidth);
            var twoLaneWidth = RecoveredNotePositionCalculator.GetNoteWidth(
                2,
                noteWidthPerLane,
                laneBorderWidth);
            return Mathf.Approximately(oneLaneWidth, 0.85f) &&
                   Mathf.Approximately(twoLaneWidth, 1.775f) &&
                   Mathf.Approximately(
                       RecoveredNotePositionCalculator.GetNotePositionX(
                           1,
                           oneLaneWidth,
                           noteWidthPerLane,
                           laneBorderWidth),
                       -5.0875f) &&
                   Mathf.Approximately(
                       RecoveredNotePositionCalculator.GetNotePositionX(
                           12,
                           oneLaneWidth,
                           noteWidthPerLane,
                           laneBorderWidth),
                       5.0875f);
        }

        private static bool ValidateSettingsDefaults()
        {
            var system = RecoveredGameSettings.System.Default();
            var basic = RecoveredGameSettings.Basic.Default();
            var detail = RecoveredGameSettings.Detail.Default();
            var custom = RecoveredGameSettings.Custom.Default();
            var sound = RecoveredGameSettings.SoundVolume.Default();
            var bluetooth = RecoveredGameSettings.Bluetooth.Default();
            return system.TextDisplaySpeed == 10 &&
                   system.TextSpeed == 10 &&
                   system.QualitySetting == 2 &&
                   !system.Is60Fps &&
                   system.IsPreLiveOptionConfirmation &&
                   system.IsLeagueNoticeOptionConfirmation &&
                   Math.Abs(basic.NoteSpeed - 5d) < double.Epsilon &&
                   basic.LaneAlphaValue == 80 &&
                   basic.SplitEffectLineOpacity == 100 &&
                   basic.IsActiveSenseDisplay &&
                   basic.IsActiveMusicVideo &&
                   !basic.IsRealTimeRenderingMusicVideo &&
                   detail.LaneWidth == 100 &&
                   detail.NoteHeight == 8 &&
                   detail.TimingEffectOffset == 60 &&
                   detail.IsActiveConcurrentLine &&
                   detail.IsActiveLaneAssistLine &&
                   custom.NormalSeId == 1 &&
                   custom.CriticalSeId == 1 &&
                   custom.BombType == 1 &&
                   sound.SystemMaster == 100 &&
                   sound.GameNotesTap == 100 &&
                   sound.StoryVoice == 100 &&
                   Math.Abs(bluetooth.NoteTimingValue) < double.Epsilon &&
                   bluetooth.GameSeVolume == 100 &&
                   bluetooth.GameNotesTapVolume == 100;
        }

        private static bool ValidateLaneSettingsMath()
        {
            return Mathf.Approximately(RecoveredGameSettings.CalculateLaneScale(80), 0.8f) &&
                   Mathf.Approximately(RecoveredGameSettings.CalculateLaneScale(100), 1f) &&
                   Mathf.Approximately(RecoveredGameSettings.CalculateLaneScale(120), 1.2f) &&
                   Mathf.Approximately(
                       RecoveredGameSettings.CalculateLaneDarknessAlpha(0), 0f) &&
                   Mathf.Approximately(
                       RecoveredGameSettings.CalculateLaneDarknessAlpha(80), 0.8f) &&
                   Mathf.Approximately(
                       RecoveredGameSettings.CalculateLaneDarknessAlpha(100), 1f) &&
                   Mathf.Approximately(
                       RecoveredGameSettings.CalculateLaneBorderAlpha(0),
                       14f / 255f) &&
                   Mathf.Approximately(
                       RecoveredGameSettings.CalculateLaneBorderAlpha(100),
                       51f / 255f);
        }

        private static bool ValidateSettingsUiBinding()
        {
            return RecoveredGameSettings.ConvertBooleanToToggleIndex(true) == 0 &&
                   RecoveredGameSettings.ConvertBooleanToToggleIndex(false) == 1 &&
                   RecoveredGameSettings.ConvertToggleIndexToBoolean(0) &&
                   !RecoveredGameSettings.ConvertToggleIndexToBoolean(1) &&
                   RecoveredGameSettings.ConvertThreeChoiceToggleIndex(0) == 2 &&
                   RecoveredGameSettings.ConvertThreeChoiceToggleIndex(1) == 1 &&
                   RecoveredGameSettings.ConvertThreeChoiceToggleIndex(2) == 0 &&
                   RecoveredGameSettings.IsValidLaneWidth(80) &&
                   RecoveredGameSettings.IsValidLaneWidth(100) &&
                   RecoveredGameSettings.IsValidLaneWidth(120) &&
                   !RecoveredGameSettings.IsValidLaneWidth(81) &&
                   RecoveredGameSettings.IsValidNoteHeight(8) &&
                   RecoveredGameSettings.IsValidNoteSpeed(1d) &&
                   RecoveredGameSettings.IsValidNoteSpeed(5d) &&
                   RecoveredGameSettings.IsValidNoteSpeed(20.5d) &&
                   RecoveredGameSettings.IsValidNoteSpeed(25d) &&
                   !RecoveredGameSettings.IsValidNoteSpeed(25.05d) &&
                   RecoveredGameSettings.CalculateNoteDisplayTime(0, 5d) == 1480 &&
                   RecoveredGameSettings.CalculateNoteDisplayTime(30, 11.1d) == 467 &&
                   RecoveredGameSettings.CalculateNoteDisplayTime(95, 25d) == 15 &&
                   RecoveredGameSettings.CalculateNoteDisplayTime(100, 5d) == 0 &&
                   Mathf.Approximately(
                       RecoveredGameSettings.CalculateCombinedVolume(100, 100),
                       1f) &&
                   Mathf.Approximately(
                       RecoveredGameSettings.CalculateCombinedVolume(80, 50),
                       0.4f) &&
                   Mathf.Approximately(
                       RecoveredGameSettings.CalculateCombinedVolume(0, 100),
                       0f);
        }

        private static bool ValidateSettingsPersistenceDescriptor()
        {
            return RecoveredSettingsPersistence.AesKeySize == 128 &&
                   RecoveredSettingsPersistence.CurrentDirectory == "Users" &&
                   RecoveredSettingsPersistence.Password == "9Yw|G_2EgDC(" &&
                   RecoveredSettingsPersistence.Salt == "jPNv$zT3Biqa" &&
                   RecoveredSettingsPersistence.SystemSettingsKey == "CsdcW8wz" &&
                   RecoveredSettingsPersistence.GameSettingsKey == "P9cziA6b" &&
                   RecoveredSettingsPersistence.GameDetailSettingsKey == "fU5ZnT6y" &&
                   RecoveredSettingsPersistence.GameCustomSettingsKey == "wD4XR8Wm" &&
                   RecoveredSettingsPersistence.SoundVolumeSettingsKey == "Tx9Lr3Pd" &&
                   RecoveredSettingsPersistence.BluetoothSettingsKey == "X3sEZdWb";
        }

        private static bool ValidateSettingsCrypto()
        {
            const string plainText = "{\"x\":1}";
            const string expectedCipherText = "ZyK7Vf+Ja3VzGmrShfqBPw==";
            var cipherText = RecoveredSettingsCrypto.EncryptUtf8(plainText);
            return cipherText == expectedCipherText &&
                   RecoveredSettingsCrypto.DecryptUtf8(cipherText) == plainText;
        }

        private static bool ValidateSettingsSession()
        {
            var session = new RecoveredSettingsSession(
                RecoveredSettingsSession.Snapshot.Default());
            if (session.HasChanges)
            {
                return false;
            }

            session.Current.GameSettings.NoteSpeed = 12d;
            if (!session.HasChanges)
            {
                return false;
            }

            session.Cancel();
            if (session.HasChanges ||
                Math.Abs(session.Current.GameSettings.NoteSpeed - 5d) > double.Epsilon)
            {
                return false;
            }

            session.Current.GameDetailSettings.LaneWidth = 120;
            session.AcceptSavedValues();
            return !session.HasChanges &&
                   session.Current.GameDetailSettings.LaneWidth == 120;
        }

        private static bool ValidateSettingsStoreRoundTrip()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "openwds-settings-validation-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new RecoveredSettingsStore(root);
                var settings = RecoveredSettingsSession.Snapshot.Default();
                settings.GameSettings.NoteSpeed = 14d;
                settings.GameDetailSettings.LaneWidth = 115;
                settings.SoundVolumeSettings.GameNotesTap = 73;
                store.Save(settings);

                var loaded = store.LoadOrDefault();
                var users = Path.Combine(
                    root,
                    RecoveredSettingsPersistence.CurrentDirectory);
                return loaded.GameSettings.NoteSpeed == 14d &&
                       loaded.GameDetailSettings.LaneWidth == 115 &&
                       loaded.SoundVolumeSettings.GameNotesTap == 73 &&
                       Directory.GetFiles(users).Length == 6 &&
                       !File.ReadAllText(Path.Combine(
                           users,
                           RecoveredSettingsPersistence.GameSettingsKey))
                           .Contains("NoteSpeed");
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static bool ValidateSettingsPrefabs()
        {
            var pause = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/Features/Game/Pause__b53abd1e.prefab");
            var option = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/Common/Dialog/Body/OptionDialogBody.prefab");
            var sideMenuButton = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/Common/Panels/SideMenuPanel/TextSideMenuButton.prefab");
            var dialog = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/Common/Dialog/Dialog.prefab");
            var pauseBody = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/DialogBody/Game/PauseGameDialogBody__1ff194e7.prefab");
            var resumeCountDown = AssetDatabase.LoadAssetAtPath<GameObject>(
                ResumeCountDownPath);
            if (pause == null || option == null || sideMenuButton == null ||
                dialog == null ||
                pauseBody == null || resumeCountDown == null ||
                pause.GetComponentInChildren<UnityEngine.UI.Button>(true) == null ||
                sideMenuButton.GetComponent<UnityEngine.UI.Button>() == null ||
                resumeCountDown.GetComponent<
                    Sirius.Animations.AnimationExitTrigger>() == null)
                return false;
            var dialogMask = dialog.GetComponentInChildren<UnityEngine.UI.Mask>(true);
            var dialogBody = Array.Find(
                dialog.GetComponentsInChildren<Transform>(true),
                item => item.name == "Body");
            if (dialogMask == null || dialogBody == null ||
                dialogBody.GetComponent<CanvasGroup>() == null)
                return false;
            foreach (var digit in new[] { "1", "2", "3" })
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    $"Assets/Resources/Sprite/" +
                    $"txt_game_txt_countdown_{digit}.asset");
                if (sprite == null ||
                    Math.Abs(sprite.rect.width - 168f) > 0.001f ||
                    Math.Abs(sprite.rect.height - 220f) > 0.001f)
                    return false;
            }

            var optionTransforms = option.GetComponentsInChildren<Transform>(true);
            var hasGame = false;
            var hasDetail = false;
            foreach (var item in optionTransforms)
            {
                if (item.name == "GameSettingsPanel") hasGame = true;
                if (item.name == "GameDetailSettingsPanel") hasDetail = true;
            }
            if (!hasGame || !hasDetail ||
                option.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length < 100)
                return false;
            var buttonScaleAnimatorCount = 0;
            foreach (var animator in option.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController != null &&
                    animator.runtimeAnimatorController.name == "ButtonScale")
                    buttonScaleAnimatorCount++;
            }
            if (buttonScaleAnimatorCount == 0)
                return false;
            var uiSePath = Path.GetFullPath(
                "Assets/StreamingAssets/OpenWDS/CRI/SE.acb");
            if (!File.Exists(uiSePath) ||
                new FileInfo(uiSePath).Length != 9810432L)
                return false;
            foreach (var label in option.GetComponentsInChildren<
                UnityEngine.UI.Text>(true))
            {
                if (label.font == null) return false;
            }
            var sideMenuLabel = sideMenuButton.GetComponentInChildren<
                UnityEngine.UI.Text>(true);
            if (sideMenuLabel == null || sideMenuLabel.font == null)
                return false;

            foreach (var component in option.GetComponentsInChildren<Component>(true))
            {
                if (component == null) return false;
            }
            return true;
        }

        private static bool ValidateMusicSelectionFlow()
        {
            const string catalogJson =
                "{\"CharacterBases\":[{" +
                "\"Id\":1,\"Name\":\"Validation Actor\",\"ShortName\":\"Actor\",\"Company\":1}]," +
                "\"Musics\":[{" +
                "\"Id\":101,\"Name\":\"Local validation music\"," +
                "\"Vocals\":\"Validation vocal\"," +
                "\"ActorIds\":[1]," +
                "\"VocalVersion\":1,\"MusicVideoType\":0," +
                "\"Lives\":[{" +
                "\"Id\":10103,\"Difficulty\":3,\"Level\":20,\"NoteCount\":8," +
                "\"DebugNotationAssetPath\":\"Assets/OpenWDS/Local/101_3_notation.asset\"," +
                "\"DebugMusicConfigAssetPath\":\"Assets/OpenWDS/Local/101_music.asset\"}]}]}";
            var catalog = RecoveredLocalMusicCatalog.FromJson(catalogJson);
            var selected = catalog.Select(101, RecoveredMusicDifficulty.Extra);
            var presenter = selected.CreatePresenterParameter(false);
            var game = presenter.CreateGameParameter();

            return (int)RecoveredMusicDifficulty.Olivier == 5 &&
                   (int)RecoveredLiveType.Normal == 1 &&
                   (int)RecoveredLiveType.MultiRoom == 13 &&
                   (int)RecoveredMusicVideoType.Movie == 2 &&
                   selected.Live.Id == 10103 &&
                   selected.Live.Level == 20 &&
                   selected.Live.NoteCount == 8 &&
                   presenter.MusicId == 101 &&
                   presenter.Difficulty == RecoveredMusicDifficulty.Extra &&
                   presenter.LiveType == RecoveredLiveType.Normal &&
                   presenter.VocalVersion == 1 &&
                   presenter.ProtoNotationUrl == string.Empty &&
                   presenter.ProtoMusicConfigUrl == string.Empty &&
                   game.MusicId == presenter.MusicId &&
                   game.Difficulty == presenter.Difficulty &&
                   game.LiveType == presenter.LiveType &&
                   game.MusicVideoType == presenter.MusicVideoType;
        }

        private static bool ValidatePauseRetireRoute()
        {
            if (RecoveredGameRuntime.GetRetireDestination(
                    RecoveredLiveType.Normal) !=
                RecoveredGameExitRoute.MusicSelection)
                return false;

            try
            {
                RecoveredGameRuntime.GetRetireDestination(
                    RecoveredLiveType.MultiRoom);
                return false;
            }
            catch (NotSupportedException)
            {
                // Unsupported Live types must stay explicit until their
                // GoToLiveModeTopAsync branches are recovered.
                return true;
            }
        }

        private static bool ValidateStandardNotation()
        {
            var root = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                "OpenWDS",
                "StandardCharts",
                "1",
                "1");
            var notation = RecoveredStandardNotation.Parse(
                File.ReadAllText(Path.Combine(root, "1.csv")));
            var config = RecoveredStandardNotation.ParseMusicConfig(
                File.ReadAllText(Path.Combine(root, "music_config.csv")));
            var expertNotation = RecoveredStandardNotation.Parse(
                File.ReadAllText(Path.Combine(root, "4.csv")));
            RecoveredNotationNote marker = null;
            foreach (var note in notation)
            {
                if (note.NoteType == 20 && note.Lane == 1 && note.Width == 4 &&
                    Mathf.Approximately(note.StartTickCount, 1.0169f))
                {
                    marker = note;
                    break;
                }
            }
            var adjacentTap = expertNotation.FirstOrDefault(note => note.Id == 264);
            var adjacentFlick = expertNotation.FirstOrDefault(note => note.Id == 265);
            var adjacentHoldStart = expertNotation.FirstOrDefault(note => note.Id == 336);
            var wideFlick = expertNotation.FirstOrDefault(note => note.Id == 338);
            var flickHit = new RecoveredHitLaneEntity(9, 10, 8, 0, 0);
            var wideFlickHit = new RecoveredHitLaneEntity(4, 5, 3, 0, 0);
            var adjacentColliderOwnershipValid =
                adjacentTap != null && adjacentFlick != null &&
                adjacentHoldStart != null && wideFlick != null &&
                adjacentTap.IgnoreRightInnerCollider &&
                adjacentTap.IgnoreRightOuterCollider &&
                adjacentFlick.IgnoreLeftInnerCollider &&
                adjacentFlick.IgnoreLeftOuterCollider &&
                adjacentHoldStart.IgnoreRightInnerCollider &&
                wideFlick.IgnoreLeftInnerCollider &&
                !RecoveredNotationNoteProvider.TryGetIncludedLane(
                    flickHit, adjacentTap, out _) &&
                RecoveredNotationNoteProvider.TryGetIncludedLane(
                    flickHit, adjacentFlick, out var flickLane) &&
                flickLane == 9 &&
                !RecoveredNotationNoteProvider.TryGetIncludedLane(
                    wideFlickHit, adjacentHoldStart, out _) &&
                RecoveredNotationNoteProvider.TryGetIncludedLane(
                    wideFlickHit, wideFlick, out var wideFlickLane) &&
                wideFlickLane == 4;
            return notation.Length == 204 &&
                   Mathf.Approximately(notation[0].StartTickCount, 0f) &&
                   marker != null &&
                   Mathf.Approximately(marker.EndTickCount, -1f) &&
                   marker.GimmickType == 0 &&
                   marker.GimmickValue == 0 &&
                   marker.StartMilliseconds == 1016 &&
                   config.CueSheetName == "Music" &&
                   config.CueName == "1" &&
                   Mathf.Approximately(config.DelayStartSeconds, 3.019f) &&
                   config.CueSheetDirectory == "Game" &&
                   adjacentColliderOwnershipValid;
        }

        private static bool ValidateSplitLaneScheduler(
            out int splitLaneCount,
            out int eventCount)
        {
            splitLaneCount = 0;
            eventCount = 0;
            var chartPath = Path.Combine(
                Application.dataPath, "StreamingAssets", "OpenWDS",
                "StandardCharts", "1", "1", "4.csv");
            var notation = RecoveredStandardNotation.Parse(File.ReadAllText(chartPath));
            var splitNotes = new List<RecoveredNotationNote>();
            foreach (var note in notation)
            {
                if (RecoveredSplitLaneRuntime.IsSplitLane(note.GimmickType))
                    splitNotes.Add(note);
            }

            var scheduler = new RecoveredSplitLaneRuntime(notation);
            splitLaneCount = scheduler.SplitLaneCount;
            if (splitLaneCount != 7 ||
                RecoveredSplitLaneRuntime.ConvertGimmickType(13) != 33 ||
                RecoveredSplitLaneRuntime.ConvertGimmickType(51) != 51 ||
                RecoveredSplitLaneRuntime.MinimumShowingMilliseconds != 1500 ||
                !Mathf.Approximately(
                    Sirius.Game.LaneGroup.CalculateLaneBorderAlpha(100), 51f / 255f) ||
                !Mathf.Approximately(
                    Sirius.Game.LaneGroup.CalculateLaneBorderAlpha(0), 14f / 255f))
            {
                return false;
            }

            var effectIds = new HashSet<int>();
            foreach (var note in splitNotes)
            {
                scheduler.Tick((long)Math.Ceiling(
                    (note.StartTickCount - 1f) * 1000d));
                foreach (var entry in scheduler.FrameEntries)
                {
                    eventCount++;
                    effectIds.Add(entry.SplitLaneEffectId);
                    if (!entry.ShouldShow ||
                        entry.SplitCount != note.GimmickType % 10 ||
                        entry.SplitLaneType != RecoveredSplitLaneType.Full)
                    {
                        return false;
                    }
                }

                scheduler.Tick((long)Math.Ceiling(note.EndTickCount * 1000d));
                foreach (var entry in scheduler.FrameEntries)
                {
                    eventCount++;
                    if (entry.ShouldShow) return false;
                }
            }

            if (eventCount != 14 || effectIds.Count != 3 ||
                !effectIds.Contains(10170) || !effectIds.Contains(1050) ||
                !effectIds.Contains(1060) || scheduler.ActiveCount != 0)
            {
                return false;
            }

            var overlap = new RecoveredSplitLaneRuntime(new[]
            {
                new RecoveredNotationNote
                {
                    Id = 1, StartTickCount = 1f, EndTickCount = 3f,
                    GimmickType = 13, GimmickValue = 10170,
                },
                new RecoveredNotationNote
                {
                    Id = 2, StartTickCount = 2f, EndTickCount = 4f,
                    GimmickType = 14, GimmickValue = 1060,
                },
            });
            overlap.Tick(1000);
            overlap.Tick(3000);
            return overlap.FrameEntries.Count == 1 &&
                   !overlap.FrameEntries[0].ShouldShow &&
                   overlap.FrameEntries[0].IsContinued &&
                   overlap.ActiveCount == 1;
        }

        private static bool ValidateGameClock()
        {
            var clock = new RecoveredGameClock(3.019f, 0d);
            clock.Sync(4f, 4000, 10f, 10000);
            var adjustedClock = new RecoveredGameClock(3.019f, 3.5d);
            adjustedClock.Sync(4f, 4000, 10f, 10000);
            return Mathf.Approximately(clock.PassedTime, 0.981f) &&
                   clock.PassedMilliseconds == 981 &&
                   clock.PassedGameMilliseconds == 10000 &&
                   clock.InputTimeToMusicMilliseconds(10000) == 981 &&
                   clock.MusicTimeToInputMilliseconds(981) == 10000 &&
                   clock.InputTimeToMusicMilliseconds(
                       clock.MusicTimeToInputMilliseconds(1016)) == 1016 &&
                   Mathf.Approximately(adjustedClock.PassedTime, 1.016f) &&
                   adjustedClock.PassedMilliseconds == 1016 &&
                   adjustedClock.InputTimeToMusicMilliseconds(10000) == 1016 &&
                   adjustedClock.MusicTimeToInputMilliseconds(1016) == 10000;
        }

        private static bool ValidateTapTiming()
        {
            var earlyBoundary = RecoveredTapTimingDecider.DecideMusicTime(991, 1016);
            var lateBoundary = RecoveredTapTimingDecider.DecideMusicTime(1056, 1016);
            var earlyOutside = RecoveredTapTimingDecider.DecideMusicTime(890, 1016);
            var lateOutside = RecoveredTapTimingDecider.DecideMusicTime(1142, 1016);
            return earlyBoundary.TimingType == RecoveredTimingType.PerfectStar &&
                   earlyBoundary.TimingAssistType == RecoveredTimingAssistType.Fast &&
                   earlyBoundary.DiffMilliseconds == -25 &&
                   lateBoundary.TimingType == RecoveredTimingType.Perfect &&
                   lateBoundary.TimingAssistType == RecoveredTimingAssistType.Slow &&
                   lateBoundary.DiffMilliseconds == 40 &&
                   earlyOutside.TimingType == RecoveredTimingType.None &&
                   lateOutside.TimingType == RecoveredTimingType.Miss;
        }

        private static bool ValidateHoldTiming()
        {
            var atMinus125 = RecoveredHoldTimingDecider.DecideEndMusicTime(875, 0, 1000);
            var atMinus100 = RecoveredHoldTimingDecider.DecideEndMusicTime(900, 0, 1000);
            var atMinus60 = RecoveredHoldTimingDecider.DecideEndMusicTime(940, 0, 1000);
            var atMinus40 = RecoveredHoldTimingDecider.DecideEndMusicTime(960, 0, 1000);
            var beforeRange = RecoveredHoldTimingDecider.DecideEndMusicTime(874, 0, 1000);
            var shortInside = RecoveredHoldTimingDecider.DecideEndMusicTime(64, 0, 100);
            var shortOutside = RecoveredHoldTimingDecider.DecideEndMusicTime(24, 0, 100);
            return RecoveredHoldTimingDecider.HoldEndThresholdMilliseconds == 150 &&
                   atMinus125.TimingType == RecoveredTimingType.Good &&
                   atMinus100.TimingType == RecoveredTimingType.Great &&
                   atMinus60.TimingType == RecoveredTimingType.PerfectStar &&
                   atMinus40.TimingType == RecoveredTimingType.PerfectStar &&
                   atMinus40.DiffMilliseconds == -40 &&
                   beforeRange.TimingType == RecoveredTimingType.None &&
                   beforeRange.DiffMilliseconds == 0 &&
                   shortInside.TimingType == RecoveredTimingType.PerfectStar &&
                   shortInside.DiffMilliseconds == -36 &&
                   shortOutside.TimingType == RecoveredTimingType.None;
        }

        private static void WriteReport(PreviewReport report)
        {
            var reportPath = Path.Combine(
                GetWorkspaceRoot(),
                "reverse",
                "reports",
                "unity-offline-preview-validation.json"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        }

        private static void RenderPreview(Camera camera)
        {
            var target = new RenderTexture(
                PreviewWidth,
                PreviewHeight,
                24,
                RenderTextureFormat.ARGB32);
            var image = new Texture2D(
                PreviewWidth,
                PreviewHeight,
                TextureFormat.RGB24,
                false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)PreviewWidth / PreviewHeight;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, PreviewWidth, PreviewHeight), 0, 0);
                image.Apply();
                var outputPath = Path.Combine(
                    GetWorkspaceRoot(),
                    "reverse",
                    "reports",
                    "unity-offline-preview.png"
                );
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static string GetWorkspaceRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        }
    }
}
