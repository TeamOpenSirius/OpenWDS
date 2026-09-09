using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace OpenWDS.Editor
{
    public static class ValidateRecoveredAssets
    {
        [Serializable]
        private sealed class PrefabResult
        {
            public string path;
            public bool loaded;
            public int gameObjects;
            public int components;
            public int missingScripts;
            public int dependencies;
            public int unresolvedPlaceholderReferences;
            public int particleRenderers;
            public int missingParticleMaterials;
            public int unsupportedParticleMaterials;
        }

        [Serializable]
        private sealed class ValidationReport
        {
            public string unityVersion;
            public int prefabCount;
            public int loadedPrefabCount;
            public int totalMissingScripts;
            public int totalUnresolvedPlaceholderReferences;
            public int totalMissingParticleMaterials;
            public int totalUnsupportedParticleMaterials;
            public int recoveredScriptCount;
            public int resolvedRecoveredScriptCount;
            public bool defaultSceneGameHudInitialized;
            public bool curtainPremultipliedAlphaSettingsValid;
            public bool lifeAndPrincipalGaugeValid;
            public bool laneSpriteTexturesValid;
            public bool timingEffectAnimationEventValid;
            public bool comboHiddenDigitsTransparent;
            public bool comboSpriteGeometryValid;
            public bool standardHoldFrameSweepValid;
            public bool longNotesShaderValid;
            public List<PrefabResult> prefabs = new List<PrefabResult>();
            public List<ScriptResult> recoveredScripts = new List<ScriptResult>();
        }

        [Serializable]
        private sealed class ScriptResult
        {
            public string path;
            public string className;
            public bool resolved;
        }

        private static readonly string[] CorePrefabs =
        {
            "Assets/Resources/Prefabs/BeamEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/LaneEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/LaneGroup.prefab",
            "Assets/Resources/Prefabs/Features/Game/TimingEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Combo.prefab",
            "Assets/Resources/Prefabs/Features/Game/Life.prefab",
            "Assets/Resources/Prefabs/Features/Game/PrincipalGauge.prefab",
            "Assets/Resources/Prefabs/Features/Game/Score__4ae4a764.prefab",
            "Assets/Resources/Prefabs/Features/Game/AchievementRate__58302b55.prefab",
            "Assets/Resources/Prefabs/Features/Game/Notes/Note.prefab",
            "Assets/Resources/Prefabs/Features/Game/Notes/HoldNote.prefab",
            "Assets/Resources/Prefabs/Features/Game/Notes/FlickNote.prefab",
            "Assets/Resources/Prefabs/Features/Game/Notes/ScratchNote.prefab",
            "Assets/Resources/Prefabs/Features/Game/Notes/ScratchHoldNote.prefab",
            "Assets/Resources/Prefabs/Features/Game/Notes/ConcurrentLineNote.prefab",
            "Assets/Resources/Prefabs/Features/Game/Notes/SoundNote.prefab",
            "Assets/Resources/Prefabs/Features/Game/Notes/SoundPurpleNote.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Default/CriticalBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Default/HoldBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Default/HoldEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Default/NormalBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Default/ScratchBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Default/SoundBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Notes/CriticalBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Notes/HoldBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Notes/HoldEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Notes/NormalBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Notes/ScratchBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Notes/SoundBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Sakura/CriticalBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Sakura/HoldBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Sakura/HoldEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Sakura/NormalBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Sakura/ScratchBombEffect.prefab",
            "Assets/Resources/Prefabs/Features/Game/Bomb/Sakura/SoundBombEffect.prefab",
        };

        private static readonly string[] AdditiveParticleMaterials =
        {
            "Assets/Resources/Material/Game/BombEffect/BombEffectDefault.mat",
            "Assets/Resources/Material/Game/BombEffect/BombEffectNote.mat",
            "Assets/Resources/Material/Game/BombEffect/BombEffectRadiation.mat",
            "Assets/Resources/Material/Game/BombEffect/BombEffectSakura.mat",
            "Assets/Resources/Material/Game/NotesLight.mat",
            "Assets/Resources/Material/Game/UI/GameUIParticle_Add.mat",
        };

        private const string RecoveredParticleShaderName =
            "OpenWDS/Recovered/MobileParticlesAdditive";

        public static void Run()
        {
            var defaultSpriteMaterial = AssetDatabase.GetBuiltinExtraResource<Material>(
                "Sprites-Default.mat"
            );
            if (defaultSpriteMaterial == null ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    defaultSpriteMaterial,
                    out string defaultSpriteMaterialGuid,
                    out long defaultSpriteMaterialFileId))
            {
                throw new InvalidOperationException("Unity built-in Sprites-Default.mat was not found.");
            }
            Debug.Log($"OPENWDS_BUILTIN_SPRITE_MATERIAL guid={defaultSpriteMaterialGuid} " +
                      $"fileID={defaultSpriteMaterialFileId}");

            ValidateAdditiveParticleMaterials();
            CreateOfflineRhythmPreviewScene.RunPresentationVisualValidation();

            var report = new ValidationReport
            {
                unityVersion = Application.unityVersion,
                prefabCount = CorePrefabs.Length,
                curtainPremultipliedAlphaSettingsValid =
                    ValidateCurtainPremultipliedAlphaSettings(),
            };

            foreach (var path in CorePrefabs)
            {
                var result = ValidatePrefab(path);
                report.prefabs.Add(result);
                if (result.loaded)
                {
                    report.loadedPrefabCount++;
                }
                report.totalMissingScripts += result.missingScripts;
                report.totalUnresolvedPlaceholderReferences +=
                    result.unresolvedPlaceholderReferences;
                report.totalMissingParticleMaterials += result.missingParticleMaterials;
                report.totalUnsupportedParticleMaterials +=
                    result.unsupportedParticleMaterials;
                Debug.Log($"OPENWDS_PREFAB path={path} loaded={result.loaded} " +
                          $"gameObjects={result.gameObjects} components={result.components} " +
                          $"missingScripts={result.missingScripts} " +
                          $"particleRenderers={result.particleRenderers} " +
                          $"missingParticleMaterials={result.missingParticleMaterials} " +
                          $"unsupportedParticleMaterials={result.unsupportedParticleMaterials} " +
                          $"unresolvedRefs={result.unresolvedPlaceholderReferences} " +
                          $"dependencies={result.dependencies}");
            }

            foreach (var guid in AssetDatabase.FindAssets(
                         "t:MonoScript",
                         new[] { "Assets/OpenWDS/RecoveredScripts" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                var type = script == null ? null : script.GetClass();
                var scriptResult = new ScriptResult
                {
                    path = path,
                    className = type == null ? null : type.FullName,
                    resolved = type != null,
                };
                report.recoveredScripts.Add(scriptResult);
                report.recoveredScriptCount++;
                if (scriptResult.resolved)
                {
                    report.resolvedRecoveredScriptCount++;
                }
                var displayedClassName = scriptResult.className ?? "<null>";
                Debug.Log($"OPENWDS_SCRIPT path={path} resolved={scriptResult.resolved} " +
                          $"class={displayedClassName}");
            }

            report.defaultSceneGameHudInitialized = ValidateDefaultSceneGameHud();
            report.lifeAndPrincipalGaugeValid = ValidateLifeAndPrincipalGauge();
            report.laneSpriteTexturesValid = ValidateLaneSpriteTextures();
            report.timingEffectAnimationEventValid = ValidateTimingEffectAnimationEvent();
            report.comboHiddenDigitsTransparent = ValidateComboHiddenDigits();
            report.comboSpriteGeometryValid = ValidateComboSpriteGeometry();
            report.standardHoldFrameSweepValid = ValidateStandardHoldFrameSweep();
            report.longNotesShaderValid = ValidateLongNotesShader();

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            var reportPath = Path.Combine(projectRoot, "reverse", "reports", "unity-rhythm-validation.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log($"OPENWDS_VALIDATION loaded={report.loadedPrefabCount}/{report.prefabCount} " +
                      $"missingScripts={report.totalMissingScripts} " +
                      $"missingParticleMaterials={report.totalMissingParticleMaterials} " +
                      $"unsupportedParticleMaterials={report.totalUnsupportedParticleMaterials} " +
                      $"unresolvedRefs={report.totalUnresolvedPlaceholderReferences} " +
                      $"resolvedScripts={report.resolvedRecoveredScriptCount}/{report.recoveredScriptCount} " +
                      $"report={reportPath}");

            if (report.loadedPrefabCount != report.prefabCount ||
                report.totalMissingScripts != 0 ||
                report.totalMissingParticleMaterials != 0 ||
                report.totalUnsupportedParticleMaterials != 0 ||
                report.totalUnresolvedPlaceholderReferences != 0 ||
                !report.curtainPremultipliedAlphaSettingsValid ||
                !report.defaultSceneGameHudInitialized ||
                !report.lifeAndPrincipalGaugeValid ||
                !report.laneSpriteTexturesValid ||
                !report.timingEffectAnimationEventValid ||
                !report.comboHiddenDigitsTransparent ||
                !report.comboSpriteGeometryValid ||
                !report.standardHoldFrameSweepValid ||
                !report.longNotesShaderValid ||
                report.resolvedRecoveredScriptCount != report.recoveredScriptCount)
            {
                throw new InvalidOperationException(
                    "Core rhythm prefab or recovered script validation failed."
                );
            }
        }

        private static bool ValidateLongNotesShader()
        {
            var materialAndShaderPaths = new[]
            {
                new[] { "Assets/Resources/Material/Game/LongNotesSprite.mat",
                    "Assets/Resources/Shader/Shader Graphs_LongNotesSprite.shader" },
                new[] { "Assets/Resources/Material/Game/LongNotesSpriteIsTouch.mat",
                    "Assets/Resources/Shader/Shader Graphs_LongNotesSprite_0.shader" },
                new[] { "Assets/Resources/Material/Game/ScratchLongNotesSprite.mat",
                    "Assets/Resources/Shader/Shader Graphs_LongNotesSprite_1.shader" },
                new[] { "Assets/Resources/Material/Game/ScratchLongNotesSpriteIsTouch.mat",
                    "Assets/Resources/Shader/Shader Graphs_LongNotesSprite_1.shader" },
            };
            var valid = true;
            foreach (var paths in materialAndShaderPaths)
            {
                var path = paths[0];
                var expectedShaderPath = paths[1];
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                var shaderPath = material == null || material.shader == null
                    ? null : AssetDatabase.GetAssetPath(material.shader);
                var itemValid = material != null && material.shader != null &&
                    material.shader.isSupported && shaderPath == expectedShaderPath &&
                    material.renderQueue == 3000 &&
                    material.HasInteger("IsTouchMask") &&
                    material.HasInteger("_IsScratch");
                valid &= itemValid;
                Debug.Log($"OPENWDS_LONG_NOTES_MATERIAL path={path} " +
                          $"shader={shaderPath ?? "<null>"} " +
                          $"supported={(material != null && material.shader != null && material.shader.isSupported)} " +
                          $"queue={(material == null ? -1 : material.renderQueue)} " +
                          $"valid={itemValid}");
            }
            return valid;
        }

        private static bool ValidateLaneSpriteTextures()
        {
            const string lanePath =
                "Assets/Resources/Prefabs/Features/Game/LaneGroup.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(lanePath);
            var requiredNames = new HashSet<string>
            {
                "StartLineSprite", "BG_LaneBorder", "JudgeArea", "BG_Lane",
            };
            var resolved = new HashSet<string>();
            if (prefab != null)
            {
                foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!requiredNames.Contains(renderer.name)) continue;
                    var valid = renderer.sprite != null && renderer.sprite.texture != null;
                    Debug.Log($"OPENWDS_LANE_SPRITE name={renderer.name} " +
                              $"sprite={(renderer.sprite == null ? "<null>" : renderer.sprite.name)} " +
                              $"texture={(renderer.sprite == null || renderer.sprite.texture == null ? "<null>" : renderer.sprite.texture.name)} " +
                              $"valid={valid}");
                    if (valid) resolved.Add(renderer.name);
                }
            }
            var allValid = resolved.SetEquals(requiredNames);
            Debug.Log($"OPENWDS_LANE_SPRITES resolved={resolved.Count}/{requiredNames.Count} " +
                      $"valid={allValid}");
            return allValid;
        }

        private static bool ValidateTimingEffectAnimationEvent()
        {
            const string prefabPath =
                "Assets/Resources/Prefabs/Features/Game/TimingEffect.prefab";
            const string clipPath =
                "Assets/Resources/Animation/Game/TimingEffect_anime.anim";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var effect = prefab == null
                ? null : prefab.GetComponent<Sirius.Game.TimingEffect>();
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            var receiver = typeof(Sirius.Game.TimingEffect).GetMethod(
                "OnExit", BindingFlags.Instance | BindingFlags.Public,
                null, Type.EmptyTypes, null);
            var eventFound = false;
            if (clip != null)
            {
                foreach (var animationEvent in AnimationUtility.GetAnimationEvents(clip))
                {
                    if (animationEvent.functionName == "OnExit")
                    {
                        eventFound = true;
                        break;
                    }
                }
            }
            var valid = effect != null && receiver != null && eventFound;
            Debug.Log($"OPENWDS_TIMING_EVENT component={effect != null} " +
                      $"receiver={receiver != null} event={eventFound} valid={valid}");
            return valid;
        }

        private static bool ValidateComboHiddenDigits()
        {
            const string scenePath = "Assets/OpenWDS/Scenes/OfflineRhythmPreview.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            RecoveredGameHudRuntime hud = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                hud = root.GetComponentInChildren<RecoveredGameHudRuntime>(true);
                if (hud != null) break;
            }
            if (hud == null) return false;
            hud.Initialize();
            var combo = hud.ComboPanel;
            combo.SetComboCount(5, Sirius.Game.UI.RecoveredComboType.None);
            var serialized = new SerializedObject(combo);
            var imagesProperty = serialized.FindProperty("_countDigitImages");
            if (imagesProperty == null || imagesProperty.arraySize < 2) return false;
            var valid = true;
            for (var index = 0; index < imagesProperty.arraySize; index++)
            {
                var image = imagesProperty.GetArrayElementAtIndex(index)
                    .objectReferenceValue as Image;
                var alpha = image == null ? -1f : image.color.a;
                var expectedVisible = index == 0;
                var slotValid = image != null &&
                    (expectedVisible ? Mathf.Approximately(alpha, 1f) : Mathf.Approximately(alpha, 0f));
                valid &= slotValid;
                Debug.Log($"OPENWDS_COMBO_DIGIT index={index} alpha={alpha} " +
                          $"expectedVisible={expectedVisible} valid={slotValid}");
            }
            return valid;
        }

        private static bool ValidateComboSpriteGeometry()
        {
            var prefixes = new[]
            {
                "txt_game_txt_combo_",
                "txt_game_txt_combo_fc_",
                "txt_game_txt_combo_ap_",
                "txt_game_txt_combo_nc_",
            };
            var valid = true;
            foreach (var prefix in prefixes)
            {
                for (var digit = 0; digit < 10; digit++)
                {
                    var path = "Assets/Resources/Sprite/" + prefix +
                               digit + ".asset";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    var padding = sprite == null
                        ? Vector4.zero
                        : UnityEngine.Sprites.DataUtility.GetPadding(sprite);
                    var sourcePixels = sprite == null
                        ? Vector2.zero
                        : new Vector2(
                            sprite.bounds.size.x * sprite.pixelsPerUnit,
                            sprite.bounds.size.y * sprite.pixelsPerUnit);
                    var trimmedSize = sprite == null
                        ? Vector2.zero : sprite.textureRect.size;
                    var digitValid = sprite != null && sprite.texture != null &&
                                     Mathf.Approximately(sprite.pixelsPerUnit, 100f) &&
                                     sprite.rect.size == new Vector2(118f, 148f) &&
                                     sourcePixels == new Vector2(118f, 148f) &&
                                     sprite.pivot == new Vector2(59f, 74f) &&
                                     Mathf.Abs(
                                         padding.x + trimmedSize.x + padding.z - 118f) <
                                     0.01f &&
                                     Mathf.Abs(
                                         padding.y + trimmedSize.y + padding.w - 148f) <
                                     0.01f;
                    valid &= digitValid;
                    Debug.Log($"OPENWDS_COMBO_GEOMETRY set={prefix} digit={digit} " +
                              $"rect={(sprite == null ? Vector2.zero : sprite.rect.size)} " +
                              $"boundsPixels={sourcePixels} padding={padding} " +
                              $"texture={(sprite == null || sprite.texture == null ? "<null>" : sprite.texture.name)} " +
                              $"valid={digitValid}");
                }
            }
            return valid;
        }

        private static bool ValidateStandardHoldFrameSweep()
        {
            var chartPath = Path.Combine(
                Application.dataPath, "StreamingAssets", "OpenWDS",
                "StandardCharts", "1", "1", "1.csv");
            var notation = RecoveredStandardNotation.Parse(
                File.ReadAllText(chartPath));
            var manager = new RecoveredStandardHoldNoteManager(notation);
            var action = new RecoveredStandardHoldAction(manager);
            var hitLane = new RecoveredHitLaneEntity(7, 0, 0, 0, 0);
            var stationary = new RecoveredInputEntity(
                5, 0, Vector2.zero, Vector2.zero, Vector2.zero,
                RecoveredTouchPhase.Stationary);
            var earlyConsumedAt = -1L;
            for (var musicMilliseconds = 4958L;
                 musicMilliseconds < 5084L;
                 musicMilliseconds++)
            {
                if (!action.TryHold(
                        stationary, hitLane, musicMilliseconds).Consumed)
                    continue;
                earlyConsumedAt = musicMilliseconds;
                break;
            }
            var endResult = action.TryHold(stationary, hitLane, 5084L);
            var valid = earlyConsumedAt < 0 && endResult.Consumed &&
                        endResult.HoldNote != null &&
                        endResult.HoldNote.EndMilliseconds == 5084L &&
                        endResult.Timing.TimingType ==
                        RecoveredTimingType.PerfectStar;
            Debug.Log($"OPENWDS_HOLD_FRAME_SWEEP earlyConsumedAt={earlyConsumedAt} " +
                      $"endConsumed={endResult.Consumed} " +
                      $"endTiming={endResult.Timing.TimingType} valid={valid}");
            return valid;
        }

        private static bool ValidateCurtainPremultipliedAlphaSettings()
        {
            const string texturePath =
                "Assets/Resources/Spine/curtain_Albedo.png";
            const string materialPath =
                "Assets/Resources/Spine/curtain_Albedo_Spine.mat";
            const string shaderPath =
                "Assets/Spine/Runtime/spine-unity/Shaders/Spine-Skeleton.shader";
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var actualShaderPath = material == null || material.shader == null
                ? null : AssetDatabase.GetAssetPath(material.shader);
            var valid = importer != null && !importer.alphaIsTransparency &&
                        actualShaderPath == shaderPath &&
                        material.renderQueue == 3000;
            Debug.Log($"OPENWDS_CURTAIN_PMA texture={texturePath} " +
                      $"alphaIsTransparency={(importer != null && importer.alphaIsTransparency)} " +
                      $"shader={actualShaderPath ?? "<null>"} " +
                      $"queue={(material == null ? -1 : material.renderQueue)} " +
                      $"valid={valid}");
            return valid;
        }

        private static bool ValidateDefaultSceneGameHud()
        {
            const string scenePath = "Assets/OpenWDS/Scenes/OfflineRhythmPreview.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            RecoveredGameHudRuntime hud = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                hud = root.GetComponentInChildren<RecoveredGameHudRuntime>(true);
                if (hud != null) break;
            }
            if (hud == null)
            {
                Debug.Log("OPENWDS_DEFAULT_SCENE_HUD found=False initialized=False");
                return false;
            }
            hud.Initialize();
            var initiallyVisible = hud.IsVisible;
            var scoreContextAbsent = hud.ScorePanel != null && hud.Score == null &&
                                     !hud.ScorePanel.gameObject.activeSelf;
            var achievementSettingOff = hud.AchievementRatePanel != null &&
                                        !hud.AchievementRatePanel.IsVisible;
            hud.Hide();
            var valid = hud.TimingPoolCount == 1 && hud.ComboPanel != null &&
                        hud.LifeGauge != null && scoreContextAbsent &&
                        achievementSettingOff && initiallyVisible && !hud.IsVisible;
            Debug.Log($"OPENWDS_DEFAULT_SCENE_HUD found=True initialized={valid} " +
                      $"timingPool={hud.TimingPoolCount} combo={hud.ComboPanel != null} " +
                      $"life={hud.LifeGauge != null} scoreContextAbsent={scoreContextAbsent} " +
                      $"achievementOff={achievementSettingOff} hide={!hud.IsVisible}");
            return valid;
        }

        private static bool ValidateLifeAndPrincipalGauge()
        {
            const string lifePath =
                "Assets/Resources/Prefabs/Features/Game/Life.prefab";
            const string principalPath =
                "Assets/Resources/Prefabs/Features/Game/PrincipalGauge.prefab";
            var lifePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(lifePath);
            var principalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(principalPath);
            if (lifePrefab == null || principalPrefab == null) return false;
            var lifeObject = UnityEngine.Object.Instantiate(lifePrefab);
            var principalObject = UnityEngine.Object.Instantiate(principalPrefab);
            try
            {
                var life = lifeObject.GetComponent<Sirius.Game.UI.LifeGauge>();
                var principal = principalObject.GetComponent<
                    Sirius.Game.UI.PrincipalGauge>();
                if (life == null || principal == null) return false;
                life.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
                life.SetLifeValue(1320, 1000, 12);
                var overLifeValid = life.CurrentLifeCount.text == "1320" &&
                                    life.CurrentLifeGuardCount.text == "+12" &&
                                    life.Gauge.maxValue == 1000f &&
                                    life.Gauge.value == 1000f &&
                                    ((Color32)life.CurrentLifeCount.color).Equals(
                                        new Color32(254, 224, 117, 255)) &&
                                    ((Color32)life.FillImage.color).Equals(
                                        new Color32(255, 255, 255, 255));
                life.SetLifeValue(200, 1000, 0);
                var dangerValid = life.CurrentLifeGuardCount.text == string.Empty &&
                                  ((Color32)life.CurrentLifeCount.color).Equals(
                                      new Color32(235, 67, 64, 255)) &&
                                  ((Color32)life.FillImage.color).Equals(
                                      new Color32(235, 67, 64, 255));
                principal.SetPrincipalValue(400, 1000);
                var principalValid = principal.CurrentPrincipalCount.text == "400" &&
                                     principal.MaxPrincipalCount.text == "1000" &&
                                     principal.Gauge.minValue == 0f &&
                                     principal.Gauge.maxValue == 1000f &&
                                     principal.Gauge.value == 400f;
                var valid = overLifeValid && dangerValid && principalValid;
                Debug.Log($"OPENWDS_LIFE_PRINCIPAL life={overLifeValid && dangerValid} " +
                          $"principal={principalValid} valid={valid}");
                return valid;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lifeObject);
                UnityEngine.Object.DestroyImmediate(principalObject);
            }
        }

        private static void ValidateAdditiveParticleMaterials()
        {
            foreach (var path in AdditiveParticleMaterials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                var shader = material == null ? null : material.shader;
                var valid = shader != null && shader.isSupported &&
                            shader.name == RecoveredParticleShaderName &&
                            material.HasProperty("_MainTex");
                Debug.Log($"OPENWDS_ADDITIVE_MATERIAL path={path} valid={valid} " +
                          $"shader={(shader == null ? "<null>" : shader.name)} " +
                          $"supported={(shader != null && shader.isSupported)} " +
                          $"queue={(material == null ? -1 : material.renderQueue)}");
                if (!valid)
                    throw new InvalidOperationException(
                        $"Recovered additive particle material validation failed: {path}");
            }
        }

        private static PrefabResult ValidatePrefab(string path)
        {
            var result = new PrefabResult { path = path };
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return result;
            }

            result.loaded = true;
            var transforms = prefab.GetComponentsInChildren<Transform>(true);
            result.gameObjects = transforms.Length;
            foreach (var transform in transforms)
            {
                var components = transform.gameObject.GetComponents<Component>();
                result.components += components.Length;
                result.missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    transform.gameObject
                );
            }
            foreach (var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                result.particleRenderers++;
                var materials = renderer.sharedMaterials;
                if (materials.Length == 0)
                {
                    result.missingParticleMaterials++;
                    continue;
                }
                foreach (var material in materials)
                {
                    if (material == null)
                        result.missingParticleMaterials++;
                    else if (material.shader == null || !material.shader.isSupported)
                        result.unsupportedParticleMaterials++;
                }
            }
            result.dependencies = AssetDatabase.GetDependencies(path, true).Length;
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var prefabText = File.ReadAllText(Path.Combine(projectRoot, path));
            result.unresolvedPlaceholderReferences = CountOccurrences(
                prefabText,
                "deadbeef"
            );
            return result;
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
    }
}
