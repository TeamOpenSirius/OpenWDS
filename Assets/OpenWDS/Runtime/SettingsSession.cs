using System;
using Newtonsoft.Json;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Six-setting transaction used by the original OptionDialogBody flow.
    /// Current objects are edited by panels, while the baseline represents the
    /// repository's before snapshot used for change detection and cancellation.
    /// </summary>
    public sealed class SettingsSession
    {
        [Serializable]
        public sealed class Snapshot
        {
            public GameSettings.System SystemSettings;
            public GameSettings.Basic GameSettings;
            public GameSettings.Detail GameDetailSettings;
            public GameSettings.Custom GameCustomSettings;
            public GameSettings.SoundVolume SoundVolumeSettings;
            public GameSettings.Bluetooth BluetoothSettings;

            public static Snapshot Default()
            {
                return new Snapshot
                {
                    SystemSettings = global::OpenWDS.Runtime.GameSettings.System.Default(),
                    GameSettings = global::OpenWDS.Runtime.GameSettings.Basic.Default(),
                    GameDetailSettings = global::OpenWDS.Runtime.GameSettings.Detail.Default(),
                    GameCustomSettings = global::OpenWDS.Runtime.GameSettings.Custom.Default(),
                    SoundVolumeSettings = global::OpenWDS.Runtime.GameSettings.SoundVolume.Default(),
                    BluetoothSettings = global::OpenWDS.Runtime.GameSettings.Bluetooth.Default(),
                };
            }
        }

        private Snapshot _baseline;

        public Snapshot Current { get; private set; }

        public bool HasChanges
        {
            get { return !AreEquivalent(_baseline, Current); }
        }

        public SettingsSession(Snapshot loaded)
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
