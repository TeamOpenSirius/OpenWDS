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
        public static void Run()
        {
            var path = Path.Combine(Application.streamingAssetsPath,
                "OpenWDS/StandardCharts/61/1/4.csv");
            var notes = RecoveredStandardNotation.Parse(File.ReadAllText(path));
            var byId = notes.ToDictionary(n => n.Id);
            var pulses = new Dictionary<int, long>();
            var autoMisses = new List<int>();
            using (var runtime = new RecoveredInputHandlerRuntime(notes, 0f,
                       n => new Vector2(n.Lane, 0f),
                       p => new RecoveredHitLaneEntity((int)p.x, 0, 0, 0, 0)))
            {
                for (long t = 0; t < 115000; t += 8)
                {
                    runtime.Tick(t, t);
                    foreach (var result in runtime.InputResults)
                    {
                        if (result.NoteType != RecoveredNoteType.HoldEighth) continue;
                        var delta = t - byId[result.NoteId].StartMilliseconds;
                        if (delta < 0 || delta > 8 || pulses.ContainsKey(result.NoteId))
                            throw new InvalidOperationException(
                                $"Music 61 pulse {result.NoteId}: {result.TimingType}, delay={delta}ms");
                        pulses.Add(result.NoteId, delta);
                        if (result.TimingType == RecoveredTimingType.Miss)
                            autoMisses.Add(result.NoteId);
                    }
                }
            }
            if (pulses.Count != notes.Count(n => n.NoteType == 900))
                throw new InvalidOperationException("Music 61 pulses were not all judged.");

            if (autoMisses.Count != 0)
                throw new InvalidOperationException("AutoTouch missed authored pulses: " + string.Join(",", autoMisses));

            var combo = new RecoveredGameResultRuntime(notes.Where(n => n.NoteType == 900).ToArray());
            var expectedCombo = 0;
            // All 72 actual chart pulses, with a contact on their authored lane:
            // judge in their first due frame, count once, never count at tail.
            foreach (var pulse in notes.Where(n => n.NoteType == 900))
            using (var runtime = new RecoveredInputHandlerRuntime(new[] { pulse }, 0f,
                       n => new Vector2(n.Lane, 0f),
                       p => new RecoveredHitLaneEntity((int)p.x, 0, 0, 0, 0)))
            {
                var position = new Vector2(pulse.Lane, 0);
                var touch = new RecoveredInputEntity(1, pulse.StartMilliseconds - 100,
                    position, position, Vector2.zero, RecoveredTouchPhase.Stationary);
                runtime.TickPlayer(pulse.StartMilliseconds, pulse.StartMilliseconds, new[] { touch });
                if (runtime.InputResults.Count != 1 ||
                    runtime.InputResults[0].TimingType != RecoveredTimingType.PerfectStar)
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
            foreach (var phase in new[] { RecoveredTouchPhase.Ended, RecoveredTouchPhase.Canceled, RecoveredTouchPhase.Stationary })
            {
                var pulse = notes.First(n => n.NoteType == 900);
                using (var runtime = new RecoveredInputHandlerRuntime(new[] { pulse }, 0f,
                           n => new Vector2(n.Lane, 0),
                           p => new RecoveredHitLaneEntity((int)p.x, 0, 0, 0, 0)))
                {
                    var lane = phase == RecoveredTouchPhase.Stationary ? 12 : pulse.Lane;
                    var position = new Vector2(lane, 0);
                    var touch = new RecoveredInputEntity(1, pulse.StartMilliseconds, position, position, Vector2.zero, phase);
                    runtime.TickPlayer(pulse.StartMilliseconds, pulse.StartMilliseconds, new[] { touch });
                    if (runtime.InputResults.Count != 1 || runtime.InputResults[0].TimingType != RecoveredTimingType.Miss)
                        throw new InvalidOperationException("Unheld pulse was not missed on time: " + phase);
                    position = new Vector2(pulse.Lane, 0);
                    touch = new RecoveredInputEntity(2, pulse.StartMilliseconds + 1000, position, position, Vector2.zero, RecoveredTouchPhase.Began);
                    runtime.TickPlayer(pulse.StartMilliseconds + 1000, pulse.StartMilliseconds + 1000, new[] { touch });
                    if (runtime.InputResults.Count != 0) throw new InvalidOperationException("Late contact recovered an expired pulse.");
                }
            }

            foreach (var type in new[] { 100, 30 })
            {
                var note = notes.First(n => n.NoteType == type);
                var deadline = type == 100 ? note.EndMilliseconds : Math.Max(note.StartMilliseconds, note.EndMilliseconds);
                using (var runtime = new RecoveredInputHandlerRuntime(new[] { note }, 0f,
                           n => new Vector2(n.Lane, 0),
                           p => new RecoveredHitLaneEntity((int)p.x, 0, 0, 0, 0)))
                {
                    runtime.TickPlayer(deadline - 1, deadline - 1, Array.Empty<RecoveredInputEntity>());
                    if (runtime.InputResults.Count != 0) throw new InvalidOperationException("Hold/Sound expired before its deadline.");
                    runtime.TickPlayer(deadline, deadline, Array.Empty<RecoveredInputEntity>());
                    if (runtime.InputResults.Count != 1 || runtime.InputResults[0].TimingType != RecoveredTimingType.Miss)
                        throw new InvalidOperationException("Hold/Sound missed after its deadline: " + type);
                }
            }

            // A sustained contact remains valid when its hardware event timestamp
            // precedes the current frame. Retail TryHoldingCore reads IClock slot 1.
            var slice = notes.Where(n => n.Lane == 6 &&
                n.StartMilliseconds >= 17872 && n.StartMilliseconds <= 19149).ToArray();
            var manager = new RecoveredStandardHoldNoteManager(slice);
            var action = new RecoveredUnifiedHoldAction(manager,
                new RecoveredScratchNoteManager(slice));
            var hit = new RecoveredHitLaneEntity(6, 0, 0, 0, 0);
            foreach (var pulse in slice.Where(n => n.NoteType == 900))
            {
                var input = new RecoveredInputEntity(1, 17872,
                    new Vector2(6, 0), new Vector2(6, 0), Vector2.zero,
                    RecoveredTouchPhase.Stationary);
                action.TryHold(input, hit, 17872, pulse.StartMilliseconds);
                if (!action.HoldingResults.Any(n => n.Id == pulse.Id))
                    throw new InvalidOperationException(
                        $"Sustained contact delayed Music 61 pulse {pulse.Id} until tail.");
            }
            Debug.Log($"OPENWDS_HOLD_PULSES_VALIDATED music=61 difficulty=Stella pulses={pulses.Count} maxDelayMs={pulses.Values.Max()} staleEventClock=true autoMisses={string.Join(",", autoMisses)}");
        }
    }
}
