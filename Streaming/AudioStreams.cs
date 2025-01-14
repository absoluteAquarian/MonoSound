﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;
using MonoSound.XACT;
using System;
using System.IO;

namespace MonoSound.Streaming {
	/// <summary>
	/// An object representing audio streaming from a WAVE (.wav) data stream
	/// </summary>
	public class WavStream : StreamPackage {
		/// <summary>
		/// Initializes a new <see cref="WavStream"/> from a .wav file
		/// </summary>
		/// <param name="file">The absolute or relative location of the file to read from</param>
		public WavStream(string file) : base(TitleContainer.OpenStream(file), AudioType.WAV) { }

		/// <summary>
		/// Initializes a new <see cref="WavStream"/> from a data stream in the .wav format
		/// </summary>
		/// <param name="stream">The data stream to read from</param>
		public WavStream(Stream stream) : base(stream, AudioType.WAV) { }

		/// <summary>
		/// A copy of <see cref="WavStream(string)"/> for overwriting the type of audio stream
		/// </summary>
		/// <param name="file">The absolute or relative location of the file to read from</param>
		/// <param name="typeOverride">Which type of audio stream should be used</param>
		protected WavStream(string file, AudioType typeOverride) : base(TitleContainer.OpenStream(file), typeOverride) { }

		/// <summary>
		/// A copy of <see cref="WavStream(Stream)"/> for overwriting the type of audio stream
		/// </summary>
		/// <param name="stream">The data stream to read from</param>
		/// <param name="typeOverride">Which type of audio stream should be used</param>
		protected WavStream(Stream stream, AudioType typeOverride) : base(stream, typeOverride) { }

		/// <inheritdoc cref="StreamPackage.Initialize"/>
		protected override void Initialize() {
			// Read the header data
			using WAVEReader reader = new WAVEReader(underlyingStream);
			reader.ReadFormat(out AudioChannels channels, out int sampleRate, out _, out _, out short bitsPerSample);
			reader.ReadDataHeader(out int totalBytes);

			Channels = channels;
			SampleRate = sampleRate;
			BitsPerSample = bitsPerSample;
			TotalBytes = totalBytes;

			InitSound();  // REQUIRED!  This initializes PlayingSound and Metrics
		}

		/// <inheritdoc cref="StreamPackage.ReadSamples"/>
		public override void ReadSamples(double seconds, out byte[] samples, out int bytesRead, out bool checkLooping) {
			int samplesToRead = (int)(seconds * BitsPerSample / 8 * SampleRate * (short)Channels);

			if (samplesToRead == 0) {
				string suffix = TotalBytes == -1 ? "" : $" out of {TotalBytes / BitsPerSample * 8 / (int)Channels}";

				throw new InvalidOperationException("MonoSound internals error: Streamed audio requested an invalid amount of samples to read" +
					$"\nObject state: {SampleRate}Hz | {Channels} | {ReadBytes / BitsPerSample * 8 / (int)Channels}{suffix} samples read");
			}

			samples = new byte[samplesToRead];
			bytesRead = underlyingStream.Read(samples, 0, samplesToRead);

			checkLooping = bytesRead < samples.Length;
		}
	}

	/// <summary>
	/// An object representing audio streaming from a compiled XNB (.xnb) data stream
	/// </summary>
	public class XnbStream : WavStream {
		/// <summary>
		/// Initializes a new <see cref="XnbStream"/> from an .xnb file
		/// </summary>
		/// <param name="file">The absolute or relative location of the file to read from</param>
		public XnbStream(string file) : base(file, AudioType.XNB) { }

		/// <summary>
		/// Initializes a new <see cref="XnbStream"/> from a data stream in the .xnb format
		/// </summary>
		/// <param name="stream">The data stream to read from</param>
		public XnbStream(Stream stream) : base(stream, AudioType.XNB) { }

		/// <inheritdoc cref="StreamPackage.Initialize"/>
		protected override void Initialize() {
			byte[] read = new byte[4];
			underlyingStream.Read(read, 0, 4);
			byte[] header = new byte[BitConverter.ToInt32(read, 0)];
			underlyingStream.Read(header, 0, header.Length);

			Channels = (AudioChannels)BitConverter.ToInt16(header, 2);
			SampleRate = BitConverter.ToInt32(header, 4);
			BitsPerSample = BitConverter.ToInt16(header, 14);

			underlyingStream.Read(read, 0, 4);
			TotalBytes = BitConverter.ToInt32(read, 0);

			InitSound();  // REQUIRED!  This initializes PlayingSound and Metrics
		}
	}

	/// <summary>
	/// An object representing audio streaming from an MPEG-1 Audio Layer 3 (.mp3) data stream
	/// </summary>
	public class Mp3Stream : WavStream {
		// MP3Sharp doesn't have a defined way to get the audio duration without reading the entire file, which defeats the purpose of streaming
		/// <summary>
		/// Ignored.  Stream duration cannot be determined for .mp3 streams.
		/// </summary>
		public sealed override TimeSpan MaxDuration => throw new NotImplementedException("MP3Sharp cannot determine the audio duration without reading the entire file");

		/// <summary>
		/// Initializes a new <see cref="Mp3Stream"/> from an .mp3 file
		/// </summary>
		/// <param name="file">The absolute or relative location of the file to read from</param>
		public Mp3Stream(string file) : base(new MP3Sharp.MP3Stream(file), AudioType.MP3) { }

		/// <summary>
		/// Initializes a new <see cref="Mp3Stream"/> from a data stream in the .mp3 format
		/// </summary>
		/// <param name="stream">The data stream to read from</param>
		public Mp3Stream(Stream stream) : base(new MP3Sharp.MP3Stream(stream), AudioType.MP3) { }

		/// <summary>
		/// Initialize information about the stream here.  This class does not initialize <see cref="StreamPackage.underlyingStream"/> due to it using a custom decoder.
		/// </summary>
		/// <exception cref="ArgumentException"/>
		protected override void Initialize() {
			MP3Sharp.MP3Stream mp3Stream = underlyingStream as MP3Sharp.MP3Stream;

			Channels = AudioChannels.Stereo;  //MP3 decoder will double mono samples to force the audio to play as "stereo"
			SampleRate = mp3Stream.Frequency;
			BitsPerSample = (short)(mp3Stream.Format == MP3Sharp.SoundFormat.Pcm16BitMono ? 8 : 16);
			TotalBytes = -1;

			if (BitsPerSample != 16)
				throw new ArgumentException("Stream format is not supported: " + mp3Stream.Format);

			InitSound();  // REQUIRED!  This initializes PlayingSound and Metrics
		}

		/// <inheritdoc cref="StreamPackage.SetStreamPosition"/>
		public sealed override void SetStreamPosition(double seconds) {
			// MP3Sharp doesn't have a defined way to arbitrarily set the position
			throw new NotImplementedException("MP3Sharp cannot seek to a specific position without reading from the start of the stream");
		}
	}

	/// <summary>
	/// An object representing audio streaming from an Ogg Vorbis (.ogg) data stream
	/// </summary>
	public class OggStream : StreamPackage {
		/// <summary>
		/// The sample decoder for the .ogg stream
		/// </summary>
		protected NVorbis.VorbisReader vorbisStream;
		/// <summary>
		/// The time to jump to when the stream loops
		/// </summary>
		protected TimeSpan loopTargetTime;

		/// <inheritdoc cref="StreamPackage.MaxDuration"/>
		public sealed override TimeSpan MaxDuration => vorbisStream.TotalTime;

		/// <summary>
		/// Initializes a new <see cref="OggStream"/> from an .ogg file
		/// </summary>
		/// <param name="file">The absolute or relative location of the file to read from</param>
		public OggStream(string file) : base(AudioType.OGG) {
			// NOTE: VorbisReader(string) uses File.OpenRead() and we need TitleContainer.OpenStream() instead
			vorbisStream = new NVorbis.VorbisReader(TitleContainer.OpenStream(file), closeStreamOnDispose: true);

			Initialize();
		}

		/// <summary>
		/// Initializes a new <see cref="OggStream"/> from a data stream in the .ogg format
		/// </summary>
		/// <param name="stream">The data stream to read from</param>
		public OggStream(Stream stream) : base(AudioType.OGG) {
			vorbisStream = new NVorbis.VorbisReader(stream, closeStreamOnDispose: true);

			Initialize();
		}

		/// <inheritdoc cref="StreamPackage.Initialize"/>
		protected override void Initialize() {
			Channels = (AudioChannels)vorbisStream.Channels;
			SampleRate = vorbisStream.SampleRate;
			BitsPerSample = 16;  // Decoding to byte buffer converts floats to shorts
			TotalBytes = -1;

			loopTargetTime = TimeSpan.Zero;

			InitSound();  // REQUIRED!  This initializes PlayingSound and Metrics
		}

		/// <inheritdoc cref="StreamPackage.GetSecondDuration"/>
		public sealed override double GetSecondDuration(long byteSampleCount) => 0;

		/// <inheritdoc cref="StreamPackage.ReadSamples"/>
		public override void ReadSamples(double seconds, out byte[] samples, out int bytesRead, out bool checkLooping) {
			//Float samples = 2 bytes per sample (converted to short samples)
			int samplesToRead = (int)(seconds * SampleRate * (short)Channels);

			if (samplesToRead == 0) {
				throw new InvalidOperationException("MonoSound internals error: Streamed audio requested an invalid amount of samples to read" +
					$"\nObject state: {SampleRate}Hz | {Channels} | {ReadBytes / BitsPerSample * 8 / (int)Channels} samples read");
			}

			float[] vorbisRead = new float[samplesToRead];
			int readOggSamples = vorbisStream.ReadSamples(vorbisRead, 0, vorbisRead.Length);
			bytesRead = readOggSamples * 2;

			samples = new byte[readOggSamples * 2];

			byte[] sampleWrite;
			for (int i = 0; i < readOggSamples; i++) {
				int temp = (int)(short.MaxValue * vorbisRead[i]);
				if (temp > short.MaxValue)
					temp = short.MaxValue;
				else if (temp < short.MinValue)
					temp = short.MinValue;

				sampleWrite = BitConverter.GetBytes((short)temp);

				samples[i * 2] = sampleWrite[0];
				samples[i * 2 + 1] = sampleWrite[1];
			}

			checkLooping = readOggSamples < samplesToRead;

			// SecondsRead is manually set here since the second duration can't really be determined based off of what was read
			SecondsRead = vorbisStream.DecodedTime.TotalSeconds;
		}

		/// <inheritdoc cref="StreamPackage.SetStreamPosition"/>
		public override void SetStreamPosition(double seconds) {
			if (seconds < 0)
				throw new ArgumentOutOfRangeException(nameof(seconds), "Position must be a positive number");

			vorbisStream.DecodedTime = TimeSpan.FromSeconds(seconds);
			ReadBytes = vorbisStream.DecodedPosition;
			ApplyImmediateJump(seconds);
		}

		/// <summary>
		/// Reset the stream here.  By default, this sets the next audio buffer to read to the point in the stream set by <see cref="loopTargetTime"/>
		/// </summary>
		public override void Reset() {
			vorbisStream.DecodedTime = loopTargetTime;

			long pos = vorbisStream.DecodedPosition;
			vorbisStream.DecodedPosition = Math.Max(pos, ModifyResetOffset(pos));

			ReadBytes = vorbisStream.DecodedPosition;
			ApplyImmediateJump(vorbisStream.DecodedTime.TotalSeconds);
		}

		/// <inheritdoc cref="StreamPackage.Dispose(bool)"/>
		protected override void Dispose(bool disposing) {
			if (disposing)
				vorbisStream?.Dispose();

			vorbisStream = null;
		}
	}

	/// <summary>
	/// An object representing audio streaming from an XACT Wavebank (.xwb) data stream
	/// </summary>
	public class WavebankStream : StreamPackage {
		/// <summary>
		/// Initializes a new <see cref="WavebankStream"/> from an XACT Wavebank (.xwb) and XACT Soundbank (.xsb) file
		/// </summary>
		/// <param name="soundBank">The path to the sound bank</param>
		/// <param name="waveBank">The path to the wave bank</param>
		/// <param name="cueName">The name of the sound cue to stream</param>
		public WavebankStream(string soundBank, string waveBank, string cueName) : base(AudioType.XWB) {
			EffectLoader.VerifyThatBanksExistInDictionary(soundBank, waveBank, out string sbName, out _, setStreaming: true);

			//Go to the location where this sound's data is stored
			var sBank = MonoSoundLibrary.soundBanks[sbName];

			sBank.GetInfo(cueName, out int wbIndex, out int trackIndex);

			var wBank = sBank._waveBanks.Get(wbIndex, streaming: true);

			//Open a stream on the wavebank file and move to the track's location, then follow how the sound is read so that "stream" ends up at the start of the sound data
			underlyingStream = new FileStream(waveBank, FileMode.Open);

			SetupStream(wBank, trackIndex);
		}

		/// <summary>
		/// Initializes a new <see cref="WavebankStream"/> from an XACT Wavebank (.xwb) and XACT Soundbank (.xsb) data stream
		/// </summary>
		/// <param name="soundBank">The stream representing the sound bank</param>
		/// <param name="soundBankIdentifier">The path to the sound bank</param>
		/// <param name="waveBank">The stream representing the wave bank</param>
		/// <param name="waveBankIdentifier">The path to the wave bank</param>
		/// <param name="cueName">The name of the sound cue to stream</param>
		public WavebankStream(Stream soundBank, string soundBankIdentifier, Stream waveBank, string waveBankIdentifier, string cueName) : base(AudioType.XWB) {
			EffectLoader.VerifyThatBanksExistInDictionary(soundBank, soundBankIdentifier, waveBank, waveBankIdentifier, out string sbName, out _, setStreaming: true);

			//Go to the location where this sound's data is stored
			var sBank = MonoSoundLibrary.soundBanks[sbName];

			sBank.GetInfo(cueName, out int wbIndex, out int trackIndex);

			var wBank = sBank._waveBanks.Get(wbIndex, streaming: true);

			//Move to the track's location, then follow how the sound is read so that "stream" ends up at the start of the sound data
			underlyingStream = waveBank;

			SetupStream(wBank, trackIndex);
		}

		private void SetupStream(MonoWaveBank wBank, int trackIndex) {
			MonoWaveBank.StreamInfo info = wBank._streams[trackIndex];

			wBank.DecodeFormat(info.Format, out _, out int channels, out int sampleRate, out _);
			Channels = (AudioChannels)channels;
			SampleRate = sampleRate;
			TotalBytes = info.FileLength;
			//Wavebank data is always 16 bits per sample
			BitsPerSample = 16;

			//Move to the beginning of the sound data
			underlyingStream.Seek(info.FileOffset + wBank._playRegionOffset, SeekOrigin.Begin);

			sampleReadStart = underlyingStream.Position;
		}

		/// <inheritdoc cref="StreamPackage.ReadSamples"/>
		public override void ReadSamples(double seconds, out byte[] samples, out int bytesRead, out bool checkLooping) {
			int samplesToRead = (int)(seconds * BitsPerSample / 8 * SampleRate * (short)Channels);

			if (samplesToRead == 0) {
				string suffix = TotalBytes == -1 ? "" : $" out of {TotalBytes / BitsPerSample * 8 / (int)Channels}";

				throw new InvalidOperationException("MonoSound internals error: Streamed audio requested an invalid amount of samples to read" +
					$"\nObject state: {SampleRate}Hz | {Channels} | {ReadBytes / BitsPerSample * 8 / (int)Channels}{suffix} samples read");
			}

			samples = new byte[samplesToRead];
			bytesRead = underlyingStream.Read(samples, 0, samplesToRead);

			checkLooping = bytesRead < samples.Length;
		}
	}
}
