using Microsoft.Xna.Framework.Audio;
using MonoSound.Streaming;
using System;
using System.IO;

namespace MonoSound.Default {
	/// <summary>
	/// An extension of the OGG format used by <see cref="SegmentedOggFormat"/> to facilitate segmented looping
	/// </summary>
	public sealed class SegmentedOggStream : OggStream {
		public class CheckpointTracker {
			public readonly SegmentedOggStream source;

			private readonly ICheckpoint[] _checkpoints;

			private int _targetCheckpoint;
			public int TargetCheckpoint {
				get => _targetCheckpoint;
				set => _targetCheckpoint = Math.Clamp(value, 0, _checkpoints.Length - 1);
			}

			public int Count => _checkpoints.Length;

			public ICheckpoint this[int index] {
				get => _checkpoints[index];
				set => _checkpoints[index] = value;
			}

			public ICheckpoint CurrentCheckpoint => _checkpoints[_targetCheckpoint];

			public CheckpointTracker(SegmentedOggStream source, ICheckpoint[] checkpoints) {
				if (checkpoints is null || checkpoints.Length == 0)
					throw new ArgumentException("Invalid checkpoint array provided", nameof(checkpoints));

				this.source = source;
				_checkpoints = checkpoints;
				_targetCheckpoint = -1;
			}

			public void GetLoopBounds(out TimeSpan start, out TimeSpan loop) {
				var checkpoint = _checkpoints[_targetCheckpoint];

				start = checkpoint.Start;
				loop = checkpoint.End;
			}

			public void GetLoopBounds(int section, out TimeSpan start, out TimeSpan loop) {
				if (section < 0 || section > _checkpoints.Length)
					throw new ArgumentOutOfRangeException(nameof(section));

				var checkpoint = _checkpoints[section];

				start = checkpoint.Start;
				loop = checkpoint.End;
			}
		}

		public readonly CheckpointTracker tracker;

		public SegmentedOggStream(string file, ICheckpoint[] checkpoints) : base(file) {
			tracker = new CheckpointTracker(this, checkpoints);
		}

		public SegmentedOggStream(Stream stream, ICheckpoint[] checkpoints) : base(stream) {
			tracker = new CheckpointTracker(this, checkpoints);
		}

		protected override void Initialize() {
			base.Initialize();

			for (int i = 0; i < tracker.Count; i++) {
				var checkpoint = tracker[i];

				if (checkpoint is EndCheckpoint e) {
					e.End = MaxDuration;
					tracker[i] = e;
				}
			}
		}

		public void JumpToStart() => JumpTo(0);

		public void JumpToNextLoopSection() => JumpTo(tracker.TargetCheckpoint + 1);

		public void JumpTo(int section) {
			if (section < 0 || section > tracker.Count)
				throw new ArgumentOutOfRangeException(nameof(section));

			int old = tracker.TargetCheckpoint;
			tracker.TargetCheckpoint = section;

			if (old != tracker.TargetCheckpoint) {
				tracker.GetLoopBounds(out TimeSpan start, out _);
				CurrentDuration = start;
				loopTargetTime = start;
			}
		}

		private bool _delayedForcedLoopCheck;

		protected override void ModifyReadSeconds(ref double seconds) {
			// If the next read would bleed across the loop boundary, cut it short
			tracker.GetLoopBounds(out _, out TimeSpan loop);

			if (CurrentDuration + TimeSpan.FromSeconds(seconds) > loop && tracker.CurrentCheckpoint.Loop(out TimeSpan loopTarget)) {
				// Clamp the seconds to the remaining portion of this loop
				seconds = (loop - CurrentDuration).TotalSeconds;

				loopTargetTime = loopTarget;

				_delayedForcedLoopCheck = true;
			}
		}

		public override void ReadSamples(double seconds, out byte[] samples, out int bytesRead, out bool checkLooping) {
			base.ReadSamples(seconds, out samples, out bytesRead, out checkLooping);

			if (_delayedForcedLoopCheck) {
				checkLooping = true;
				_delayedForcedLoopCheck = false;
			}
		}

		public override void SetStreamPosition(double seconds) {
			TimeSpan pos = TimeSpan.FromSeconds(seconds);

			// Assume callee wanted to jump to the section that contains the timestamp
			for (int i = 0; i <= tracker.Count; i++) {
				tracker.GetLoopBounds(i, out _, out TimeSpan loop);

				// Allow multiple assignment in the event that the position is between checkpoints
				if (pos <= loop)
					tracker.TargetCheckpoint = i;
			}

			base.SetStreamPosition(seconds);
		}

		public override void Reset(bool clearQueue) {
			// If the audio stream was stopped, reset the loop info to the start of the audio
			if (PlayingSound.State == SoundState.Stopped) {
				loopTargetTime = TimeSpan.Zero;
				tracker.TargetCheckpoint = 0;
			}

			base.Reset(clearQueue);
		}
	}
}
