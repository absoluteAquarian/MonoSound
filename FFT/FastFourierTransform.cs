using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace MonoSound.FFT {
	/// <summary>
	/// An implementation of the Fast Fourier Transform algorithm
	/// </summary>
	public class FastFourierTransform : IDisposable {
		/// <summary>
		/// The sample rate of the audio data that this FFT will analyze
		/// </summary>
		public readonly int sampleRate;
		private readonly CancellationTokenSource _cancelSource;

		private ConcurrentQueue<FFTQuery> _queryQueue = [];
		private FFTQuery _activeQuery;
		
		private FFTGraph _graph;
		private FFTGraphRenderMode _graphMode;
		private double _graphDecayFactor;
		private bool _dirtyGraph;

		/// <summary>
		/// Creates a new <see cref="FastFourierTransform"/> instance with the specified sample rate
		/// </summary>
		/// <param name="sampleRate">The sample rate of the audio data</param>
		public FastFourierTransform(int sampleRate) {
			this.sampleRate = sampleRate;
			_cancelSource = new();
		}

		/// <summary>
		/// Calculates the Fast Fourier Transform (FFT) of the specified samples
		/// </summary>
		/// <param name="samples">The sample data to analyze</param>
		public void Process(float[] samples) {
			var query = new FFTQuery(samples, sampleRate, _cancelSource);

			_queryQueue.Enqueue(query);

			query.Begin();
		}

		/// <summary>
		/// Sets the graph to static render mode, where the graph does not decay over time and will be set to whatever the most recent data is
		/// </summary>
		public void SetGraphToStaticRenderMode() {
			_graphMode = FFTGraphRenderMode.Static;
			_graphDecayFactor = -1;
			_dirtyGraph = true;
		}

		/// <summary>
		/// Sets the graph to decay render mode, where the graph will decay over time based on the specified decay factor
		/// </summary>
		public void SetGraphToDecayRenderMode(double decayFactor) {
			_graphMode = FFTGraphRenderMode.DecayOverTime;
			_graphDecayFactor = decayFactor;
			_dirtyGraph = true;
		}

		/// <summary>
		/// Queries the FFT data for a Root Mean Square (RMS) graph
		/// </summary>
		/// <param name="time">The total elapsed time since the most recent sample data</param>
		public IEnumerable<FFTGraphPoint> QueryRMSGraph(ref double time) => QueryGraph<RootMeanSquareGraph>(ref time);

		/// <summary>
		/// Queries the FFT data for a Decibel (dB) graph
		/// </summary>
		/// <param name="time">The total elapsed time since the most recent sample data</param>
		public IEnumerable<FFTGraphPoint> QueryDBGraph(ref double time) => QueryGraph<DecibelGraph>(ref time);

		private IEnumerable<FFTGraphPoint> QueryGraph<T>(ref double time) where T : IFFTFrequencyGraph<T> {
			if (_graph is not FFTGraph<T>) {
				_graph?.Clear();
				_graph = new FFTGraph<T>();
			}

			bool changed = false;
			while (_queryQueue.TryPeek(out var query) && !query.Active) {
				_activeQuery = query;
				changed = true;

				_queryQueue.TryDequeue(out _);
			}

			if (changed) {
				_graph.Populate(_activeQuery);
				time = 0;
			}

			if (_dirtyGraph) {
				_dirtyGraph = false;
				if (_graphMode == FFTGraphRenderMode.Static)
					_graph.SetToStaticRenderMode();
				else
					_graph.SetToDecayRenderMode(_graphDecayFactor);
			}

			return _graph.ExtractAxesData(time);
		}

		#region Implement IDisposable
		private bool disposed;

		/// <inheritdoc/>
		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			if (disposed)
				return;

			if (disposing) {
				_cancelSource.Cancel();
				_cancelSource.Dispose();
			}

			_graph = null;
			_queryQueue = null;
			_activeQuery = null;

			disposed = true;
		}

		/// <inheritdoc/>
		~FastFourierTransform() => Dispose(false);
		#endregion
	}
}
