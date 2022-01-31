using Microsoft.Xna.Framework.Audio;
using MonoSound.XACT;
using System;
using System.IO;

namespace MonoSound.Streaming{
	internal class WavStream : StreamPackage{
		public WavStream(string file) : base(File.OpenRead(file), AudioType.WAV){ }

		public WavStream(Stream stream) : base(stream, AudioType.WAV){ }

		protected WavStream(string file, AudioType typeOverride) : base(File.OpenRead(file), typeOverride){ }

		protected WavStream(Stream stream, AudioType typeOverride) : base(stream, typeOverride){ }

		protected override void Initialize(){
			//Read the header
			byte[] header = new byte[44];
			underlyingStream.Read(header, 0, 44);
			Channels = (AudioChannels)BitConverter.ToInt16(header, 22);
			SampleRate = BitConverter.ToInt32(header, 24);
			BitsPerSample = BitConverter.ToInt16(header, 34);
			TotalBytes = BitConverter.ToInt32(header, 40);

			sampleReadStart = underlyingStream.Position;
		}

		public override void ReadSamples(double seconds, out byte[] samples, out int bytesRead, out bool endOfStream){
			int samplesToRead = (int)(seconds * BitsPerSample / 8 * SampleRate * (short)Channels);

			if(samplesToRead == 0){
				string suffix = TotalBytes == -1 ? "" : $" out of {TotalBytes / BitsPerSample * 8 / (int)Channels}";

				throw new InvalidOperationException("MonoSound internals error: Streamed audio requested an invalid amount of samples to read" +
					$"\nObject state: {SampleRate}Hz | {Channels} | {ReadBytes / BitsPerSample * 8 / (int)Channels}{suffix} samples read");
			}

			samples = new byte[samplesToRead];
			bytesRead = underlyingStream.Read(samples, 0, samplesToRead);

			endOfStream = samplesToRead < samples.Length;
		}
	}

	internal class XnbStream : WavStream{
		public XnbStream(string file) : base(file, AudioType.XNB){ }

		public XnbStream(Stream stream) : base(stream, AudioType.XNB){ }

		protected override void Initialize(){
			byte[] read = new byte[4];
			underlyingStream.Read(read, 0, 4);
			byte[] header = new byte[BitConverter.ToInt32(read, 0)];
			underlyingStream.Read(header, 0, header.Length);

			Channels = (AudioChannels)BitConverter.ToInt16(header, 2);
			SampleRate = BitConverter.ToInt32(header, 4);
			BitsPerSample = BitConverter.ToInt16(header, 14);

			underlyingStream.Read(read, 0, 4);
			TotalBytes = BitConverter.ToInt32(read, 0);
		}
	}

	internal class Mp3Stream : WavStream{
		public Mp3Stream(string file) : base(new MP3Sharp.MP3Stream(file), AudioType.MP3){ }

		public Mp3Stream(Stream stream) : base(new MP3Sharp.MP3Stream(stream), AudioType.MP3){ }

		protected override void Initialize(){
			MP3Sharp.MP3Stream mp3Stream = underlyingStream as MP3Sharp.MP3Stream;

			Channels = AudioChannels.Stereo;  //MP3 decoder will double mono samples to force the audio to play as "stereo"
			SampleRate = mp3Stream.Frequency;
			BitsPerSample = (short)(mp3Stream.Format == MP3Sharp.SoundFormat.Pcm16BitMono ? 8 : 16);
			TotalBytes = -1;
		}
	}

	internal class OggStream : StreamPackage{
		private NVorbis.VorbisReader vorbisStream;
		private TimeSpan vorbisReadStart;

		public OggStream(string file) : base(AudioType.OGG){
			//VorbisReader(string) closes the stream on dispose by default
			vorbisStream = new NVorbis.VorbisReader(file);

			Initialize();
		}

		public OggStream(Stream stream) : base(AudioType.OGG){
			vorbisStream = new NVorbis.VorbisReader(stream, closeStreamOnDispose: true);

			Initialize();
		}

		protected override void Initialize(){
			Channels = (AudioChannels)vorbisStream.Channels;
			SampleRate = vorbisStream.SampleRate;
			BitsPerSample = -1;
			TotalBytes = -1;

			vorbisReadStart = vorbisStream.DecodedTime;
		}

		public override void ReadSamples(double seconds, out byte[] samples, out int bytesRead, out bool endOfStream){
			//Float samples = 2 bytes per sample (converted to short samples)
			int samplesToRead = (int)(seconds * SampleRate * (short)Channels);

			float[] vorbisRead = new float[samplesToRead];
			int readOggSamples = vorbisStream.ReadSamples(vorbisRead, 0, vorbisRead.Length);
			bytesRead = readOggSamples * 2;

			samples = new byte[readOggSamples * 2];

			byte[] sampleWrite;
			for(int i = 0; i < readOggSamples; i++){
				int temp = (int)(short.MaxValue * vorbisRead[i]);
				if(temp > short.MaxValue)
					temp = short.MaxValue;
				else if(temp < short.MinValue)
					temp = short.MinValue;

				sampleWrite = BitConverter.GetBytes((short)temp);

				samples[i * 2] = sampleWrite[0];
				samples[i * 2 + 1] = sampleWrite[1];
			}

			endOfStream = readOggSamples < vorbisRead.Length;
		}

		public override void Reset(){
			vorbisStream.DecodedTime = vorbisReadStart;

			base.Reset();
		}

		protected override void ChildDispose(bool disposing){
			if(disposing)
				vorbisStream?.Dispose();

			vorbisStream = null;
		}
	}

	internal class WavebankStream : StreamPackage{
		/// <summary>
		/// Creates a new streaming package for XWB sounds
		/// </summary>
		/// <param name="soundBank">The path to the sound bank</param>
		/// <param name="waveBank">The path to the wave bank</param>
		/// <param name="cueName">The name of the sound cue to stream</param>
		public WavebankStream(string soundBank, string waveBank, string cueName) : base(AudioType.XWB){
			MonoSoundManager.VerifyThatBanksExistInDictionary(soundBank, waveBank, out string sbName, out _, setStreaming: true);

			//Go to the location where this sound's data is stored
			var sBank = MonoSoundManager.soundBanks[sbName];

			sBank.GetInfo(cueName, out int wbIndex, out int trackIndex);

			var wBank = sBank._waveBanks[wbIndex];
			if(wBank is null){
				string bankName = sBank._waveBankNames[wbIndex];

				if(!MonoSoundManager.waveBanks.TryGetValue(bankName, out wBank))
					throw new Exception("The wave bank '" + bankName + "' was not found!");

				sBank._waveBanks[wbIndex] = wBank;
			}

			//Open a stream on the wavebank file and move to the track's location, then follow how the sound is read so that "stream" ends up at the start of the sound data
			underlyingStream = new FileStream(waveBank, FileMode.Open);

			MonoWaveBank.StreamInfo info = wBank._streams[trackIndex];

			wBank.DecodeFormat(info.Format, out _, out int channels, out int sampleRate, out _);
			Channels = (AudioChannels)channels;
			SampleRate = sampleRate;
			TotalBytes = info.FileLength;
			//Wavebank data is always 16 bits per sample
			BitsPerSample = 16;

			//Move to the beginning of the sound data
			underlyingStream.Seek(info.FileOffset + wBank._playRegionOffset, SeekOrigin.Begin);
		}

		/// <summary>
		/// Creates a new streaming package for XWB sounds
		/// </summary>
		/// <param name="soundBank">The stream representing the sound bank</param>
		/// <param name="soundBankIdentifier">The path to the sound bank</param>
		/// <param name="waveBank">The stream representing the wave bank</param>
		/// <param name="waveBankIdentifier">The path to the wave bank</param>
		/// <param name="cueName">The name of the sound cue to stream</param>
		public WavebankStream(Stream soundBank, string soundBankIdentifier, Stream waveBank, string waveBankIdentifier, string cueName) : base(AudioType.XWB){
			MonoSoundManager.VerifyThatBanksExistInDictionary(soundBank, soundBankIdentifier, waveBank, waveBankIdentifier, out string sbName, out _, setStreaming: true);

			//Go to the location where this sound's data is stored
			var sBank = MonoSoundManager.soundBanks[sbName];

			sBank.GetInfo(cueName, out int wbIndex, out int trackIndex);

			var wBank = sBank._waveBanks[wbIndex];
			if(wBank is null){
				string bankName = sBank._waveBankNames[wbIndex];

				if(!MonoSoundManager.waveBanks.TryGetValue(bankName, out wBank))
					throw new Exception("The wave bank '" + bankName + "' was not found!");

				sBank._waveBanks[wbIndex] = wBank;
			}

			//Move to the track's location, then follow how the sound is read so that "stream" ends up at the start of the sound data
			underlyingStream = waveBank;

			MonoWaveBank.StreamInfo info = wBank._streams[trackIndex];

			wBank.DecodeFormat(info.Format, out _, out int channels, out int sampleRate, out _);
			Channels = (AudioChannels)channels;
			SampleRate = sampleRate;
			TotalBytes = info.FileLength;
			//Wavebank data is always 16 bits per sample
			BitsPerSample = 16;

			//Move to the beginning of the sound data
			underlyingStream.Seek(info.FileOffset + wBank._playRegionOffset, SeekOrigin.Begin);
		}

		public override void ReadSamples(double seconds, out byte[] samples, out int bytesRead, out bool endOfStream){
			int samplesToRead = (int)(seconds * BitsPerSample / 8 * SampleRate * (short)Channels);

			if(samplesToRead == 0){
				string suffix = TotalBytes == -1 ? "" : $" out of {TotalBytes / BitsPerSample * 8 / (int)Channels}";

				throw new InvalidOperationException("MonoSound internals error: Streamed audio requested an invalid amount of samples to read" +
					$"\nObject state: {SampleRate}Hz | {Channels} | {ReadBytes / BitsPerSample * 8 / (int)Channels}{suffix} samples read");
			}

			samples = new byte[samplesToRead];
			bytesRead = underlyingStream.Read(samples, 0, samplesToRead);

			endOfStream = samplesToRead < samples.Length;
		}
	}
}
