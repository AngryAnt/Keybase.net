/*
 *
Copyright 2019 Emil "AngryAnt" Johansen

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
documentation files (the "Software"), to deal in the Software without restriction, including without limitation the
rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit
persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the
Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 *
 */


using System.IO;
using System.Linq;
using System.Threading;


namespace Keybase
{
	partial class API
	{
		public static class Environment
		{
			private static readonly string[] kAutoTestBinaryPaths =
			{
				// Ubuntu default
				"/usr/bin/keybase",
				// macOS default
				"/usr/local/bin/keybase",
				// Windows default
				Path.Combine (
					System.Environment.GetFolderPath (System.Environment.SpecialFolder.LocalApplicationData),
					Path.Combine ("Keybase", "Keybase.exe")
				)
			};


			[NotNull] public static string BinaryPath = kAutoTestBinaryPaths[0];
			[NotNull] public static string Username = string.Empty;


			public static bool Initialized { get; private set; } = false;


			/// <summary>
			/// Run <see cref="Reinitialize"/> if it has not been run in this session.
			/// </summary>
			/// <returns>Whether initialization succeeded or not</returns>
			public static bool EnsureInitialization ()
			{
				return Initialized || Reinitialize ();
			}


			/// <summary>
			/// Auto-detect environment settings
			/// </summary>
			/// <returns>Whether all settings were populated or not</returns>
			public static bool Reinitialize ()
			{
				// Auto-detect Keybase binary on default path or go with alternative set path

				string detectedBinaryPath = kAutoTestBinaryPaths.FirstOrDefault (File.Exists);

				if (null == detectedBinaryPath)
				{
					Log.Error ("Keybase.API.Reinitialize: Unable to locate Keybase binary on default path");

					if (!File.Exists (BinaryPath))
					{
						Log.Error ("Keybase.API.Reinitialize: No Keybase binary available - unable to complete initialization");

						return Initialized = false;
					}
				}
				else
				{
					BinaryPath = detectedBinaryPath;
				}

				// With a binary available, we can ask it for the name of the current user

				AutoResetEvent callbackWaitHandle = new AutoResetEvent (false);
				string detectedUsername = string.Empty;

				ID.Request (
					result =>
					{
						detectedUsername = result;
						callbackWaitHandle.Set ();
					},
					() => callbackWaitHandle.Set ()
				);

				callbackWaitHandle.WaitOne ();

				if (string.IsNullOrEmpty (detectedUsername))
				{
					Log.Error ("Keybase.API.Reinitialize: User auto-detect failed");

					if (string.IsNullOrEmpty (Username))
					{
						Log.Error ("Keybase.API.Reinitialize: No Keybase user available - unable to complete initialization");

						return Initialized = false;
					}
				}
				else
				{
					Username = detectedUsername;
				}

				Log.Message ("Fully initialized as {0}", Username);

				return Initialized = true;
			}
		}
	}
}
