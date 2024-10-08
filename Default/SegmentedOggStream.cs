﻿using MonoSound.Streaming;
using System;
using System.IO;

namespace MonoSound.Default {
	/// <summary>
	/// An extension of the OGG format used by <see cref="SegmentedOggFormat"/> to facilitate segmented looping
	/// </summary>
	public sealed class SegmentedOggStream : OggStream {
		/// <summary>
		/// A tracker for the current segment of the audio stream
		/// </summary>
		public class SegmentTracker {
			private readonly IAudioSegment[] _segments;

			private int _targetSegment;
			/// <summary>
			/// The index of the current segment of the audio stream
			/// </summary>
			public int TargetSegment {
				get => _targetSegment;
				set => _targetSegment = Math.Clamp(value, 0, _segments.Length - 1);
			}

			/// <summary>
			/// The number of segments in the audio stream
			/// </summary>
			public int Count => _segments.Length;

			/// <summary>
			/// Gets or sets the segment at the given index
			/// </summary>
			public IAudioSegment this[int index] {
				get => _segments[index];
				set => _segments[index] = value;
			}

			/// <summary>
			/// The current segment of the audio stream
			/// </summary>
			public IAudioSegment CurrentSegment => _segments[_targetSegment];

			/// <summary>
			/// Creates a new instance of <see cref="SegmentTracker"/> with the given segments
			/// </summary>
			/// <exception cref="ArgumentException"/>
			public SegmentTracker(IAudioSegment[] segments) {
				if (segments is null || segments.Length == 0)
					throw new ArgumentException("Invalid segment array provided", nameof(segments));

				_segments = segments;
			}

			/// <summary>
			/// Gets the times of the start and loop points of the current segment
			/// </summary>
			public void GetLoopBounds(out TimeSpan start, out TimeSpan loop) {
				var checkpoint = _segments[_targetSegment];

				start = checkpoint.Start;
				loop = checkpoint.End;
			}

			/// <summary>
			/// Gets the times of the start and loop points of the given segment
			/// </summary>
			/// <exception cref="ArgumentOutOfRangeException"/>
			public void GetLoopBounds(int section, out TimeSpan start, out TimeSpan loop) {
				if (section < 0 || section > _segments.Length)
					throw new ArgumentOutOfRangeException(nameof(section));

				var checkpoint = _segments[section];

				start = checkpoint.Start;
				loop = checkpoint.End;
			}
		}

		/// <summary>
		/// The tracker for the current segment of the audio stream
		/// </summary>
		public readonly SegmentTracker tracker;
		private int _delayedJumpTarget;

		/// <summary>
		/// An event that is called when the audio stream is about to jump to a delayed section
		/// </summary>
		public event Action<StreamPackage> OnDelayedSectionStart;

		/// <summary>
		/// Creates a new instance of <see cref="SegmentedOggStream"/> with the given file and audio segment checkpoints
		/// </summary>
		public SegmentedOggStream(string file, IAudioSegment[] checkpoints) : base(file) {
			tracker = new SegmentTracker(checkpoints);
			_delayedJumpTarget = -1;
			ModifyTracker();
		}

		/// <summary>
		/// Creates a new instance of <see cref="SegmentedOggStream"/> with the given stream and audio segment checkpoints
		/// </summary>
		public SegmentedOggStream(Stream stream, IAudioSegment[] checkpoints) : base(stream) {
			tracker = new SegmentTracker(checkpoints);
			_delayedJumpTarget = -1;
			ModifyTracker();
		}

		private void ModifyTracker() {
			for (int i = 0; i < tracker.Count; i++) {
				var checkpoint = tracker[i];

				if (checkpoint is EndSegment e) {
					e.End = MaxDuration;
					tracker[i] = e;
				}
			}
		}

		/// <summary>
		/// Jumps to the start of the first segment
		/// </summary>
		/// <param name="onCurrentCheckpointEnd">Whether to jump immediately (<see langword="false"/>) or when the current segment ends (<see langword="true"/>)</param>
		public void JumpToStart(bool onCurrentCheckpointEnd = false) => JumpTo(0, onCurrentCheckpointEnd);

		/// <summary>
		/// Jumps to the start of the next segment.  Does nothing if the current segment is the final segment.
		/// </summary>
		/// <param name="onCurrentCheckpointEnd">Whether to jump immediately (<see langword="false"/>) or when the current segment ends (<see langword="true"/>)</param>
		public void JumpToNextLoopSection(bool onCurrentCheckpointEnd = false) {
			if (tracker.TargetSegment == tracker.Count - 1)
				return;

			JumpTo(tracker.TargetSegment + 1, onCurrentCheckpointEnd);
		}

		/// <summary>
		/// Immediately jumps to the currently delayed segment, if one was set
		/// </summary>
		public void JumpToDelayedSection() {
			if (_delayedJumpTarget >= 0) {
				JumpTo(_delayedJumpTarget, false);
				_delayedJumpTarget = -1;
			}
		}

		/// <summary>
		/// Jumps to the start of a given segment.  If <paramref name="section"/> is the current section, nothing happens.
		/// </summary>
		/// <param name="section">The 0-indexed segment number</param>
		/// <param name="onCurrentCheckpointEnd">Whether to jump immediately (<see langword="false"/>) or when the current segment ends (<see langword="true"/>)</param>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public void JumpTo(int section, bool onCurrentCheckpointEnd = false) {
			if (section < 0 || section >= tracker.Count)
				throw new ArgumentOutOfRangeException(nameof(section));

			if (onCurrentCheckpointEnd) {
				_delayedJumpTarget = section;
				return;
			}

			if (section == tracker.TargetSegment)
				return;

			tracker.TargetSegment = section;

			tracker.GetLoopBounds(out TimeSpan start, out _);
			base.SetStreamPosition(start.TotalSeconds);
			loopTargetTime = start;
		}

		private bool _delayedForcedLoopCheck;

		/// <inheritdoc cref="StreamPackage.ModifyReadSeconds"/>
		protected override void ModifyReadSeconds(ref double seconds) {
			// If the next read would bleed across the loop boundary, cut it short
			tracker.GetLoopBounds(out TimeSpan start, out TimeSpan loop);

			// Ensure that the tracker is always in a valid segment
			if (ReadTime < start || ReadTime > loop)
				FindValidTrackerSegment(ReadTime);

			// First read from the delayed section will ALWAYS be exactly at the starting point
			if (ReadTime == start && OnDelayedSectionStart != null) {
				OnDelayedSectionStart(this);
				OnDelayedSectionStart = null;
			}

			if (ReadTime + TimeSpan.FromSeconds(seconds) > loop && tracker.CurrentSegment.Loop(out TimeSpan loopTarget)) {
				if (tracker.CurrentSegment is EndSegment end && end.LoopToStartOfAudio) {
					// Reset the tracker
					tracker.TargetSegment = 0;
				}

				// Clamp the seconds to the remaining portion of this loop
				seconds = (loop - ReadTime).TotalSeconds;

				loopTargetTime = loopTarget;

				_delayedForcedLoopCheck = true;
			}
		}

		/// <inheritdoc cref="StreamPackage.ReadSamples"/>
		public override void ReadSamples(double seconds, out byte[] samples, out int bytesRead, out bool checkLooping) {
			base.ReadSamples(seconds, out samples, out bytesRead, out checkLooping);

			if (_delayedForcedLoopCheck) {
				checkLooping = true;
				_delayedForcedLoopCheck = false;
			}
		}

		/// <inheritdoc cref="StreamPackage.SetStreamPosition"/>
		public override void SetStreamPosition(double seconds) {
			// Assume callee wanted to jump to the section that contains the timestamp
			FindValidTrackerSegment(TimeSpan.FromSeconds(seconds));

			base.SetStreamPosition(seconds);
		}

		private void FindValidTrackerSegment(TimeSpan position) {
			for (int i = 0; i < tracker.Count; i++) {
				tracker.GetLoopBounds(i, out TimeSpan start, out _);

				// Allow multiple assignment in the event that the position is between checkpoints
				if (start <= position)
					tracker.TargetSegment = i;
			}
		}

		/// <inheritdoc cref="StreamPackage.Reset"/>
		public override void Reset() {
			// If the audio stream was stopped, reset the loop info to the start of the audio
			if (PlayingSound.State == Microsoft.Xna.Framework.Audio.SoundState.Stopped) {
				loopTargetTime = TimeSpan.Zero;
				tracker.TargetSegment = 0;
				_delayedJumpTarget = -1;
			}

			JumpToDelayedSection();

			base.Reset();
		}

		/// <inheritdoc cref="StreamPackage.HandleLooping"/>
		protected override void HandleLooping() {
			// If the tracker is at any segment except the last one, force looping to occur
			if (tracker.TargetSegment < tracker.Count - 1) {
				bool old = IsLooping;
				IsLooping = true;
				base.HandleLooping();
				IsLooping = old;
			} else
				base.HandleLooping();
		}
	}
}
