using System;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Pure recovery of Sirius.Game.GameClock and the time offsets calculated by
    /// Sirius.Game.MusicTime. Values are supplied by the caller so this class can be
    /// tested without an audio backend or Unity frame timing.
    /// </summary>
    public sealed class GameClock
    {
        private readonly float _offsetSeconds;
        private readonly long _offsetMilliseconds;
        private float _musicPlayerSeconds;
        private long _musicPlayerMilliseconds;
        private float _gameSeconds;
        private long _gameMilliseconds;

        public GameClock(
            float delayStartSeconds,
            double noteTimingValue)
        {
            // MusicTime ARM64: each setting step is 10 ms.
            _offsetSeconds = (float)(noteTimingValue * 10d / 1000d) - delayStartSeconds;
            _offsetMilliseconds = (long)(noteTimingValue * 10d) +
                                  (long)(delayStartSeconds * -1000f);
        }

        public float PassedTime => _musicPlayerSeconds + _offsetSeconds;
        public long PassedMilliseconds => _musicPlayerMilliseconds + _offsetMilliseconds;
        public float PassedGameTime => _gameSeconds;
        public long PassedGameMilliseconds => _gameMilliseconds;

        public void Sync(
            float musicPlayerSeconds,
            long musicPlayerMilliseconds,
            float realtimeSinceStartup,
            long realtimeSinceStartupMilliseconds)
        {
            _musicPlayerSeconds = musicPlayerSeconds;
            _musicPlayerMilliseconds = musicPlayerMilliseconds;
            _gameSeconds = realtimeSinceStartup;
            _gameMilliseconds = realtimeSinceStartupMilliseconds;
        }

        public long InputTimeToMusicMilliseconds(long inputMilliseconds)
        {
            return PassedMilliseconds + inputMilliseconds - PassedGameMilliseconds;
        }

        public long MusicTimeToInputMilliseconds(long musicMilliseconds)
        {
            return PassedGameMilliseconds + musicMilliseconds - PassedMilliseconds;
        }
    }
}
