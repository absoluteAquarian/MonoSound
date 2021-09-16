using Microsoft.Xna.Framework.Audio;
using MonoSound.XACT;
using NVorbis;
using System;
using System.IO;

namespace MonoSound.Streaming{
	internal class StreamPackage : IDisposable{
		//The thing that keeps track of the sound data
		public DynamicSoundEffectInstance sfx;
		//Useless, but helps in clarifying what package is from what type of file
		public readonly StreamType type;

		//Each stream package keeps track of a separate instance of a "reader" to allow reading of the same file
		private FileStream stream;
		private VorbisReader vorbisStream;
		public bool FinishedStreaming{ get; private set; }

		public readonly int sampleRate;
		public readonly AudioChannels channels;
		public readonly short bitsPerSample;
		public readonly int totalBytes;
		private int readBytes;

		private long sampleReadStart;

		public double secondsRead;
		public double playTimeSeconds;

		public bool looping;

		private TimeSpan vorbisReadStart;

		private StreamPackage(){ }

		/// <summary>
		/// Creates a new streaming package for WAV and XNB sounds
		/// </summary>
		/// <param name="file">The destination file.</param>
		/// <param name="type">The streaming type to use</param>
		public StreamPackage(string file, StreamType type){
			if(type == StreamType.XWB)
				throw new ArgumentException("MonoSound internals error. Location: MonoSound.Streaming.StreamPackage..ctor(string, StreamType)");

			this.type = type;

			//Open the stream for use
			if(type == StreamType.WAV){
				stream = new FileStream(file, FileMode.Open);

				//Read the header
				byte[] header = new byte[44];
				stream.Read(header, 0, 44);
				channels = (AudioChannels)BitConverter.ToInt16(header, 22);
				sampleRate = BitConverter.ToInt32(header, 24);
				bitsPerSample = BitConverter.ToInt16(header, 34);
				totalBytes = BitConverter.ToInt32(header, 40);
			}else if(type == StreamType.XNB){
				stream = new FileStream(file, FileMode.Open);

				byte[] read = new byte[4];
				stream.Read(read, 0, 4);
				byte[] header = new byte[BitConverter.ToInt32(read, 0)];
				stream.Read(header, 0, header.Length);

				channels = (AudioChannels)BitConverter.ToInt16(header, 2);
				sampleRate = BitConverter.ToInt32(header, 4);
				bitsPerSample = BitConverter.ToInt16(header, 14);

				stream.Read(read, 0, 4);
				totalBytes = BitConverter.ToInt32(read, 0);
			}else if(type == StreamType.OGG){
				//VorbisReader(string) disposes the stream on close by default
				vorbisStream = new VorbisReader(file);
				channels = (AudioChannels)vorbisStream.Channels;
				sampleRate = vorbisStream.SampleRate;
				bitsPerSample = -1;
				totalBytes = -1;
			}

			PostCtor();
		}

		/// <summary>
		/// Creates a new streaming package for XWB sounds
		/// </summary>
		/// <param name="soundBank">The path to the sound bank</param>
		/// <param name="waveBank">The path to the wave bank</param>
		/// <param name="cueName">The name of the sound cue to stream</param>
		public StreamPackage(string soundBank, string waveBank, string cueName){
			type = StreamType.XWB;
			
			MonoSoundManager.VerifyThatBanksExist(soundBank, waveBank, out string sbName, out _, setStreaming: true);

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
			stream = new FileStream(waveBank, FileMode.Open);

			MonoWaveBank.StreamInfo info = wBank._streams[trackIndex];

			wBank.DecodeFormat(info.Format, out _, out int channels, out int sampleRate, out _);
			this.channels = (AudioChannels)channels;
			this.sampleRate = sampleRate;
			totalBytes = info.FileLength;
			//Wavebank data is always 16 bits per sample
			bitsPerSample = 16;

			//Move to the beginning of the sound data
			stream.Seek(info.FileOffset + wBank._playRegionOffset, SeekOrigin.Begin);

			PostCtor();
		}

		private void PostCtor(){
			sfx = new DynamicSoundEffectInstance(sampleRate, channels);
			sfx.BufferNeeded += QueueBuffers;

			if(type != StreamType.OGG)
				sampleReadStart = stream.Position;
			else
				vorbisReadStart = vorbisStream.DecodedTime;

			readBytes = 0;
			secondsRead = 0;
			playTimeSeconds = 0;

			looping = false;
		}

		public void Reset(){
			//Move the "cursor" back to the beginning and reset the counters
			if(type == StreamType.OGG)
				vorbisStream.DecodedTime = vorbisReadStart;
			else
				stream.Position = sampleReadStart;

			readBytes = 0;
			secondsRead = 0;
		}

		private void QueueBuffers(object sender, EventArgs e){
			//Read() won't add new buffers if this sound shouldn't be looping, so calling it twice here is just fine
			Read(0.1);
			Read(0.1);
		}

		public void Read(double seconds){
			//The sound has finished playing.  No need to keep trying to stream more data
			if(FinishedStreaming)
				return;

			int samplesToRead;
			byte[] read;

			//Handle OGG files separately
			if(type == StreamType.OGG){
				//Float samples = 2 bytes per sample (converted to short samples)
				samplesToRead = (int)(seconds * sampleRate * (short)channels);

				float[] vorbisRead = new float[samplesToRead];
				int readOggSamples = vorbisStream.ReadSamples(vorbisRead, 0, vorbisRead.Length);
				readBytes += readOggSamples * 2;

				read = new byte[readOggSamples * 2];

				byte[] sampleWrite;
				for(int i = 0; i < readOggSamples; i++){
					int temp = (int)(short.MaxValue * vorbisRead[i]);
					if(temp > short.MaxValue)
						temp = short.MaxValue;
					else if(temp < short.MinValue)
						temp = short.MinValue;

					sampleWrite = BitConverter.GetBytes((short)temp);

					read[i * 2] = sampleWrite[0];
					read[i * 2 + 1] = sampleWrite[1];
				}

				secondsRead += seconds;
			
				sfx.SubmitBuffer(read);

				if(readOggSamples < vorbisRead.Length)
					CheckLooping();

				return;
			}

			//Read "seconds" amount of data from the stream, then send it to "sfx"
			samplesToRead = (int)(seconds * bitsPerSample / 8 * sampleRate * (short)channels);
			if(samplesToRead + readBytes > totalBytes)
				samplesToRead = totalBytes - readBytes;

			if(samplesToRead == 0)
				throw new InvalidOperationException("MonoSound internals error: Streamed audio requested an invalid amount of samples to read" +
					$"\nObject state: {sampleRate}Hz | {channels} | {readBytes / bitsPerSample * 8 / (int)channels} out of {totalBytes / bitsPerSample * 8 / (int)channels} samples read");

			read = new byte[samplesToRead];
			readBytes += stream.Read(read, 0, samplesToRead);

			secondsRead += seconds;
			
			sfx.SubmitBuffer(read);

			if(readBytes >= totalBytes)
				CheckLooping();
		}

		private void CheckLooping(){
			if(!looping){
				FinishedStreaming = true;

				stream?.Close();
				stream?.Dispose();

				vorbisStream?.Dispose();
			}else
				Reset();
		}

		private bool disposed;
		public bool Disposed => disposed;

		~StreamPackage() => Dispose(false);

		public void Dispose(){
			Dispose(true);
		}

		private void Dispose(bool disposing){
			if(!disposed){
				disposed = true;

				if(disposing){
					try{
						sfx.Stop(immediate: true);
						sfx.Dispose();
					}catch(NoAudioHardwareException){
						//Exception can be thrown during the final stages of an app closing.  Just ignore it
					}

					stream?.Close();
					stream?.Dispose();

					vorbisStream?.Dispose();

					FinishedStreaming = true;
				}

				sfx = null;
				stream = null;
				vorbisStream = null;
			}
		}
	}
}
