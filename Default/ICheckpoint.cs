using System;

namespace MonoSound.Default {
	public interface ICheckpoint {
		/// <summary>
		/// The location of the start of the loop.  This is where the audio stream will jump to once <see cref="Start"/> has been reached.
		/// </summary>
		TimeSpan Start { get; }

		/// <summary>
		/// The location of the end of the loop.  The audio stream will jump to <see cref="End"/> once this has been reached.
		/// </summary>
		TimeSpan End { get; }

		/// <summary>
		/// Called when the audio stream has reached the end of this checkpoint and wants to try to loop
		/// </summary>
		/// <param name="loopTarget">The position to jump to should a loop happen</param>
		/// <returns>Whether to jump to <paramref name="loopTarget"/></returns>
		bool Loop(out TimeSpan loopTarget);
	}

	/// <summary>
	/// A structure representing a non-looping audio segment that begins at the start of the audio data
	/// </summary>
	public readonly struct StartCheckpoint : ICheckpoint {
		public TimeSpan Start => TimeSpan.Zero;

		public TimeSpan End { get; }

		public StartCheckpoint(TimeSpan end) {
			if (end <= TimeSpan.Zero)
				throw new ArgumentException("End point must be positive");

			End = end;
		}

		public bool Loop(out TimeSpan loopTarget) {
			loopTarget = default;
			return false;
		}
	}

	/// <summary>
	/// A structure representing a looping audio segment that starts somewhere between the start and end of the audio data
	/// </summary>
	public readonly struct Checkpoint : ICheckpoint {
		public TimeSpan Start { get; }

		public TimeSpan End { get; }

		public Checkpoint(TimeSpan start, TimeSpan end) {
			if (start >= end)
				throw new ArgumentException("Starting point must be before ending point");

			Start = start;
			End = end;
		}

		public bool Loop(out TimeSpan loopTarget) {
			loopTarget = Start;
			return true;
		}
	}

	/// <summary>
	/// A structure representing a looping audio segment that ends at the end of the audio data
	/// </summary>
	public struct EndCheckpoint : ICheckpoint {
		public TimeSpan Start { get; }

		public TimeSpan End { get; internal set; }

		/// <summary>
		/// Whether the audio stream should jump to the start of audio data (<see langword="true"/>) or to <see cref="Start"/> (<see langword="false"/>).<br/>
		/// Defaults to <see langword="true"/>.
		/// </summary>
		public bool LoopToStartOfAudio { get; set; }

		public EndCheckpoint(TimeSpan start) {
			if (start <= TimeSpan.Zero)
				throw new ArgumentException("End point must be positive");

			Start = start;
			End = default;  // Cannot determine until stream has been initialized
			LoopToStartOfAudio = true;
		}

		public bool Loop(out TimeSpan loopTarget) {
			// Jump to the start of the audio file
			loopTarget = LoopToStartOfAudio ? TimeSpan.Zero : Start;
			return true;
		}
	}
}
