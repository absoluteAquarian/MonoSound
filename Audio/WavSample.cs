using System;
using System.Runtime.InteropServices;

namespace MonoSound.Audio {
	/// <summary>
	/// Represents a single audio sample from a WAVE file
	/// </summary>
	public struct WavSample {
		[StructLayout(LayoutKind.Explicit, Size = sizeof(int))]
		private struct SampleData {
			[FieldOffset(0)] public readonly byte size;
			[FieldOffset(0)] public PCM16Bit PCM16;
			[FieldOffset(0)] public PCM24Bit PCM24;

			public readonly float ToFloatSample() {
				return size switch {
					SAMPLESIZE_16BIT => PCM16.ToFloatSample(),
					SAMPLESIZE_24BIT => PCM24.ToFloatSample(),
					_ => throw new InvalidOperationException("Sample size was invalid.  Expected either 16-bit or 24-bit PCM.")
				};
			}

			public readonly int GetSampleBitSize() {
				return size switch {
					SAMPLESIZE_16BIT => 16,
					SAMPLESIZE_24BIT => 24,
					_ => throw new InvalidOperationException("Sample size was invalid.  Expected either 16-bit or 24-bit PCM.")
				};
			}
		}

		/// <summary>
		/// Splits a byte array into individual WAVE samples.  The byte array must be a multiple of the sample size.
		/// </summary>
		/// <param name="data">The sample data</param>
		/// <param name="bitsPerSample">The size of each sample in bits.  Must be either 16 or 24.</param>
		/// <exception cref="ArgumentException"/>
		/// <exception cref="InvalidOperationException"/>
		public static WavSample[] SpliceSampleData(byte[] data, int bitsPerSample) {
			if (bitsPerSample != 16 && bitsPerSample != 24)
				throw new ArgumentException("Sample bit depth must be 16-bit or 24-bit PCM.", nameof(bitsPerSample));

			int bytesPerSample = bitsPerSample / 8;
			if (data.Length % bytesPerSample != 0)
				throw new ArgumentException($"Data length ({data.Length}) was not a multiple of PCM format size ({bytesPerSample})", nameof(data));

			ReadOnlySpan<byte> dataSpan = data;

			WavSample[] samples = new WavSample[data.Length / bytesPerSample];
			for (int i = 0, j = 0; i < data.Length; i += bytesPerSample, j++)
				samples[j] = new WavSample(dataSpan.Slice(i, bytesPerSample));

			return samples;
		}

		internal const byte SAMPLESIZE_16BIT = 2;
		internal const byte SAMPLESIZE_24BIT = 3;

		private SampleData _sample;

		/// <summary>
		/// The size of the sample in bits.  Equal to <c>16</c> for 16-bit PCM and <c>24</c> for 24-bit PCM
		/// </summary>
		public readonly int SampleSize => _sample.GetSampleBitSize();

		/// <summary>
		/// The sample represented as a 16-bit PCM sample.  Throws an exception if the sample size is not 16-bit PCM.
		/// </summary>
		public PCM16Bit Sample16Bit {
			readonly get => _sample.size == SAMPLESIZE_16BIT ? _sample.PCM16 : throw new InvalidOperationException("Sample size was not 16-bit PCM");
			set => _sample.PCM16 = value;
		}

		/// <summary>
		/// The sample represented as a 24-bit PCM sample.  Throws an exception if the sample size is not 24-bit PCM.
		/// </summary>
		public PCM24Bit Sample24Bit {
			readonly get => _sample.size == SAMPLESIZE_24BIT ? _sample.PCM24 : throw new InvalidOperationException("Sample size was not 24-bit PCM");
			set => _sample.PCM24 = value;
		}

		/// <summary>
		/// Creates a new WAVE sample using the given 16-bit PCM sample
		/// </summary>
		/// <exception cref="ArgumentException"/>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public WavSample(PCM16Bit sample) {
			_sample = default;
			_sample.PCM16 = sample;
		}

		/// <summary>
		/// Creates a new WAVE sample using the given 24-bit PCM sample
		/// </summary>
		/// <exception cref="ArgumentException"/>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public WavSample(PCM24Bit sample) {
			_sample = default;
			_sample.PCM24 = sample;
		}

		/// <summary>
		/// Creates a new WAVE sample using the given byte array.  The byte array must be 2 elements long for 16-bit PCM data or 3 elements long for 24-bit PCM data.
		/// </summary>
		/// <exception cref="ArgumentException"/>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public WavSample(byte[] data) {
			_sample = default;
			switch (data.Length) {
				case 2:
					_sample.PCM16 = new PCM16Bit(data);
					break;
				case 3:
					_sample.PCM24 = new PCM24Bit(data);
					break;
				default:
					throw new ArgumentException("Byte array was not 2 or 3 elements long", nameof(data));
			}
		}

		/// <summary>
		/// Creates a new WAVE sample using the given byte span.  The span must be 2 elements long for 16-bit PCM data or 3 elements long for 24-bit PCM data.
		/// </summary>
		/// <exception cref="ArgumentException"/>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public WavSample(ReadOnlySpan<byte> span) {
			_sample = default;
			switch (span.Length) {
				case 2:
					_sample.PCM16 = new PCM16Bit(span);
					break;
				case 3:
					_sample.PCM24 = new PCM24Bit(span);
					break;
				default:
					throw new ArgumentException("Span was not 2 or 3 elements long", nameof(span));
			}
		}

		/// <inheritdoc cref="PCM16Bit.ToFloatSample"/>
		public readonly float ToFloatSample() => _sample.ToFloatSample();

		internal readonly void WriteToStream(byte[] audioData, int startIndex) {
			switch (_sample.size) {
				case SAMPLESIZE_16BIT:
					_sample.PCM16.WriteToStream(audioData, startIndex);
					break;
				case SAMPLESIZE_24BIT:
					_sample.PCM24.WriteToStream(audioData, startIndex);
					break;
				default:
					throw new InvalidOperationException("Sample size was invalid.  Expected either 16-bit or 24-bit PCM.");
			}
		}

		/// <summary>
		/// Converts an array of WAVE samples to a byte array
		/// </summary>
		/// <param name="samples">The samples.  The PCM bit depth must be the same for all samples, or an exception is thrown.</param>
		/// <exception cref="ArgumentException"/>
		public static byte[] ToByteArray(WavSample[] samples) {
			if (samples is not { Length: > 0 })
				throw new ArgumentException("Sample array was empty", nameof(samples));

			int size = samples[0]._sample.size;

			byte[] data = new byte[samples.Length * size];
			for (int i = 0, j = 0; i < samples.Length; i++, j += size) {
				WavSample sample = samples[i];

				if (sample._sample.size != size)
					throw new ArgumentException("Sample sizes were not consistent", nameof(samples));

				sample.WriteToStream(data, j);
			}

			return data;
		}
	}

	/// <summary>
	/// Represents a single 16-bit PCM sample
	/// </summary>
	public struct PCM16Bit {
		/// <summary>
		/// The maximum value for a 16-bit PCM sample
		/// </summary>
		public const short MaxValue = short.MaxValue;
		/// <summary>
		/// The minimum value for a 16-bit PCM sample
		/// </summary>
		public const short MinValue = short.MinValue;

		private int _sample;
		/// <summary>
		/// The sample represented as an integer
		/// </summary>
		public short Sample {
			readonly get => (short)(_sample >> 8);
			set => _sample = (value << 8) | WavSample.SAMPLESIZE_16BIT;
		}

		/// <summary>
		/// Creates a new 16-bit PCM sample using the given sample value
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public PCM16Bit(short sample) {
			_sample = 0;
			Sample = sample;
		}

		/// <summary>
		/// Creates a new 16-bit PCM sample using the given float sample value.  The float value must be between -1 and 1.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public PCM16Bit(float sample) {
			if (sample < -1 || sample > 1)
				throw new ArgumentOutOfRangeException(nameof(sample), sample, "Sample value was outside the range of a 16-bit PCM sample");

			_sample = 0;
			Sample = (short)(sample < 0 ? sample * MinValue : sample * MaxValue);
		}

		/// <summary>
		/// Creates a new 16-bit PCM sample using the given byte array.  The byte array must be 2 elements long.
		/// </summary>
		/// <exception cref="ArgumentException"/>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public PCM16Bit(byte[] data) {
			if (data.Length != 2)
				throw new ArgumentException("Byte array was not 2 elements long", nameof(data));

			_sample = 0;
			Sample = BitConverter.ToInt16(data, 0);
		}

		/// <summary>
		/// Creates a new 16-bit PCM sample using the given byte array.  The byte array must be at least 2 elements long and the start index must point to a section of the array that is at least 2 elements long.
		/// </summary>
		/// <exception cref="ArgumentException"/>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public PCM16Bit(byte[] data, int startIndex) {
			if (data.Length < 2)
				throw new ArgumentException("Byte array was not at least 2 elements long", nameof(data));
			if (startIndex < 0)
				throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, "Start index was negative");
			if (startIndex > data.Length - 2)
				throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, $"Start index was too large for an array of length {data.Length}");

			_sample = 0;
			Sample = BitConverter.ToInt16(data, startIndex);
		}

		/// <summary>
		/// Creates a new 16-bit PCM sample using the given span.  The span must be 2 elements long.
		/// </summary>
		/// <exception cref="ArgumentException"/>
		public PCM16Bit(ReadOnlySpan<byte> span) {
			if (span.Length < 2)
				throw new ArgumentException("Span was not at least 2 elements long", nameof(span));

			_sample = 0;
			Sample = BitConverter.ToInt16(span);
		}

		/// <summary>
		/// Converts the sample to a float value between -1 and 1
		/// </summary>
		public readonly float ToFloatSample() => Sample >= 0 ? Sample / (float)MaxValue : -(Sample / (float)MinValue);

		/// <summary>
		/// Converts the sample to a byte array
		/// </summary>
		public readonly byte[] ToSampleData() => BitConverter.GetBytes(Sample);

		internal readonly void WriteToStream(byte[] audioData, int startIndex) => ToSampleData().AsSpan().CopyTo(audioData.AsSpan()[startIndex..]);
	}

	/// <summary>
	/// Represents a single 24-bit PCM sample
	/// </summary>
	public struct PCM24Bit {
		/// <summary>
		/// The maximum value for a 24-bit PCM sample
		/// </summary>
		public const int MaxValue = 0x7FFFFF;
		/// <summary>
		/// The minimum value for a 24-bit PCM sample
		/// </summary>
		public const int MinValue = 0x800000;

		private const int VALUE_MASK = 0xFFFFFF;

		private int _sample;
		/// <summary>
		/// The sample represented as an integer
		/// </summary>
		public int Sample {
			readonly get => (_sample >> 8) & VALUE_MASK;
			set {
				if (unchecked((uint)value) > VALUE_MASK)
					throw new ArgumentOutOfRangeException(nameof(value), $"0x{value:X08}", "Sample value was outside the range of a 24-bit PCM sample");

				_sample = (value << 8) | WavSample.SAMPLESIZE_24BIT;
			}
		}

		/// <summary>
		/// Creates a new 24-bit PCM sample using the given sample value
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public PCM24Bit(int sample) {
			_sample = 0;
			Sample = sample;
		}

		/// <summary>
		/// Creates a new 24-bit PCM sample using the given float sample value.  The float value must be between -1 and 1.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public PCM24Bit(float sample) {
			if (sample < -1 || sample > 1)
				throw new ArgumentOutOfRangeException(nameof(sample), sample, "Sample value was outside the range of a 24-bit PCM sample");

			_sample = 0;
			Sample = (int)(sample < 0 ? sample * MinValue : sample * MaxValue);
		}

		/// <summary>
		/// Creates a new 24-bit PCM sample using the given byte array.  The byte array must be 3 elements long.
		/// </summary>
		/// <exception cref="ArgumentException"/>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public PCM24Bit(byte[] data) {
			if (data.Length != 3)
				throw new ArgumentException("Byte array was not 3 elements long", nameof(data));

			_sample = 0;
			Sample = BitConverter.ToInt32([ .. data, 0x00 ], 0);
		}

		/// <summary>
		/// Creates a new 24-bit PCM sample using the given byte array.  The byte array must be at least 3 elements long and the start index must point to a section of the array that is at least 3 elements long.
		/// </summary>
		/// <exception cref="ArgumentException"/>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public PCM24Bit(byte[] data, int startIndex) {
			if (data.Length < 3)
				throw new ArgumentException("Byte array was not at least 3 elements long", nameof(data));
			if (startIndex < 0)
				throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, "Start index was negative");
			if (startIndex > data.Length - 3)
				throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, $"Start index was too large for an array of length {data.Length}");

			_sample = 0;
			Sample = BitConverter.ToInt32([ .. data.AsSpan().Slice(startIndex, 3), 0x00 ]);
		}

		/// <summary>
		/// Creates a new 24-bit PCM sample using the given span.  The span must be 3 elements long.
		/// </summary>
		/// <exception cref="ArgumentException"/>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public PCM24Bit(ReadOnlySpan<byte> span) {
			if (span.Length < 3)
				throw new ArgumentException("Span was not at least 3 elements long", nameof(span));

			_sample = 0;
			Sample = BitConverter.ToInt32([.. span, 0x00]);
		}

		/// <summary>
		/// Converts the sample to a float value between -1 and 1
		/// </summary>
		public readonly float ToFloatSample() => Sample >= MinValue ? -1 * Sample / (float)MinValue : -(Sample / (float)MaxValue);

		/// <summary>
		/// Converts the sample to a byte array
		/// </summary>
		public readonly byte[] ToSampleData() => BitConverter.GetBytes(Sample).AsSpan()[..3].ToArray();

		internal readonly void WriteToStream(byte[] audioData, int startIndex) => ToSampleData().AsSpan().CopyTo(audioData.AsSpan()[startIndex..]);
	}
}
