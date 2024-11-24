using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace MonoSound.FFT {
	/// <summary>
	/// A helper class for implementing complex number arithmetic
	/// </summary>
	public static class ComplexMath {
		/// <summary>
		/// Converts an angle to a complex (X, Y) coordinate on the unit circle
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Complex CosSin(double omega) {
			(double sin, double cos) = Math.SinCos(omega);
			return new Complex(cos, sin);
		}
	}
}
