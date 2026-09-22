#if UNITY_ANDROID
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;

namespace OpenWDS.Editor
{
    // Local APK/JAR reads need no INTERNET permission. Remove Unity's auto-added permission.
    public sealed class OfflineAndroidManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10000;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // Preserve the original game's legacy, round and adaptive launcher artwork.
            var source = Path.GetFullPath("Assets/OpenWDS/Editor/AndroidLauncherIcons");
            var launcher = Path.GetFullPath(Path.Combine(path, "../launcher/src/main"));
            if (!Directory.Exists(source) || !Directory.Exists(launcher))
                throw new DirectoryNotFoundException("Original launcher artwork or generated launcher is missing.");
            foreach (var icon in Directory.GetFiles(source, "*", SearchOption.AllDirectories)
                .Where(file => file.EndsWith(".png") || file.EndsWith(".xml")))
            {
                var destination = Path.Combine(launcher, "res", icon.Substring(source.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(icon, destination, true);
            }
            var launcherManifest = Path.Combine(launcher, "AndroidManifest.xml");
            var launcherDocument = XDocument.Load(launcherManifest);
            XNamespace launcherAndroid = "http://schemas.android.com/apk/res/android";
            var application = launcherDocument.Root.Element("application");
            application.SetAttributeValue(launcherAndroid + "icon", "@mipmap/app_icon");
            application.SetAttributeValue(launcherAndroid + "roundIcon", "@mipmap/app_icon_round");
            launcherDocument.Save(launcherManifest);
            foreach (var manifest in Directory.GetFiles(path, "AndroidManifest.xml", SearchOption.AllDirectories))
            {
                var document = XDocument.Load(manifest);
                XNamespace android = "http://schemas.android.com/apk/res/android";
                var permissions = document.Descendants("uses-permission").Where(e =>
                    (string)e.Attribute(android + "name") == "android.permission.INTERNET" ||
                    (string)e.Attribute(android + "name") == "android.permission.ACCESS_NETWORK_STATE").ToArray();
                foreach (var permission in permissions) permission.Remove();
                document.Save(manifest);
            }
        }
    }
}
#endif
