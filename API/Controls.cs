namespace MonoSound {
	/// <summary>
	/// The class responsible for controlling how various aspects of MonoSound are handled.
	/// </summary>
	public static class Controls {
		/// <summary>
		/// Whether the Echo filter is allowed to generate over 30 seconds' worth of samples for a sound, which can happen when using high Delay and low Decay parameters.
		/// </summary>
		public static bool AllowEchoOversampling { get; set; }

		/// <summary>
		/// If enabled, this folder path will be where filtered sounds are saved to. This property should be set after <seealso cref="MonoSoundLibrary.Init"/> is called.
		/// </summary>
		public static string LogDirectory { get; set; }

		/// <summary>
		/// Enables or disables the saving of filtered sounds.  Set <seealso cref="LogDirectory"/> to where the filtered sounds will be saved to
		/// </summary>
		public static bool LogFilters { get; set; }

		internal static double streamBufferLengthInSeconds = 0.01;

		/// <summary>
		/// How many seconds' worth of data is read from a data stream when streaming audio.  Defaults to <c>0.01</c> seconds.
		/// </summary>
		public static double StreamBufferLengthInSeconds {
			get => streamBufferLengthInSeconds;
			set {
				if (value < 1 / 500d)
					value = 1 / 500d;  // At most 500 reads per second
				if (value > 1 / 10d)
					value = 1 / 10d;   // At minimum 10 reads per second
				streamBufferLengthInSeconds = value;
			}
		}
	}
}
