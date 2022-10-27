using Microsoft.Xna.Framework;
using MonoSound.Audio;
using System;
using System.IO;

namespace MonoSound.XACT {
	public enum MiniFormatTag {
		Pcm = 0x0,
		Xma = 0x1,
		Adpcm = 0x2,
		Wma = 0x3,

		// We allow XMA to be reused for a platform specific format.
		PlatformSpecific = Xma,
	}

	internal class MonoWaveBank {
		//Almost an identical copy to Microsoft.Xna.Framework.Audio.WaveBank
		//Code can be found at https://github.com/MonoGame/MonoGame/blob/master/MonoGame.Framework/Audio/Xact/WaveBank.cs

		//These arrays used to be 'private', but that's unnecessary for MonoSound
		internal FormatWav[] _sounds;
		internal StreamInfo[] _streams;

		private int _version;
		internal int _playRegionOffset;

		private string _waveBankFileName;

		private bool _streaming;

		/// <summary>
		/// Whether this bank is streaming its sound data or has the data already loaded
		/// </summary>
		public bool IsStreaming => _streaming;

		struct Segment {
			public int Offset;
			public int Length;
		}

		struct WaveBankHeader {
			public int Version;
			public Segment[] Segments;
		}

		struct WaveBankData {
			public int Flags;                                // Bank flags
			public int EntryCount;                           // Number of entries in the bank
			public string BankName;                          // Bank friendly name
			public int EntryMetaDataElementSize;             // Size of each entry meta-data element, in bytes
			public int EntryNameElementSize;                 // Size of each entry name element, in bytes
			public int Alignment;                            // Entry alignment, in bytes
			public int CompactFormat;                        // Format data for compact bank
			public int BuildTime;                            // Build timestamp
		}

		internal struct StreamInfo {
			public int Format;
			public int FileOffset;
			public int FileLength;
			public int LoopStart;
			public int LoopLength;
		}

		private const int Flag_Compact = 0x00020000; // Bank uses compact format

		private MonoWaveBank() { }

		[Obsolete("Use MonoWaveBank.LoadFromXWBFile instead", error: true)]
		public static MonoWaveBank FromXNA(string file, bool streaming = false) => LoadFromXWBFile(file, streaming);

		/// <summary>
		/// Loads a wavebank from an .xwb file or returns the existing instance if it already exists
		/// </summary>
		/// <param name="file">The absolute or relative path to the file to read from</param>
		/// <param name="streaming">Whether the bank should be opened for streaming</param>
		public static MonoWaveBank LoadFromXWBFile(string file, bool streaming = false) => LoadFromXWBStream(TitleContainer.OpenStream(file), file, streaming);

		[Obsolete("Use MonoWaveBank.LoadFromXWBStream instead", error: true)]
		public static MonoWaveBank FromXNA(Stream stream, string fileName, bool streaming = false) => LoadFromXWBStream(stream, fileName, streaming);

		/// <summary>
		/// Loads a wavebank from a data stream or returns the existing instance if it already exists
		/// </summary>
		/// <param name="stream">The data stream to read from</param>
		/// <param name="fileName">The identifier for the wavebank</param>
		/// <param name="streaming">Whether the bank should be opened for streaming</param>
		public static MonoWaveBank LoadFromXWBStream(Stream stream, string fileName, bool streaming = false) {
			if (!streaming && MonoSound.waveBanks.TryGetValue(fileName, out var bank)) {
				// Wavebank was already loaded
				return bank;
			}

			if (streaming && MonoSound.streamedWaveBanks.TryGetValue(fileName, out bank)) {
				// Wavebank was already loaded
				return bank;
			}

			//Any of the metadata the wavebank would use is irrelevant; we only want the sound effect data
			MonoWaveBank ret = new MonoWaveBank() {
				_waveBankFileName = fileName,
				_streaming = streaming
			};

			//Streaming banks aren't allowed, since we want to grab the data in one go
			WaveBankHeader wavebankheader;
			WaveBankData wavebankdata;

			wavebankdata.EntryNameElementSize = 0;
			wavebankdata.CompactFormat = 0;
			wavebankdata.Alignment = 0;
			wavebankdata.BuildTime = 0;

			int wavebank_offset = 0;

			BinaryReader reader = new BinaryReader(stream);

			reader.ReadBytes(4);

			ret._version = wavebankheader.Version = reader.ReadInt32();

			int last_segment = 4;
			if (wavebankheader.Version <= 3)
				last_segment = 3;
			if (wavebankheader.Version >= 42)
				reader.ReadInt32();    // skip HeaderVersion

			wavebankheader.Segments = new Segment[5];

			for (int i = 0; i <= last_segment; i++) {
				wavebankheader.Segments[i].Offset = reader.ReadInt32();
				wavebankheader.Segments[i].Length = reader.ReadInt32();
			}

			reader.BaseStream.Seek(wavebankheader.Segments[0].Offset, SeekOrigin.Begin);

			//WAVEBANKDATA:

			wavebankdata.Flags = reader.ReadInt32();
			wavebankdata.EntryCount = reader.ReadInt32();

			if ((wavebankheader.Version == 2) || (wavebankheader.Version == 3))
				wavebankdata.BankName = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(16), 0, 16).Replace("\0", "");
			else
				wavebankdata.BankName = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(64), 0, 64).Replace("\0", "");

			if (wavebankheader.Version == 1) {
				//wavebank_offset = (int)ftell(fd) - file_offset;
				wavebankdata.EntryMetaDataElementSize = 20;
			} else {
				wavebankdata.EntryMetaDataElementSize = reader.ReadInt32();
				wavebankdata.EntryNameElementSize = reader.ReadInt32();
				wavebankdata.Alignment = reader.ReadInt32();
				wavebank_offset = wavebankheader.Segments[1].Offset; //METADATASEGMENT
			}

			if ((wavebankdata.Flags & Flag_Compact) != 0)
				reader.ReadInt32(); // compact_format

			ret._playRegionOffset = wavebankheader.Segments[last_segment].Offset;
			if (ret._playRegionOffset == 0) {
				ret._playRegionOffset = wavebank_offset + wavebankdata.EntryCount * wavebankdata.EntryMetaDataElementSize;
			}

			int segidx_entry_name = 2;
			if (wavebankheader.Version >= 42) segidx_entry_name = 3;

			if (wavebankheader.Segments[segidx_entry_name].Offset != 0 && wavebankheader.Segments[segidx_entry_name].Length != 0) {
				if (wavebankdata.EntryNameElementSize == -1) wavebankdata.EntryNameElementSize = 0;
				byte[] entry_name = new byte[wavebankdata.EntryNameElementSize + 1];
				entry_name[wavebankdata.EntryNameElementSize] = 0;
			}

			ret._sounds = new FormatWav[wavebankdata.EntryCount];
			ret._streams = new StreamInfo[wavebankdata.EntryCount];

			reader.BaseStream.Seek(wavebank_offset, SeekOrigin.Begin);

			// The compact format requires us to load stuff differently.
			var isCompactFormat = (wavebankdata.Flags & Flag_Compact) != 0;
			if (isCompactFormat) {
				// Load the sound data offset table from disk.
				for (var i = 0; i < wavebankdata.EntryCount; i++) {
					var len = reader.ReadInt32();
					ret._streams[i].Format = wavebankdata.CompactFormat;
					ret._streams[i].FileOffset = (len & ((1 << 21) - 1)) * wavebankdata.Alignment;
				}

				// Now figure out the sound data lengths.
				for (var i = 0; i < wavebankdata.EntryCount; i++) {
					int nextOffset;
					if (i == (wavebankdata.EntryCount - 1))
						nextOffset = wavebankheader.Segments[last_segment].Length;
					else
						nextOffset = ret._streams[i + 1].FileOffset;

					// The next and current offsets used to calculate the length.
					ret._streams[i].FileLength = nextOffset - ret._streams[i].FileOffset;
				}
			} else {
				for (var i = 0; i < wavebankdata.EntryCount; i++) {
					var info = new StreamInfo();
					if (wavebankheader.Version == 1) {
						info.Format = reader.ReadInt32();
						info.FileOffset = reader.ReadInt32();
						info.FileLength = reader.ReadInt32();
						info.LoopStart = reader.ReadInt32();
						info.LoopLength = reader.ReadInt32();
					} else {
						reader.ReadInt32(); // Unused

						if (wavebankdata.EntryMetaDataElementSize >= 8)
							info.Format = reader.ReadInt32();
						if (wavebankdata.EntryMetaDataElementSize >= 12)
							info.FileOffset = reader.ReadInt32();
						if (wavebankdata.EntryMetaDataElementSize >= 16)
							info.FileLength = reader.ReadInt32();
						if (wavebankdata.EntryMetaDataElementSize >= 20)
							info.LoopStart = reader.ReadInt32();
						if (wavebankdata.EntryMetaDataElementSize >= 24)
							info.LoopLength = reader.ReadInt32();
					}

					if (wavebankdata.EntryMetaDataElementSize < 24) {
						if (info.FileLength != 0)
							info.FileLength = wavebankheader.Segments[last_segment].Length;
					}

					ret._streams[i] = info;
				}
			}

			//If this wavebank should be for streaming ONLY, then don't load the sound data and keep the StreamInfo data intact
			if (!streaming) {
				for (var i = 0; i < ret._streams.Length; i++) {
					var info = ret._streams[i];

					// Read the data.
					reader.BaseStream.Seek(info.FileOffset + ret._playRegionOffset, SeekOrigin.Begin);
					var audiodata = reader.ReadBytes(info.FileLength);

					// Decode the format information.
					ret.DecodeFormat(info.Format, out MiniFormatTag codec, out int channels, out int rate, out int alignment);

					// Call the special constuctor on SoundEffect to sort it out.
					//MonoSound: hijack the call that would be here and instead read the data into a FormatWav
					//Also, lucky us, the sound data is right there for the taking!    

					ret._sounds[i] = FormatWav.FromSoundEffectConstructor(codec, audiodata, channels, rate, alignment, info.LoopStart, info.LoopLength);
				}

				ret._streams = null;
			}

			//No need to leave the reader open, since streaming will be handled elsewhere
			reader.Close();
			reader.Dispose();

			(!streaming ? MonoSound.waveBanks : MonoSound.streamedWaveBanks)[ret._waveBankFileName] = ret;

			return ret;
		}

		internal void DecodeFormat(int format, out MiniFormatTag codec, out int channels, out int rate, out int alignment) {
			if (_version == 1) {
				// I'm not 100% sure if the following is correct
				// version 1:
				// 1 00000000 000101011000100010 0 001 0
				// | |         |                 | |   |
				// | |         |                 | |   wFormatTag
				// | |         |                 | nChannels
				// | |         |                 ???
				// | |         nSamplesPerSec
				// | wBlockAlign
				// wBitsPerSample

				codec = (MiniFormatTag)((format) & ((1 << 1) - 1));
				channels = (format >> (1)) & ((1 << 3) - 1);
				rate = (format >> (1 + 3 + 1)) & ((1 << 18) - 1);
				alignment = (format >> (1 + 3 + 1 + 18)) & ((1 << 8) - 1);
				//bits = (format >> (1 + 3 + 1 + 18 + 8)) & ((1 << 1) - 1);

				/*} else if(wavebankheader.dwVersion == 23) { // I'm not 100% sure if the following is correct
					// version 23:
					// 1000000000 001011101110000000 001 1
					// | |        |                  |   |
					// | |        |                  |   ???
					// | |        |                  nChannels?
					// | |        nSamplesPerSec
					// | ???
					// !!!UNKNOWN FORMAT!!!
					//codec = -1;
					//chans = (wavebankentry.Format >>  1) & ((1 <<  3) - 1);
					//rate  = (wavebankentry.Format >>  4) & ((1 << 18) - 1);
					//bits  = (wavebankentry.Format >> 31) & ((1 <<  1) - 1);
					codec = (wavebankentry.Format                    ) & ((1 <<  1) - 1);
					chans = (wavebankentry.Format >> (1)             ) & ((1 <<  3) - 1);
					rate  = (wavebankentry.Format >> (1 + 3)         ) & ((1 << 18) - 1);
					align = (wavebankentry.Format >> (1 + 3 + 18)    ) & ((1 <<  9) - 1);
					bits  = (wavebankentry.Format >> (1 + 3 + 18 + 9)) & ((1 <<  1) - 1); */

			} else {
				// 0 00000000 000111110100000000 010 01
				// | |        |                  |   |
				// | |        |                  |   wFormatTag
				// | |        |                  nChannels
				// | |        nSamplesPerSec
				// | wBlockAlign
				// wBitsPerSample

				codec = (MiniFormatTag)((format) & ((1 << 2) - 1));
				channels = (format >> (2)) & ((1 << 3) - 1);
				rate = (format >> (2 + 3)) & ((1 << 18) - 1);
				alignment = (format >> (2 + 3 + 18)) & ((1 << 8) - 1);
				//bits = (info.Format >> (2 + 3 + 18 + 8)) & ((1 << 1) - 1);
			}
		}

		public FormatWav GetAudio(int trackIndex) {
			if (_streaming)
				throw new InvalidOperationException($"The requested wave bank has been initialized for streaming only: {_waveBankFileName}");

			return _sounds[trackIndex];
		}
	}
}
