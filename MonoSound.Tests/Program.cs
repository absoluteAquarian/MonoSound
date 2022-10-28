using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace MonoSound.Tests {
	public static class Program {
		[STAThread]
		static void Main() {
			AppDomain.CurrentDomain.FirstChanceException += LogException;

			try {
				using var game = new Game1();
				game.Run();
			} catch (Exception ex) {
				Debug.WriteLine(ex);
			}
		}

		private static void LogException(object sender, FirstChanceExceptionEventArgs e) {
			Debug.WriteLine(e.Exception);
		}
	}
}
