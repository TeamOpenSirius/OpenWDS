namespace OpenWDS.Runtime
{
    public enum TimingType
    {
        None = 0,
        Miss = 1,
        Bad = 2,
        Good = 3,
        Great = 4,
        Perfect = 5,
        PerfectStar = 6,
    }

    public enum TimingAssistType
    {
        None = 0,
        Slow = 1,
        Fast = 2,
    }

    public readonly struct TimingDecision
    {
        public readonly TimingType TimingType;
        public readonly TimingAssistType TimingAssistType;
        public readonly long DiffMilliseconds;

        public TimingDecision(
            TimingType timingType,
            TimingAssistType timingAssistType,
            long diffMilliseconds)
        {
            TimingType = timingType;
            TimingAssistType = timingAssistType;
            DiffMilliseconds = diffMilliseconds;
        }
    }

    /// <summary>Recovered TapTimingDecider boundary and early/late behavior.</summary>
    public static class TapTimingDecider
    {
        public const long BadMilliseconds = 125L;

        public static TimingDecision Decide(
            GameClock clock,
            long inputGameMilliseconds,
            long noteMusicMilliseconds)
        {
            return DecideMusicTime(
                clock.InputTimeToMusicMilliseconds(inputGameMilliseconds),
                noteMusicMilliseconds);
        }

        public static TimingDecision DecideMusicTime(
            long inputMusicMilliseconds,
            long noteMusicMilliseconds)
        {
            var diff = inputMusicMilliseconds - noteMusicMilliseconds;
            var absolute = diff < 0 ? -diff : diff;
            var type = TimingType.None;
            if (absolute <= 25) type = TimingType.PerfectStar;
            else if (absolute <= 40) type = TimingType.Perfect;
            else if (absolute <= 70) type = TimingType.Great;
            else if (absolute <= 100) type = TimingType.Good;
            else if (absolute <= BadMilliseconds) type = TimingType.Bad;
            else if (diff > 0) type = TimingType.Miss;

            if (type == TimingType.None)
            {
                return new TimingDecision(type, TimingAssistType.None, 0);
            }
            var assist = diff < 0
                ? TimingAssistType.Fast
                : diff > 0
                    ? TimingAssistType.Slow
                    : TimingAssistType.None;
            return new TimingDecision(type, assist, diff);
        }
    }
}
