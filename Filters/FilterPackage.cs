using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;

namespace MonoSound.Filters{
	internal class FilterPackage{
		public string asset;
		public SoundFilterType[] types;
		public SoundEffect effect;
		public PCMData metaData;
		public int[] filterIDs;
	}
}
