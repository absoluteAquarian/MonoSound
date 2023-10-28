using MonoSound.Streaming;
using System.IO;

namespace MonoSound.Audio {
	/// <summary>
	/// The base class for an object representing a custom audio file format
	/// </summary>
	public abstract class CustomAudioFormat {
		internal bool DoesExtensionApply(string fileExtension) {
			foreach (string ext in ValidExtensions) {
				if (ext == fileExtension)
					return true;
			}

			return false;
		}

		/// <summary>
		/// The valid file types that this audio format can read from
		/// </summary>
		public abstract string[] ValidExtensions { get; }

		/// <summary>
		/// Read the contents of the file and convert the audio data to the WAVE format here
		/// </summary>
		/// <param name="filePath">The path to the file</param>
		/// <returns>An object representing the converted WAVE data for the audio</returns>
		public abstract FormatWav ReadWav(string filePath);

		/// <summary>
		/// Read the contents of the data stream and convert the audio data to the WAVE format here
		/// </summary>
		/// <param name="dataStream">The data stream</param>
		/// <returns>An object representing the converted WAVE data for the audio, or <see langword="null"/> if the stream isn't in the correct format</returns>
		public abstract FormatWav ReadWav(Stream dataStream);

		/// <summary>
		/// Create an object for streaming this audio format from a given file
		/// </summary>
		/// <param name="filePath">The path to the file</param>
		/// <param name="state">Extra data provided by the caller</param>
		/// <returns>An object that will handle the audio streaming from the file</returns>
		public abstract StreamPackage CreateStream(string filePath, object state);

		/// <summary>
		/// Create an object for streaming this audio format from a given data stream
		/// </summary>
		/// <param name="dataStream">The data stream</param>
		/// <param name="state">Extra data provided by the caller</param>
		/// <returns>An object that will handle the audio streaming from the data stream, or <see langword="null"/> if the stream isn't in the correct format</returns>
		public abstract StreamPackage CreateStream(Stream dataStream, object state);
	}
}
