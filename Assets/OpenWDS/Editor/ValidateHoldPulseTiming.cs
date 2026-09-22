using TouchPhase = OpenWDS.Runtime.TouchPhase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenWDS.Runtime;
using UnityEngine;

namespace OpenWDS.Editor
{
    public static class ValidateHoldPulseTiming
    {
        private static void ValidateHeldTailsWithDelayedEvents()
        {
            int cases = 0;
            foreach (var chart in Directory.GetFiles(Path.Combine(SongResourceStore.Root,
                         "OpenWDS/StandardCharts/1/1"), "*.csv"))
            {
                if (Path.GetFileName(chart) == "music_config.csv") continue;
                var notes = StandardNotation.Parse(File.ReadAllText(chart));
                foreach (var hold in notes.Where(n => n.NoteType == 100 || n.NoteType == 101).Take(4))
                foreach (var lag in new[] { 1, 8, 33, 100 })
                foreach (var phase in new[] { TouchPhase.Stationary, TouchPhase.Moved })
                using (var runtime = new InputHandlerRuntime(new[] { hold }, 0f,
                           n => new Vector2(n.Lane, 0), p => new HitLaneEntity((int)p.x, 0, 0, 0, 0)))
                {
                    var position = new Vector2(hold.Lane, 0);
                    var before = hold.EndMilliseconds - 1;
                    runtime.TickPlayer(before, before, new[] { new InputEntity(1,
                        before - lag, position, position, Vector2.zero, phase) });
                    if (runtime.ResolvedNoteCount != 0) throw new InvalidOperationException("Held tail completed early");
                    var due = hold.EndMilliseconds + 1;
                    runtime.TickPlayer(due, due, new[] { new InputEntity(1,
                        due - lag, position, position, Vector2.zero, phase) });
                    if (runtime.InputResults.Count != 1 || runtime.InputResults[0].TimingType != TimingType.PerfectStar ||
                        runtime.RemainingHoldCount != 0 || runtime.PublishedMissCount != 0)
                        throw new InvalidOperationException($"Music 1 held tail: {chart} note={hold.Id} phase={phase} lag={lag}");
                    runtime.TickPlayer(due + 33, due + 33, new[] { new InputEntity(1,
                        due, position, position, Vector2.zero, phase) });
                    if (runtime.InputResults.Count != 0) throw new InvalidOperationException("Held tail counted twice");
                    cases++;
                }
            }
            if (cases == 0) throw new InvalidOperationException("No Music 1 held-tail cases");
            Debug.Log($"OPENWDS_MANUAL_HOLD_TAILS cases={cases} frameClock=true passed=true");
        }

        public static void Run()
        {
            ValidateSharedHoldRelease();
            ValidateHeldTailsWithDelayedEvents();
            var path = Path.Combine(SongResourceStore.Root,
                "OpenWDS/StandardCharts/61/1/4.csv");
            var notes = StandardNotation.Parse(File.ReadAllText(path));
            var byId = notes.ToDictionary(n => n.Id);
            var pulses = new Dictionary<int, long>();
            var autoMisses = new List<int>();
            using (var runtime = new InputHandlerRuntime(notes, 0f,
                       n => new Vector2(n.Lane, 0f),
                       p => new HitLaneEntity((int)p.x, 0, 0, 0, 0)))
            {
                for (long t = 0; t < 115000; t += 8)
                {
                    runtime.Tick(t, t);
                    foreach (var result in runtime.InputResults)
                    {
                        if (result.NoteType != NoteType.HoldEighth) continue;
                        var delta = t - byId[result.NoteId].StartMilliseconds;
                        if (delta < 0 || delta > 8 || pulses.ContainsKey(result.NoteId))
                            throw new InvalidOperationException(
                                $"Music 61 pulse {result.NoteId}: {result.TimingType}, delay={delta}ms");
                        pulses.Add(result.NoteId, delta);
                        if (result.TimingType == TimingType.Miss)
                            autoMisses.Add(result.NoteId);
                    }
                }
            }
            if (pulses.Count != notes.Count(n => n.NoteType == 900))
                throw new InvalidOperationException("Music 61 pulses were not all judged.");

            if (autoMisses.Count != 0)
                throw new InvalidOperationException("AutoTouch missed authored pulses: " + string.Join(",", autoMisses));

            var combo = new GameResultRuntime(notes.Where(n => n.NoteType == 900).ToArray());
            var expectedCombo = 0;
            // All 72 actual chart pulses, with a contact on their authored lane:
            // judge in their first due frame, count once, never count at tail.
            foreach (var pulse in notes.Where(n => n.NoteType == 900))
            using (var runtime = new InputHandlerRuntime(new[] { pulse }, 0f,
                       n => new Vector2(n.Lane, 0f),
                       p => new HitLaneEntity((int)p.x, 0, 0, 0, 0)))
            {
                var position = new Vector2(pulse.Lane, 0);
                var touch = new InputEntity(1, pulse.StartMilliseconds - 100,
                    position, position, Vector2.zero, TouchPhase.Stationary);
                runtime.TickPlayer(pulse.StartMilliseconds, pulse.StartMilliseconds, new[] { touch });
                if (runtime.InputResults.Count != 1 ||
                    runtime.InputResults[0].TimingType != TimingType.PerfectStar)
                    throw new InvalidOperationException("Held pulse did not judge on time: " + pulse.Id);
                combo.Collect(runtime.InputResults[0]);
                if (combo.Count != ++expectedCombo)
                    throw new InvalidOperationException("Pulse combo did not increment immediately.");
                runtime.TickPlayer(pulse.StartMilliseconds + 1000,
                    pulse.StartMilliseconds + 1000, new[] { touch });
                if (runtime.InputResults.Count != 0)
                    throw new InvalidOperationException("Pulse counted twice: " + pulse.Id);
            }

            // Missing contact, lifted/canceled contact and off-lane contact
            // must expire now; touching the lane later must not restore combo.
            foreach (var phase in new[] { TouchPhase.Ended, TouchPhase.Canceled, TouchPhase.Stationary })
            {
                var pulse = notes.First(n => n.NoteType == 900);
                using (var runtime = new InputHandlerRuntime(new[] { pulse }, 0f,
                           n => new Vector2(n.Lane, 0),
                           p => new HitLaneEntity((int)p.x, 0, 0, 0, 0)))
                {
                    var lane = phase == TouchPhase.Stationary ? 12 : pulse.Lane;
                    var position = new Vector2(lane, 0);
                    var touch = new InputEntity(1, pulse.StartMilliseconds, position, position, Vector2.zero, phase);
                    runtime.TickPlayer(pulse.StartMilliseconds, pulse.StartMilliseconds, new[] { touch });
                    if (runtime.InputResults.Count != 1 || runtime.InputResults[0].TimingType != TimingType.Miss)
                        throw new InvalidOperationException("Unheld pulse was not missed on time: " + phase);
                    position = new Vector2(pulse.Lane, 0);
                    touch = new InputEntity(2, pulse.StartMilliseconds + 1000, position, position, Vector2.zero, TouchPhase.Began);
                    runtime.TickPlayer(pulse.StartMilliseconds + 1000, pulse.StartMilliseconds + 1000, new[] { touch });
                    if (runtime.InputResults.Count != 0) throw new InvalidOperationException("Late contact recovered an expired pulse.");
                }
            }

            foreach (var type in new[] { 100, 30 })
            {
                var note = notes.First(n => n.NoteType == type);
                var deadline = type == 100 ? note.EndMilliseconds : Math.Max(note.StartMilliseconds, note.EndMilliseconds);
                using (var runtime = new InputHandlerRuntime(new[] { note }, 0f,
                           n => new Vector2(n.Lane, 0),
                           p => new HitLaneEntity((int)p.x, 0, 0, 0, 0)))
                {
                    runtime.TickPlayer(deadline - 1, deadline - 1, Array.Empty<InputEntity>());
                    if (runtime.InputResults.Count != 0) throw new InvalidOperationException("Hold/Sound expired before its deadline.");
                    runtime.TickPlayer(deadline, deadline, Array.Empty<InputEntity>());
                    if (runtime.InputResults.Count != 1 || runtime.InputResults[0].TimingType != TimingType.Miss)
                        throw new InvalidOperationException("Hold/Sound missed after its deadline: " + type);
                }
            }

            // A sustained contact remains valid when its hardware event timestamp
            // precedes the current frame. Retail TryHoldingCore reads IClock slot 1.
            var slice = notes.Where(n => n.Lane == 6 &&
                n.StartMilliseconds >= 17872 && n.StartMilliseconds <= 19149).ToArray();
            var manager = new StandardHoldNoteManager(slice);
            var action = new UnifiedHoldAction(manager,
                new ScratchNoteManager(slice));
            var hit = new HitLaneEntity(6, 0, 0, 0, 0);
            foreach (var pulse in slice.Where(n => n.NoteType == 900))
            {
                var input = new InputEntity(1, 17872,
                    new Vector2(6, 0), new Vector2(6, 0), Vector2.zero,
                    TouchPhase.Stationary);
                action.TryHold(input, hit, 17872, pulse.StartMilliseconds);
                if (!action.HoldingResults.Any(n => n.Id == pulse.Id))
                    throw new InvalidOperationException(
                        $"Sustained contact delayed Music 61 pulse {pulse.Id} until tail.");
            }
            Debug.Log($"OPENWDS_HOLD_PULSES_VALIDATED music=61 difficulty=Stella pulses={pulses.Count} maxDelayMs={pulses.Values.Max()} staleEventClock=true autoMisses={string.Join(",", autoMisses)}");
        }

        public static void ValidateSharedHoldRelease()
        {
            // OnHoldEnd eligibility uses frame time; the result still uses
            // the hardware event time. Removing one of two fingers early must
            // not score the body while the second finger continues holding it.
            foreach (var otherTouch in new[] { false, true })
            foreach (var frame in new long[] { 1900, 2000 })
            {
                var note = new NotationNote { Id = 1, NoteType = 100, Lane = 6,
                    Width = 1, StartTickCount = 1f, EndTickCount = 2f };
                var action = new UnifiedHoldAction(new StandardHoldNoteManager(new[] { note }),
                    new ScratchNoteManager(new[] { note }));
                var position = new Vector2(6, 0);
                var input = new InputEntity(1, 1900, position, position, Vector2.zero, TouchPhase.Ended);
                var result = action.TryHold(input, new HitLaneEntity(6, 0, 0, 0, 0),
                    1900, frame, note, otherTouch);
                var shouldConsume = !otherTouch || frame >= 2000;
                if (result.Consumed != shouldConsume ||
                    (shouldConsume && result.Timing.TimingType != TimingType.Great) ||
                    (!shouldConsume && !result.ReleaseCurrent))
                    throw new InvalidOperationException($"Shared Hold release: other={otherTouch} frame={frame} consumed={result.Consumed} timing={result.Timing.TimingType}");
            }
            Debug.Log("OPENWDS_SHARED_HOLD_RELEASE_VALIDATED cases=4 frameClock=true eventTiming=true");
        }
    }
}
