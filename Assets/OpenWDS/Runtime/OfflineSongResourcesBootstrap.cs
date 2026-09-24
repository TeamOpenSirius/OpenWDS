using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace OpenWDS.Runtime
{
    /// <summary>The release entry works without any song payload; import is entirely local.</summary>
    public sealed class OfflineSongResourcesBootstrap : MonoBehaviour
    {
        private const string InstalledDirectoryKey = "OpenWDS.SongResourceDirectory";
        private bool _busy, _ready, _importing;
        private Text _status;
        private Font _dialogFont;
        private Button _choose, _continue;
        private InputField _path;
        private bool _previousRunInBackground;
        private int _previousSleepTimeout;
        private volatile bool _preservedResources;
        private volatile string _message = "Checking local resources...";
        private Task _operation;
        private string _root;
        private void Start()
        {
            gameObject.name = "OfflineSongResources";
            _root = SongResourceStore.Root;
            _previousRunInBackground = Application.runInBackground;
            _previousSleepTimeout = Screen.sleepTimeout;
            Application.runInBackground = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (!PlayerPrefs.HasKey(InstalledDirectoryKey)) CreateDialog();
            CheckInstalled();
        }
        private void CheckInstalled()
        {
            _busy = true; _ready = false;
            _operation = Task.Run(() =>
            {
                SongResourceImporter.ValidateInstalled(_root);
            });
        }
        private void Update()
        {
            if (_status != null)
            {
                _status.text = _message;
                _choose.interactable = !_busy;
                _continue.transform.parent.gameObject.SetActive(_ready && !_busy);
            }
            if (_operation == null || !_operation.IsCompleted) return;
            _busy = false;
            if (_operation.IsFaulted)
            {
                if (_status == null) CreateDialog();
                _message = _operation.Exception.GetBaseException().Message;
                _ready = _preservedResources;
                _importing = false;
#if UNITY_ANDROID && !UNITY_EDITOR
                try { using (var picker = new AndroidJavaClass("dev.openwds.resources.SongResourcePicker")) picker.CallStatic("abandonSource"); }
                catch (Exception) { }
#endif
                _operation = null;
                return;
            }
            _operation = null;
            _ready = true;
            SongResourceStore.Invalidate();
            // Validate before persisting, including installations made by an older APK.
            PlayerPrefs.SetString(InstalledDirectoryKey, _root);
            PlayerPrefs.Save();
            if (_importing)
            {
                _importing = false;
#if UNITY_ANDROID && !UNITY_EDITOR
                _busy = true;
                _message = "Resources installed. Removing original ZIP...";
                try
                {
                    using (var picker = new AndroidJavaClass("dev.openwds.resources.SongResourcePicker"))
                        picker.CallStatic("deleteImportedSource");
                }
                catch (Exception) { OnSourceDeleted("retained"); }
                return;
#endif
            }
            EnterGame();
        }
        private void EnterGame()
        {
            if (!_ready) return;
            _busy = true;
            _message = "Starting game...";
            SceneManager.LoadSceneAsync(SceneNavigationRuntime.MainScene);
        }
        private void OnDestroy()
        {
            Application.runInBackground = _previousRunInBackground;
            Screen.sleepTimeout = _previousSleepTimeout;
        }
        private RectTransform Box(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }
        private Text Label(Transform parent, string value, Vector2 size, Vector2 position, int fontSize)
        {
            var text = Box("Text", parent, size, position).gameObject.AddComponent<Text>();
            text.font = _dialogFont;
            text.text = value; text.fontSize = fontSize; text.color = new Color(.337f, .345f, .404f);
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            return text;
        }
        private Button BindButton(Transform shell, string parentName, string value,
            UnityEngine.Events.UnityAction action)
        {
            var parent = shell.GetComponentsInChildren<Transform>(true).Single(t => t.name == parentName);
            var button = parent.GetComponentInChildren<Button>(true);
            button.GetComponentInChildren<Text>(true).text = value;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            parent.gameObject.SetActive(true);
            return button;
        }
        private void CreateDialog()
        {
            if (_status != null) return;
            var canvas = new GameObject("Resource import dialog", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 31000;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1200); scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform);
            var prefab = Resources.Load<GameObject>("Prefabs/Common/Dialog/Dialog");
            if (prefab == null) throw new InvalidOperationException("Original resource-install Dialog prefab is missing.");
            var shell = Instantiate(prefab, canvas.transform, false);
            shell.name = "SongResourceDialog";
            shell.SetActive(true);
            shell.GetComponent<RectTransform>().sizeDelta = new Vector2(1228, 796);
            foreach (var group in shell.GetComponentsInChildren<CanvasGroup>(true))
            {
                group.alpha = 1; group.interactable = true; group.blocksRaycasts = true;
            }
            var title = shell.GetComponentsInChildren<Text>(true).Single(t => t.name == "TitleText");
            title.text = "楽曲データのインストール";
            _dialogFont = title.font;
            var body = shell.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Body");
            Label(body, "アプリに付属する楽曲データ ZIP を選択してください。\n確認後にインストールします。\n完了すると、選択した ZIP は削除されます。",
                new Vector2(1080, 180), new Vector2(0, 130), 32);
            _status = Label(body, _message, new Vector2(1080, 170), new Vector2(0, -65), 28);
#if !UNITY_ANDROID || UNITY_EDITOR
            var input = Box("ZIP path", body, new Vector2(1080, 48), new Vector2(0, -190));
            input.gameObject.AddComponent<Image>().color = new Color(.92f, .93f, .96f);
            _path = input.gameObject.AddComponent<InputField>();
            _path.textComponent = Label(input, "", new Vector2(1050, 46), Vector2.zero, 24);
#endif
            _choose = BindButton(shell.transform, "FirstButtonParent", "ZIPを選択", ChooseZip);
            _continue = BindButton(shell.transform, "SecondButtonParent", "ゲームを開始", EnterGame);
            shell.GetComponentsInChildren<Transform>(true).Single(t => t.name == "ThirdButtonParent").gameObject.SetActive(false);
            _continue.transform.parent.gameObject.SetActive(false);
        }
        private void ChooseZip()
        {
            _busy = true; _message = "Select a local ZIP. Preparing large files may take a moment...";
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var picker = new AndroidJavaClass("dev.openwds.resources.SongResourcePicker")) picker.CallStatic("open");
            }
            catch (Exception e) { OnPickError(e.Message); }
#else
            OnPicked(_path.text);
#endif
        }
        [Preserve] public void OnSourceDeleted(string result)
        {
            _busy = false;
            if (result == "deleted") EnterGame();
            else _message = "Resources installed successfully. The original ZIP could not be deleted. You can remove it manually and start the game.";
        }
        [Preserve] public void OnPickError(string message) { _busy = false; _message = message; }
        [Preserve] public void OnPicked(string path)
        {
            if (string.IsNullOrEmpty(path)) { _busy = false; _message = "Import cancelled."; return; }
            _busy = true; _ready = false; _preservedResources = false; _importing = true;
            _operation = Task.Run(() =>
            {
                try
                {
                    SongResourceImporter.Import(path, _root, value => _message = value);
                    SongResourceImporter.ValidateInstalled(_root);
                }
                catch
                {
                    try { SongResourceImporter.ValidateInstalled(_root); _preservedResources = true; } catch { }
                    throw;
                }
                finally
                {
#if UNITY_ANDROID && !UNITY_EDITOR
                    // Source deletion is requested on the main thread only after successful persistence.
                    if (File.Exists(path)) File.Delete(path);
#endif
                }
            });
        }
    }
}
