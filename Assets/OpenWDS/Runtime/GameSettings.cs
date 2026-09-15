using System;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Settings ranges and defaults recovered from Sirius.Settings machine code.
    /// Enum-backed values remain integers until their enum members are recovered.
    /// </summary>
    public static class GameSettings
    {
        public const double MinimumNoteSpeed = 1d;
        public const double MaximumNoteSpeed = 25d;
        public const double NoteSpeedStep = 1d;
        public const double NoteSpeedFineStep = 0.1d;
        public const double MinimumNoteTimingValue = -50d;
        public const double MaximumNoteTimingValue = 50d;
        public const double NoteTimingValueStep = 0.5d;
        public const double NoteTimingValueFineStep = 0.1d;
        public const double MinimumNoteOffsetValue = -2d;
        public const double MaximumNoteOffsetValue = 2d;
        public const double NoteOffsetValueStep = 0.5d;
        public const double NoteOffsetValueFineStep = 0.1d;
        public const int MinimumLaneWidth = 80;
        public const int MaximumLaneWidth = 120;
        public const int LaneWidthStep = 5;
        public const int MinimumLaneAlpha = 0;
        public const int MaximumLaneAlpha = 100;
        public const int LaneAlphaStep = 10;
        public const int MinimumNoteHeight = 1;
        public const int MaximumNoteHeight = 10;
        public const int NoteHeightStep = 1;
        public const int MinimumSplitEffectLineOpacity = 10;
        public const int MaximumSplitEffectLineOpacity = 100;
        public const int SplitEffectLineOpacityStep = 10;
        public const int MinimumNoteStartOffset = 0;
        public const int MaximumNoteStartOffset = 100;
        public const int NoteStartOffsetStep = 5;
        public const int MinimumTimingEffectOffset = 0;
        public const int MaximumTimingEffectOffset = 100;
        public const int TimingEffectOffsetStep = 5;
        public const int MinimumGameSeId = 1;
        public const int MaximumGameSeId = 2;
        public const int GameSeIdStep = 1;
        public const int MinimumBombType = 1;
        public const int MaximumBombType = 3;
        public const int BombTypeStep = 1;
        public const int MinimumVolume = 0;
        public const int MaximumVolume = 100;
        public const int MinimumTextSpeed = 5;
        public const int MaximumTextSpeed = 20;
        public const int TextSpeedStep = 1;

        private const float MinimumLaneBorderAlpha = 14f;
        private const float LaneBorderAlphaRange = 37f;
        private const float ByteMaximum = 255f;

        [Serializable]
        public sealed class System
        {
            public int TextDisplaySpeed;
            public int TextSpeed;
            public int QualitySetting;
            public bool IsPurchaseAlert;
            public bool Is60Fps;
            public bool IsPreLiveOptionConfirmation;
            public bool IsDefaultTitleBackground;
            public bool IsFriendInviteOptionConfirmation;
            public bool IsCircleInviteOptionConfirmation;
            public bool IsStaminaMaxOptionConfirmation;
            public bool IsPracticeOptionConfirmation;
            public bool IsEventNoticeOptionConfirmation;
            public bool IsGachaNoticeOptionConfirmation;
            public bool IsNightTimeNoticeOptionConfirmation;
            public bool IsChatNoticeOptionConfirmation;
            public bool IsMessageNoticeOptionConfirmation;
            public bool IsCircleStaminaMaxOptionConfirmation;
            public bool IsLeagueNoticeOptionConfirmation;

            public static System Default()
            {
                return new System
                {
                    TextDisplaySpeed = 10,
                    TextSpeed = 10,
                    QualitySetting = 2,
                    IsPurchaseAlert = false,
                    Is60Fps = false,
                    IsPreLiveOptionConfirmation = true,
                    IsDefaultTitleBackground = false,
                    IsFriendInviteOptionConfirmation = true,
                    IsCircleInviteOptionConfirmation = true,
                    IsStaminaMaxOptionConfirmation = true,
                    IsPracticeOptionConfirmation = true,
                    IsEventNoticeOptionConfirmation = true,
                    IsGachaNoticeOptionConfirmation = true,
                    IsNightTimeNoticeOptionConfirmation = false,
                    IsChatNoticeOptionConfirmation = false,
                    IsMessageNoticeOptionConfirmation = false,
                    IsCircleStaminaMaxOptionConfirmation = true,
                    IsLeagueNoticeOptionConfirmation = true,
                };
            }
        }

        [Serializable]
        public sealed class Basic
        {
            public double NoteSpeed;
            public double NoteTimingValue;
            public double NoteOffsetValue;
            public int LaneAlphaValue;
            public bool IsActiveSenseDisplay;
            public bool IsActiveSenseCutIn;
            public int SplitEffectLineOpacity;
            public int SpritEffectSettingType;
            public bool IsActiveVibration;
            public bool IsActiveMusicVideo;
            public bool IsRealTimeRenderingMusicVideo;
            public int GameEndVoiceSettingType;

            public static Basic Default()
            {
                return new Basic
                {
                    NoteSpeed = 5d,
                    NoteTimingValue = 0d,
                    NoteOffsetValue = 0d,
                    LaneAlphaValue = 80,
                    IsActiveSenseDisplay = true,
                    IsActiveSenseCutIn = true,
                    SplitEffectLineOpacity = 100,
                    SpritEffectSettingType = 1,
                    IsActiveVibration = true,
                    IsActiveMusicVideo = true,
                    IsRealTimeRenderingMusicVideo = false,
                    GameEndVoiceSettingType = 0,
                };
            }
        }

        [Serializable]
        public sealed class Detail
        {
            public int LaneWidth;
            public bool IsActiveConcurrentLine;
            public bool IsActiveKeyBeam;
            public int TapEffectType;
            public int TimingAssistSettingType;
            public int AchievementRateSettingType;
            public int NoteStartOffset;
            public int TimingEffectOffset;
            public int TimingEffectScaleType;
            public int NoteHeight;
            public bool IsActiveComboEffect;
            public bool IsPerfectContinuous;
            public bool IsLowFrameRate;
            public int ChallengeType;
            public bool IsActiveMirror;
            public bool IsActiveSplitRandom;
            public bool IsActiveLaneAssistLine;
            public bool ShouldShowPerfectStar;

            public static Detail Default()
            {
                return new Detail
                {
                    LaneWidth = 100,
                    IsActiveConcurrentLine = true,
                    IsActiveKeyBeam = true,
                    TapEffectType = 0,
                    TimingAssistSettingType = 0,
                    AchievementRateSettingType = 0,
                    NoteStartOffset = 0,
                    TimingEffectOffset = 60,
                    TimingEffectScaleType = 1,
                    NoteHeight = 8,
                    IsActiveComboEffect = true,
                    IsPerfectContinuous = true,
                    IsLowFrameRate = true,
                    ChallengeType = 0,
                    IsActiveMirror = false,
                    IsActiveSplitRandom = false,
                    IsActiveLaneAssistLine = true,
                    ShouldShowPerfectStar = true,
                };
            }
        }

        [Serializable]
        public sealed class Custom
        {
            public int NormalSeId;
            public int CriticalSeId;
            public int BombType;

            public static Custom Default()
            {
                return new Custom
                {
                    NormalSeId = 1,
                    CriticalSeId = 1,
                    BombType = 1,
                };
            }
        }

        [Serializable]
        public sealed class SoundVolume
        {
            public int SystemMaster;
            public int SystemBGM;
            public int SystemSE;
            public int SystemVoice;
            public int SystemStampVoice;
            public int GameMaster;
            public int GameBGM;
            public int GameSE;
            public int GameNotesTap;
            public int GameVoice;
            public int StoryMaster;
            public int StoryBGM;
            public int StorySE;
            public int StoryVoice;

            public static SoundVolume Default()
            {
                return new SoundVolume
                {
                    SystemMaster = 100,
                    SystemBGM = 100,
                    SystemSE = 100,
                    SystemVoice = 100,
                    SystemStampVoice = 100,
                    GameMaster = 100,
                    GameBGM = 100,
                    GameSE = 100,
                    GameNotesTap = 100,
                    GameVoice = 100,
                    StoryMaster = 100,
                    StoryBGM = 100,
                    StorySE = 100,
                    StoryVoice = 100,
                };
            }
        }

        [Serializable]
        public sealed class Bluetooth
        {
            public double NoteTimingValue;
            public double NoteOffsetValue;
            public int GameSeVolume;
            public int GameNotesTapVolume;

            public static Bluetooth Default()
            {
                return new Bluetooth
                {
                    NoteTimingValue = 0d,
                    NoteOffsetValue = 0d,
                    GameSeVolume = 100,
                    GameNotesTapVolume = 100,
                };
            }
        }

        public static float CalculateLaneScale(int laneWidth)
        {
            return laneWidth / 100f;
        }

        public static float CalculateLaneDarknessAlpha(int laneAlphaValue)
        {
            return Math.Max(MinimumLaneAlpha,
                       Math.Min(MaximumLaneAlpha, laneAlphaValue)) /
                   100f;
        }

        public static float CalculateLaneBorderAlpha(int splitLineOpacity)
        {
            return (MinimumLaneBorderAlpha +
                    LaneBorderAlphaRange * splitLineOpacity / 100f) / ByteMaximum;
        }

        public static float CalculateCombinedVolume(int master, int category)
        {
            return Math.Max(MinimumVolume, Math.Min(MaximumVolume, master)) /
                   100f *
                   (Math.Max(MinimumVolume, Math.Min(MaximumVolume, category)) /
                    100f);
        }

        /// <summary>
        /// Sirius.UserDataHelper.GetNoteDisplayTime, recovered from ARM64.
        /// The coefficient switch covers 0..95 in five-point steps; as in the
        /// original method, other selector values return a zero coefficient.
        /// </summary>
        public static int CalculateNoteDisplayTime(
            int noteStartOffset,
            double noteSpeed)
        {
            double coefficient;
            switch (noteStartOffset)
            {
                case 0: coefficient = 4.44d; break;
                case 5: coefficient = 4.23d; break;
                case 10: coefficient = 4.00d; break;
                case 15: coefficient = 3.78d; break;
                case 20: coefficient = 3.56d; break;
                case 25: coefficient = 3.34d; break;
                case 30: coefficient = 3.11d; break;
                case 35: coefficient = 2.89d; break;
                case 40: coefficient = 2.67d; break;
                case 45: coefficient = 2.45d; break;
                case 50: coefficient = 2.22d; break;
                case 55: coefficient = 2.00d; break;
                case 60: coefficient = 1.78d; break;
                case 65: coefficient = 1.56d; break;
                case 70: coefficient = 1.33d; break;
                case 75: coefficient = 1.11d; break;
                case 80: coefficient = 0.89d; break;
                case 85: coefficient = 0.67d; break;
                case 90: coefficient = 0.44d; break;
                case 95: coefficient = 0.22d; break;
                default: coefficient = 0d; break;
            }
            if (noteSpeed <= 0d) return 0;
            return (int)Math.Round(
                coefficient * 5000d / (noteSpeed * 3d),
                MidpointRounding.ToEven);
        }

        public static int ConvertBooleanToToggleIndex(bool isOn)
        {
            return isOn ? 0 : 1;
        }

        public static bool ConvertToggleIndexToBoolean(int activeIndex)
        {
            return activeIndex == 0;
        }

        public static int ConvertThreeChoiceToggleIndex(int activeIndex)
        {
            return Math.Abs((activeIndex - 2) % 3);
        }

        public static bool IsValidLaneWidth(int value)
        {
            return value >= MinimumLaneWidth &&
                   value <= MaximumLaneWidth &&
                   (value - MinimumLaneWidth) % LaneWidthStep == 0;
        }

        public static bool IsValidNoteHeight(int value)
        {
            return value >= MinimumNoteHeight &&
                   value <= MaximumNoteHeight &&
                   (value - MinimumNoteHeight) % NoteHeightStep == 0;
        }

        public static bool IsValidNoteSpeed(double value)
        {
            return value >= MinimumNoteSpeed &&
                   value <= MaximumNoteSpeed &&
                   Math.Abs(
                       (value - MinimumNoteSpeed) / NoteSpeedFineStep -
                       Math.Round((value - MinimumNoteSpeed) / NoteSpeedFineStep)
                   ) < 0.000001d;
        }
    }
}
