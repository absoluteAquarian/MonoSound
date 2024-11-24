using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System;
using System.Threading;

namespace MonoSound.FFT {
	internal class FFTQuery(float[] samples, int sampleRate, CancellationTokenSource tokenSource) {
		public readonly float[] samples = samples;
		public readonly int sampleRate = sampleRate;

		private Task _task;
		private readonly CancellationToken _token = tokenSource.Token;

		private bool _processing;
		private bool _finished;
		private Complex[] _buffer;

		public event Action<FFTQuery> OnQueryCompleted;

		public bool Active => _processing || !_finished;

		public void Begin() {
			if (_processing || _finished)
				return;

			_processing = true;

			if (MonoSoundLibrary.IsMainThread) {
				// Process the FFT on a worker thread so as to not block the main thread
				_task = new Task(Process, _token, TaskCreationOptions.LongRunning);
				_task.Start();
			} else
				Process();
		}

		private void Process() {
			try {
				ResetBuffer();
				FormatSampleData();
				FFTOperations.FFT(_buffer);

				OnQueryCompleted?.Invoke(this);
			} catch when (_token.IsCancellationRequested) {
				// Destroy the buffer if the operation was cancelled
				_buffer = null;
			} finally {
				_processing = false;
				_finished = true;
			}
		}

		private void ResetBuffer() {
			// Get the largest power of two that is greater than or equal to the specified length
			int bitCount = BitOperations.Log2((uint)samples.Length - 1) + 1;

			if (bitCount > 24)
				throw new ArgumentException($"Attempted to create an FFT buffer that was too large.  Maximum size is {1 << 24} samples.");

			int fftSize = 1 << bitCount;

			_buffer = new Complex[fftSize];
		}

		private void FormatSampleData() {
			ref float sample = ref samples[0];
			ref Complex fft = ref _buffer[0];

			for (int i = 0; i < samples.Length; i++, sample = ref Unsafe.Add(ref sample, 1), fft = ref Unsafe.Add(ref fft, 1))
				fft = sample;

			// FftSharp zeroes out the rest of the buffer since it uses an in-place algorithm
			// We don't need to do that since we're using a separate buffer
		}

		public bool TryGetFFT(out Complex[] buffer) {
			if (Active) {
				buffer = null;
				return false;
			}

			buffer = _buffer;
			return true;
		}
	}
}
