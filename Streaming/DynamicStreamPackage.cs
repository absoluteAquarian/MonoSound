using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;
using System;

namespace MonoSound.Streaming {
	/// <summary>
	/// An initially empty stream package that can be modified to provide audio data.
	/// </summary>
	public class DynamicStreamPackage : StreamPackage {
		/// <summary/>
		public delegate void ModifySamplesDelegate(DynamicStreamPackage self, ref byte[] samples);

		/// <summary/>
		public delegate void ModifyReadSecondsDelegate(DynamicStreamPackage self, ref double seconds);

		/// <summary/>
		public delegate void ModifyByteSamplesDelegate(DynamicStreamPackage self, ref byte[] samples);

		/// <summary/>
		public delegate void ModifyWaveSamplesDelegate(DynamicStreamPackage self, ref WavSample[] samples);

		/// <summary>
		/// Invoked by <see cref="Reset"/>
		/// </summary>
		public event Action<DynamicStreamPackage> OnReset;

		/// <summary>
		/// Invoked by <see cref="ReadSamples(double, out byte[], out int, out bool)"/> after reading samples from <see cref="ReadSamples(double)"/>
		/// </summary>
		public event ModifySamplesDelegate OnSamplesRead;

		/// <summary>
		/// Invoked by <see cref="ModifyReadSeconds"/>
		/// </summary>
		public event ModifyReadSecondsDelegate OnModifyReadSeconds;

		/// <summary>
		/// Invoked by <see cref="PreSubmitBuffer(ref byte[])"/>
		/// </summary>
		public event ModifyByteSamplesDelegate OnPreSubmitByteBuffer;

		/// <summary>
		/// Invoked by <see cref="PreSubmitBuffer(ref WavSample[])"/>
		/// </summary>
		public event ModifyWaveSamplesDelegate OnPreSubmitWaveBuffer;

		/// <summary>
		/// Creates a new <see cref="DynamicStreamPackage"/> instance
		/// </summary>
		/// <param name="channels">Whether to use Mono or Stereo audio</param>
		/// <param name="sampleRate">The sample rate of the audio</param>
		/// <param name="bitsPerSample">The size in bits for one sample per channel</param>
		public DynamicStreamPackage(AudioChannels channels, int sampleRate, short bitsPerSample) : base(AudioType.Custom) {
			Channels = channels;
			SampleRate = sampleRate;
			BitsPerSample = bitsPerSample;

			Initialize();
		}

		/// <inheritdoc cref="StreamPackage.Reset"/>
		public sealed override void Reset() {
			OnReset?.Invoke(this);

			ReadBytes = 0;
			SecondsRead = 0;
		}

		/// <inheritdoc cref="StreamPackage.GetSecondDuration"/>
		public sealed override double GetSecondDuration(long byteSampleCount) => base.GetSecondDuration(byteSampleCount);

		/// <inheritdoc cref="StreamPackage.ModifyResetOffset(long)"/>
		protected sealed override long ModifyResetOffset(long byteOffset) => base.ModifyResetOffset(byteOffset);

		/// <inheritdoc cref="StreamPackage.PreQueueBuffers"/>
		protected override void PreQueueBuffers(ref SubmitBufferControls controls) {
			controls.permitAudioJumps = false;
		}

		/// <inheritdoc cref="StreamPackage.PreSubmitBuffer(ref byte[])"/>
		protected override void PreSubmitBuffer(ref byte[] buffer) => OnPreSubmitByteBuffer?.Invoke(this, ref buffer);

		/// <inheritdoc cref="StreamPackage.ReadSamples"/>
		protected override void PreSubmitBuffer(ref WavSample[] buffer) => OnPreSubmitWaveBuffer?.Invoke(this, ref buffer);

		/// <inheritdoc cref="StreamPackage.ReadSamples"/>
		public sealed override void ReadSamples(double seconds, out byte[] samples, out int bytesRead, out bool checkLooping) {
			samples = ReadSamples(seconds);
			OnSamplesRead?.Invoke(this, ref samples);

			if (samples is null)
				throw new NullReferenceException("Sample read returned a null array");

			bytesRead = samples.Length;
			checkLooping = false;
		}

		/// <inheritdoc cref="ReadSamples(double, out byte[], out int, out bool)"/>
		public virtual byte[] ReadSamples(double seconds) => null;

		/// <inheritdoc cref="StreamPackage.ModifyReadSeconds"/>
		protected sealed override void ModifyReadSeconds(ref double seconds) => OnModifyReadSeconds?.Invoke(this, ref seconds);

		/// <summary>
		/// Ignored.  Dynamic streams do not support setting the stream position.
		/// </summary>
		/// <exception cref="NotSupportedException"/>
		public sealed override void SetStreamPosition(double seconds) => throw new NotSupportedException("Dynamic streams do not support setting the stream position");

		/// <summary>
		/// Dynamic streams do not support looping, so this method forces the stream to stop instead.
		/// </summary>
		protected sealed override void HandleLooping() {
			IsLooping = false;
			base.HandleLooping();
		}

		/// <summary>
		/// Ignored.  Dynamic streams do not support looping.
		/// </summary>
		protected sealed override void OnLooping() { }
	}
}
