using Microsoft.Xna.Framework.Audio;

namespace MonoSound.Audio{
	internal struct PCMData{
		public AudioChannels channels;
		public int sampleRate;
		public short bitsPerSample;
		public int duration;
		public int loopStart;
		public int loopLength;
	}
}
