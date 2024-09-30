﻿using Microsoft.Xna.Framework.Audio;
using MonoSound.API;
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
		/// <summary>
		/// The instance responsible for dynamically playing streamed audio
		/// </summary>
		public StreamedSoundEffectInstance PlayingSound { get; private set; }

		/// <summary>
		/// The parameters for the sound played by this instance
		/// </summary>
		public SoundMetrics Metrics { get; private set; }

		//Useless, but helps in clarifying what package is from what type of file
		/// <summary>
		/// The type of audio file this stream package is from.  This is mainly used for debugging purposes.
		/// </summary>
		public readonly AudioType type;

		//Each stream package keeps track of a separate instance of a "reader" to allow reading of the same file
		/// <summary>
		/// The underlying stream that this package reads from
		/// </summary>
		protected Stream underlyingStream;

		/// <summary>
		/// Whether the stream has finished streaming all of its audio data
		/// </summary>
		public bool FinishedStreaming { get; private set; }

		/// <summary>
		/// The sample rate of the audio data
		/// </summary>
		public int SampleRate { get; protected set; }
		/// <summary>
		/// Whether the audio data is mono or stereo
		/// </summary>
		public AudioChannels Channels { get; protected set; }
		/// <summary>
		/// The size of each sample in bits PER CHANNEL
		/// </summary>
		public short BitsPerSample { get; protected set; }
		/// <summary>
		/// The total number of bytes in the audio data
		/// </summary>
		public long TotalBytes { get; protected set; }
		/// <summary>
		/// The number of bytes read from the audio data
		/// </summary>
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

		private bool focusPause;

		/// <summary>
		/// Gets or sets the audio streaming behavior for this streamed audio.<br/>
		/// If <see langword="null"/>, <see cref="Controls.DefaultStreamFocusBehavior"/> will be used instead.
		/// </summary>
		public StreamFocusBehavior? FocusBehavior { get; set; } = null;

		private readonly object _filterLock = new object();
		private int[] filterIDs;
		private Filter[] filterObjects;  // Used to speed up filter applications

		private readonly ConcurrentQueue<byte[]> _queuedReads = [];

		/// <summary>
		/// Creates a new instance of <see cref="StreamPackage"/><br/>
		/// This constructor does not call <see cref="Initialize"/>
		/// </summary>
		protected StreamPackage(AudioType type) {
			//This constructor is mainly for the OGG streams, which would need to set "underlyingStream" to null anyway
			this.type = type;
		}

		/// <summary>
		/// Creates a new instance of <see cref="StreamPackage"/> with a stream to read from
		/// </summary>
		protected StreamPackage(Stream stream, AudioType type) {
			underlyingStream = stream;
			this.type = type;

			Initialize();
		}

		internal StreamFocusBehavior GetActualFocusBehavior() => FocusBehavior ?? Controls.DefaultStreamFocusBehavior;

		internal void FocusPause() {
			if (Metrics.State == SoundState.Playing && !focusPause) {
				Pause();
				focusPause = true;
			}
		}

		internal void FocusResume() {
			if (Metrics.State == SoundState.Paused && focusPause) {
				Resume();
				focusPause = false;
			}
		}

		private void UpdatePlayTime(double time) {
			if (disposed)
				throw new ObjectDisposedException(null);
			
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

		/// <inheritdoc cref="StreamedSoundEffectInstance.Play"/>
		public void Play() {
			if (disposed)
				throw new ObjectDisposedException(null);

			PlayingSound.Play();
		}

		/// <inheritdoc cref="StreamedSoundEffectInstance.Pause"/>
		public void Pause() {
			if (disposed)
				throw new ObjectDisposedException(null);

			PlayingSound.Pause();
		}

		/// <inheritdoc cref="StreamedSoundEffectInstance.Resume"/>
		public void Resume() {
			if (disposed)
				throw new ObjectDisposedException(null);

			PlayingSound.Resume();
		}

		/// <inheritdoc cref="StreamedSoundEffectInstance.Stop"/>
		public void Stop() {
			if (disposed)
				throw new ObjectDisposedException(null);

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

		/// <summary>
		/// Converts the given byte count to a duration in seconds
		/// </summary>
		/// <param name="byteSampleCount">The amount of bytes of sample data</param>
		public virtual double GetSecondDuration(long byteSampleCount) {
			// sample count = seconds * BitsPerSample / 8 * SampleRate * Channels
			return byteSampleCount / (float)BitsPerSample * 8f / SampleRate / (int)Channels;
		}

		/// <summary>
		/// Adjust where the audio stream will reset to when looping here.
		/// </summary>
		/// <param name="byteOffset">The offset in the data stream.  Defaults to the start of sample data.</param>
		protected virtual long ModifyResetOffset(long byteOffset) => byteOffset;

		/// <summary>
		/// Initialize information about the stream here.  <see cref="underlyingStream"/> has been initialized by the time this method is invoked.
		/// </summary>
		protected virtual void Initialize() => InitSound();

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
						read = [];
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
		/// <exception cref="ArgumentOutOfRangeException"/>
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
			const int SIZE = 2;
			int length = data.Length / SIZE;
			float[] filterSamples = new float[length];

			for (int i = 0; i < data.Length; i += SIZE) {
				filterSamples[i / SIZE] = new PCM16Bit(data, i).ToFloatSample();
				FormatWav.ClampSample(ref filterSamples[i / SIZE]);
			}

			// Apply the filters
			for (int i = 0; i < filterIDs.Length; i++)
				FilterSimulations.ApplyFilterTo(ref filterObjects[i], filterIDs[i], filterSamples, SampleRate);

			// Convert the float samples back to byte samples
			data = new byte[length * SIZE];

			for (int i = 0; i < filterSamples.Length; i++) {
				FormatWav.ClampSample(ref filterSamples[i]);
				new PCM16Bit(filterSamples[i]).WriteToStream(data, i * SIZE);
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
		/// <summary>
		/// Whether this stream package has been disposed
		/// </summary>
		public bool Disposed => disposed;

		/// <summary>
		/// The finalizer for this object
		/// </summary>
		~StreamPackage() => Dispose_Inner(false);

		/// <inheritdoc cref="IDisposable.Dispose"/>
		public void Dispose() {
			Dispose_Inner(true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc cref="Dispose(bool)"/>
		[Obsolete("Use the Dispose method instead", error: true)]
		protected virtual void ChildDispose(bool disposing) => Dispose(disposing);

		/// <summary>
		/// Free resources used by this stream package here
		/// </summary>
		/// <param name="disposing"><see langword="true"/> if this method is running from the <see cref="Dispose()"/> call or <see langword="false"/> if it's running from the finalizer instead.</param>
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
