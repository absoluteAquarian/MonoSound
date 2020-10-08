namespace MonoSound.Filters{
	/// <summary>
	/// An enumeration of all implemented sound filter types
	/// </summary>
	public enum SoundFilterType{
		/// <summary>
		/// No filtering.
		/// </summary>
		None,
		/// <summary>
		/// (Biquad Resonant) Low Pass filtering.  Reduces the amplitude of higher frequencies than the set frequency
		/// </summary>
		LowPass,
		/// <summary>
		/// (Biquad Resonant) Band Pass filtering.  Reduces the amplitude of frequencies not within close proximity to the set frequency
		/// </summary>
		BandPass,
		/// <summary>
		/// (Biquad Resonant) High Pass filtering.  Reduces the amplitude of lower frequencies than the set frequency
		/// </summary>
		HighPass,
		/// <summary>
		/// Reverb filtering.  Makes a sound appear to "taper off" slower
		/// </summary>
		Reverb,
		/// <summary>
		/// Echo filtering.  Repeats the same sound at smaller amplitudes several times
		/// </summary>
		Echo
	}
}
