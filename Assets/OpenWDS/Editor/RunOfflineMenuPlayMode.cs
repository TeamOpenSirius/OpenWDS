using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace OpenWDS.Editor
{
    [InitializeOnLoad]
    public static class RunOfflineMenuPlayMode
    {
        private const string Key = "OpenWDS.MenuGate.";
        private static string Report => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../reverse/reports/offline-menu-playmode.json"));
        private static int _phase;
        private static double _next;
        private static SettingsSession.Snapshot _original;
        private static string _error;
        static RunOfflineMenuPlayMode() { EditorApplication.update += Poll; Application.logMessageReceived += Log; }
        public static void Run()
        {
            SessionState.SetBool(Key + "active", true);
            SessionState.SetBool(Key + "exit", false);
            SessionState.SetString(Key + "start", DateTime.UtcNow.ToString("O"));
            var keys = new[] { SettingsPersistence.SystemSettingsKey, SettingsPersistence.GameSettingsKey,
                SettingsPersistence.GameDetailSettingsKey, SettingsPersistence.GameCustomSettingsKey,
                SettingsPersistence.SoundVolumeSettingsKey, SettingsPersistence.BluetoothSettingsKey };
            var originalFiles = keys.ToDictionary(k => Path.Combine(Application.persistentDataPath, "Users", k),
                k => File.Exists(Path.Combine(Application.persistentDataPath, "Users", k))
                    ? File.ReadAllBytes(Path.Combine(Application.persistentDataPath, "Users", k)) : null);
            SessionState.SetString(Key + "settingsFiles", JsonConvert.SerializeObject(originalFiles));
            EditorSceneManager.OpenScene("Assets/OpenWDS/Scenes/LocalMusicSelection.unity");
            EditorApplication.isPlaying = true;
        }
        private static void Log(string message, string stack, LogType type)
        {
            if (SessionState.GetBool(Key + "active", false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert ||
                message.Contains("referenced script") && message.Contains("missing")))
                _error = _error ?? message;
        }
        private static void Poll()
        {
            if (!SessionState.GetBool(Key + "active", false)) return;
            if (SessionState.GetBool(Key + "exit", false))
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetBool(Key + "active", false);
                    EditorApplication.Exit(SessionState.GetBool(Key + "passed", false) ? 0 : 1);
                }
                return;
            }
            if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + "start", ""))).TotalSeconds > 180) { Finish("Timed out"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (_error != null) { Finish(_error); return; }
            if (EditorApplication.timeSinceStartup < _next) return;
            try
            {
                var selection = UnityEngine.Object.FindObjectOfType<LocalMusicSelectionRuntime>();
                var menu = UnityEngine.Object.FindObjectOfType<OfflineMenuRuntime>();
                if (selection == null || !selection.IsInitialized || menu == null || !menu.IsReady || menu.IsTransitioning) return;
                var documents = UnityEngine.Object.FindObjectOfType<OfflineMenuDocuments>();
                if (documents != null && documents.IsTransitioning) return;
                var settings = UnityEngine.Object.FindObjectOfType<GamePauseRuntime>();
                switch (_phase)
                {
                    case 0:
                        _original = new SettingsStore().LoadOrDefault();
                        Click(GameObject.Find("MenuView"), "MenuButton"); break;
                    case 1:
                        Require(menu.IsOpen, "Menu did not open");
                        Require(menu.Popup.GetComponentsInChildren<Image>().All(i => i.sprite != null || i.name == "Background"), "Missing menu sprite");
                        Capture("offline-menu.png");
                        var dots = UnityEngine.Object.FindObjectOfType<EllipsisIconGraphic>();
                        var mesh = dots.canvasRenderer.GetMesh();
                        Require(mesh != null && mesh.vertexCount > 0, "Custom chart menu icon has no rendered geometry");
                        ClickFeature(menu, "Option"); break;
                    case 2:
                        Require(settings.IsSettingsOpen, "Settings did not open");
                        Capture("offline-menu-settings.png");
                        // Exercise the actual note-speed stepper and verify the transaction.
                        var speed = Find(GameObject.Find("OptionDialogBody").transform, "NoteSpeedSettings");
                        var direction = _original.GameSettings.NoteSpeed >= 25 ? "Minus" : "Plus";
                        var plus = speed.GetComponentsInChildren<Button>().First(b => b.name.Contains(direction));
                        ClickButton(plus);
                        Require(settings.HasPendingChanges, "Stepper did not edit settings");
                        Click(GameObject.Find("OptionDialogBody").transform.parent.parent.gameObject, "FirstButton"); break;
                    case 3:
                        Require(!settings.IsSettingsOpen && menu.Popup.activeSelf, "Cancel did not return to menu");
                        Require(JsonConvert.SerializeObject(new SettingsStore().LoadOrDefault()) == JsonConvert.SerializeObject(_original), "Cancel changed saved settings");
                        ClickFeature(menu, "Option"); break;
                    case 4:
                        settings.CurrentSettings.GameSettings.NoteSpeed = _original.GameSettings.NoteSpeed == 9 ? 10 : 9;
                        Click(GameObject.Find("OptionDialogBody").transform.parent.parent.gameObject, "SecondButton"); break;
                    case 5:
                        Require(!settings.IsSettingsOpen && !settings.IsSettingsConfirmationOpen, "Selection settings entered restart confirmation");
                        Require(new SettingsStore().LoadOrDefault().GameSettings.NoteSpeed != _original.GameSettings.NoteSpeed, "Save not persisted");
                        ClickFeature(menu, "Option"); break;
                    case 6:
                        Require(settings.CurrentSettings.GameSettings.NoteSpeed == new SettingsStore().LoadOrDefault().GameSettings.NoteSpeed, "Reopen did not load saved value");
                        settings.Cancel(); break;
                    case 7: ClickFeature(menu, "Notification"); break;
                    case 8:
                        Require(documents.Notice != null, "Original notification view missing");
                        Require(documents.Notice.GetComponentInChildren<EnhancedUI.EnhancedScroller.EnhancedScroller>() != null,
                            "Original EnhancedScroller missing");
                        CheckContained((RectTransform)Find(documents.Notice.transform, "ListScroller"),
                            (RectTransform)GameObject.Find("ProjectStatementNotice").transform);
                        foreach (var tab in documents.Notice.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("DocumentTab")))
                            CheckContained((RectTransform)Find(documents.Notice.transform, "MenuList"), (RectTransform)tab.transform);
                        Capture("offline-menu-notices.png");
                        Click(GameObject.Find("ProjectStatementNotice"), "Content"); break;
                    case 9:
                        Require(Find(documents.Notice.transform, "BodyPanel").gameObject.activeSelf, "Notice body did not open");
                        var noticeText = Find(Find(documents.Notice.transform, "NotificationContentScroller"), "Body").GetComponent<Text>();
                        Require(noticeText.text.Contains("非公式"), "Project statement missing from notice entry");
                        CheckScroll(Find(documents.Notice.transform, "NotificationContentScroller").GetComponent<ScrollRect>());
                        Capture("offline-menu-statement.png");
                        Click(documents.Notice, "BackButton"); break;
                    case 10:
                        Require(Find(documents.Notice.transform, "ListPanel").gameObject.activeSelf, "Back did not restore notice list");
                        Click(documents.Notice, "DocumentTab1"); break;
                    case 11:
                        Require(!documents.Notice.GetComponentsInChildren<Text>().Any(t => t.text == "OpenWDS オフライン版について"),
                            "Empty category retained previous notice");
                        Click(documents.Notice, "DocumentTab0"); break;
                    case 12:
                        Require(GameObject.Find("ProjectStatementNotice") != null, "Returning to important notices lost statement");
                        Click(documents.Notice, "CloseButton"); break;
                    case 13:
                        Require(documents.Notice == null, "Notice did not close");
                        ClickFeature(menu, "TermOfService"); break;
                    case 14:
                        CheckTerms(documents, "TermsOfService", 1);
                        Capture("offline-menu-terms.png");
                        Click(documents.Terms, "DocumentTab1"); break;
                    case 15:
                        CheckTerms(documents, "Copyright", 4);
                        Capture("offline-menu-copyright.png");
                        Click(documents.Terms, "DocumentTab2"); break;
                    case 16:
                        CheckTerms(documents, "PrivacyPolicy", 1);
                        Capture("offline-menu-privacy.png");
                        Click(GameObject.Find("OriginalTermsDialog"), "CloseButton"); break;
                    case 17: Click(menu.Popup, "CloseButton"); break;
                    case 18:
                        Require(!menu.IsOpen, "Menu blocker remained");
                        Click(GameObject.Find("MenuView"), "MenuButton"); break;
                    case 19: Click(menu.Popup, "CloseButton"); break;
                    case 20:
                        Require(!menu.IsOpen, "Repeated close failed");
                        Require(menu.CompletedHideCount == 7, "Original hide animation did not play for every navigation");
                        Finish(null); return;
                }
                _phase++;
                _next = EditorApplication.timeSinceStartup + 0.8;
            }
            catch (Exception e) { Finish(e.ToString()); }
        }
        private static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
        private static void ClickFeature(OfflineMenuRuntime menu, string name)
        {
            var button = menu.Popup.GetComponentsInChildren<Button>().Single(b => b.transform.parent.name == name);
            ClickButton(button);
        }
        private static void Click(GameObject root, string name)
        {
            var b = root.GetComponentsInChildren<Button>(true).First(x => x.name == name);
            ClickButton(b);
        }
        private static void Capture(string file)
        {
            var path = Path.Combine(Path.GetDirectoryName(Report), file);
            Require(RunLocalMusicSelectionPlayMode.CaptureSelection(path) > 100000, "Empty menu capture");
            Require(File.Exists(path), "Menu capture did not reach disk");
        }
        private static void CheckTerms(OfflineMenuDocuments documents, string resource, int count)
        {
            Require(documents.Terms != null, "Original terms prefab missing");
            for (var i = 1; i <= 4; i++)
            {
                var text = Find(documents.Terms.transform, "ContentText" + i).GetComponent<Text>();
                var expected = i > count ? "" : Resources.Load<TextAsset>("TextAsset/OfflineMenu/" + resource + (count == 4 ? i.ToString() : "")).text.Replace(' ', '\u00a0');
                Require(text.text == expected, "Original APK text differs: " + resource + " block " + i);
                Require(text.color.grayscale < .5f && text.color.a > .9f, "Original text contrast lost");
                Require(text.preferredHeight <= text.rectTransform.rect.height + 2, "Original text clipped: " + resource + " block " + i);
            }
            var scroll = Find(documents.Terms.transform, "Scroll View").GetComponent<ScrollRect>();
            Require(scroll.verticalNormalizedPosition > .99f, "Terms tab did not reset scroll position");
            CheckScroll(scroll);
        }
        private static void CheckContained(RectTransform viewport, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            var bounds = viewport.rect;
            foreach (var corner in corners)
            {
                var point = viewport.InverseTransformPoint(corner);
                Require(point.x >= bounds.xMin - 2 && point.x <= bounds.xMax + 2 &&
                    point.y >= bounds.yMin - 2 && point.y <= bounds.yMax + 2,
                    "Original layout clips " + child.name + " in " + viewport.name);
            }
        }
        private static void CheckScroll(ScrollRect scroll)
        {
            scroll.verticalNormalizedPosition = 0;
            Require(scroll.verticalNormalizedPosition < .01f || scroll.content.rect.height <= scroll.viewport.rect.height,
                "Document cannot scroll to final line");
            Canvas.ForceUpdateCanvases();
            var finalText = scroll.content.GetComponentsInChildren<Text>().Last(t => !string.IsNullOrEmpty(t.text));
            var corners = new Vector3[4];
            finalText.rectTransform.GetWorldCorners(corners);
            var bottom = scroll.viewport.InverseTransformPoint(corners[0]).y;
            Require(bottom >= scroll.viewport.rect.yMin - 2 && bottom <= scroll.viewport.rect.yMax + 2,
                "Final document text is outside the viewport at scroll bottom");
            scroll.verticalNormalizedPosition = 1;
        }
        private static void ClickButton(Button button)
        {
            Require(button.IsActive() && button.IsInteractable(), "Inactive button: " + button.name);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var data = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
                "Button blocked: " + button.name + " first=" + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(string error)
        {
            var originalFiles = JsonConvert.DeserializeObject<Dictionary<string, byte[]>>(
                SessionState.GetString(Key + "settingsFiles", "{}"));
            foreach (var item in originalFiles)
            {
                if (item.Value == null) File.Delete(item.Key);
                else { Directory.CreateDirectory(Path.GetDirectoryName(item.Key)); File.WriteAllBytes(item.Key, item.Value); }
            }
            File.WriteAllText(Report, JsonConvert.SerializeObject(new { passed = error == null, phase = _phase, error, screenshots = new[] { "offline-menu.png", "offline-menu-settings.png", "offline-menu-notices.png", "offline-menu-statement.png", "offline-menu-terms.png", "offline-menu-copyright.png", "offline-menu-privacy.png" }, settingsFilesRestored = true, completedHides = UnityEngine.Object.FindObjectOfType<OfflineMenuRuntime>()?.CompletedHideCount }, Formatting.Indented));
            SessionState.SetBool(Key + "passed", error == null);
            SessionState.SetBool(Key + "exit", true);
            Debug.Log("OPENWDS_MENU_PLAYMODE passed=" + (error == null) + " " + error);
            EditorApplication.isPlaying = false;
        }
    }
}
