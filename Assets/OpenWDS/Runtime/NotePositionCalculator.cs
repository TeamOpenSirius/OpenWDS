using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Pure formulas recovered from Sirius.Game.NotePositionCalculator ARM64 code.
    /// The scalar configuration values are extracted from sharedassets1.assets and
    /// mirrored by GameConfigValues.
    /// </summary>
    public static class NotePositionCalculator
    {
        public const float SpeedCorrectValue = 0.6f;

        public static float CalculateSpeedRate(double noteSpeed)
        {
            return (float)noteSpeed * SpeedCorrectValue;
        }

        public static float CalculateMoveSeconds(
            double noteSpeed,
            int noteVisibleTimeRate1,
            int noteVisibleTimeRate2,
            float maxNoteVisiblePositionY)
        {
            // NotePositionCalculator.CalculateMoveSeconds performs these operations
            // as Decimal arithmetic before converting the result back to Single.
            return (float)((decimal)noteVisibleTimeRate1 *
                           (decimal)maxNoteVisiblePositionY /
                           noteVisibleTimeRate2 /
                           (decimal)noteSpeed /
                           1000m);
        }

        public static float CalculatePositionY(
            long targetMilliseconds,
            long passedMilliseconds,
            float speedRate,
            double offsetValue,
            double positionPow3Rate,
            double positionPow1Rate)
        {
            var time = speedRate * (targetMilliseconds - passedMilliseconds) / 1000f;
            return (float)(
                offsetValue +
                positionPow3Rate * time * time * time +
                positionPow1Rate * time
            );
        }

        public static float GetLengthFromDiff(
            long startMilliseconds,
            long endMilliseconds,
            long passedMilliseconds,
            float speedRate,
            double offsetValue,
            double positionPow3Rate,
            double positionPow1Rate)
        {
            return CalculatePositionY(
                       endMilliseconds,
                       passedMilliseconds,
                       speedRate,
                       offsetValue,
                       positionPow3Rate,
                       positionPow1Rate) -
                   CalculatePositionY(
                       startMilliseconds,
                       passedMilliseconds,
                       speedRate,
                       offsetValue,
                       positionPow3Rate,
                       positionPow1Rate);
        }

        public static float GetNoteWidth(
            int laneCount,
            float noteWidthPerLane,
            float laneBorderWidth)
        {
            return laneCount * noteWidthPerLane + (laneCount - 1) * laneBorderWidth;
        }

        public static float GetNotePositionX(
            int laneNumber,
            float noteWidth,
            float noteWidthPerLane,
            float laneBorderWidth)
        {
            return noteWidth * 0.5f +
                   (laneNumber - 7) * (noteWidthPerLane + laneBorderWidth) +
                   laneBorderWidth * 0.5f;
        }
    }


    /// <summary>Values extracted from the original serialized GameConfig.</summary>
    public static class GameConfigValues
    {
        public const float MaxNoteMoveSeconds = 4.6f;
        public const float NoteWidthPerLane = 0.915f;
        public const float LaneBorderWidth = 0.01f;
        public const float NoteStartPositionY = 58f;
        public const float CameraFieldOfView = 50f;
        public static readonly Vector3 JudgeAreaOffset = new Vector3(0f, -3.9f, 0f);
        public const int NoteVisibleTimeRate1 = 5000;
        public const int NoteVisibleTimeRate2 = 3;
        public const float MaxNoteVisiblePositionY = 4.45f;
        public const double PositionPow3Rate = 0.2d;
        public const double PositionPow1Rate = 10d;
    }
}
