namespace OpenWDS.Runtime
{
    public enum RecoveredTimingType
    {
        None = 0,
        Miss = 1,
        Bad = 2,
        Good = 3,
        Great = 4,
        Perfect = 5,
        PerfectStar = 6,
    }

    public enum RecoveredTimingAssistType
    {
        None = 0,
        Slow = 1,
        Fast = 2,
    }

    public readonly struct RecoveredTimingDecision
    {
        public readonly RecoveredTimingType TimingType;
        public readonly RecoveredTimingAssistType TimingAssistType;
        public readonly long DiffMilliseconds;

        public RecoveredTimingDecision(
            RecoveredTimingType timingType,
            RecoveredTimingAssistType timingAssistType,
            long diffMilliseconds)
        {
            TimingType = timingType;
            TimingAssistType = timingAssistType;
            DiffMilliseconds = diffMilliseconds;
        }
    }

    /// <summary>Recovered TapTimingDecider boundary and early/late behavior.</summary>
    public static class RecoveredTapTimingDecider
    {
        public const long BadMilliseconds = 125L;

        public static RecoveredTimingDecision Decide(
            RecoveredGameClock clock,
            long inputGameMilliseconds,
            long noteMusicMilliseconds)
        {
            return DecideMusicTime(
                clock.InputTimeToMusicMilliseconds(inputGameMilliseconds),
                noteMusicMilliseconds);
        }

        public static RecoveredTimingDecision DecideMusicTime(
            long inputMusicMilliseconds,
            long noteMusicMilliseconds)
        {
            var diff = inputMusicMilliseconds - noteMusicMilliseconds;
            var absolute = diff < 0 ? -diff : diff;
            var type = RecoveredTimingType.None;
            if (absolute <= 25) type = RecoveredTimingType.PerfectStar;
            else if (absolute <= 40) type = RecoveredTimingType.Perfect;
            else if (absolute <= 70) type = RecoveredTimingType.Great;
            else if (absolute <= 100) type = RecoveredTimingType.Good;
            else if (absolute <= BadMilliseconds) type = RecoveredTimingType.Bad;
            else if (diff > 0) type = RecoveredTimingType.Miss;

            if (type == RecoveredTimingType.None)
            {
                return new RecoveredTimingDecision(type, RecoveredTimingAssistType.None, 0);
            }
            var assist = diff < 0
                ? RecoveredTimingAssistType.Fast
                : diff > 0
                    ? RecoveredTimingAssistType.Slow
                    : RecoveredTimingAssistType.None;
            return new RecoveredTimingDecision(type, assist, diff);
        }
    }
}
