using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;
using MonoSound.Filters;
using MonoSound.Filters.Instances;
using System;
using System.Collections.Concurrent;
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
		public long TotalBytes { get; protected set; }
		public long ReadBytes { get; protected set; }

		/// <summary>
		/// The byte offset of the audio sample data in the streamed data
		/// </summary>
		protected long sampleReadStart;

		/// <summary>
		/// How many seconds' worth of audio have been read by this stream.  This variable is reset in <see cref="Reset"/>
		/// </summary>
		public double SecondsRead { get; protected set; }

		/// <summary>
		/// Whether this stream should loop to the beginning of the audio samples once it has completed.
		/// </summary>
		public bool IsLooping { get; internal set; }

		/// <summary>
		/// Gets or sets the current play duration for the streamed audio
		/// </summary>
		public TimeSpan CurrentDuration {
			get => TimeSpan.FromSeconds(SecondsRead);
			set => SetStreamPosition(value.TotalSeconds);
		}

		/// <summary>
		/// Gets the max duration for the streamed audio
		/// </summary>
		public virtual TimeSpan MaxDuration => TimeSpan.FromSeconds(GetSecondDuration(TotalBytes));

		private readonly object _filterLock = new object();
		private int[] filterIDs;
		private Filter[] filterObjects;  // Used to speed up filter applications

		private readonly ConcurrentQueue<byte[]> _queuedReads = new ConcurrentQueue<byte[]>();

		protected StreamPackage(AudioType type) {
			//This constructor is mainly for the OGG streams, which would need to set "underlyingStream" to null anyway
			this.type = type;
		}

		protected StreamPackage(Stream stream, AudioType type) {
			underlyingStream = stream;
			this.type = type;

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
			Reset_Inner(true);
		}

		private void Reset_Inner(bool clearQueue) {
			//Move the "cursor" back to the beginning and reset the counters
			ReadBytes = 0;
			SecondsRead = 0;

			if (underlyingStream != null) {
				long position = Math.Max(sampleReadStart, ModifyResetOffset(sampleReadStart));
				long diff = sampleReadStart - position;

				underlyingStream.Position = position;

				ReadBytes = diff;
				SecondsRead = GetSecondDuration(diff);
			}

			if (clearQueue)
				_queuedReads.Clear();
		}

		public virtual double GetSecondDuration(long byteSampleCount) {
			// sample count = seconds * BitsPerSample / 8 * SampleRate * Channels
			return byteSampleCount / (float)BitsPerSample * 8f / SampleRate / (int)Channels;
		}

		/// <summary>
		/// Adjust where the audio stream will reset to when looping here.
		/// </summary>
		/// <param name="byteOffset">The offset in the data stream.  Defaults to the start of sample data.</param>
		protected virtual long ModifyResetOffset(long byteOffset) {
			return byteOffset;
		}

		/// <summary>
		/// Initialize information about the stream here.  <see cref="underlyingStream"/> has been initialized by the time this method is invoked.
		/// </summary>
		protected virtual void Initialize() {
			InitSound();
		}

		private void QueueBuffers(object sender, EventArgs e) {
			FillQueue(2);  // Must be at least 2 for the buffering to work properly, for whatever reason

			while (_queuedReads.TryDequeue(out byte[] read))
				(sender as DynamicSoundEffectInstance).SubmitBuffer(read);
		}

		internal void FillQueue(int max) {
			if (PlayingSound.State != SoundState.Playing)
				return;  // Don't fill the queues

			if (_queuedReads.Count < max)
				Read(Controls.StreamBufferLengthInSeconds, max);
		}

		/// <summary>
		/// Read samples of data here.
		/// </summary>
		/// <param name="seconds">How many seconds' worth of samples should be read.</param>
		/// <param name="samples">An array of WAVE-formatted sample data.</param>
		/// <param name="bytesRead">How many bytes of data were read.</param>
		/// <param name="endOfStream">Whether the end of the stream has been reached.</param>
		public abstract void ReadSamples(double seconds, out byte[] samples, out int bytesRead, out bool endOfStream);

		private void Read(double seconds, int max) {
			//The sound has finished playing.  No need to keep trying to stream more data
			if (FinishedStreaming || max < 1)
				return;

			while (_queuedReads.Count < max) {
				//Read "seconds" amount of data from the stream, then send it to "sfx"
				ReadSamples(seconds, out byte[] read, out int bytesRead, out bool endOfStream);

				lock (_filterLock) {
					ProcessFilters(ref read);
				}

				ReadBytes += bytesRead;

				SecondsRead += GetSecondDuration(read.Length);

				_queuedReads.Enqueue(read);

				if (endOfStream)
					CheckLooping();
			}
		}

		/// <summary>
		/// Sets the starting location of the next batch of samples to read from the underlying stream, in seconds
		/// </summary>
		public virtual void SetStreamPosition(double seconds) {
			if (seconds < 0)
				throw new ArgumentOutOfRangeException(nameof(seconds), "Position must be a positive number");

			// sample count = seconds * BitsPerSample / 8 * SampleRate * Channels
			if (underlyingStream != null) {
				long offset = (long)(seconds * BitsPerSample / 8 * SampleRate * (int)Channels);
				underlyingStream.Position = sampleReadStart + offset;

				ReadBytes = offset;
				SecondsRead = seconds;
			}
		}

		private void ProcessFilters(ref byte[] data) {
			if (filterIDs is null)
				return;

			if (BitsPerSample != 16)
				throw new InvalidOperationException("Effect data had an invalid BitsPerSample value: " + BitsPerSample);
			
			// Convert the byte samples to float samples
			int size = 2;
			int length = data.Length / size;
			float[] filterSamples = new float[length];

			for (int i = 0; i < data.Length; i += size) {
				byte[] pass = new byte[size];
				Array.Copy(data, i, pass, 0, size);
				
				WavSample sample = new WavSample((short)size, pass);
				filterSamples[i / size] = sample.ToFloatSample();
				FormatWav.ClampSample(ref filterSamples[i / size]);
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
				// Reset the stream, but don't clear the queue
				Reset_Inner(clearQueue: false);
				OnLooping();
			}
		}

		/// <summary>
		/// Called just after the stream is reset to its initial position.<br/>
		/// You can use this method to call <see cref="SetStreamPosition(double)"/> to override where the stream resets to, or override <see cref="ModifyResetOffset(long)"/> if you want to use byte offsets.
		/// </summary>
		protected virtual void OnLooping() { }

		/// <summary>
		/// Applies a set of filters to any audio data streamed by this package.  Only certain filter types are supported, however.
		/// </summary>
		/// <param name="ids">The list of filters to use, or <see langword="null"/> if no filters should be used.</param>
		public void ApplyFilters(params int[] ids) {
			lock (_filterLock) {
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
		}

		private bool disposed;
		public bool Disposed => disposed;

		~StreamPackage() => Dispose_Inner(false);

		public void Dispose() {
			Dispose_Inner(true);
			GC.SuppressFinalize(this);
		}

		[Obsolete("Use the Dispose method instead", error: true)]
		protected virtual void ChildDispose(bool disposing) => Dispose(disposing);

		protected virtual void Dispose(bool disposing) { }

		private void Dispose_Inner(bool disposing) {
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

				Dispose(disposing);

				PlayingSound = null;
				underlyingStream = null;
			}
		}
	}
}
