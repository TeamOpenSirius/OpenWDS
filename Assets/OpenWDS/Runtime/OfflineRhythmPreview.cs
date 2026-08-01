using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWDS.Runtime
{
    public sealed class OfflineRhythmPreview : MonoBehaviour
    {
        [Serializable]
        private sealed class PreviewNote
        {
            public Transform transform;
            public int lane;
            public int laneSpan;
            public float hitTime;
        }

        private const float NoteWidthPerLane = 0.85f;
        private const float LaneBorderWidth = 0.075f;

        [SerializeField] private float _loopDuration = 8f;
        [SerializeField] private float _travelDuration = 2.5f;
        [Tooltip("Synthetic preview input only; replace with PositionPow3Rate from the original GameConfig.")]
        [SerializeField] private double _previewPositionPow3Rate = 0.192;
        [Tooltip("Synthetic preview input only; replace with PositionPow1Rate from the original GameConfig.")]
        [SerializeField] private double _previewPositionPow1Rate = 1.2;
        [Tooltip("Synthetic preview input only; the original runtime derives this from noteSpeed * 0.6.")]
        [SerializeField] private float _previewSpeedRate = 1f;
        [SerializeField] private float _despawnDelay = 0.35f;
        [SerializeField] private List<PreviewNote> _notes = new List<PreviewNote>();

        private float _startedAt;

        public int ConfiguredNoteCount => _notes.Count;
        public int ActiveNoteCount { get; private set; }
        public float LoopDuration => _loopDuration;

        public void Configure(IReadOnlyList<Transform> noteTransforms)
        {
            int[] lanes = { 1, 3, 5, 7, 9, 2, 6, 10 };
            int[] laneSpans = { 1, 2, 1, 1, 2, 1, 2, 1 };
            _notes.Clear();
            for (var index = 0; index < noteTransforms.Count; index++)
            {
                _notes.Add(new PreviewNote
                {
                    transform = noteTransforms[index],
                    lane = lanes[index % lanes.Length],
                    laneSpan = laneSpans[index % laneSpans.Length],
                    hitTime = 1.5f + index * 0.75f,
                });
            }
            Evaluate(0f);
        }

        public void Evaluate(float elapsedSeconds)
        {
            var loopTime = Mathf.Repeat(elapsedSeconds, _loopDuration);
            ActiveNoteCount = 0;
            foreach (var note in _notes)
            {
                if (note.transform == null)
                {
                    continue;
                }

                var timeUntilHit = Mathf.Repeat(note.hitTime - loopTime, _loopDuration);
                var visible = timeUntilHit <= _travelDuration ||
                              timeUntilHit >= _loopDuration - _despawnDelay;
                note.transform.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                ActiveNoteCount++;
                var laneSpan = Mathf.Max(1, note.laneSpan);
                var noteWidth = RecoveredNotePositionCalculator.GetNoteWidth(
                    laneSpan,
                    NoteWidthPerLane,
                    LaneBorderWidth
                );
                var positionY = timeUntilHit <= _travelDuration
                    ? RecoveredNotePositionCalculator.CalculatePositionY(
                        Mathf.RoundToInt(timeUntilHit * 1000f),
                        0,
                        _previewSpeedRate,
                        0d,
                        _previewPositionPow3Rate,
                        _previewPositionPow1Rate
                    )
                    : 0f;
                note.transform.localPosition = new Vector3(
                    RecoveredNotePositionCalculator.GetNotePositionX(
                        note.lane,
                        noteWidth,
                        NoteWidthPerLane,
                        LaneBorderWidth
                    ),
                    positionY,
                    0f
                );
            }
        }

        private void OnEnable()
        {
            _startedAt = Time.time;
        }

        private void Update()
        {
            Evaluate(Time.time - _startedAt);
        }
    }
}
