using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;
using MonoSound.Filters;
using MonoSound.Filters.Instances;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace MonoSound.Streaming {
	/// <summary>
	/// An object representing streamed audio from a data stream
	/// </summary>
	public abstract class StreamPackage : IDisposable {
		internal StreamedSoundEffectInstance PlayingSound { get; private set; }

		/// <summary>
		/// The parameters for the sound played by this instance
		/// </summary>
		public SoundMetrics Metrics { get; private set; }

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
		/// How many seconds' worth of audio have been read by this stream from the start of the audio (<b>NOT</b> the total time read if the audio loops!)
		/// </summary>
		public double SecondsRead { get; protected set; }

		/// <summary>
		/// How many seconds' worth of audio have been read by this stream from the start of the audio (<b>NOT</b> the total time read if the audio loops!)
		/// </summary>
		public TimeSpan ReadTime => TimeSpan.FromSeconds(SecondsRead);

		/// <summary>
		/// Whether this stream should loop its audio samples once it has reached the end.
		/// </summary>
		public bool IsLooping { get; set; }

		internal long _playTime;
		/// <summary>
		/// Gets or sets the current play duration for the streamed audio
		/// </summary>
		public TimeSpan CurrentDuration => TimeSpan.FromTicks(Interlocked.Read(ref _playTime));

		private double _lastBufferDuration;

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

		private void UpdatePlayTime(double time) {
			if (disposed)
				throw new ObjectDisposedException("this");
			
			if (PlayingSound.State != SoundState.Playing)
				return;

			Interlocked.Add(ref _playTime, TimeSpan.FromSeconds(time).Ticks);
		}

		private double _jumpReadStart;
		private bool _hasActiveJump;

		/// <summary>
		/// Sets <see cref="SecondsRead"/> to <paramref name="seconds"/>, and sets a hidden variable used to track <see cref="CurrentDuration"/><br/>
		/// This method is automatically called by <see cref="SetStreamPosition"/>
		/// </summary>
		protected void ApplyImmediateJump(double seconds) {
			SecondsRead = seconds;
			_jumpReadStart = seconds;
			_hasActiveJump = true;
		}

		public void Play() {
			if (disposed)
				throw new ObjectDisposedException("this");

			PlayingSound.Play();
		}

		public void Pause() {
			if (disposed)
				throw new ObjectDisposedException("this");

			PlayingSound.Pause();
		}

		public void Resume() {
			if (disposed)
				throw new ObjectDisposedException("this");

			PlayingSound.Resume();
		}

		public void Stop() {
			if (disposed)
				throw new ObjectDisposedException("this");

			PlayingSound.Stop();
			Reset_Impl(true);
		}

		private void InitSound() {
			//Initialize the instance
			PlayingSound?.Dispose();
			PlayingSound = new StreamedSoundEffectInstance(SampleRate, Channels);
			PlayingSound.BufferNeeded += QueueBuffers;

			// BUG FIX: Sometimes the streamed audio will randomly start with the wrong parameters
			PlayingSound.Volume = 1;
			PlayingSound.Pan = 0;
			PlayingSound.Pitch = 0;

			Metrics = new SoundMetrics(PlayingSound);
		}

		/// <summary>
		/// Reset the stream here.  By default, this sets the next audio buffer to read to the start of the audio data.
		/// </summary>
		public virtual void Reset() {
			if (underlyingStream != null) {
				long position = Math.Max(sampleReadStart, ModifyResetOffset(sampleReadStart));
				long diff = sampleReadStart - position;

				underlyingStream.Position = position;

				ReadBytes = diff;
				ApplyImmediateJump(GetSecondDuration(diff));
			}
		}

		private void Reset_Impl(bool clearQueue) {
			Reset();

			if (PlayingSound.State == SoundState.Stopped)
				Interlocked.Exchange(ref _playTime, 0);

			if (clearQueue)
				_queuedReads.Clear();
		}

		/// <summary>
		/// Clears the audio data queue
		/// </summary>
		public void ClearAudioQueue() => _queuedReads.Clear();

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
			double currentDuration = CalculateBufferTime();

			// Stopwatch usage is unreliable for tracking time
			// Instead, apply any changes to the buffer once the event has been raised
			double deltaTime = _lastBufferDuration - currentDuration;
			if (deltaTime > 0)
				UpdatePlayTime(deltaTime);

			if (_hasActiveJump) {
				if (PlayingSound.PendingBufferCount > 0) {
					// Don't submit new buffers until the current buffers have been exhaused
					_lastBufferDuration = CalculateBufferTime();
					return;
				}

				// Forcibly set CurrentDuration to where the jump started at
				Interlocked.Exchange(ref _playTime, TimeSpan.FromSeconds(_jumpReadStart).Ticks);
				_jumpReadStart = 0;
				_hasActiveJump = false;
			}

			if (PlayingSound.State != SoundState.Playing)
				return;  // Don't fill the queues

			FillQueue(3);  // Must be at least 2 for the buffering to work properly, for whatever reason

			var sfx = sender as StreamedSoundEffectInstance;
			while (_queuedReads.TryDequeue(out byte[] read))
				sfx.SubmitBuffer(read);

			_lastBufferDuration = CalculateBufferTime();
		}

		internal void FillQueue(int max) {
			if (_queuedReads.Count < max - PlayingSound.PendingBufferCount)
				Read(Controls.StreamBufferLengthInSeconds, max);
		}

		private double CalculateBufferTime() {
			double time = 0;
			
			// TODO: implementation for tracking play time may need to be different if MonoSound ever tries to compile against WindowsDX
			foreach (var item in PlayingSound._queuedBuffers)
				time += item.Duration;

			return time;
		}

		/// <summary>
		/// Read samples of data here.
		/// </summary>
		/// <param name="seconds">How many seconds' worth of samples should be read.</param>
		/// <param name="samples">An array of WAVE-formatted sample data.</param>
		/// <param name="bytesRead">How many bytes of data were read.</param>
		/// <param name="checkLooping">Whether looping should be checked after the samples are read.</param>
		public abstract void ReadSamples(double seconds, out byte[] samples, out int bytesRead, out bool checkLooping);

		private void Read(double seconds, int max) {
			//The sound has finished playing.  No need to keep trying to stream more data
			if (FinishedStreaming || max < 1)
				return;

			double origSeconds = seconds;
			while (_queuedReads.Count < max) {
				seconds = origSeconds;
				ModifyReadSeconds(ref seconds);
				if (seconds <= 0) {
					HandleLooping();
					break;
				}

				//Read "seconds" amount of data from the stream, then send it to "sfx"
				ReadSamples(seconds, out byte[] read, out int bytesRead, out bool checkLooping);

				// Ensure that the buffer meets the requirement for the DynamicSoundEffectInstance
				int requiredSampleSize = 2 * (int)Channels;
				if (read.Length % requiredSampleSize != 0) {
					int length = read.Length - read.Length % requiredSampleSize;
					if (length <= 0) {
						// Just set it to an empty array
						read = Array.Empty<byte>();
					} else {
						// Resize the array.  Some samples at the end will be trimmed off, but that's fine
						Array.Resize(ref read, length);
					}
				}

				// If no bytes were read, assuming something went wrong and bail after checking for looping
				if (bytesRead <= 0 || read.Length == 0) {
					HandleLooping();
					break;
				}

				lock (_filterLock) {
					ProcessFilters(ref read);
				}

				ReadBytes += bytesRead;

				SecondsRead += GetSecondDuration(read.Length);

				_queuedReads.Enqueue(read);

				if (checkLooping)
					HandleLooping();
			}
		}

		/// <summary>
		/// Modify how much audio is read by the next <see cref="ReadSamples"/> call here.<br/>
		/// This method can be useful for e.g. preventing audio bleed across loop boundaries.
		/// </summary>
		/// <param name="seconds">The duration of samples to read</param>
		protected virtual void ModifyReadSeconds(ref double seconds) { }

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
				ApplyImmediateJump(seconds);
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

		/// <summary>
		/// Executes when sample reading has indicating that a loop should be checked.<br/>
		/// By default, this method stops the stream via <see cref="StreamLoader.FreeStreamedSound(ref StreamPackage)"/> if <see cref="IsLooping"/> is <see langword="false"/>, or resets the stream to the loop point if <see cref="IsLooping"/> is <see langword="true"/>.
		/// </summary>
		protected virtual void HandleLooping() {
			if (!IsLooping) {
				FinishedStreaming = true;
				StreamPackage redirect = this;
				StreamManager.StopStreamingSound(ref redirect);
			} else {
				// Reset the stream, but don't clear the queue
				Reset_Impl(clearQueue: false);
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
				Metrics = null;
				underlyingStream = null;
			}
		}
	}
}
