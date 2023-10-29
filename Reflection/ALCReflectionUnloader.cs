using System;
using System.Reflection;
using System.Runtime.Loader;

namespace MonoSound.Reflection {
	internal static class ALCReflectionUnloader {
		public static void OnUnload(Assembly assembly, Action action) {
			// Normally, there'd be a check against certain assemblies here... but that's not
			//   necessary since MonoSound only uses FastReflection for certain members

			AssemblyLoadContext alc = AssemblyLoadContext.GetLoadContext(assembly);

			if (alc != null)
				alc.Unloading += _ => action();
		}
	}
}
