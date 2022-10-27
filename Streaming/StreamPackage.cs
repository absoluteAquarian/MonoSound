using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;
using MonoSound.Filters;
using MonoSound.Filters.Instances;
using System;
using System.IO;

namespace MonoSound.Streaming {
	/// <summary>
	/// An object representing streamed audio from a data stream
	/// </summary>
	public abstract class StreamPackage : IDisposable {
		/// <summary>
		/// The object responsible for queuing samples and playing them
		/// </summary>
		public DynamicSoundEffectInstance PlayingSound { get; private set; }
		//Useless, but helps in clarifying what package is from what type of file
		public readonly AudioType type;

		//Each stream package keeps track of a separate instance of a "reader" to allow reading of the same file
		protected Stream underlyingStream;
		public bool FinishedStreaming { get; private set; }

		public int SampleRate { get; protected set; }
		public AudioChannels Channels { get; protected set; }
		public short BitsPerSample { get; protected set; }
		public int TotalBytes { get; protected set; }
		public int ReadBytes { get; private set; }

		/// <summary>
		/// The byte offset of the audio sample data in the streamed data
		/// </summary>
		protected long sampleReadStart;

		/// <summary>
		/// How many seconds' worth of audio have been read by this stream.  This variable is reset in <see cref="Reset"/>
		/// </summary>
		public double SecondsRead { get; private set; }

		/// <summary>
		/// Whether this stream should loop to the beginning of the audio samples once it has completed.
		/// </summary>
		public bool IsLooping { get; internal set; }

		private int[] filterIDs;
		private Filter[] filterObjects;  // Used to speed up filter applications

		protected StreamPackage(AudioType type) {
			//This constructor is mainly for the OGG streams, which would need to set "underlyingStream" to null anyway
			this.type = type;

			InitSound();
		}

		protected StreamPackage(Stream stream, AudioType type) {
			underlyingStream = stream;
			this.type = type;

			InitSound();
			Initialize();
		}

		private void InitSound() {
			//Initialize the instance
			PlayingSound?.Dispose();
			PlayingSound = new DynamicSoundEffectInstance(SampleRate, Channels);
			PlayingSound.BufferNeeded += QueueBuffers;
		}

		/// <summary>
		/// Reset the stream here.  By default, sets <see cref="ReadBytes"/> and <see cref="SecondsRead"/> to zero and sets the position of the underlying stream to the start of the sample data
		/// </summary>
		public virtual void Reset() {
			//Move the "cursor" back to the beginning and reset the counters
			ReadBytes = 0;
			SecondsRead = 0;

			if (underlyingStream != null)
				underlyingStream.Position = sampleReadStart;
		}

		/// <summary>
		/// Initialize information about the stream here.  <see cref="underlyingStream"/> has been initialized by the time this method is invoked.
		/// </summary>
		protected virtual void Initialize() { }

		private void QueueBuffers(object sender, EventArgs e) => Read(Controls.StreamBufferLengthInSeconds);

		/// <summary>
		/// Read samples of data here.
		/// </summary>
		/// <param name="seconds">How many seconds' worth of samples should be read.</param>
		/// <param name="samples">An array of WAVE-formatted sample data.</param>
		/// <param name="bytesRead">How many bytes of data were read.</param>
		/// <param name="endOfStream">Whether the end of the stream has been reached.</param>
		public abstract void ReadSamples(double seconds, out byte[] samples, out int bytesRead, out bool endOfStream);

		private void Read(double seconds) {
			//The sound has finished playing.  No need to keep trying to stream more data
			if (FinishedStreaming)
				return;

			//Read "seconds" amount of data from the stream, then send it to "sfx"
			ReadSamples(seconds, out byte[] read, out int bytesRead, out bool endOfStream);

			ProcessFilters(ref read);

			ReadBytes += bytesRead;

			SecondsRead += seconds;

			PlayingSound.SubmitBuffer(read);

			if (endOfStream)
				CheckLooping();
		}

		private void ProcessFilters(ref byte[] data) {
			if (filterIDs is null)
				return;

			if (BitsPerSample != 16)
				throw new InvalidOperationException("Effect data had an invalid BitsPerSample value: " + BitsPerSample);
			
			// Convert the byte samples to float samples
			int size = BitsPerSample / 8;
			int length = data.Length / size;
			float[] filterSamples = new float[length];

			for (int i = 0; i < data.Length; i += size) {
				byte[] pass = new byte[size];
				Array.Copy(data, i, pass, 0, size);
				
				WavSample sample = new WavSample((short)size, pass);
				filterSamples[i] = sample.ToFloatSample();
				FormatWav.ClampSample(ref filterSamples[i]);
			}

			// Apply the filters
			for (int i = 0; i < filterIDs.Length; i++)
				FilterSimulations.ApplyFilterTo(ref filterObjects[i], filterIDs[i], filterSamples, SampleRate);

			// Convert the float samples back to byte samples
			data = new byte[length * size];

			for (int i = 0; i < filterSamples.Length; i++) {
				FormatWav.ClampSample(ref filterSamples[i]);

				short convert = (short)(filterSamples[i] * short.MaxValue);
				byte[] temp = BitConverter.GetBytes(convert);
				data[2 * i] = temp[0];
				data[2 * i + 1] = temp[1];
			}
		}

		private void CheckLooping() {
			if (!IsLooping) {
				FinishedStreaming = true;

				Dispose();
			} else {
				Reset();

				// Read data so that the looping doesn't get choppy
				QueueBuffers(null, null);
			}
		}

		/// <summary>
		/// Applies a set of filters to any audio data streamed by this package.  Only certain filter types are supported, however.
		/// </summary>
		/// <param name="ids">The list of filters to use, or <see langword="null"/> if no filters should be used.</param>
		public void ApplyFilters(params int[] ids) {
			if (ids is null || ids.Length == 0) {
				filterIDs = null;
				filterObjects = null;
			} else {
				filterIDs = new int[ids.Length];
				ids.CopyTo(filterIDs, 0);

				if (filterObjects != null) {
					for (int i = 0; i < filterObjects.Length; i++)
						filterObjects[i]?.Free();
				}

				filterObjects = new Filter[ids.Length];
			}
		}

		private bool disposed;
		public bool Disposed => disposed;

		~StreamPackage() => Dispose(false);

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void ChildDispose(bool disposing) { }

		private void Dispose(bool disposing) {
			if (!disposed) {
				disposed = true;

				if (disposing) {
					try {
						PlayingSound.Stop(immediate: true);
						PlayingSound.Dispose();
					} catch (NoAudioHardwareException) {
						//Exception can be thrown during the final stages of an app closing.  Just ignore it
					}

					FinishedStreaming = true;

					underlyingStream?.Dispose();
				}

				ChildDispose(disposing);

				PlayingSound = null;
				underlyingStream = null;
			}
		}
	}
}
