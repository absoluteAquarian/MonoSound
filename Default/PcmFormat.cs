using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;
using MonoSound.Streaming;
using System;
using System.IO;

namespace MonoSound.Default {
	/// <summary>
	/// A format for reading PCM audio data
	/// </summary>
	public sealed class PcmFormat : CustomAudioFormat {
		/// <inheritdoc cref="CustomAudioFormat.ValidExtensions"/>
		public override string[] ValidExtensions => [ ".pcm" ];

		/// <inheritdoc cref="CustomAudioFormat.CreateStream(string, object)"/>
		public override StreamPackage CreateStream(string filePath, object state) {
			if (state is not PcmFormatSettings settings)
				throw new ArgumentException($"Expected a {nameof(PcmFormatSettings)} object", nameof(state));

			return new PcmStream(filePath, settings);
		}

		/// <inheritdoc cref="CustomAudioFormat.CreateStream(Stream, object)"/>
		public override StreamPackage CreateStream(Stream dataStream, object state) {
			if (state is not PcmFormatSettings settings)
				throw new ArgumentException($"Expected a {nameof(PcmFormatSettings)} object", nameof(state));

			return new PcmStream(dataStream, settings);
		}

		/// <summary>
		/// Ignored.  This format requires settings to read from a file.
		/// </summary>
		/// <exception cref="NotSupportedException"/>
		public override FormatWav ReadWav(string filePath) => throw new NotSupportedException("PcmFormat requires settings to read from a file");

		/// <inheritdoc cref="CustomAudioFormat.ReadWav(string, object)"/>
		public override FormatWav ReadWav(string filePath, object state) {
			if (state is not PcmFormatSettings settings)
				throw new ArgumentException($"Expected a {nameof(PcmFormatSettings)} object", nameof(state));

			using Stream readStream = TitleContainer.OpenStream(filePath);
			using MemoryStream stream = new MemoryStream();
			readStream.CopyTo(stream);
			byte[] samples = stream.ToArray();
			return FormatWav.FromSampleDataAndSettings(samples, settings.channels, settings.sampleRate, settings.bitsPerSample);
		}

		/// <summary>
		/// Ignored.  This format requires settings to read from a stream.
		/// </summary>
		/// <exception cref="NotSupportedException"/>
		public override FormatWav ReadWav(Stream dataStream) => throw new NotSupportedException("PcmFormat requires settings to read from a stream");

		/// <inheritdoc cref="CustomAudioFormat.ReadWav(Stream, object)"/>
		public override FormatWav ReadWav(Stream dataStream, object state) {
			if (state is not PcmFormatSettings settings)
				throw new ArgumentException($"Expected a {nameof(PcmFormatSettings)} object", nameof(state));

			using MemoryStream stream = new MemoryStream();
			dataStream.CopyTo(stream);
			byte[] samples = stream.ToArray();
			return FormatWav.FromSampleDataAndSettings(samples, settings.channels, settings.sampleRate, settings.bitsPerSample);
		}
	}

	/// <summary>
	/// Settings for initializing <see cref="PcmFormat"/> audio streams
	/// </summary>
	public readonly struct PcmFormatSettings {
		/// <summary/>
		public readonly AudioChannels channels;
		/// <summary/>
		public readonly int sampleRate;
		/// <summary/>
		public readonly short bitsPerSample;

		/// <summary>
		/// Creates a new <see cref="PcmFormatSettings"/> instance
		/// </summary>
		/// <param name="channels">Whether the audio data will be Mono or Stereo</param>
		/// <param name="sampleRate">How many samples PER CHANNEL will be read per second</param>
		/// <param name="bitsPerSample">The size in bits for one sample PER CHANNEL</param>
		/// <exception cref="ArgumentException"/>
		public PcmFormatSettings(AudioChannels channels, int sampleRate, short bitsPerSample) {
			if (channels != AudioChannels.Mono && channels != AudioChannels.Stereo)
				throw new ArgumentException("Audio data must be Mono or Stereo", nameof(channels));
			if (sampleRate <= 0)
				throw new ArgumentException("Sample rate must be greater than zero", nameof(sampleRate));
			if (bitsPerSample != 16 && bitsPerSample != 24)
				throw new ArgumentException("Sample bit depth must be 16-bit or 24-bit PCM", nameof(bitsPerSample));

			this.channels = channels;
			this.sampleRate = sampleRate;
			this.bitsPerSample = bitsPerSample;
		}
	}
}
