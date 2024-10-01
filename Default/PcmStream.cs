using MonoSound.Streaming;
using System.IO;

namespace MonoSound.Default {
	/// <summary>
	/// An object representing audio streaming from a PCM (.pcm) file
	/// </summary>
	public class PcmStream : WavStream {
		private readonly PcmFormatSettings _settings;

		/// <summary>
		/// Creates a new <see cref="PcmStream"/> instance from the given file and settings
		/// </summary>
		/// <param name="file">The absolute or relative location of the file to read from</param>
		/// <param name="settings">Settings for how the channels and sampling are formatted</param>
		public PcmStream(string file, PcmFormatSettings settings) : base(file, AudioType.Custom) {
			_settings = settings;
		}

		/// <summary>
		/// Creates a new <see cref="PcmStream"/> instance from the given data stream and settings
		/// </summary>
		/// <param name="stream">The data stream to read from</param>
		/// <param name="settings">Settings for how the channels and sampling are formatted</param>
		public PcmStream(Stream stream, PcmFormatSettings settings) : base(stream, AudioType.Custom) {
			_settings = settings;
		}

		/// <inheritdoc cref="WavStream.Initialize"/>
		protected override void Initialize() {
			Channels = _settings.channels;
			SampleRate = _settings.sampleRate;
			BitsPerSample = _settings.bitsPerSample;
			TotalBytes = underlyingStream.Length;

			sampleReadStart = 0;

			InitSound();  // REQUIRED!  This initializes PlayingSound and Metrics
		}
	}
}
