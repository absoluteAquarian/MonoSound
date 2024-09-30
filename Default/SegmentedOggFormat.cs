using Microsoft.Xna.Framework;
using MonoSound.Audio;
using MonoSound.Streaming;
using System;
using System.IO;

namespace MonoSound.Default {
	/// <summary>
	/// An extension of the OGG format that uses external data to control how an .ogg file is read.<br/>
	/// This format is purposefully not registered via <see cref="MonoSoundLibrary.RegisterFormat(CustomAudioFormat)"/> and is intended to be created separately.
	/// </summary>
	public sealed class SegmentedOggFormat : CustomAudioFormat {
		/// <summary>
		/// Ignored.  This format is not intended to be registered.
		/// </summary>
		public override string[] ValidExtensions => throw new NotImplementedException();  // Force registering the format to fail

		/// <inheritdoc cref="CustomAudioFormat.CreateStream(string, object)"/>
		public override StreamPackage CreateStream(string filePath, object state) {
			if (state is not IAudioSegment[] checkpoints)
				throw new ArgumentException($"Expected an array of {nameof(IAudioSegment)}", nameof(state));

			return new SegmentedOggStream(TitleContainer.OpenStream(filePath), checkpoints);
		}

		/// <inheritdoc cref="CustomAudioFormat.CreateStream(Stream, object)"/>
		public override StreamPackage CreateStream(Stream dataStream, object state) {
			if (state is not IAudioSegment[] checkpoints)
				throw new ArgumentException($"Expected an array of {nameof(IAudioSegment)}", nameof(state));

			try {
				return new SegmentedOggStream(dataStream, checkpoints);
			} catch {
				// Stream initialized failed for whatever reason, be it an invalid stream or corrupted data
				// Stream should not be recognized; return null
				return null;
			}
		}

		/// <summary>
		/// Ignored.  This format is not intended to be registered.
		/// </summary>
		public override FormatWav ReadWav(string filePath) => throw new NotImplementedException();

		/// <summary>
		/// Ignored.  This format is not intended to be registered.
		/// </summary>
		public override FormatWav ReadWav(Stream dataStream) => throw new NotImplementedException();
	}
}
