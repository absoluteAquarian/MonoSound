using System;

namespace MonoSound.Default {
	/// <summary>
	/// An interface for defining a segment of audio data that can be looped
	/// </summary>
	public interface IAudioSegment {
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
	public readonly struct StartSegment : IAudioSegment {
		/// <inheritdoc cref="IAudioSegment.Start"/>
		public TimeSpan Start => TimeSpan.Zero;

		/// <inheritdoc cref="IAudioSegment.End"/>
		public TimeSpan End { get; }

		/// <summary>
		/// Creates a non-looping audio segment that begins at the start of the audio data
		/// </summary>
		/// <param name="end">Where the segment ends</param>
		/// <exception cref="ArgumentException"/>
		public StartSegment(TimeSpan end) {
			if (end <= TimeSpan.Zero)
				throw new ArgumentException("End point must be positive");

			End = end;
		}

		/// <inheritdoc cref="IAudioSegment.Loop"/>
		public bool Loop(out TimeSpan loopTarget) {
			loopTarget = default;
			return false;
		}
	}

	/// <summary>
	/// A structure representing a looping audio segment that starts somewhere between the start and end of the audio data
	/// </summary>
	public readonly struct Segment : IAudioSegment {
		/// <inheritdoc cref="IAudioSegment.Start"/>
		public TimeSpan Start { get; }

		/// <inheritdoc cref="IAudioSegment.End"/>
		public TimeSpan End { get; }

		/// <summary>
		/// Creates a looping audio segment that starts somewhere between the start and end of the audio data
		/// </summary>
		/// <param name="start">Where the segment starts</param>
		/// <param name="end">Where the segment ends</param>
		/// <exception cref="ArgumentException"/>
		public Segment(TimeSpan start, TimeSpan end) {
			if (start >= end)
				throw new ArgumentException("Starting point must be before ending point");

			Start = start;
			End = end;
		}

		/// <inheritdoc cref="IAudioSegment.Loop"/>
		public bool Loop(out TimeSpan loopTarget) {
			loopTarget = Start;
			return true;
		}
	}

	/// <summary>
	/// A structure representing a looping audio segment that ends at the end of the audio data
	/// </summary>
	public struct EndSegment : IAudioSegment {
		/// <inheritdoc cref="IAudioSegment.Start"/>
		public TimeSpan Start { get; }

		/// <inheritdoc cref="IAudioSegment.End"/>
		public TimeSpan End { get; internal set; }

		/// <summary>
		/// Whether the audio stream should jump to the start of audio data (<see langword="true"/>) or to <see cref="Start"/> (<see langword="false"/>).<br/>
		/// Defaults to <see langword="true"/>.
		/// </summary>
		public bool LoopToStartOfAudio { get; set; }

		/// <summary>
		/// Creates a looping audio segment that ends at the end of the audio data
		/// </summary>
		/// <param name="start">Where the segment starts</param>
		/// <exception cref="ArgumentException"/>
		public EndSegment(TimeSpan start) {
			if (start <= TimeSpan.Zero)
				throw new ArgumentException("End point must be positive");

			Start = start;
			End = default;  // Cannot determine until stream has been initialized
			LoopToStartOfAudio = true;
		}

		/// <inheritdoc cref="IAudioSegment.Loop"/>
		public readonly bool Loop(out TimeSpan loopTarget) {
			// Jump to the start of the audio file or segment
			loopTarget = LoopToStartOfAudio ? TimeSpan.Zero : Start;
			return true;
		}
	}
}
