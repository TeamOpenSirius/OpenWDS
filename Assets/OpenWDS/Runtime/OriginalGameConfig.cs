namespace OpenWDS.Runtime
{
    /// <summary>
    /// Values read from the original serialized GameConfig in sharedassets1.assets.
    /// </summary>
    public static class OriginalGameConfig
    {
        public const float NoteStartPositionY = 58f;
        public const float LaneMaskSpriteHeight = 0.08f;
        public const float LaneMaskOffsetY = 1f;
        public const float ArrowIntervalBase = 0.35f;
        public const float ArrowIntervalLaneStep = 0.01f;
        public const float NormalArrowEdgeOffset = -0.145f;
        public const float JumpScratchArrowWidthRate = 0.7f;
        public const float ScratchDistance = 50f;
        public const float FlickDistance = 50f;
        public const long FlickExpiredMilliseconds = 80L;
        public const float NoteMarginWidth = 0.15f;
        public const float HoldNoteLineAdditionalWidth = 0.1f;
        public const float HoldNoteGrayOutOffsetSeconds = 0.125f;
        // HoldNoteObject constructor literal at 0x231BBD0 in libil2cpp.so.
        public const float HoldNoteGrayOutChannel = 0.7529412f;

        public static readonly float[] NoteVisiblePositionY =
        {
            58f, 53.5f, 48.9f, 44.7f, 40.7f, 36.9f, 33.2f,
            29.9f, 26.6f, 23.5f, 20.5f, 17.7f, 15f, 12.5f,
            9.9f, 7.5f, 5.1f, 2.9f, 0.5f, -1.7f, -4.2f,
        };

        // GameConfig.GetNoteHeight switch table, indexed by the 1..10 setting.
        public static readonly float[] NoteHeightRotationX =
        {
            6f, 3f, 0f, -3f, -6f, -9f, -12f, -15f, -18f, -21f,
        };

        public static float GetNoteHeightRotationX(int noteHeight)
        {
            return NoteHeightRotationX[UnityEngine.Mathf.Clamp(noteHeight, 1, 10) - 1];
        }

        public static float GetNoteVisiblePositionY(int noteStartOffset)
        {
            var index = UnityEngine.Mathf.Clamp(noteStartOffset / 5, 0, 20);
            return NoteVisiblePositionY[index];
        }

        public static float GetLaneMaskScaleY(int noteStartOffset)
        {
            return (NoteStartPositionY + LaneMaskOffsetY -
                    GetNoteVisiblePositionY(noteStartOffset)) /
                   LaneMaskSpriteHeight;
        }

        public static UnityEngine.Color HoldNoteGrayOutColor =>
            new UnityEngine.Color(
                HoldNoteGrayOutChannel,
                HoldNoteGrayOutChannel,
                HoldNoteGrayOutChannel,
                1f);
    }
}
