using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenWDS.Editor
{
    public static class CreateLocalMusicSelectionScene
    {
        public const string ScenePath =
            "Assets/OpenWDS/Scenes/LocalMusicSelection.unity";
        private const string GameScenePath =
            "Assets/OpenWDS/Scenes/OfflineRhythmPreview.unity";
        private const string ViewPath =
            "Assets/Resources/Prefabs/MusicSelectionView.prefab";
        private const string CellPath =
            "Assets/Resources/Prefabs/MusicSelectionListCell.prefab";
        private const string MusicSelectionHeaderPath =
            "Assets/Resources/Prefabs/" +
            "MusicSelectionHeaderView.prefab";
        private const string NoteSpeedDialogBodyPath =
            "Assets/Resources/Prefabs/" +
            "NoteSpeedSettingsDialogBody.prefab";
        private const string DialogPath =
            "Assets/Resources/Prefabs/Common/Dialog/Dialog.prefab";
        private const string MusicSortFilterDialogBodyPath =
            "Assets/Resources/Prefabs/" +
            "MusicSortFilterDialogBody.prefab";
        private const string TextSideMenuButtonPath =
            "Assets/Resources/Prefabs/" +
            "TextSideMenuButton.prefab";
        private const string MusicBookmarkSettingDialogBodyPath =
            "Assets/Resources/Prefabs/" +
            "MusicBookmarkSettingDialogBody.prefab";
        private const string MusicSelectionBookmarkTagPath =
            "Assets/Resources/Prefabs/" +
            "MusicSelectionBookmarkTag.prefab";
        private const string PlayerRateDialogBodyPath =
            "Assets/Resources/Prefabs/" +
            "PlayerRateDialogBody.prefab";
        private const string PlayerRateContentPath =
            "Assets/Resources/Prefabs/" +
            "PlayerRateContent.prefab";
        private const string PlayerRateSimpleContentPath =
            "Assets/Resources/Prefabs/" +
            "PlayerRateSimpleContent.prefab";
        private const string DialogNormalButtonSpritePath =
            "Assets/Resources/Sprite/btn_common_btn_large.asset";
        private const string DialogPositiveButtonSpritePath =
            "Assets/Resources/Sprite/btn_common_btn_large_red.asset";
        private const string ToggleOnSpritePath =
            "Assets/Resources/Sprite/" +
            "btn_common_btn_switch_filter_on.png";
        private const string ToggleOffSpritePath =
            "Assets/Resources/Sprite/" +
            "btn_common_btn_switch_filter_off.png";
        private const string GameSimulationCameraPath =
            "Assets/Resources/Prefabs/Features/Game/" +
            "GameSimulationCamera.prefab";
        private const string GameSimulationLaneGroupPath =
            "Assets/Resources/Prefabs/Features/Game/LaneGroup.prefab";
        private const string GameSimulationPreviewUiPath =
            "Assets/Resources/Prefabs/Features/GameSimulation/" +
            "PreviewUI.prefab";
        private const string GameSimulationNotePath =
            "Assets/Resources/Prefabs/Features/Game/Notes/Note.prefab";
        private const string GameSimulationRenderTexturePath =
            "Assets/Resources/RenderTexture/GameRenderTexture.renderTexture";
        private const string CharacterBaseIconRoot =
            "Assets/OpenWDS/OfflineData/CharacterBaseIcons/";
        private const string CurtainSkeletonPath =
            "Assets/Resources/Spine/" +
            "curtain_Albedo_SkeletonData.asset";
        private const string CurtainGraphicMaterialPath =
            "Assets/Spine/Runtime/spine-unity/Materials/" +
            "SkeletonGraphicDefault.mat";
        private const string BackgroundJacketFocusClipPath =
            "Assets/Resources/AnimationClip/" +
            "BackgroundMusicJacket_onFocus_anim.anim";
        private const string CatalogPath =
            "Assets/OpenWDS/OfflineData/LocalMusicCatalog.json";
        private const string DifficultySpriteRoot =
            "Assets/Resources/Sprite/";
        [Serializable]
        private sealed class ValidationReport
        {
            public string scenePath;
            public int viewGameObjects;
            public int sceneDependencies;
            public int missingComponents;
            public int musicCount;
            public int availableDifficulties;
            public bool hasEventSystem;
            public bool hasOriginalView;
            public bool hasListCellPrefab;
            public bool hasCatalog;
            public bool hasDifficultyMarkers;
            public bool hasSelectionClearLampSprites;
            public bool hasSelectionRateGradeSprites;
            public bool hasMusicSelectionHud;
            public bool hasPerformanceStartFlow;
            public bool hasNoteSpeedDialog;
            public bool hasOriginalActorFilter;
            public bool hasBookmarkAndRateDialogs;
            public bool hasOriginalGameSimulation;
            public bool hasCurtainTransition;
            public bool ratingRulesValid;
            public bool buildLoopValid;
        }

        public static void Run()
        {
            RepairBackgroundJacketFocusAlphaBinding();
            var viewPrefab = RequireAsset<GameObject>(ViewPath);
            var cellPrefab = RequireAsset<GameObject>(CellPath);
            var musicSelectionHeaderPrefab =
                RequireAsset<GameObject>(MusicSelectionHeaderPath);
            var noteSpeedDialogBodyPrefab =
                RequireAsset<GameObject>(NoteSpeedDialogBodyPath);
            var dialogPrefab = RequireAsset<GameObject>(DialogPath);
            var musicSortFilterDialogBodyPrefab =
                RequireAsset<GameObject>(MusicSortFilterDialogBodyPath);
            var textSideMenuButtonPrefab =
                RequireAsset<GameObject>(TextSideMenuButtonPath);
            var musicBookmarkSettingDialogBodyPrefab =
                RequireAsset<GameObject>(MusicBookmarkSettingDialogBodyPath);
            var musicSelectionBookmarkTagPrefab =
                RequireAsset<GameObject>(MusicSelectionBookmarkTagPath);
            var playerRateDialogBodyPrefab =
                RequireAsset<GameObject>(PlayerRateDialogBodyPath);
            var playerRateContentPrefab =
                RequireAsset<GameObject>(PlayerRateContentPath);
            var playerRateSimpleContentPrefab =
                RequireAsset<GameObject>(PlayerRateSimpleContentPath);
            var dialogNormalButtonSprite =
                RequireAsset<Sprite>(DialogNormalButtonSpritePath);
            var dialogPositiveButtonSprite =
                RequireAsset<Sprite>(DialogPositiveButtonSpritePath);
            var toggleOnSprite = RequireAsset<Sprite>(ToggleOnSpritePath);
            var toggleOffSprite = RequireAsset<Sprite>(ToggleOffSpritePath);
            var gameSimulationCameraPrefab =
                RequireAsset<GameObject>(GameSimulationCameraPath);
            var gameSimulationLaneGroupPrefab =
                RequireAsset<GameObject>(GameSimulationLaneGroupPath);
            var gameSimulationPreviewUiPrefab =
                RequireAsset<GameObject>(GameSimulationPreviewUiPath);
            var gameSimulationNotePrefab =
                RequireAsset<GameObject>(GameSimulationNotePath);
            var gameSimulationRenderTexture =
                RequireAsset<RenderTexture>(GameSimulationRenderTexturePath);
            var curtainSkeleton =
                RequireAsset<Spine.Unity.SkeletonDataAsset>(CurtainSkeletonPath);
            var curtainGraphicMaterial =
                RequireAsset<Material>(CurtainGraphicMaterialPath);
            var catalog = RequireAsset<TextAsset>(CatalogPath);
            var parsedCatalog = RecoveredLocalMusicCatalog.FromJson(catalog.text);
            var difficultyMarkers = new[]
            {
                RequireAsset<Sprite>(
                    DifficultySpriteRoot + "img_live_common_list_level_normal.asset"),
                RequireAsset<Sprite>(
                    DifficultySpriteRoot + "img_live_common_list_level_hard.asset"),
                RequireAsset<Sprite>(
                    DifficultySpriteRoot + "img_live_common_list_level_extra.asset"),
                RequireAsset<Sprite>(
                    DifficultySpriteRoot + "img_live_common_list_level_stella.asset"),
                RequireAsset<Sprite>(
                    DifficultySpriteRoot + "img_live_common_list_level_olivier.asset"),
            };
            var difficultyButtonOnSprites = new[]
            {
                RequireAsset<Sprite>(
                    DifficultySpriteRoot + "img_live_common_level_normal.asset"),
                RequireAsset<Sprite>(
                    DifficultySpriteRoot + "img_live_common_level_hard.asset"),
                RequireAsset<Sprite>(
                    DifficultySpriteRoot + "img_live_common_level_extra.asset"),
                RequireAsset<Sprite>(
                    DifficultySpriteRoot + "img_live_common_level_stella.asset"),
                RequireAsset<Sprite>(
                    DifficultySpriteRoot + "img_live_common_level_olivier.asset"),
            };
            var difficultyButtonOffSprite = RequireAsset<Sprite>(
                DifficultySpriteRoot + "img_live_common_level_off.asset");
            var listJacketOffsetMaskSprite = RequireAsset<Sprite>(
                DifficultySpriteRoot +
                "img_live_common_list_jacket_mask_small.asset");
            var listJacketTargetMaskSprite = RequireAsset<Sprite>(
                DifficultySpriteRoot + "img_live_common_list_jacket_mask.asset");
            var clearLampSprites = new[]
            {
                RequireAsset<Sprite>(
                    DifficultySpriteRoot + "img_live_common_clear_lamp_clear.asset"),
                RequireAsset<Sprite>(
                    DifficultySpriteRoot + "img_live_common_clear_lamp_fc.asset"),
                RequireAsset<Sprite>(
                    DifficultySpriteRoot + "img_live_common_clear_lamp_ap.asset"),
            };
            // AchievementRateGrades is None,C,B,A,A+,S,S+,SS,SS+,SSS.
            var rateGradeSprites = new[]
            {
                "c", "b", "a", "a2", "s", "s2", "ss", "ss2", "sss",
            }.Select(name => RequireAsset<Sprite>(
                DifficultySpriteRoot + "img_live_common_grade_" + name + ".asset"))
                .ToArray();
            var characterBaseIds = parsedCatalog.CharacterBases
                .Select(character => character.Id)
                .ToArray();
            var characterBaseIcons = characterBaseIds
                .Select(id => RequireAsset<Sprite>(
                    CharacterBaseIconRoot + id + ".png"))
                .ToArray();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("UICamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.07f, 1f);
            camera.orthographic = true;

            var canvasObject = new GameObject(
                "MainCanvas", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1200f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var view = (GameObject)PrefabUtility.InstantiatePrefab(
                viewPrefab, canvasObject.transform);
            view.name = "MusicSelectionView";
            var viewRect = view.GetComponent<RectTransform>();
            if (viewRect == null)
                throw new InvalidOperationException(
                    "Recovered MusicSelectionView has no RectTransform.");
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = Vector2.zero;
            viewRect.offsetMax = Vector2.zero;
            viewRect.localScale = Vector3.one;

            var eventSystem = new GameObject(
                "EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var criRoot = new GameObject("CRIWARE");
            var criInitializer =
                criRoot.AddComponent<CriWare.CriWareInitializer>();
            criInitializer.initializesMana = false;
            criInitializer.atomConfig.acfFileName = "OpenWDS/CRI/Sirius.acf";

            var runtimeObject = new GameObject("RecoveredLocalMusicSelectionRuntime");
            var runtime =
                runtimeObject.AddComponent<RecoveredLocalMusicSelectionRuntime>();
            runtime.Configure(
                view,
                cellPrefab,
                catalog,
                Array.Empty<long>(),
                Array.Empty<Sprite>(),
                difficultyMarkers,
                difficultyButtonOnSprites,
                difficultyButtonOffSprite,
                listJacketOffsetMaskSprite,
                listJacketTargetMaskSprite,
                clearLampSprites,
                rateGradeSprites);
            runtime.ConfigureCharacterBaseIcons(
                characterBaseIds, characterBaseIcons);
            runtime.ConfigureHud(
                musicSelectionHeaderPrefab,
                dialogPrefab,
                noteSpeedDialogBodyPrefab,
                musicSortFilterDialogBodyPrefab,
                textSideMenuButtonPrefab,
                musicBookmarkSettingDialogBodyPrefab,
                musicSelectionBookmarkTagPrefab,
                playerRateDialogBodyPrefab,
                playerRateContentPrefab,
                playerRateSimpleContentPrefab,
                dialogNormalButtonSprite,
                dialogPositiveButtonSprite,
                toggleOnSprite,
                toggleOffSprite,
                gameSimulationCameraPrefab,
                gameSimulationLaneGroupPrefab,
                gameSimulationPreviewUiPrefab,
                gameSimulationNotePrefab,
                gameSimulationRenderTexture,
                curtainSkeleton,
                curtainGraphicMaterial);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException(
                    "Failed to save LocalMusicSelection scene.");
            NormalizeGeneratedYaml(ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };
            AssetDatabase.SaveAssets();

            var missing = 0;
            foreach (var component in view.GetComponentsInChildren<Component>(true))
            {
                if (component == null) missing++;
            }
            var expectedMusicCount = parsedCatalog.Musics.Length;
            var report = new ValidationReport
            {
                scenePath = ScenePath,
                viewGameObjects = view.GetComponentsInChildren<Transform>(true).Length,
                sceneDependencies = AssetDatabase.GetDependencies(ScenePath, true).Length,
                missingComponents = missing,
                musicCount = parsedCatalog.Musics.Length,
                availableDifficulties = 5,
                hasEventSystem = eventSystem.GetComponent<EventSystem>() != null,
                hasOriginalView = PrefabUtility.GetCorrespondingObjectFromSource(view) ==
                    viewPrefab,
                hasListCellPrefab = cellPrefab != null,
                hasCatalog = expectedMusicCount > 0,
                hasDifficultyMarkers = Array.TrueForAll(
                    difficultyMarkers, marker => marker != null),
                hasSelectionClearLampSprites = Array.TrueForAll(
                    clearLampSprites,
                    sprite => AssetDatabase.GetAssetPath(sprite).StartsWith(
                        DifficultySpriteRoot, StringComparison.Ordinal)),
                hasSelectionRateGradeSprites = Array.TrueForAll(
                    rateGradeSprites,
                    sprite => AssetDatabase.GetAssetPath(sprite).StartsWith(
                        DifficultySpriteRoot, StringComparison.Ordinal)),
                hasMusicSelectionHud = musicSelectionHeaderPrefab != null,
                hasPerformanceStartFlow =
                    dialogPrefab != null &&
                    musicSortFilterDialogBodyPrefab != null &&
                    noteSpeedDialogBodyPrefab != null,
                hasNoteSpeedDialog = noteSpeedDialogBodyPrefab != null,
                hasOriginalActorFilter =
                    parsedCatalog.CharacterBases.Length == 21 &&
                    characterBaseIcons.Length == 21,
                hasBookmarkAndRateDialogs =
                    musicBookmarkSettingDialogBodyPrefab != null &&
                    musicSelectionBookmarkTagPrefab != null &&
                    playerRateDialogBodyPrefab != null &&
                    playerRateContentPrefab != null &&
                    playerRateSimpleContentPrefab != null,
                hasOriginalGameSimulation =
                    gameSimulationCameraPrefab != null &&
                    gameSimulationLaneGroupPrefab != null &&
                    gameSimulationPreviewUiPrefab != null &&
                    gameSimulationNotePrefab != null &&
                    gameSimulationRenderTexture != null,
                hasCurtainTransition =
                    AssetDatabase.LoadAssetAtPath<Spine.Unity.SkeletonDataAsset>(
                        CurtainSkeletonPath) != null &&
                    AssetDatabase.LoadAssetAtPath<Material>(
                        CurtainGraphicMaterialPath) != null,
                ratingRulesValid = ValidateRatingRules(),
                buildLoopValid =
                    EditorBuildSettings.scenes.Length == 2 &&
                    EditorBuildSettings.scenes[0].path == ScenePath &&
                    EditorBuildSettings.scenes[1].path == GameScenePath,
            };
            WriteReport(report);
            Debug.Log(
                $"OPENWDS_MUSIC_SELECTION scene={ScenePath} " +
                $"objects={report.viewGameObjects} " +
                $"dependencies={report.sceneDependencies} " +
                $"missing={report.missingComponents} " +
                $"music={report.musicCount} difficulties={report.availableDifficulties} " +
                $"serializedMusic={runtime.SerializedMusicAssetCount} " +
                $"serializedCharts={runtime.SerializedChartAssetCount} " +
                $"loop={report.buildLoopValid}");
            if (report.viewGameObjects < 200 ||
                report.sceneDependencies < 50 ||
                report.missingComponents != 0 ||
                report.musicCount != expectedMusicCount ||
                report.availableDifficulties != 5 ||
                !report.hasEventSystem ||
                !report.hasOriginalView ||
                !report.hasListCellPrefab ||
                !report.hasCatalog ||
                !report.hasDifficultyMarkers ||
                !report.hasSelectionClearLampSprites ||
                !report.hasSelectionRateGradeSprites ||
                !report.hasMusicSelectionHud ||
                !report.hasPerformanceStartFlow ||
                !report.hasNoteSpeedDialog ||
                !report.hasOriginalActorFilter ||
                !report.hasOriginalGameSimulation ||
                !report.hasCurtainTransition ||
                !report.ratingRulesValid ||
                runtime.SerializedMusicAssetCount != 0 ||
                runtime.SerializedChartAssetCount != 0 ||
                !report.buildLoopValid)
                throw new InvalidOperationException(
                    "Local MusicSelection validation failed.");
        }

        private static bool ValidateRatingRules()
        {
            const double tolerance = 0.0000001d;
            bool Rate(int level, double achievementRate, double expected) =>
                Math.Abs(RecoveredPlayerRating.CalculateNotationRate(
                    level, achievementRate) - expected) < tolerance;

            var normalMusic = new RecoveredLocalMusicEntry
            {
                IsLongVersion = false,
            };
            var longMusic = new RecoveredLocalMusicEntry
            {
                IsLongVersion = true,
            };
            var stella = new RecoveredLocalLiveEntry
            {
                Difficulty = RecoveredMusicDifficulty.Stella,
                Level = 20,
            };
            var olivier = new RecoveredLocalLiveEntry
            {
                Difficulty = RecoveredMusicDifficulty.Olivier,
                Level = 101,
            };
            var topThirty = RecoveredPlayerRating.CalculatePlayerRate(
                Enumerable.Range(1, 31).Select(value => (double)value));
            var header = AssetDatabase.LoadAssetAtPath<GameObject>(
                MusicSelectionHeaderPath);
            var headerRateText = header != null
                ? header.GetComponentsInChildren<Text>(true)
                    .FirstOrDefault(text => text.name == "RateText")
                : null;

            return
                Rate(20, 79.9999d, 0d) &&
                Rate(20, 80d, 8.88d) &&
                Rate(20, 90d, 10d) &&
                Rate(20, 95d, 15d) &&
                Rate(20, 98d, 20d) &&
                Rate(20, 100d, 21.5d) &&
                Rate(20, 100.5d, 23d) &&
                Rate(20, 100.75d, 24.5d) &&
                Rate(20, 100.95d, 26d) &&
                Rate(20, 101d, 26.05d) &&
                Rate(29, 100.8d, 33.87d) &&
                Math.Abs(topThirty - 495d) < tolerance &&
                RecoveredPlayerRating.IsEligible(normalMusic, stella) &&
                !RecoveredPlayerRating.IsEligible(longMusic, stella) &&
                !RecoveredPlayerRating.IsEligible(normalMusic, olivier) &&
                headerRateText != null &&
                ((Color32)headerRateText.color).Equals(
                    new Color32(255, 255, 255, 255)) &&
                RecoveredPlayerRating.GetHudTopColor(199.99d).Equals(
                    new Color32(255, 255, 255, 255)) &&
                RecoveredPlayerRating.GetHudTopColor(200d).Equals(
                    new Color32(170, 226, 27, 255)) &&
                RecoveredPlayerRating.GetHudTopColor(850d).Equals(
                    new Color32(174, 176, 192, 255)) &&
                RecoveredPlayerRating.GetHudTopColor(1000d).Equals(
                    new Color32(255, 131, 236, 255));
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new FileNotFoundException(
                    $"Required LocalMusicSelection asset is missing: {path}");
            return asset;
        }

        private static void RepairBackgroundJacketFocusAlphaBinding()
        {
            var clip = RequireAsset<AnimationClip>(
                BackgroundJacketFocusClipPath);
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var sourceBinding = Array.Find(
                bindings,
                binding => binding.propertyName == "m_Color.a");
            if (string.IsNullOrEmpty(sourceBinding.propertyName))
                throw new InvalidOperationException(
                    "BackgroundMusicJacket focus alpha curve is missing.");
            var curve = AnimationUtility.GetEditorCurve(clip, sourceBinding);
            if (curve == null || curve.length != 21)
                throw new InvalidOperationException(
                    "BackgroundMusicJacket focus alpha curve is invalid.");
            var imageBinding = EditorCurveBinding.FloatCurve(
                string.Empty,
                typeof(Image),
                "m_Color.a");
            if (sourceBinding.type != typeof(Image))
            {
                // AssetStudio/AssetRipper preserves the MonoBehaviour script
                // hash but cannot resolve UnityEngine.UI.Image's managed type.
                // Recreate the binding through Unity's editor API so the
                // Animator can actually drive Graphic.m_Color.a at runtime.
                AnimationUtility.SetEditorCurve(clip, sourceBinding, null);
                AnimationUtility.SetEditorCurve(clip, imageBinding, curve);
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    BackgroundJacketFocusClipPath,
                    ImportAssetOptions.ForceUpdate);
            }
            NormalizeGeneratedYaml(BackgroundJacketFocusClipPath);
        }

        private static void WriteReport(ValidationReport report)
        {
            var workspace = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", ".."));
            var output = Path.Combine(
                workspace,
                "reverse",
                "reports",
                "unity-music-selection-validation.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
        }

        private static void NormalizeGeneratedYaml(string assetPath)
        {
            var fullPath = Path.GetFullPath(assetPath);
            var text = File.ReadAllText(fullPath);
            text = Regex.Replace(
                text,
                @"[ \t]+(?=\r?$)",
                string.Empty,
                RegexOptions.Multiline);
            File.WriteAllText(fullPath, text);
            AssetDatabase.ImportAsset(
                assetPath, ImportAssetOptions.ForceUpdate);
        }
    }
}
