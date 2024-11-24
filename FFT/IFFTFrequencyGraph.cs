namespace MonoSound.FFT {
	internal interface IFFTFrequencyGraph<T> where T : IFFTFrequencyGraph<T> {
	//	static abstract double ModifyMagnitude(double magnitude, int fftLength);
		static abstract double ModifyMagnitude(double magnitude);
	}
}
