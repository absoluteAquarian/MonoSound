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

	// Stupid hack for backwards compatibility

	/// <inheritdoc cref="IAudioSegment"/>
	public interface ILoopableAudioSegment : IAudioSegment {
		/// <summary>
		/// Whether this segment should loop.<br/>
		/// If <see langword="true"/>, the audio stream will jump to the start of this audio segment once the end is reached; otherwise, the audio stream will continue reading past the end of this segment.<br/>
		/// </summary>
		bool Looping { get; }
	}

	/// <summary>
	/// A structure representing a loopable audio segment that begins at the start of the audio data
	/// </summary>
	public readonly struct StartSegment : ILoopableAudioSegment {
		/// <inheritdoc cref="IAudioSegment.Start"/>
		public TimeSpan Start => TimeSpan.Zero;

		/// <inheritdoc cref="IAudioSegment.End"/>
		public TimeSpan End { get; }

		/// <inheritdoc cref="ILoopableAudioSegment.Looping"/>
		public bool Looping { get; }

		/// <summary>
		/// Creates a non-looping audio segment that begins at the start of the audio data
		/// </summary>
		/// <param name="end">Where the segment ends</param>
		/// <exception cref="ArgumentException"/>
		public StartSegment(TimeSpan end) {
			if (end <= TimeSpan.Zero)
				throw new ArgumentException("End point must be positive");

			End = end;
			Looping = false;
		}

		/// <summary>
		/// Creates an optionally looping audio segment that begins at the start of the audio data
		/// </summary>
		/// <param name="end">Where the segment ends</param>
		/// <param name="looping">Whether the segment should loop</param>
		/// <exception cref="ArgumentException"/>
		public StartSegment(TimeSpan end, bool looping) : this(end) {
			Looping = looping;
		}

		/// <inheritdoc cref="IAudioSegment.Loop"/>
		public bool Loop(out TimeSpan loopTarget) {
			loopTarget = default;
			return Looping;
		}
	}

	/// <summary>
	/// A structure representing a loopable audio segment that starts somewhere between the start and end of the audio data
	/// </summary>
	public readonly struct Segment : ILoopableAudioSegment {
		/// <inheritdoc cref="IAudioSegment.Start"/>
		public TimeSpan Start { get; }

		/// <inheritdoc cref="IAudioSegment.End"/>
		public TimeSpan End { get; }

		/// <inheritdoc cref="ILoopableAudioSegment.Looping"/>
		public bool Looping => !_notLooping;
		private readonly bool _notLooping;  // Implemented so that "false" is treated as "true" in logic, for backwards compatibility

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
			_notLooping = false;
		}

		/// <summary>
		/// Creates an optionally looping audio segment that starts somewhere between the start and end of the audio data
		/// </summary>
		/// <param name="start">Where the segment starts</param>
		/// <param name="end">Where the segment ends</param>
		/// <param name="looping">Whether the segment should loop</param>
		/// <exception cref="ArgumentException"/>
		public Segment(TimeSpan start, TimeSpan end, bool looping) : this(start, end) {
			_notLooping = !looping;
		}

		/// <inheritdoc cref="IAudioSegment.Loop"/>
		public bool Loop(out TimeSpan loopTarget) {
			loopTarget = Start;
			return Looping;
		}
	}

	/// <summary>
	/// A structure representing a loopable audio segment that ends at the end of the audio data
	/// </summary>
	public struct EndSegment : ILoopableAudioSegment {
		/// <inheritdoc cref="IAudioSegment.Start"/>
		public readonly TimeSpan Start { get; }

		/// <inheritdoc cref="IAudioSegment.End"/>
		public TimeSpan End { get; internal set; }

		/// <inheritdoc cref="ILoopableAudioSegment.Looping"/>
		public readonly bool Looping => !_notLooping;
		private readonly bool _notLooping;  // Implemented so that "false" is treated as "true" in logic, for backwards compatibility

		/// <summary>
		/// Whether the audio stream should jump to the start of audio data (<see langword="true"/>) or to <see cref="Start"/> (<see langword="false"/>) if this audio segment loops.<br/>
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
			_notLooping = false;
			LoopToStartOfAudio = true;
		}

		/// <summary>
		/// Creates an optionally looping audio segment that ends at the end of the audio data
		/// </summary>
		/// <param name="start">Where the segment starts</param>
		/// <param name="looping">Whether the segment should loop</param>
		/// <exception cref="ArgumentException"/>
		public EndSegment(TimeSpan start, bool looping) : this(start) {
			_notLooping = !looping;
		}

		/// <inheritdoc cref="IAudioSegment.Loop"/>
		public readonly bool Loop(out TimeSpan loopTarget) {
			// Jump to the start of the audio file or segment
			loopTarget = LoopToStartOfAudio ? TimeSpan.Zero : Start;
			return Looping;
		}
	}
}
