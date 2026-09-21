using System;
using OpenWDS.Runtime;
using UnityEngine;
using TouchPhase = OpenWDS.Runtime.TouchPhase;

namespace OpenWDS.Editor
{
    // Native TryFlick -> TrySetBeganTime -> TryFlickCore passes the same
    // began timestamp to IsBeganRangeTarget, IsExpired and GetTimingType.
    public static class ValidateFlickTiming
    {
        public static void Run()
        {
            Check(99920, 99990, TimingType.Great, FlickCompletionReason.Moved);
            Check(100069, 100090, TimingType.PerfectStar, FlickCompletionReason.Moved);
            Check(99999, 100078, TimingType.PerfectStar, FlickCompletionReason.Moved);
            Check(99999, 100079, TimingType.Great, FlickCompletionReason.Expired);
            CheckDelayedBatch(79, TimingType.PerfectStar, FlickCompletionReason.Moved);
            CheckDelayedBatch(80, TimingType.Great, FlickCompletionReason.Expired);
            var note = Note();
            var input = new InputHandlerRuntime(new[] { note }, 3.019f,
                _ => Vector2.zero, _ => new HitLaneEntity(1, 0, 0, 0, 0));
            input.Tick(0, 99000);
            input.Tick(1079, 100079);
            if (input.InputResults.Count != 1 || input.InputResults[0].TimingType != TimingType.Great ||
                input.NonPerfectFlickDiagnostics.Count != 1 ||
                !input.NonPerfectFlickDiagnostics[0].Contains("reason=Expired;phase=Began;") ||
                !input.NonPerfectFlickDiagnostics[0].Contains("beganGame=99999;"))
                throw new InvalidOperationException("Delayed AutoTouch batch did not preserve the native expired branch and diagnostic.");
            Debug.Log("OPENWDS_FLICK_DELAYED_BATCH " + input.NonPerfectFlickDiagnostics[0]);
            Debug.Log("OPENWDS_FLICK_TIMING passed=True beganTime=True expiry79_80=True delayedBatch=True");
        }

        private static NotationNote Note() => new NotationNote
        {
            Id = 1, NoteType = (int)NoteType.Flick,
            StartTickCount = 1f, EndTickCount = 1f, Lane = 1, Width = 1
        };

        private static void Sync(GameClock clock, long gameTime)
        {
            // A deliberately distinct music epoch catches accidental use of
            // a raw game timestamp as music time. Includes the 3.019 s delay.
            var playerTime = gameTime - 99000 + 3019;
            clock.Sync(playerTime / 1000f, playerTime, gameTime / 1000f, gameTime);
        }

        private static InputEntity Input(long time, TouchPhase phase) => new InputEntity(
            1, time, Vector2.zero, Vector2.zero, new Vector2(50, 50), phase);

        private static void Check(long beganTime, long movedTime,
            TimingType timing, FlickCompletionReason reason)
        {
            var clock = new GameClock(3.019f, 0);
            var note = Note();
            var action = new FlickAction(clock, new FlickInputManager(), new FlickNoteManager(new[] { note }));
            Sync(clock, beganTime);
            var began = action.TryFlick(Input(beganTime, TouchPhase.Began), note);
            if (!began.Handled || began.Consumed) throw new InvalidOperationException("Flick began was not retained.");
            Sync(clock, movedTime);
            var moved = action.TryFlick(Input(movedTime, TouchPhase.Moved), note);
            Require(moved, timing, reason, beganTime);
        }

        private static void CheckDelayedBatch(long elapsedFromBegan,
            TimingType timing, FlickCompletionReason reason)
        {
            var clock = new GameClock(3.019f, 0);
            var note = Note();
            var action = new FlickAction(clock, new FlickInputManager(), new FlickNoteManager(new[] { note }));
            Sync(clock, 99999 + elapsedFromBegan);
            // Both scheduled inputs arrive in one delayed Unity frame.
            // The original expires on Began itself once its timestamp is old.
            var result = action.TryFlick(Input(99999, TouchPhase.Began), note);
            if (!result.Consumed) result = action.TryFlick(Input(100000, TouchPhase.Moved), note);
            Require(result, timing, reason, 99999);
        }

        private static void Require(FlickActionResult result, TimingType timing,
            FlickCompletionReason reason, long beganTime)
        {
            if (!result.Consumed || !result.DeletedNote || result.Timing.TimingType != timing ||
                result.CompletionReason != reason || result.BeganMilliseconds != beganTime)
                throw new InvalidOperationException($"Flick timing mismatch: {result.Timing.TimingType}/{result.CompletionReason}, expected {timing}/{reason}");
        }
    }
}
