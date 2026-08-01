using System;
using Newtonsoft.Json;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Six-setting transaction used by the original OptionDialogBody flow.
    /// Current objects are edited by panels, while the baseline represents the
    /// repository's before snapshot used for change detection and cancellation.
    /// </summary>
    public sealed class RecoveredSettingsSession
    {
        [Serializable]
        public sealed class Snapshot
        {
            public RecoveredGameSettings.System SystemSettings;
            public RecoveredGameSettings.Basic GameSettings;
            public RecoveredGameSettings.Detail GameDetailSettings;
            public RecoveredGameSettings.Custom GameCustomSettings;
            public RecoveredGameSettings.SoundVolume SoundVolumeSettings;
            public RecoveredGameSettings.Bluetooth BluetoothSettings;

            public static Snapshot Default()
            {
                return new Snapshot
                {
                    SystemSettings = RecoveredGameSettings.System.Default(),
                    GameSettings = RecoveredGameSettings.Basic.Default(),
                    GameDetailSettings = RecoveredGameSettings.Detail.Default(),
                    GameCustomSettings = RecoveredGameSettings.Custom.Default(),
                    SoundVolumeSettings = RecoveredGameSettings.SoundVolume.Default(),
                    BluetoothSettings = RecoveredGameSettings.Bluetooth.Default(),
                };
            }
        }

        private Snapshot _baseline;

        public Snapshot Current { get; private set; }

        public bool HasChanges
        {
            get { return !AreEquivalent(_baseline, Current); }
        }

        public RecoveredSettingsSession(Snapshot loaded)
        {
            if (loaded == null)
            {
                throw new ArgumentNullException(nameof(loaded));
            }

            Current = Clone(loaded);
            _baseline = Clone(loaded);
        }

        public void Cancel()
        {
            Current = Clone(_baseline);
        }

        public void AcceptSavedValues()
        {
            _baseline = Clone(Current);
        }

        public static Snapshot Clone(Snapshot value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return JsonConvert.DeserializeObject<Snapshot>(
                JsonConvert.SerializeObject(value));
        }

        private static bool AreEquivalent(Snapshot left, Snapshot right)
        {
            return string.Equals(
                JsonConvert.SerializeObject(left),
                JsonConvert.SerializeObject(right),
                StringComparison.Ordinal);
        }
    }
}
