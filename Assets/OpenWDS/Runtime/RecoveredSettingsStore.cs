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
    public sealed class RecoveredSettingsStore
    {
        private readonly string _settingsDirectory;

        public RecoveredSettingsStore(string persistentDataPath = null)
        {
            var root = string.IsNullOrEmpty(persistentDataPath)
                ? Application.persistentDataPath
                : persistentDataPath;
            _settingsDirectory = Path.Combine(
                root,
                RecoveredSettingsPersistence.CurrentDirectory);
        }

        public RecoveredSettingsSession.Snapshot LoadOrDefault()
        {
            return new RecoveredSettingsSession.Snapshot
            {
                SystemSettings = Load(
                    RecoveredSettingsPersistence.SystemSettingsKey,
                    RecoveredGameSettings.System.Default),
                GameSettings = Load(
                    RecoveredSettingsPersistence.GameSettingsKey,
                    RecoveredGameSettings.Basic.Default),
                GameDetailSettings = Load(
                    RecoveredSettingsPersistence.GameDetailSettingsKey,
                    RecoveredGameSettings.Detail.Default),
                GameCustomSettings = Load(
                    RecoveredSettingsPersistence.GameCustomSettingsKey,
                    RecoveredGameSettings.Custom.Default),
                SoundVolumeSettings = Load(
                    RecoveredSettingsPersistence.SoundVolumeSettingsKey,
                    RecoveredGameSettings.SoundVolume.Default),
                BluetoothSettings = Load(
                    RecoveredSettingsPersistence.BluetoothSettingsKey,
                    RecoveredGameSettings.Bluetooth.Default),
            };
        }

        public void Save(RecoveredSettingsSession.Snapshot settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            Directory.CreateDirectory(_settingsDirectory);
            Save(RecoveredSettingsPersistence.SystemSettingsKey, settings.SystemSettings);
            Save(RecoveredSettingsPersistence.GameSettingsKey, settings.GameSettings);
            Save(RecoveredSettingsPersistence.GameDetailSettingsKey, settings.GameDetailSettings);
            Save(RecoveredSettingsPersistence.GameCustomSettingsKey, settings.GameCustomSettings);
            Save(RecoveredSettingsPersistence.SoundVolumeSettingsKey, settings.SoundVolumeSettings);
            Save(RecoveredSettingsPersistence.BluetoothSettingsKey, settings.BluetoothSettings);
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
                var json = RecoveredSettingsCrypto.DecryptUtf8(encrypted);
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
            var encrypted = RecoveredSettingsCrypto.EncryptUtf8(json);
            File.WriteAllText(Path.Combine(_settingsDirectory, key), encrypted);
        }
    }
}
