namespace MonoSound.API {
	/// <summary>
	/// An enumeration for how streamed audio should behave when the game is not in focus.
	/// </summary>
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
