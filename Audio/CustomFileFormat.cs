using MonoSound.Streaming;
using System;
using System.IO;

namespace MonoSound.Audio {
	/// <summary>
	/// An object representing a custom file format
	/// </summary>
	[Obsolete("This class has been deprecated.  Use the CustomAudioFormat type instead")]
	public class CustomFileFormat {
		/// <summary>
		/// The extension for the format
		/// </summary>
		public readonly string extension;

		/// <summary>
		/// A function which creates a <see cref="FormatWav"/> object from a data stream
		/// </summary>
		public readonly Func<Stream, FormatWav> read;

		/// <summary>
		/// A function which creates a <see cref="StreamPackage"/> object from a data stream
		/// </summary>
		public readonly Func<Stream, StreamPackage> readStreamed;

		internal Func<Stream, object, StreamPackage> RedirectReadStreamed => (s, o) => readStreamed(s);

		internal CustomFileFormat(string extension, Func<Stream, FormatWav> read, Func<Stream, StreamPackage> readStreamed) {
			if (string.IsNullOrWhiteSpace(extension))
				throw new ArgumentException("Invalid extension: " + extension, nameof(extension));
			if (read is null)
				throw new ArgumentNullException(nameof(read));
			if (readStreamed is null)
				throw new ArgumentNullException(nameof(readStreamed));

			this.extension = extension;
			this.read = read;
			this.readStreamed = readStreamed;
		}
	}
}
