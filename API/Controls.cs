﻿using Microsoft.Xna.Framework;
using MonoSound.API;
using MonoSound.Streaming;

namespace MonoSound {
	/// <summary>
	/// The class responsible for controlling how various aspects of MonoSound are handled.
	/// </summary>
	public static class Controls {
		/// <summary>
		/// Whether the Echo and Freevarb filters are allowed to generate over 30 seconds' worth of samples for a sound, which can happen when using high Delay and low Decay parameters (for Echo) or a low Feedback parameter (for Freeverb).
		/// </summary>
		public static bool AllowEchoOversampling { get; set; }

		/// <summary>
		/// If enabled, this folder path will be where filtered sounds are saved to. This property should be set after <see cref="MonoSoundLibrary.Init(Game)"/> is called.
		/// </summary>
		public static string LogDirectory { get; set; }

		/// <summary>
		/// Enables or disables the saving of filtered sounds.  Set <see cref="LogDirectory"/> to where the filtered sounds will be saved to
		/// </summary>
		public static bool LogFilters { get; set; }

		internal static double streamBufferLengthInSeconds = 0.01;

		/// <summary>
		/// How many seconds' worth of data is read from a data stream when streaming audio.  Defaults to <c>0.01</c> seconds.
		/// </summary>
		public static double StreamBufferLengthInSeconds {
			get => streamBufferLengthInSeconds;
			set => streamBufferLengthInSeconds = double.Clamp(value, 1 / 500d, 1 / 10d);  // At most 500 reads per second and at least 10 reads per second
		}

		/// <summary>
		/// The default behavior for all <see cref="StreamPackage"/> audio streaming.
		/// </summary>
		public static StreamFocusBehavior DefaultStreamFocusBehavior { get; set; } = StreamFocusBehavior.KeepPlaying;
	}
}
