using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;

namespace MonoSound.Filters{
	internal class FilterPackage{
		public string asset;
		public SoundFilterType type;
		public SoundEffect effect;
		public PCMData metaData;
		public int filterID;
	}
}
