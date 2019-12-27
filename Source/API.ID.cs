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


// #define DEBUG_API_TRANSMISSION


using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Utf8Json;
using Utf8Json.Resolvers;


namespace Keybase
{
	partial class API
	{
		public static class ID
		{
			private const string kAPIArguments = "id -j";


#pragma warning disable CS0169, CS0649
			[
				SuppressMessage ("ReSharper", "ClassNeverInstantiated.Local"),
				SuppressMessage ("ReSharper", "UnusedAutoPropertyAccessor.Local"),
				SuppressMessage ("ReSharper", "InconsistentNaming"),
				SuppressMessage ("ReSharper", "IdentifierTypo"),
				SuppressMessage ("ReSharper", "ConvertToAutoProperty")
			]
			private class Response
			{
				private class Cryptocurrency
				{
					private int rowId;
					private string
						pkhash,
						address,
						sigID,
						type,
						family;
				}


				private class Stellar
				{
					private string
						accountID,
						federationAddress,
						sigID;
				}


				private class IdentifyKey
				{
					private string
						pgpFingerprint,
						KID;
					private bool breaksTracking;
					private string sigID;
				}


				private class Proof
				{
					private class Definition
					{
						private int proofType;
						private string
							key,
							value,
							displayMarkup,
							sigID;
						private ulong mTime;
					}


					private class Data
					{
						private class Result
						{
							private int
								state,
								status;
							private string desc;
						}


						private class Cache
						{
							private Result proofResult;
							private ulong
								time,
								freshness;
						}


						private class Hint
						{
							private string
								remoteId,
								humanURL,
								apiURL,
								checkText;
						}


						private int proofId;
						private Result
							proofResult,
							snoozedResult;
						private bool torWarning;
						private int tmpTrackExpireTime;
						private Cache cached;
						private Hint hint;
						private bool breaksTracking;
					}


					private Definition proof;
					private Data result;
				}


				private string
					username,
					lastTrack;
				private Cryptocurrency[] cryptocurrencies;
				private Stellar stellar;
				private IdentifyKey identifyKey;
				private Proof[] proofs;


				public string Name => username;
			}
#pragma warning restore CS0169, CS0649


			/// <remarks>Assumes valid <see cref="Keybase.API.Environment.BinaryPath"/>,
			/// but does not run <see cref="Keybase.API.Environment.EnsureInitialization"/>.</remarks>
			public static void Request ([NotNull] Action<string> onResult, [NotNull] Action onError)
			{
				Process process = CreateProcess (Environment.BinaryPath, kAPIArguments);

				process.ErrorDataReceived += (sender, arguments) =>
				{
					if (null == process || null == arguments.Data)
					{
						return;
					}

					Log.Error ("API.ID process received error output: {0}", arguments.Data);
				};

				process.Exited += (sender, arguments) =>
				{
					if (null == process)
					{
						return;
					}

#if DEBUG_API_ID
					Log.Message ("API.ID process exit");
#endif

					string data = process.StandardOutput.ReadToEnd ();

					if (!string.IsNullOrWhiteSpace (data))
					{
#if DEBUG_API_ID
						Log.Message("API.ID process data: {0}", data);
#endif

						Response response = JsonSerializer.Deserialize<Response> (data, StandardResolver.AllowPrivateCamelCase);

#if DEBUG_API_ID
						Log.Message("API.ID.Request invoking result handler: {0}", response.Name);
#endif

						onResult (response.Name);
					}
					else
					{
						Log.Warning ($"API.ID received invalid response data: '{data ?? "(null)"}'");
					}

					process.EnableRaisingEvents = false;
					process.Dispose ();
					process = null;
				};

#if DEBUG_API_ID
				Log.Message (
					"API.ID starting process at {0} with arguments '{1}'",
					process.StartInfo.FileName,
					process.StartInfo.Arguments
				);
#endif

				if (!process.Start ())
				{
#if DEBUG_API_ID
					Log.Message ("API.ID process failed to start");
#endif

					onError ();

					return;
				}

				process.BeginErrorReadLine ();
			}
		}
	}
}
