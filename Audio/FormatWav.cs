using Microsoft.Xna.Framework.Audio;
using MonoSound.XACT;
using MP3Sharp;
using NVorbis;
using System;
using System.Collections.Generic;
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

		private byte[] data;

		/// <summary>
		/// Gets a clone of the underlying byte stream
		/// </summary>
		public byte[] Data => (byte[])data.Clone();

		//Header data is always the first 44 bytes
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
		public string FormatChunkMarker => Encoding.UTF8.GetString(data, 12, 4);
		/// <summary>
		/// The length of the format data in bytes
		/// </summary>
		public int FormatLength => BitConverter.ToInt32(data, 16);
		/// <summary>
		/// The audio format each sample is saved as.
		/// <para>PCM is 1</para>
		/// <para>Any other values are assumed to be some other form of compression</para>
		/// </summary>
		public short FormatType => BitConverter.ToInt16(data, 20);
		/// <summary>
		/// What channels this WAV sound will use.
		/// <para>Mono is 1</para>
		/// <para>Stereo is 2</para>
		/// </summary>
		public short ChannelCount => BitConverter.ToInt16(data, 22);
		/// <summary>
		/// How many samples are played per second.  Measured in Hertz (Hz)
		/// </summary>
		public int SampleRate => BitConverter.ToInt32(data, 24);
		/// <summary>
		/// How many bytes are played per second
		/// <para>Usually set to: <c>SampleRate * BitsPerSample * ChannelCount / 8</c></para>
		/// </summary>
		public int ByteRate => BitConverter.ToInt32(data, 28);
		/// <summary>
		/// The number of bytes per sample including all channels
		/// <para>Usually set to: <c>BitsPerSample * ChannelCount / 8</c></para>
		/// </summary>
		public short BlockAlign => BitConverter.ToInt16(data, 32);
		/// <summary>
		/// How many bits PER CHANNEL are in one sample
		/// </summary>
		public short BitsPerSample => BitConverter.ToInt16(data, 34);
		/// <summary>
		/// The data chunk marker.  Always set to "data"
		/// </summary>
		public string DataChunkMarker => Encoding.UTF8.GetString(data, 36, 4);
		/// <summary>
		/// The length of the sample data in bytes
		/// </summary>
		public int DataLength => BitConverter.ToInt32(data, 40);

		public WavSample[] GetSamples() {
			byte[] audioData = GetSoundBytes();

			List<WavSample> samples = new List<WavSample>();
			int size = BitsPerSample / 8;

			for (int i = 0; i < audioData.Length; i += size) {
				byte[] pass = new byte[size];
				Array.Copy(audioData, i, pass, 0, size);
				samples.Add(new WavSample((short)(BitsPerSample / 8), pass));
			}

			return samples.ToArray();
		}

		/// <summary>
		/// Retrieves a copy of the sample data
		/// </summary>
		/// <returns></returns>
		public byte[] GetSoundBytes() {
			byte[] audioData = new byte[DataLength];
			Buffer.BlockCopy(data, 44, audioData, 0, audioData.Length);
			return audioData;
		}

		private FormatWav() { }

		/// <summary>
		/// Loads a <see cref="FormatWav"/> from a .wav file
		/// </summary>
		/// <exception cref="ArgumentException"/>
		public static FormatWav FromFileWAV(string file) {
			if (Path.GetExtension(file) != ".wav")
				throw new ArgumentException("File must be a .wav file", "file");

			return FromFileWAV(File.OpenRead(file));
		}

		/// <summary>
		/// Loads a <see cref="FormatWav"/> from a .wav stream
		/// </summary>
		public static FormatWav FromFileWAV(Stream readStream) {
			using BinaryReader reader = new BinaryReader(readStream);
			using MemoryStream stream = new MemoryStream();
			reader.BaseStream.CopyTo(stream);
			return FromBytes(stream.GetBuffer());
		}

		/// <summary>
		/// Loads a <see cref="FormatWav"/> from an .ogg file
		/// </summary>
		/// <exception cref="ArgumentException"/>
		public static FormatWav FromFileOGG(string file) {
			if (Path.GetExtension(file) != ".ogg")
				throw new ArgumentException("File must be an .ogg file", "file");

			return FromFileOGG(File.OpenRead(file));
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

			byte[] header = new byte[16];

			//Type of Format
			header[0] = 0x01;

			//Number of Channels
			byte[] arr = BitConverter.GetBytes((short)reader.Channels);
			header[2] = arr[0];
			header[3] = arr[1];

			//Samples per Second
			arr = BitConverter.GetBytes(reader.SampleRate);
			header[4] = arr[0];
			header[5] = arr[1];
			header[6] = arr[2];
			header[7] = arr[3];

			//Bytes per Second
			arr = BitConverter.GetBytes(reader.SampleRate * reader.Channels * 2);
			header[8] = arr[0];
			header[9] = arr[1];
			header[10] = arr[2];
			header[11] = arr[3];

			//Block Align
			arr = BitConverter.GetBytes((short)(reader.Channels * 2));
			header[12] = arr[0];
			header[13] = arr[1];

			//Bits per Sample
			arr = BitConverter.GetBytes((short)16);
			header[14] = arr[0];
			header[15] = arr[1];

			//Read the samples
			float[] buffer = new float[reader.SampleRate / 10 * reader.Channels];
			byte[] sampleWrite;
			List<byte> samples = new List<byte>();
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

			return FromDecompressorData(samples.ToArray(), header);
		}

		internal static FormatWav FromDecompressorData(byte[] sampleData, byte[] header) {
			byte[] addon = new byte[44];
			addon[0] = (byte)'R';
			addon[1] = (byte)'I';
			addon[2] = (byte)'F';
			addon[3] = (byte)'F';

			//Excluded: Total file size

			addon[8] = (byte)'W';
			addon[9] = (byte)'A';
			addon[10] = (byte)'V';
			addon[11] = (byte)'E';

			addon[12] = (byte)'f';
			addon[13] = (byte)'m';
			addon[14] = (byte)'t';
			addon[15] = (byte)' ';

			//Format header length
			addon[16] = 16;

			//Type of Format, Number of Channels, Samples per Second, Bytes per Second, Block Align, Bits per Sample
			Buffer.BlockCopy(header, 0, addon, 20, 16);

			addon[36] = (byte)'d';
			addon[37] = (byte)'a';
			addon[38] = (byte)'t';
			addon[39] = (byte)'a';

			byte[] arr = BitConverter.GetBytes(sampleData.Length);
			addon[40] = arr[0];
			addon[41] = arr[1];
			addon[42] = arr[2];
			addon[43] = arr[3];

			byte[] actualStream = new byte[addon.Length + sampleData.Length];

			//Copy the data
			Buffer.BlockCopy(addon, 0, actualStream, 0, addon.Length);
			Buffer.BlockCopy(sampleData, 0, actualStream, addon.Length, sampleData.Length);

			arr = BitConverter.GetBytes(actualStream.Length);
			actualStream[4] = arr[0];
			actualStream[5] = arr[1];
			actualStream[6] = arr[2];
			actualStream[7] = arr[3];

			return FromBytes(actualStream);
		}

		/// <summary>
		/// Loads a <see cref="FormatWav"/> from an .mp3 file
		/// </summary>
		/// <exception cref="ArgumentException"/>
		public static FormatWav FromFileMP3(string file) {
			if (Path.GetExtension(file) != ".mp3")
				throw new ArgumentException("File must be an .mp3 file", "file");

			return FromFileMP3(File.OpenRead(file));
		}

		/// <summary>
		/// Loads a <see cref="FormatWav"/> from an .mp3 stream
		/// </summary>
		public static FormatWav FromFileMP3(Stream readStream) {
			using MP3Stream stream = new MP3Stream(readStream);

			byte[] header = new byte[16];

			//Type of Format
			header[0] = 0x01;

			//Number of Channels
			byte[] arr = BitConverter.GetBytes((short)AudioChannels.Stereo);  //MP3 decoder forces the samples to align to stereo
			header[2] = arr[0];
			header[3] = arr[1];

			//Samples per Second
			arr = BitConverter.GetBytes(stream.Frequency);
			header[4] = arr[0];
			header[5] = arr[1];
			header[6] = arr[2];
			header[7] = arr[3];

			//Bytes per Second
			arr = BitConverter.GetBytes(stream.Frequency * 4);
			header[8] = arr[0];
			header[9] = arr[1];
			header[10] = arr[2];
			header[11] = arr[3];

			//Block Align
			arr = BitConverter.GetBytes((short)4);
			header[12] = arr[0];
			header[13] = arr[1];

			//Bits per Sample
			arr = BitConverter.GetBytes((short)16);
			header[14] = arr[0];
			header[15] = arr[1];

			//Read the samples
			byte[] sampleWrite = new byte[1024];
			List<byte> samples = new List<byte>();
			int count;

			while ((count = stream.Read(sampleWrite, 0, 1024)) > 0) {
				byte[] read = new byte[count];
				Buffer.BlockCopy(sampleWrite, 0, read, 0, count);

				samples.AddRange(read);
			}

			return FromDecompressorData(samples.ToArray(), header);
		}

		/// <summary>
		/// Loads a <see cref="FormatWav"/> from a .wav byte stream
		/// </summary>
		public static FormatWav FromBytes(byte[] data) {
			if (data.Length < 44)
				throw new ArgumentException("Data was too short to contain a header.", "data");

			FormatWav wav = new FormatWav() {
				data = data
			};

			//Verify that the input data was correct
			try {
HeaderCheckStart:

				string eHeader = wav.EndianHeader;
				if (eHeader != "RIFF" && eHeader != "RIFX")
					throw new Exception("Endian header string was not \"RIFF\" nor \"RIFX\".");

				if (wav.FileTypeHeader != "WAVE")
					throw new Exception("File type header string was not \"WAVE\".");

				if (wav.FormatChunkMarker != "fmt ")
					throw new Exception("Format chunk header string was not \"fmt \".");

				if (wav.DataChunkMarker != "data") {
					//If the data chunk marker was instead "LIST", then there's metadata in the WAV file
					//That metadata is completely irrelevant for MonoSound, so the data array needs to be rebuilt with the "LIST" chunk missing
					if (wav.DataChunkMarker == "LIST") {
						int infoLength = wav.DataLength;

						byte[] overwrite = new byte[data.Length - infoLength];
						Buffer.BlockCopy(data, 0, overwrite, 0, 36);

						int afterInfo = 44 + infoLength;
						Buffer.BlockCopy(data, afterInfo, overwrite, 36, data.Length - afterInfo);

						byte[] bytes = BitConverter.GetBytes(overwrite.Length);
						overwrite[4] = bytes[0];
						overwrite[5] = bytes[1];
						overwrite[6] = bytes[2];
						overwrite[7] = bytes[3];

						wav.data = overwrite;

						goto HeaderCheckStart;
					}

					throw new Exception("Data chunk header string was not \"data\".");
				}

				if (wav.data.Length != wav.Size)
					throw new Exception("File size did not match stored size.");

				int sampleRate = wav.SampleRate;
				if (sampleRate < 8000 || sampleRate > 48000)
					throw new Exception("Sample rate was outside the range of valid values.");
			} catch (Exception ex) {
				throw new ArgumentException("Data was invalid for the WAV format.", "data", ex);
			}

			return wav;
		}

#pragma warning disable IDE0060
		internal static FormatWav FromSoundEffectConstructor(MiniFormatTag codec, byte[] buffer, int channels, int sampleRate, int blockAlignment, int loopStart, int loopLength) {
#pragma warning restore IDE0060
			//WaveBank sounds always have 16 bits/sample for some reason
			const int bitsPerSample = 16;

			byte[] header = new byte[16];
			var bytes = BitConverter.GetBytes((short)1);    //Force the PCM encoding... Others aren't allowed
			header[0] = bytes[0];
			header[1] = bytes[1];
			bytes = BitConverter.GetBytes((short)channels);
			header[2] = bytes[0];
			header[3] = bytes[1];
			bytes = BitConverter.GetBytes(sampleRate);
			header[4] = bytes[0];
			header[5] = bytes[1];
			header[6] = bytes[2];
			header[7] = bytes[3];
			bytes = BitConverter.GetBytes(sampleRate * channels * bitsPerSample / 8);
			header[8] = bytes[0];
			header[9] = bytes[1];
			header[10] = bytes[2];
			header[11] = bytes[3];
			bytes = BitConverter.GetBytes((short)blockAlignment);
			header[12] = bytes[0];
			header[13] = bytes[1];
			bytes = BitConverter.GetBytes((short)bitsPerSample);
			header[14] = bytes[0];
			header[15] = bytes[1];

			return FromDecompressorData(buffer, header);
		}

		public void SaveToFile(string file) {
			if (Path.GetExtension(file) != ".wav")
				throw new ArgumentException("Destination file must be a .wav file", "file");

			Directory.CreateDirectory(Path.GetDirectoryName(file));

			using BinaryWriter writer = new BinaryWriter(File.Open(file, FileMode.Create));
			writer.Write(data);
		}

		internal void DeconstructToFloatSamples(out float[] allSamples) {
			int length = DataLength / 2;

			WavSample[] samples = GetSamples();

			//All of the data goes to one "channel" since it's processed in pairs anyway
			allSamples = new float[length];

			for (int i = 0; i < samples.Length; i++) {
				allSamples[i] = samples[i].ToFloatSample();
				ClampSample(ref allSamples[i]);
			}
		}

		internal void ReconstructFromFloatSamples(float[] allSamples) {
			if (BitsPerSample == 16) {
				byte[] newData = new byte[allSamples.Length * 2];

				for (int i = 0; i < allSamples.Length; i++) {
					ClampSample(ref allSamples[i]);

					short convert = (short)(allSamples[i] * short.MaxValue);
					byte[] temp = BitConverter.GetBytes(convert);
					newData[2 * i] = temp[0];
					newData[2 * i + 1] = temp[1];
				}

				//Echo filter can cause extra data to be given
				if (newData.Length + 44 > data.Length)
					Array.Resize(ref data, newData.Length + 44);

				Buffer.BlockCopy(newData, 0, data, 44, newData.Length);
			} else if (BitsPerSample == 24) {
				byte[] newData = new byte[allSamples.Length * 3];

				for (int i = 0; i < allSamples.Length; i++) {
					ClampSample(ref allSamples[i]);

					int convert = (int)(allSamples[i] * WavSample.MaxValue_24BitPCM);
					byte[] temp = BitConverter.GetBytes(convert);

					if (BitConverter.IsLittleEndian) {
						newData[3 * i] = temp[1];
						newData[3 * i + 1] = temp[2];
						newData[3 * i + 2] = temp[3];
					} else {
						newData[3 * i] = temp[3];
						newData[3 * i + 1] = temp[2];
						newData[3 * i + 2] = temp[1];
					}
				}

				//Echo filter can cause extra data to be given
				if (newData.Length + 44 > data.Length)
					Array.Resize(ref data, newData.Length + 44);

				Buffer.BlockCopy(newData, 0, data, 44, newData.Length);
			} else
				throw new InvalidOperationException("Attempted to process data for a bit depth that's not supported.  WAV files must use 16-bit or 24-bit PCM data");
		}

		/// <summary>
		/// Forces the sample to be within (-1, 1)
		/// </summary>
		public static void ClampSample(ref float sample) {
			if (sample <= -1)
				sample = -1 + 4e-5f;
			if (sample >= 1)
				sample = 1 - 4e-5f;
		}

		private bool disposed;

		~FormatWav() {
			Dispose(false);
			GC.SuppressFinalize(this);
		}

		public void Dispose() {
			Dispose(true);
		}

		public void Dispose(bool disposing) {
			if (!disposed) {
				if (disposing) {
					data = null;
				}

				disposed = true;
			}
		}
	}
}
