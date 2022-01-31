using Microsoft.Xna.Framework.Audio;
using System;
using System.IO;

namespace MonoSound.Streaming{
	internal abstract class StreamPackage : IDisposable{
		//The thing that keeps track of the sound data
		public DynamicSoundEffectInstance sfx;
		//Useless, but helps in clarifying what package is from what type of file
		public readonly AudioType type;

		//Each stream package keeps track of a separate instance of a "reader" to allow reading of the same file
		protected Stream underlyingStream;
		public bool FinishedStreaming{ get; private set; }

		public int SampleRate{ get; protected set; }
		public AudioChannels Channels{ get; protected set; }
		public short BitsPerSample{ get; protected set; }
		public int TotalBytes{ get; protected set; }
		public int ReadBytes{ get; private set; }

		protected long sampleReadStart;

		public double secondsRead;

		public bool looping;

		protected StreamPackage(AudioType type){
			//This constructor is mainly for the OGG streams, which would need to set "underlyingStream" to null anyway
			this.type = type;
		}

		protected StreamPackage(Stream stream, AudioType type){
			underlyingStream = stream;
			this.type = type;

			Initialize();
		}

		public virtual void Reset(){
			//Move the "cursor" back to the beginning and reset the counters
			ReadBytes = 0;
			secondsRead = 0;

			if(underlyingStream != null)
				underlyingStream.Position = sampleReadStart;
		}

		protected virtual void Initialize(){ }

		private void QueueBuffers(object sender, EventArgs e){
			//Read() won't add new buffers if this sound shouldn't be looping, so calling it twice here is just fine
			Read(0.1);
			Read(0.1);
		}

		public abstract void ReadSamples(double seconds, out byte[] samples, out int bytesRead, out bool endOfStream);

		private void Read(double seconds){
			//The sound has finished playing.  No need to keep trying to stream more data
			if(FinishedStreaming)
				return;

			if(sfx is null){
				//Initialize the instance
				sfx = new DynamicSoundEffectInstance(SampleRate, Channels);
				sfx.BufferNeeded += QueueBuffers;
			}

			//Read "seconds" amount of data from the stream, then send it to "sfx"
			ReadSamples(seconds, out byte[] read, out int bytesRead, out bool endOfStream);
			ReadBytes += bytesRead;

			secondsRead += seconds;
			
			sfx.SubmitBuffer(read);

			if(endOfStream)
				CheckLooping();
		}

		private void CheckLooping(){
			if(!looping){
				FinishedStreaming = true;

				Dispose();
			}else
				Reset();
		}

		private bool disposed;
		public bool Disposed => disposed;

		~StreamPackage() => Dispose(false);

		public void Dispose(){
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void ChildDispose(bool disposing){ }

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

					FinishedStreaming = true;

					underlyingStream?.Dispose();
				}

				ChildDispose(disposing);

				sfx = null;
				underlyingStream = null;
			}
		}
	}
}
