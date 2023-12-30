namespace MonoSound.API {
	public enum StreamFocusBehavior {
		/// <summary>
		/// Streamed audio will continue to queue and play sound data, even when the game is not in focus.
		/// </summary>
		KeepPlaying = 0,
		/// <summary>
		/// Streamed audio will be paused until the game is in focus.
		/// </summary>
		PauseOnLostFocus = 1
	}
}
