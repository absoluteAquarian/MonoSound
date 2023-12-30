using Microsoft.Xna.Framework.Audio;
using System;
using System.Runtime.CompilerServices;

[assembly: IgnoresAccessChecksTo("MonoGame.Framework")]
namespace MonoSound.Audio {
	// A copy of DynamicSoundEffectInstance that's tailored for threaded buffer loading
	// Typically I'd use something like MonoMod to adjust the behavior instead, but I don't know how well it would mesh with NativeAOT/BRUTE
	/// <summary>
	/// A <see cref="SoundEffectInstance"/> whose audio data is streamed by a worker thread
	/// </summary>
	internal sealed partial class StreamedSoundEffectInstance : SoundEffectInstance {
		private const int TargetPendingBufferCount = 3;
		private int _buffersNeeded;
		private int _sampleRate;
		private AudioChannels _channels;
		private SoundState _state;

		/// <summary>
		/// This value has no effect on StreamedSoundEffectInstance.
		/// It may not be set.
		/// </summary>
		public override bool IsLooped {
			get => false;
			set {
				AssertNotDisposed();
				if (value)
					throw new InvalidOperationException("IsLooped cannot be set true. Submit looped audio data to implement looping.");
			}
		}

		public override SoundState State {
			get {
				AssertNotDisposed();
				return _state;
			}
		}

		/// <summary>
		/// Returns the number of audio buffers queued for playback.
		/// </summary>
		public int PendingBufferCount {
			get {
				AssertNotDisposed();
				return PlatformGetPendingBufferCount();
			}
		}

		/// <summary>
		/// The event that occurs when the number of queued audio buffers is less than or equal to 2.
		/// </summary>
		/// <remarks>
		/// This event may occur when <see cref="Play()"/> is called or during playback when a buffer is completed.
		/// </remarks>
		public event EventHandler<EventArgs> BufferNeeded;

		/// <summary>
		/// Creates a <see cref="StreamedSoundEffectInstance"/>
		/// </summary>
		/// <param name="sampleRate">Sample rate, in Hertz (Hz).</param>
		/// <param name="channels">Number of channels (mono or stereo).</param>
		/// <exception cref="NoAudioHardwareException"/>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public StreamedSoundEffectInstance(int sampleRate, AudioChannels channels) {
			SoundEffect.Initialize();
			if (SoundEffect._systemState != SoundEffect.SoundSystemState.Initialized)
				throw new NoAudioHardwareException("Audio has failed to initialize. Call SoundEffect.Initialize() before sound operation to get more specific errors.");

			if ((sampleRate < 8000) || (sampleRate > 48000))
				throw new ArgumentOutOfRangeException("sampleRate");
			if ((channels != AudioChannels.Mono) && (channels != AudioChannels.Stereo))
				throw new ArgumentOutOfRangeException("channels");

			_sampleRate = sampleRate;
			_channels = channels;
			_state = SoundState.Stopped;
			PlatformCreate();

			// This instance is added to the pool so that its volume reflects master volume changes
			// and it contributes to the playing instances limit, but the source/voice is not owned by the pool.
			_isPooled = false;
			_isDynamic = true;
		}

		/// <summary>
		/// Plays or resumes the DynamicSoundEffectInstance.
		/// </summary>
		public override void Play() {
			AssertNotDisposed();

			if (_state != SoundState.Playing) {
				// Ensure that the volume reflects master volume, which is done by the setter.
				Volume = Volume;

				// Add the instance to the pool
				if (!SoundEffectInstancePool.SoundsAvailable)
					throw new InstancePlayLimitException();
				SoundEffectInstancePool.Remove(this);

				PlatformPlay();
				_state = SoundState.Playing;

				CheckBufferCount();
			}
		}

		/// <summary>
		/// Pauses playback of the DynamicSoundEffectInstance.
		/// </summary>
		public override void Pause() {
			AssertNotDisposed();
			PlatformPause();
			_state = SoundState.Paused;
		}

		/// <summary>
		/// Resumes playback of the DynamicSoundEffectInstance.
		/// </summary>
		public override void Resume() {
			AssertNotDisposed();

			if (_state != SoundState.Playing) {
				Volume = Volume;

				// Add the instance to the pool
				if (!SoundEffectInstancePool.SoundsAvailable)
					throw new InstancePlayLimitException();
				SoundEffectInstancePool.Remove(this);
			}

			PlatformResume();
			_state = SoundState.Playing;
		}

		/// <summary>
		/// Immediately stops playing the DynamicSoundEffectInstance.
		/// </summary>
		/// <remarks>
		/// Calling this also releases all queued buffers.
		/// </remarks>
		public override void Stop() {
			AssertNotDisposed();

			PlatformStop();
			_state = SoundState.Stopped;

			SoundEffectInstancePool.Add(this);
		}

		/// <summary>
		/// Queues an audio buffer for playback.
		/// </summary>
		/// <remarks>
		/// The buffer length must conform to alignment requirements for the audio format.
		/// </remarks>
		/// <param name="buffer">The buffer containing PCM audio data.</param>
		public void SubmitBuffer(byte[] buffer) {
			AssertNotDisposed();

			if (buffer.Length == 0)
				throw new ArgumentException("Buffer may not be empty.");

			// Ensure that the buffer length matches alignment.
			// The data must be 16-bit, so the length is a multiple of 2 (mono) or 4 (stereo).
			var sampleSize = 2 * (int)_channels;
			if (buffer.Length % sampleSize != 0)
				throw new ArgumentException("Buffer length does not match format alignment.");

			PlatformSubmitBuffer(buffer, 0, buffer.Length);
		}

		private void AssertNotDisposed() {
			if (IsDisposed)
				throw new ObjectDisposedException(null);
		}

		protected override void Dispose(bool disposing) {
			PlatformDispose(disposing);
			base.Dispose(disposing);
		}

		private void CheckBufferCount() {
			if ((PendingBufferCount < TargetPendingBufferCount) && (_state == SoundState.Playing))
				_buffersNeeded++;
		}

		internal void StrobeQueue() {
			// Update the buffers
			PlatformUpdateQueue();

			// Raise the event
			var handler = BufferNeeded;
			if (handler != null) {
				int count = _buffersNeeded < 3 ? _buffersNeeded : 3;
				for (int i = 0; i < count; i++)
					handler(this, EventArgs.Empty);
			}

			_buffersNeeded = 0;
		}
	}
}
