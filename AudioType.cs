using Microsoft.Xna.Framework.Audio;

namespace MonoSound{
	/// <summary>
	/// An enumeration representing the supported audio stream types
	/// </summary>
	public enum AudioType{
		/// <summary>
		/// Compiled <seealso cref="SoundEffect"/> files
		/// </summary>
		XNB,
		/// <summary>
		/// XACT wave banks
		/// </summary>
		XWB,
		/// <summary>
		/// WAVE files (.wav)
		/// </summary>
		WAV,
		/// <summary>
		/// OGG Vorbis files (.ogg)
		/// </summary>
		OGG,
		/// <summary>
		/// MPEG Audio Layer III files (.mp3)
		/// </summary>
		MP3
	}
}
