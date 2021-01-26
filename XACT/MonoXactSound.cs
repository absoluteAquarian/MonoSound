using MonoSound.Audio;
using System;
using System.IO;

namespace MonoSound.XACT{
	internal class MonoXactSound{
		//A barebones object just for storing indices and meaningful information

		//These 'readonly' fields were changed to be 'internal' so that StreamManager can use them
		internal readonly int _waveBankIndex;
		internal readonly int _trackIndex;

		private readonly MonoSoundBank soundBank;

		private MonoXactSound(){ }

		public MonoXactSound(MonoSoundBank bank, BinaryReader soundReader){
			soundBank = bank;

			var flags = soundReader.ReadByte();
			bool _complexSound = (flags & 0x1) != 0;
			var hasRPCs = (flags & 0x0E) != 0;
			var hasDSPs = (flags & 0x10) != 0;

			if(_complexSound || hasRPCs || hasDSPs)
				throw new Exception("MonoSound currently does not support complex sounds, sounds with RPC curves or Microsoft Reverb");

			soundReader.ReadUInt16();
			soundReader.ReadByte();
			soundReader.ReadInt16();
			soundReader.ReadByte(); //priority
			soundReader.ReadUInt16(); // filter stuff?
			
			_trackIndex = soundReader.ReadUInt16();
			_waveBankIndex = soundReader.ReadByte();
		}

		public FormatWav GetAudio() => soundBank.GetAudio(_waveBankIndex, _trackIndex);
	}
}
