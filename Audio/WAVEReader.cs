using Microsoft.Xna.Framework.Audio;
using System;
using System.IO;
using System.Text;

namespace MonoSound.Audio {
	/// <summary>
	/// A wrapper around <see cref="Stream"/> for reading .wav files
	/// </summary>
	public class WAVEReader : StreamReader {
		private const int MAGIC_RIFF = 0x46464952;  // ASCII for "RIFF" in little-endian
		private const int MAGIC_WAVE = 0x45564157;  // ASCII for "WAVE" in little-endian

		/// <summary>
		/// Creates a new <see cref="WAVEReader"/> from a <see cref="Stream"/>
		/// </summary>
		/// <param name="stream">The <see cref="Stream"/> to wrap</param>
		public WAVEReader(Stream stream) : base(stream, detectEncodingFromByteOrderMarks: false, leaveOpen: true) {  // Important!  The stream must be left open so that the caller can read the sample data afterwards
			// Verify that the file header is correct
			Span<byte> header = stackalloc byte[4];
			int bytes = BaseStream.Read(header);

			if (bytes != 4 || BitConverter.ToInt32(header) != MAGIC_RIFF)
				throw new InvalidDataException("File is not a RIFF file");

			// Skip the file size
			BaseStream.Seek(4, SeekOrigin.Current);

			bytes = BaseStream.Read(header);

			if (bytes != 4 || BitConverter.ToInt32(header) != MAGIC_WAVE)
				throw new InvalidDataException("File is not a WAVE file");
		}

		/// <summary>
		/// Parses the "fmt " subchunk of the WAVE file, skipping any other subchunks that may be present
		/// </summary>
		/// <param name="channels">The channel count</param>
		/// <param name="sampleRate">The sample rate</param>
		/// <param name="byteRate">The number of bytes read per second</param>
		/// <param name="blockAlign">The size of one sample in bytes</param>
		/// <param name="bitsPerSample">The number of bits per seample</param>
		/// <exception cref="InvalidDataException"/>
		/// <exception cref="EndOfStreamException"/>
		/// <returns>The offset of the "fmt " subchunk in the WAVE file</returns>
		public int ReadFormat(out AudioChannels channels, out int sampleRate, out int byteRate, out short blockAlign, out short bitsPerSample) {
			// Skip forward until we find the "fmt " subchunk
			string subchunkID;
			int subchunkSize;
			
		ReadSubchunk:
			int offset = (int)BaseStream.Position;
			ReadSubchunkHeader(out subchunkID, out subchunkSize);
			if (subchunkID != "fmt ") {
				// Skip the subchunk data
				BaseStream.Seek(subchunkSize, SeekOrigin.Current);
				goto ReadSubchunk;
			}

			if (subchunkSize != 16)
				throw new InvalidDataException($"Subchunk size is {subchunkSize}, expected 16");

			// Read the format
			Span<byte> format2 = stackalloc byte[2];
			int bytes = BaseStream.Read(format2);
			if (bytes != 2)
				throw new EndOfStreamException("Failed to read audio format");
			if (BitConverter.ToInt16(format2) != 1)
				throw new InvalidDataException("MonoSound does not support compressed WAVE audio formats");

			// Read the channel count
			bytes = BaseStream.Read(format2);
			if (bytes != 2)
				throw new EndOfStreamException("Failed to read channel count");

			channels = (AudioChannels)BitConverter.ToInt16(format2);
			if (channels != AudioChannels.Mono && channels != AudioChannels.Stereo)
				throw new InvalidDataException("MonoSound only supports Mono and Stereo audio");

			// Read the sample rate
			Span<byte> format4 = stackalloc byte[4];
			bytes = BaseStream.Read(format4);
			if (bytes != 4)
				throw new EndOfStreamException("Failed to read sample rate");

			sampleRate = BitConverter.ToInt32(format4);
			if (sampleRate < 8000 || sampleRate > 48000)
				throw new InvalidDataException("MonoSound only supports sample rates between 8000 and 48000 Hz");

			// Read the rest of the header since they rely on the bits per sample being read, and that's the last field
			bytes = BaseStream.Read(format4);
			if (bytes != 4)
				throw new EndOfStreamException("Failed to read byte rate");

			byteRate = BitConverter.ToInt32(format4);

			bytes = BaseStream.Read(format2);
			if (bytes != 2)
				throw new EndOfStreamException("Failed to read block align");

			blockAlign = BitConverter.ToInt16(format2);

			bytes = BaseStream.Read(format2);
			if (bytes != 2)
				throw new EndOfStreamException("Failed to read bits per sample");

			bitsPerSample = BitConverter.ToInt16(format2);
			if (bitsPerSample != 16 && bitsPerSample != 24)
				throw new InvalidDataException("MonoSound only supports 16-bit and 24-bit PCM audio");

			int bytesPerSample = bitsPerSample / 8;

			// Check the byte rate and block align
			int expectedByteRate = sampleRate * (int)channels * bytesPerSample;
			if (byteRate != expectedByteRate)
				throw new InvalidDataException($"Byte rate is {byteRate}, expected {expectedByteRate}");

			int expectedBlockAlign = (int)channels * bytesPerSample;
			if (blockAlign != expectedBlockAlign)
				throw new InvalidDataException($"Block align is {blockAlign}, expected {expectedBlockAlign}");

			return offset;
		}

		/// <summary>
		/// Parses the header for the "data" subchunk of the WAVE file, skipping any other subchunks that may be present
		/// </summary>
		/// <param name="dataSize">The size of the sample data</param>
		/// <exception cref="InvalidDataException"/>
		/// <returns>The offset of the "data" subchunk in the WAVE file</returns>
		public int ReadDataHeader(out int dataSize) {
		ReadSubchunk:
			int offset = (int)BaseStream.Position;
			ReadSubchunkHeader(out string subchunkID, out int subchunkSize);
			if (subchunkID != "data") {
				// Skip the subchunk data
				BaseStream.Seek(subchunkSize, SeekOrigin.Current);
				goto ReadSubchunk;
			}

			dataSize = subchunkSize;

			return offset;
		}

		/// <summary>
		/// Reads the header for the next subchunk in the stream
		/// </summary>
		/// <param name="subchunkID">The name of the subchunk</param>
		/// <param name="subchunkSize">The size of the subchunk in bytes</param>
		/// <exception cref="EndOfStreamException"/>
		public void ReadSubchunkHeader(out string subchunkID, out int subchunkSize) {
			Span<byte> header = stackalloc byte[4];
			int bytes = BaseStream.Read(header);

			if (bytes != 4)
				throw new EndOfStreamException("Failed to read subchunk ID");

			subchunkID = Encoding.ASCII.GetString(header);

			// Coincidentally, both the ID and subchunk length are 4 bytes long
			bytes = BaseStream.Read(header);

			if (bytes != 4)
				throw new EndOfStreamException("Failed to read subchunk size");

			subchunkSize = BitConverter.ToInt32(header);
		}
	}
}
