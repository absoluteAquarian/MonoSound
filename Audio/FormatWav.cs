using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using MP3Sharp;
using MP3Sharp.Decoding;
using NVorbis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MonoSound.Audio {
	/// <summary>
	/// A class representing the data in WAVE-formatted audio
	/// </summary>
	public sealed class FormatWav : IDisposable {
		//WAVE data format found here:
		// http://soundfile.sapp.org/doc/WaveFormat/
		// https://medium.com/swlh/reversing-a-wav-file-in-c-482fc3dfe3c4

		private const int RIFF_HEADER_SIZE = 12;  // "RIFF" + Size + "WAVE"
		private const int SUBCHUNK_HEADER_SIZE = 8;
		private const int FMT_SUBCHUNK_DATA_LENGTH = 16;

		private byte[] data;
		private int _fmtSubchunkOffset = RIFF_HEADER_SIZE;
		private int _dataSubchunkOffset = RIFF_HEADER_SIZE + SUBCHUNK_HEADER_SIZE + FMT_SUBCHUNK_DATA_LENGTH;

		/// <summary>
		/// "RIFF" if the file stores its values as Little-Endian, "RIFX" otherwise
		/// </summary>
		public string EndianHeader => Encoding.UTF8.GetString(data, 0, 4);
		/// <summary>
		/// The total size of the file in bytes
		/// </summary>
		public int Size => BitConverter.ToInt32(data, 4);
		/// <summary>
		/// The type of audio saved.  Always set to "WAVE"
		/// </summary>
		public string FileTypeHeader => Encoding.UTF8.GetString(data, 8, 4);
		/// <summary>
		/// The format chunk marker.  Always set to "fmt "
		/// </summary>
		public string FormatChunkMarker => Encoding.UTF8.GetString(data, _fmtSubchunkOffset, 4);
		/// <summary>
		/// The length of the format data in bytes
		/// </summary>
		public int FormatLength => BitConverter.ToInt32(data, _fmtSubchunkOffset + 4);
		/// <summary>
		/// The audio format each sample is saved as.
		/// <para>PCM is 1</para>
		/// <para>Any other values are assumed to be some other form of compression</para>
		/// </summary>
		public short FormatType => BitConverter.ToInt16(data, _fmtSubchunkOffset + 8);
		/// <summary>
		/// What channels this WAV sound will use.
		/// <para>Mono is 1</para>
		/// <para>Stereo is 2</para>
		/// </summary>
		public short ChannelCount => BitConverter.ToInt16(data, _fmtSubchunkOffset + 10);
		/// <summary>
		/// How many samples are played per second.  Measured in Hertz (Hz)
		/// </summary>
		public int SampleRate => BitConverter.ToInt32(data, _fmtSubchunkOffset + 12);
		/// <summary>
		/// How many bytes are played per second
		/// <para>Usually set to: <c>SampleRate * BitsPerSample * ChannelCount / 8</c></para>
		/// </summary>
		public int ByteRate => BitConverter.ToInt32(data, _fmtSubchunkOffset + 16);
		/// <summary>
		/// The number of bytes per sample including all channels
		/// <para>Usually set to: <c>BitsPerSample * ChannelCount / 8</c></para>
		/// </summary>
		public short BlockAlign => BitConverter.ToInt16(data, _fmtSubchunkOffset + 20);
		/// <summary>
		/// How many bits PER CHANNEL are in one sample
		/// </summary>
		public short BitsPerSample => BitConverter.ToInt16(data, _fmtSubchunkOffset + 22);
		/// <summary>
		/// The data chunk marker.  Always set to "data"
		/// </summary>
		public string DataChunkMarker => Encoding.UTF8.GetString(data, _fmtSubchunkOffset + 24, 4);
		/// <summary>
		/// The length of the sample data in bytes
		/// </summary>
		public int DataLength {
			get => BitConverter.ToInt32(data, _dataSubchunkOffset + 4);
			private set => BitConverter.GetBytes(value).CopyTo(data, _dataSubchunkOffset + 4);
		}

		/// <summary>
		/// Converts the portion of the underlying byte stream that contains audio data into a series of sample instances
		/// </summary>
		/// <returns></returns>
		public WavSample[] GetSamples() {
			byte[] audioData = GetSoundBytes();

			int size = BitsPerSample / 8;
			WavSample[] samples = new WavSample[audioData.Length / size];

			for (int i = 0, j = 0; i < audioData.Length; i += size, j++)
				samples[j] = new WavSample(audioData.AsSpan().Slice(i, size));

			return samples;
		}

		/// <summary>
		/// Retrieves a copy of the sample data
		/// </summary>
		/// <returns></returns>
		public byte[] GetSoundBytes() {
			byte[] audioData = new byte[DataLength];
			Buffer.BlockCopy(data, _dataSubchunkOffset + 8, audioData, 0, audioData.Length);
			return audioData;
		}

		internal PCMData GetMetadata() {
			float duration = (int)((float)DataLength / ByteRate);
			return new PCMData() {
				bitsPerSample = BitsPerSample,
				channels = (AudioChannels)ChannelCount,
				duration = (int)(duration * 1000),
				sampleRate = SampleRate
			};
		}

		private FormatWav() { }

		/// <summary>
		/// Attempts to load a <see cref="FormatWav"/> from the given file path.<br/>
		/// If the file extension refers to a custom format and no format could parse the file, an exception is thrown.
		/// </summary>
		/// <exception cref="ArgumentException"/>
		public static FormatWav FromFile(string path) {
			string extension = Path.GetExtension(path);
			if (!MonoSoundLibrary.AllValidExtensions.Contains(extension))
				throw new ArgumentException($"The given path did not contain a valid extension: {extension}", nameof(path));

			switch (extension) {
				case ".xnb":
					byte[] data = Decompressor.DecompressSoundEffectXNB(path, out _, out byte[] header);
					return FromDecompressorData(data, header);
				case ".wav":
					return FromFileWAV(path);
				case ".ogg":
					return FromFileOGG(path);
				case ".mp3":
					return FromFileMP3(path);
				default:
					return (MonoSoundLibrary.customAudioFormats.TryGetValue(extension, out var audioFormat)
						? audioFormat.ReadWav(path)
						: MonoSoundLibrary.registeredFormats.TryGetValue(extension, out var format)
							? format.read(TitleContainer.OpenStream(path))
							: throw new ArgumentException("File extension was not supported: " + extension))
						?? throw new ArgumentException($"Registered format for file extension \"{extension}\" could not read file \"{path}\"");
			}
		}

		/// <summary>
		/// Attempts to load a <see cref="FormatWav"/> from the given stream.<br/>
		/// If <see cref="AudioType.Custom"/> is specified and no custom format could parse the stream, an exception is thrown.
		/// </summary>
		/// <exception cref="InvalidOperationException"/>
		public static FormatWav FromStream(Stream stream, AudioType type) {
			switch (type) {
				case AudioType.XNB:
					byte[] data = Decompressor.DecompressSoundEffectXNB(stream, out _, out byte[] header);

					return FromDecompressorData(data, header);
				case AudioType.WAV:
					return FromFileWAV(stream);
				case AudioType.OGG:
					return FromFileOGG(stream);
				case AudioType.MP3:
					return FromFileMP3(stream);
				case AudioType.Custom:
					return AttemptCustomLoad(stream, MonoSoundLibrary.customAudioFormats, static (fmt, stream) => fmt.ReadWav(stream))
						?? AttemptCustomLoad(stream, MonoSoundLibrary.registeredFormats, static (fmt, stream) => fmt.read(stream))  // Legacy API
						?? throw new InvalidOperationException("Audio stream is not supported by any of the registered custom formats");
				default:
					throw new InvalidOperationException("Audio type is not supported: " + type);
			}
		}

		private static FormatWav AttemptCustomLoad<T>(Stream stream, Dictionary<string, T> formats, Func<T, Stream, FormatWav> wavFactory) {
			foreach (var format in formats.Values) {
				long pos = stream.Position;

				FormatWav wav = wavFactory(format, stream);

				if (wav is not null)
					return wav;

				stream.Position = pos;
			}

			return null;
		}

		/// <summary>
		/// Loads a <see cref="FormatWav"/> from a .wav file
		/// </summary>
		/// <exception cref="ArgumentException"/>
		public static FormatWav FromFileWAV(string file) {
			if (Path.GetExtension(file) != ".wav")
				throw new ArgumentException("File must be a .wav file", nameof(file));

			return FromFileWAV(TitleContainer.OpenStream(file));
		}

		/// <summary>
		/// Loads a <see cref="FormatWav"/> from a .wav stream
		/// </summary>
		public static FormatWav FromFileWAV(Stream readStream) {
			using MemoryStream stream = new MemoryStream();
			readStream.CopyTo(stream);
			return FromBytes(stream.ToArray());
		}

		/// <summary>
		/// Loads a <see cref="FormatWav"/> from an .ogg file
		/// </summary>
		/// <exception cref="ArgumentException"/>
		public static FormatWav FromFileOGG(string file) {
			if (Path.GetExtension(file) != ".ogg")
				throw new ArgumentException("File must be an .ogg file", nameof(file));

			return FromFileOGG(TitleContainer.OpenStream(file));
		}

		/// <summary>
		/// Loads a <see cref="FormatWav"/> from an .ogg stream
		/// </summary>
		public static FormatWav FromFileOGG(Stream readStream) {
			//OGG Vorbis specifications defined here: https://www.xiph.org/vorbis/doc/Vorbis_I_spec.html#x1-230001.3.2
			//Example use found here: https://csharp.hotexamples.com/examples/-/NVorbis.VorbisReader/-/php-nvorbis.vorbisreader-class-examples.html
			// TODO: finish this multiline comment

			/*  OGG Vorbis format
			 *  Note: the first three pages are always the identification, comment and setup Vorbis headers
			 *        all pages after those three are for sound data alone
			 *  
			 *  Page format:
			 *    4 bytes - capture pattern; always "OggS"
			 *    1 byte  - version; always 0x00
			 *    1 byte  - header type; 0x00 for normal page, 0x01 for continued page, 0x02 for starting page, 0x04 for ending page
			 *    8 bytes - granule position; the "position" of this page.  how this is defined depends on what was used to encode the file (audio/video)
			 *    4 bytes - serial number; a unique identifier shared by all pages in the file
			 *    4 bytes - page sequence number; 0-based index of this page
			 *    4 bytes - checksum
			 *    1 byte  - segment count; how many segments this page contains
			 *    N bytes - (amount of bytes dependent on what was read for "segment count") how many bytes this segment's packet contains; 0x00-0xFE means
			 *              this segment has its own packet.  0xFF means this segment shares a packet with the next segment.  a final packet size that
			 *              is a multiple of 255 will always end in a 0x00 (null) packet
			 *    M bytes - (amount of bytes dependent on what segment this is based on the segment table) the segment data, in order
			 *  
			 *  Vorbis Identification Header format:
			 *    1 byte  - packet type; 0x01 is identification header, 0x03 is comment header, 0x05 is setup header
			 *    6 bytes - vorbis header indentifier; always "vorbis"
			 *    4 bytes - vorbis version
			 *    1 byte  - channel count
			 *    4 bytes - sample rate
			 *    4 bytes - upper bitrate
			 *    4 bytes - nominal bitrate
			 *    4 bytes - lower bitrate
			 *    1 byte  - block sizes; split into 4 bits each; block size is 2^{4 bits read}; block size 0 (first 4 bits) must be smaller than block
			 *              size 1 (second 4 bits)
			 *    1 byte  - MSB must be not set for this header to be considered valid
			 *    
			 *    Failure to pass the last two checks renders the stream unreadable
			 *  
			 *  ----
			 */

			using VorbisReader reader = new VorbisReader(readStream, closeStreamOnDispose: true);

			byte[] fmtChunk = [
				0x01, 0x00,
				.. BitConverter.GetBytes((short)reader.Channels),
				.. BitConverter.GetBytes(reader.SampleRate),
				.. BitConverter.GetBytes(reader.SampleRate * reader.Channels * 2),
				.. BitConverter.GetBytes((short)(reader.Channels * 2)),
				0x10, 0x00  // 16-bit PCM
			];

			//Read the samples
			float[] buffer = new float[reader.SampleRate / 10 * reader.Channels];
			byte[] sampleWrite;
			List<byte> samples = [];
			int count;
			while ((count = reader.ReadSamples(buffer, 0, buffer.Length)) > 0) {
				for (int i = 0; i < count; i++) {
					int temp = (int)(short.MaxValue * buffer[i]);
					if (temp > short.MaxValue)
						temp = short.MaxValue;
					else if (temp < short.MinValue)
						temp = short.MinValue;

					sampleWrite = BitConverter.GetBytes((short)temp);

					samples.Add(sampleWrite[0]);
					samples.Add(sampleWrite[1]);
				}
			}

			return FromDecompressorData([.. samples], fmtChunk);
		}

		/// <summary>
		/// Creates a <see cref="FormatWav"/> from the given sample data and WAVE settings
		/// </summary>
		/// <param name="sampleData">The sample data</param>
		/// <param name="channels">Mono or Stereo</param>
		/// <param name="sampleRate">How many samples PER CHANNEL are read per second (the frequency of the audio)</param>
		/// <param name="bitsPerSample">The PCM format of each sample</param>
		/// <exception cref="ArgumentOutOfRangeException"/>
		/// <exception cref="ArgumentException"/>
		public static FormatWav FromSampleDataAndSettings(byte[] sampleData, AudioChannels channels, int sampleRate, int bitsPerSample) {
			if (sampleData is not { Length: > 0 })
				throw new ArgumentException("No samples were provided", nameof(sampleData));
			if (channels != AudioChannels.Mono && channels != AudioChannels.Stereo)
				throw new ArgumentException("Audio data must be Mono or Stereo", nameof(channels));
			if (sampleRate < 0)
				throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be a positive number");
			if (bitsPerSample != 16 && bitsPerSample != 24)
				throw new ArgumentException("Sample bit depth must be 16-bit or 24-bit PCM", nameof(bitsPerSample));

			int bytesPerSample = bitsPerSample / 8;

			byte[] header = [
				.. AsBytes("RIFF"),
				.. BitConverter.GetBytes(44 + sampleData.Length),  // Total file size = header + sample data length
				.. AsBytes("WAVE"),
				.. AsBytes("fmt "),
				0x10, 0x00, 0x00, 0x00,  // Length of format chunk = 16 bytes
				0x01, 0x00,  // Type of format, PCM = 1
				.. BitConverter.GetBytes((short)channels),
				.. BitConverter.GetBytes(sampleRate),
				.. BitConverter.GetBytes(sampleRate * (int)channels * bytesPerSample),  // Bytes per second
				.. BitConverter.GetBytes((short)((int)channels * bytesPerSample)),  // Block align
				.. BitConverter.GetBytes((short)bitsPerSample),
				.. AsBytes("data"),
				.. BitConverter.GetBytes(sampleData.Length)  // Sample data length
			];

			byte[] actualStream = new byte[header.Length + sampleData.Length];

			// Copy the data
			Buffer.BlockCopy(header, 0, actualStream, 0, header.Length);
			Buffer.BlockCopy(sampleData, 0, actualStream, header.Length, sampleData.Length);

			return FromBytes(actualStream);
		}

		private static IEnumerable<byte> AsBytes(string word) {
			foreach (char c in word)
				yield return (byte)c;
		}

		internal static FormatWav FromDecompressorData(byte[] sampleData, byte[] fmtChunk) {
			byte[] header = [
				.. AsBytes("RIFF"),
				.. BitConverter.GetBytes(RIFF_HEADER_SIZE + SUBCHUNK_HEADER_SIZE + FMT_SUBCHUNK_DATA_LENGTH + SUBCHUNK_HEADER_SIZE + sampleData.Length),
				.. AsBytes("WAVE"),
				.. AsBytes("fmt "),
				0x10, 0x00, 0x00, 0x00,  // Length of format chunk = 16 bytes
				.. fmtChunk,
				.. AsBytes("data"),
				.. BitConverter.GetBytes(sampleData.Length)  // Sample data length
			];

			return FromBytes([ .. header, .. sampleData ]);
		}

		/// <summary>
		/// Loads a <see cref="FormatWav"/> from an .mp3 file
		/// </summary>
		/// <exception cref="ArgumentException"/>
		public static FormatWav FromFileMP3(string file) {
			if (Path.GetExtension(file) != ".mp3")
				throw new ArgumentException("File must be an .mp3 file", nameof(file));

			return FromFileMP3(TitleContainer.OpenStream(file));
		}

		/// <summary>
		/// Loads a <see cref="FormatWav"/> from an .mp3 stream
		/// </summary>
		public static FormatWav FromFileMP3(Stream readStream) {
			int attempts = 0;

			repeat:

			try {
				using MP3Stream stream = new MP3Stream(readStream);

				byte[] fmtChunk =  [
					0x01, 0x00,  // PCM format
					0x02, 0x00,  // MP3 decoder forces the samples to align to Stereo
					.. BitConverter.GetBytes(stream.Frequency),
					.. BitConverter.GetBytes(stream.Frequency * 4),
					0x04, 0x00,  // Block Align is always 4 bytes
					0x10, 0x00   // 16-bit PCM
				];

				//Read the samples
				byte[] sampleWrite = new byte[1024];
				List<byte> samples = [];
				int count;

				while ((count = stream.Read(sampleWrite, 0, 1024)) > 0) {
					byte[] read = new byte[count];
					Buffer.BlockCopy(sampleWrite, 0, read, 0, count);

					samples.AddRange(read);
				}

				return FromDecompressorData([.. samples], fmtChunk);
			} catch (BitstreamException) {
				// Try again
				attempts++;

				if (attempts < 100)
					goto repeat;

				Debug.WriteLine("Failed to load file after 100 attempts");
				throw;
			}
		}

		/// <summary>
		/// Loads a <see cref="FormatWav"/> from a .wav byte stream
		/// </summary>
		public static FormatWav FromBytes(byte[] data) {
			if (data.Length < RIFF_HEADER_SIZE + SUBCHUNK_HEADER_SIZE + FMT_SUBCHUNK_DATA_LENGTH + SUBCHUNK_HEADER_SIZE)
				throw new ArgumentException("Data was too short to contain a header.", nameof(data));

			FormatWav wav = new FormatWav() {
				data = data
			};

			//Verify that the input data was correct
			try {
				using MemoryStream ms = new MemoryStream(data);
				using WAVEReader reader = new WAVEReader(ms);

				wav._fmtSubchunkOffset = reader.ReadFormat(out _, out _, out _, out _, out _);
				wav._dataSubchunkOffset = reader.ReadDataHeader(out _);
			} catch (Exception ex) {
				throw new ArgumentException("Data was invalid for the WAV format.", nameof(data), ex);
			}

			return wav;
		}

		internal static FormatWav FromSoundEffectConstructor(XACT.MiniFormatTag codec, byte[] buffer, int channels, int sampleRate, int blockAlignment, int loopStart, int loopLength) {
			byte[] fmtChunk = [
				0x01, 0x00,  // Force the PCM encoding... Others aren't allowed
				.. BitConverter.GetBytes((short)channels),
				.. BitConverter.GetBytes(sampleRate),
				.. BitConverter.GetBytes(sampleRate * channels * 2),
				.. BitConverter.GetBytes((short)blockAlignment),
				0x10, 0x00  // WaveBank sounds always have 16 bits/sample for some reason
			];

			return FromDecompressorData(buffer, fmtChunk);
		}

		/// <summary>
		/// Saves the current instance to a <i>.wav</i> file
		/// </summary>
		/// <param name="file">The file to write the data to.  Must have the <i>.wav</i> extension</param>
		/// <exception cref="ArgumentException"/>
		public void SaveToFile(string file) {
			if (Path.GetExtension(file) != ".wav")
				throw new ArgumentException("Destination file must be a .wav file", nameof(file));

			Directory.CreateDirectory(Path.GetDirectoryName(file));

			using BinaryWriter writer = new BinaryWriter(File.Open(file, FileMode.Create));
			writer.Write(data);
		}

		internal float[] DeconstructToFloatSamples() {
			WavSample[] samples = GetSamples();
			float[] allSamples = new float[samples.Length];

			for (int i = 0; i < samples.Length; i++) {
				float sample = samples[i].ToFloatSample();
				ClampSample(ref sample);
				allSamples[i] = sample;
			}

			return allSamples;
		}

		internal static float[] UninterleaveSamples(float[] samples, int channelCount) {
			if (channelCount != 1) {
				if (samples.Length % channelCount != 0)
					throw new ArgumentException("The number of samples provided was not divisible by the number of channels", nameof(samples));

				float[] newSamples = new float[samples.Length];
				int channelSize = samples.Length / channelCount;

				// [ A B A B A B A B ] -> [ A A A A B B B B ]

				for (int s = 0; s < channelSize; s++) {
					for (int c = 0; c < channelCount; c++)
						newSamples[c * channelSize + s] = samples[s * channelCount + c];
				}

				samples = newSamples;
			}

			return samples;
		}

		internal float[] DeconstructAndUninterleaveSamples() {
			WavSample[] samples = GetSamples();
			float[] allSamples = new float[samples.Length];
			int channelCount = ChannelCount;

			if (channelCount != 1) {
				if (allSamples.Length % channelCount != 0)
					throw new ArgumentException("WAVE data was malformed; the number of samples was not divisible by the number of channels", nameof(data));

				int channelSize = samples.Length / channelCount;

				for (int s = 0; s < channelSize; s++) {
					for (int c = 0; c < channelCount; c++) {
						float sample = samples[s * channelCount + c].ToFloatSample();
						ClampSample(ref sample);
						allSamples[c * channelSize + s] = sample;
					}
				}
			} else {
				// Samples don't need to be uninterleaved if there's only one channel
				for (int i = 0; i < samples.Length; i++) {
					float sample = samples[i].ToFloatSample();
					ClampSample(ref sample);
					allSamples[i] = sample;
				}
			}

			return allSamples;
		}

		internal static float[] InterleaveSamples(float[] samples, int channelCount) {
			if (channelCount != 1) {
				if (samples.Length % channelCount != 0)
					throw new ArgumentException("The number of samples provided was not divisible by the number of channels", nameof(samples));

				float[] newSamples = new float[samples.Length];
				int channelSize = samples.Length / channelCount;

				for (int s = 0; s < channelSize; s++) {
					for (int c = 0; c < channelCount; c++)
						newSamples[s * channelCount + c] = samples[c * channelSize + s];
				}

				samples = newSamples;
			}

			return samples;
		}

		internal void ReconstructFromSamples(float[] samples) {
			byte[] newData;

			if (BitsPerSample == 16) {
				newData = new byte[samples.Length * 2];

				for (int i = 0; i < samples.Length; i++) {
					float sample = samples[i];
					ClampSample(ref sample);  // Filters can cause samples to go out of bounds... Restricting the value shouldn't affect the sound quality too much
					new PCM16Bit(sample).WriteToStream(newData, i * 2);
				}
			} else if (BitsPerSample == 24) {
				newData = new byte[samples.Length * 3];

				for (int i = 0; i < samples.Length; i++) {
					float sample = samples[i];
					ClampSample(ref sample);  // Filters can cause samples to go out of bounds... Restricting the value shouldn't affect the sound quality too much
					new PCM24Bit(sample).WriteToStream(newData, i * 3);
				}
			} else
				throw new InvalidOperationException("Attempted to process data for a bit depth that's not supported.  WAV files must use 16-bit or 24-bit PCM data");

			// Echo filter can cause extra data to be given
			if (newData.Length + 44 > data.Length) {
				Array.Resize(ref data, newData.Length + 44);

				// Update the new data length in the header since it changed
				var bytes = BitConverter.GetBytes(newData.Length);
				data[40] = bytes[0];
				data[41] = bytes[1];
				data[42] = bytes[2];
				data[43] = bytes[3];
			}

			Buffer.BlockCopy(newData, 0, data, 44, newData.Length);
		}

		/// <summary>
		/// Forces the sample to be within (-1, 1)
		/// </summary>
		public static void ClampSample(ref float sample) => sample = Math.Clamp(sample, -1, 1);

		private bool disposed;

		/// <summary>
		/// Finalizer for the <see cref="FormatWav"/> class
		/// </summary>
		~FormatWav() => Dispose(false);

		/// <inheritdoc cref="IDisposable.Dispose"/>
		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			if (!disposed) {
				if (disposing) {
					// Dispose managed resources
				}

				data = null;
				disposed = true;
			}
		}
	}
}
