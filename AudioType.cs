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
		/// MPEG Audio Layer 3 files (.mp3)
		/// </summary>
		MP3,
		/// <summary>
		/// User-defined audio types.  Care needs to be taken when using this since MonoSound will not check the validity of the file.
		/// </summary>
		Custom
	}
}
