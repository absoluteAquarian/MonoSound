using Microsoft.Xna.Framework.Audio;

namespace MonoSound.Audio {
	/// <summary>
	/// An object representing the metrics from a <see cref="SoundEffectInstance"/>
	/// </summary>
	public class SoundMetrics {
		private readonly SoundEffectInstance _source;

		public SoundMetrics(SoundEffectInstance source) {
			_source = source;
		}

		/// <inheritdoc cref="SoundEffectInstance.Volume"/>
		public float Volume {
			get => _source.Volume;
			set => _source.Volume = value;
		}

		/// <inheritdoc cref="SoundEffectInstance.Pan"/>
		public float Pan {
			get => _source.Pan;
			set => _source.Pan = value;
		}

		/// <inheritdoc cref="SoundEffectInstance.Pitch"/>
		public float Pitch {
			get => _source.Pitch;
			set => _source.Pitch = value;
		}

		/// <inheritdoc cref="SoundEffectInstance.State"/>
		public SoundState State => _source.State;

		/// <inheritdoc cref="SoundEffectInstance.IsDisposed"/>
		public bool IsDisposed => _source.IsDisposed;
	}
}
