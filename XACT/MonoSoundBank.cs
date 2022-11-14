using Microsoft.Xna.Framework;
using MonoSound.Audio;
using System;
using System.Collections.Generic;
using System.IO;

namespace MonoSound.XACT {
	internal class MonoSoundBank {
		internal class WaveBankTable {
			internal readonly MonoSoundBank _soundBank;
			internal MonoWaveBank[] _waveBanks, _streamedWaveBanks;

			public WaveBankTable(MonoSoundBank soundBank, uint count) {
				_soundBank = soundBank;
				_waveBanks = new MonoWaveBank[count];
				_streamedWaveBanks = new MonoWaveBank[count];
			}

			public MonoWaveBank Get(int index, bool streaming) {
				var wBank = (!streaming ? _waveBanks : _streamedWaveBanks)[index];

				if (wBank is null) {
					string bankName = _soundBank._waveBankNames[index];

					if (!MonoSoundLibrary.waveBanks.TryGetValue(bankName, out wBank) && !MonoSoundLibrary.streamedWaveBanks.TryGetValue(bankName, out wBank))
						throw new Exception("The wave bank '" + bankName + "' was not found!");

					Set(index, wBank);
				}

				return wBank;
			}

			public void Set(int index, MonoWaveBank bank) => (!bank.IsStreaming ? _waveBanks : _streamedWaveBanks)[index] = bank;
		}

		//The only thing I really need this class for is the names of the sounds used
		//Therefore, that's all this will do.  Any extra data read during initialization will be discarded
		//Furthermore, the use of XNA's XactSound would be utterly pointless.  MonoSound only cares about saving the sound data as FormatWav

		internal string[] _waveBankNames;
		internal WaveBankTable _waveBanks;

		readonly Dictionary<string, MonoXactSound> _sounds = new Dictionary<string, MonoXactSound>();

		private MonoSoundBank() { }

		public static MonoSoundBank FromXNA(string file) {
			if (string.IsNullOrWhiteSpace(file))
				throw new ArgumentNullException("file");

			return FromXNA(TitleContainer.OpenStream(file));
		}

		public static MonoSoundBank FromXNA(Stream stream) {
			MonoSoundBank bank = new MonoSoundBank();

			using (BinaryReader reader = new BinaryReader(stream)) {
				//Just read stuff until we have enough to satisfy grabbing the sounds
				uint magic = reader.ReadUInt32();
				if (magic != 0x4B424453) //"SDBK"
					throw new Exception("Bad soundbank format");

				reader.ReadUInt16(); // toolVersion

				uint formatVersion = reader.ReadUInt16();
				if (formatVersion != 43)
					throw new Exception($"SoundBank format {formatVersion} not supported.");

				reader.ReadUInt16(); // crc, TODO: Verify crc (FCS16)

				reader.ReadUInt32(); // lastModifiedLow
				reader.ReadUInt32(); // lastModifiedHigh
				reader.ReadByte(); // platform ???

				uint numSimpleCues = reader.ReadUInt16();
				if (numSimpleCues == 0)
					throw new Exception($"No simple sounds were found in the Sound Bank");

				uint numComplexCues = reader.ReadUInt16();
				if (numComplexCues > 0)
					throw new Exception($"MonoSound does not support complex Sound Bank sounds");

				reader.ReadUInt16(); //unkn
				reader.ReadUInt16(); // numTotalCues
				uint numWaveBanks = reader.ReadByte();
				reader.ReadUInt16(); // numSounds
				uint cueNameTableLen = reader.ReadUInt16();
				reader.ReadUInt16(); //unkn

				uint simpleCuesOffset = reader.ReadUInt32();
				reader.ReadUInt32(); //unkn
				uint cueNamesOffset = reader.ReadUInt32();
				reader.ReadUInt32(); //unkn
				reader.ReadUInt32(); // variationTablesOffset
				reader.ReadUInt32(); //unkn
				uint waveBankNameTableOffset = reader.ReadUInt32();
				reader.ReadUInt32(); // cueNameHashTableOffset
				reader.ReadUInt32(); // cueNameHashValsOffset
				reader.ReadUInt32(); // soundsOffset

				//name = System.Text.Encoding.UTF8.GetString(soundbankreader.ReadBytes(64),0,64).Replace("\0","");

				//parse wave bank name table
				stream.Seek(waveBankNameTableOffset, SeekOrigin.Begin);
				bank._waveBanks = new WaveBankTable(bank, numWaveBanks);
				bank._waveBankNames = new string[numWaveBanks];
				for (int i = 0; i < numWaveBanks; i++)
					bank._waveBankNames[i] = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(64), 0, 64).Replace("\0", "");

				//parse cue name table
				stream.Seek(cueNamesOffset, SeekOrigin.Begin);
				string[] cueNames = System.Text.Encoding.UTF8.GetString(reader.ReadBytes((int)cueNameTableLen), 0, (int)cueNameTableLen).Split('\0');

				//MonoSound: only simple sounds should be here
				// Simple cues
				if (numSimpleCues > 0) {
					stream.Seek(simpleCuesOffset, SeekOrigin.Begin);
					for (int i = 0; i < numSimpleCues; i++) {
						reader.ReadByte(); // flags
						uint soundOffset = reader.ReadUInt32();

						var oldPosition = stream.Position;
						stream.Seek(soundOffset, SeekOrigin.Begin);
						MonoXactSound sound = new MonoXactSound(bank, reader);
						stream.Seek(oldPosition, SeekOrigin.Begin);

						bank._sounds.Add(cueNames[i], sound);
					}
				}

				//Complex cues ignored
			}

			return bank;
		}

		public FormatWav GetAudio(int waveBankIndex, int trackIndex) {
			var bank = _waveBanks.Get(waveBankIndex, streaming: false);

			if (bank == null) {
				var name = _waveBankNames[waveBankIndex];

				if (!MonoSoundLibrary.waveBanks.TryGetValue(name, out bank) && !MonoSoundLibrary.streamedWaveBanks.TryGetValue(name, out bank))
					throw new Exception("The wave bank '" + name + "' was not found!");

				_waveBanks.Set(waveBankIndex, bank);
			}

			return bank.GetAudio(trackIndex);
		}

		public FormatWav GetAudio(string name) {
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentNullException("name");

			if (!_sounds.TryGetValue(name, out MonoXactSound sound))
				throw new ArgumentException($"The sound bank did not contain a cue named \"{name}\"", "name");

			return sound.GetAudio();
		}

		public void GetInfo(string cueName, out int waveBankIndex, out int trackIndex) {
			if (string.IsNullOrWhiteSpace(cueName))
				throw new ArgumentNullException("cueName");

			if (!_sounds.TryGetValue(cueName, out MonoXactSound sound))
				throw new ArgumentException($"The sound bank did not contain a cue named \"{cueName}\"", "cueName");

			waveBankIndex = sound._waveBankIndex;
			trackIndex = sound._trackIndex;
		}
	}
}
