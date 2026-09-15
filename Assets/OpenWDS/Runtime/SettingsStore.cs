using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Wire-compatible adapter for the original PersistentJsonPreferencesRepository.
    /// Every settings object is serialized by Json.NET, AES-encrypted, and stored in
    /// persistentDataPath/Users under its recovered preference key.
    /// </summary>
    public sealed class SettingsStore
    {
        private readonly string _settingsDirectory;

        public SettingsStore(string persistentDataPath = null)
        {
            var root = string.IsNullOrEmpty(persistentDataPath)
                ? Application.persistentDataPath
                : persistentDataPath;
            _settingsDirectory = Path.Combine(
                root,
                SettingsPersistence.CurrentDirectory);
        }

        public SettingsSession.Snapshot LoadOrDefault()
        {
            return new SettingsSession.Snapshot
            {
                SystemSettings = Load(
                    SettingsPersistence.SystemSettingsKey,
                    GameSettings.System.Default),
                GameSettings = Load(
                    SettingsPersistence.GameSettingsKey,
                    GameSettings.Basic.Default),
                GameDetailSettings = Load(
                    SettingsPersistence.GameDetailSettingsKey,
                    GameSettings.Detail.Default),
                GameCustomSettings = Load(
                    SettingsPersistence.GameCustomSettingsKey,
                    GameSettings.Custom.Default),
                SoundVolumeSettings = Load(
                    SettingsPersistence.SoundVolumeSettingsKey,
                    GameSettings.SoundVolume.Default),
                BluetoothSettings = Load(
                    SettingsPersistence.BluetoothSettingsKey,
                    GameSettings.Bluetooth.Default),
            };
        }

        public void Save(SettingsSession.Snapshot settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            Directory.CreateDirectory(_settingsDirectory);
            Save(SettingsPersistence.SystemSettingsKey, settings.SystemSettings);
            Save(SettingsPersistence.GameSettingsKey, settings.GameSettings);
            Save(SettingsPersistence.GameDetailSettingsKey, settings.GameDetailSettings);
            Save(SettingsPersistence.GameCustomSettingsKey, settings.GameCustomSettings);
            Save(SettingsPersistence.SoundVolumeSettingsKey, settings.SoundVolumeSettings);
            Save(SettingsPersistence.BluetoothSettingsKey, settings.BluetoothSettings);
        }

        private T Load<T>(string key, Func<T> defaultFactory)
        {
            var path = Path.Combine(_settingsDirectory, key);
            if (!File.Exists(path))
            {
                return defaultFactory();
            }

            try
            {
                var encrypted = File.ReadAllText(path);
                var json = SettingsCrypto.DecryptUtf8(encrypted);
                var value = JsonConvert.DeserializeObject<T>(json);
                return value == null ? defaultFactory() : value;
            }
            catch (Exception exception)
            {
                Debug.LogError(string.Format(
                    "Failed to load recovered settings '{0}': {1}",
                    key,
                    exception));
                return defaultFactory();
            }
        }

        private void Save<T>(string key, T value)
        {
            var json = JsonConvert.SerializeObject(value);
            var encrypted = SettingsCrypto.EncryptUtf8(json);
            File.WriteAllText(Path.Combine(_settingsDirectory, key), encrypted);
        }
    }
}
