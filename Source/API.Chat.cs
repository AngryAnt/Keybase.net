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


using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Timers;
using framebunker;
using Utf8Json;
using Utf8Json.Resolvers;


namespace Keybase
{
	partial class API
	{
		public static class Chat
		{
			private const string
				kAPIArguments = "chat api",
				kListenArguments = "chat api-listen",
				kListenOutputStart = "Listening for chat notifications.";
			private const int kAPIProcessPoolSize = 10;
			private const double kAPIProcessTimeoutSeconds = 10;


#pragma warning disable CS0169, CS0649
			[
				SuppressMessage ("ReSharper", "ClassNeverInstantiated.Local"),
				SuppressMessage ("ReSharper", "UnusedAutoPropertyAccessor.Local"),
				SuppressMessage ("ReSharper", "InconsistentNaming"),
				SuppressMessage ("ReSharper", "IdentifierTypo")
			]
			private class Response
			{
				private class Result
				{
					private class Ratelimit
					{
						private string tank;
						private int
							capacity,
							reset,
							gas;
					}


					public string Message { get; private set; }
					private int id;
					private Ratelimit[] ratelimits;
				}


				private Result result;


				public override string ToString()
				{
					return result?.Message ?? "";
				}
			}


			[
				SuppressMessage ("ReSharper", "ClassNeverInstantiated.Local"),
				SuppressMessage ("ReSharper", "UnusedAutoPropertyAccessor.Local"),
				SuppressMessage ("ReSharper", "InconsistentNaming"),
				SuppressMessage ("ReSharper", "IdentifierTypo")
			]
			private class Incoming
			{
				private class Message
				{
					private ulong id;
					private string conversation_id;
					private Channel channel;
					private Sender sender;
					private ulong
						sent_at,
						sent_at_ms;
					private Content content;
					//private string prev; // TODO: Test API
					private bool unread;
					//private string[] at_mention_usernames; // TODO: Optional
					private string channel_mention;


					public Channel GetChannel () => channel;
					public Sender GetSender () => sender;
					public Content GetContent () => content;
				}


				private class Channel
				{
					private string
						name,
						members_type,
						topic_type/*,
						topic_name*/; // TODO: Optional


					public string GetName () => name;
				}


				private class Sender
				{
					private string
						uid,
						username,
						deviceid,
						device_name;


					public string GetName () => username;
				}


				private class Content
				{
					private string type;
					private Text text;


					public Text GetText () => text;
				}


				private class Text
				{
					private string
						body/*,
						payments*/; // TODO: Test API
					//private ulong replyTo; // TODO: Optional
					//private string replyToUID; // TODO: Optional
					private UserMention[] userMentions;
					private TeamMention[] teamMentions;


					public string GetBody () => body;
				}


				private class UserMention
				{
					private string
						text,
						uid;
				}


				private class TeamMention
				{
					private string
						name,
						channel;
				}


				private class Pagination
				{
					private string
						next,
						previous;
					private int num;
					private bool last;
				}


				private string
					type,
					source;
				private Message msg;
				private Pagination pagination;


				public Chat.Message ToMessage ()
				{
					return new Chat.Message
					{
						Channel = msg?.GetChannel ()?.GetName () ?? "",
						Author = msg?.GetSender ()?.GetName () ?? "",
						Contents = msg?.GetContent ()?.GetText ()?.GetBody () ?? ""
					};
				}
			}
#pragma warning restore CS0169, CS0649


			public struct Message
			{
				public string Channel { get; internal set; }
				public string Author { get; internal set; }
				public string Contents { get; internal set; }
			}


			private static Pool<PooledProcess> s_APIProcessPool = null;


			[CanBeNull] static PooledProcess RetainAPIProcess ()
			{
				return (s_APIProcessPool ??= PooledProcess.CreatePool (kAPIProcessPoolSize, kAPIArguments))?.Retain ();
			}


			/// <remarks>Runs <see cref="Keybase.API.Environment.EnsureInitialization"/>.</remarks>
			public static void Request ([NotNull] string json, [NotNull] Action<string> onResult, [NotNull] Action onError)
			{
				PooledProcess pooledProcess;
				if (null == (pooledProcess = RetainAPIProcess ()))
				{
					onError ();

					return;
				}

				pooledProcess.Initialize
				(
					onStandardOutput: (sender, arguments) =>
					{
						if (null == pooledProcess || null == arguments.Data)
						{
							return;
						}

#if DEBUG_API_TRANSMISSION
						Log.Message ("API.Chat.Request process received data: {0}", arguments.Data);
#endif

						Response response = JsonSerializer.Deserialize<Response> (arguments.Data, StandardResolver.AllowPrivateCamelCase);

#if DEBUG_API_TRANSMISSION
						Log.Message ("API.Chat.Request invoking result handler: {0}", response.Message);
#endif

						onResult (response.ToString ());

						pooledProcess.Dispose ();
						pooledProcess = null;
					},
					onStandardError: (sender, arguments) =>
					{
						if (null == pooledProcess || null == arguments.Data)
						{
							return;
						}

						Log.Error ("API.Chat.Request process received error output: {0}", arguments.Data);
					},
					onExit: (sender, arguments) =>
					{
						if (null == pooledProcess)
						{
							return;
						}

#if DEBUG_API_TRANSMISSION
						Log.Message ("API.Chat.Request process " + (arguments is ElapsedEventArgs ? "timeout" : "unexpected exit"));
#endif

						onError ();

						pooledProcess.Dispose ();
						pooledProcess = null;
					}
				).Timeout = kAPIProcessTimeoutSeconds * 100;

#if DEBUG_API_TRANSMISSION
				Log.Message ("API.Chat.Request: Writing to process: {0}", json);
#endif

				pooledProcess.StandardInput.Write (json);
			}


			/// <remarks>Runs <see cref="Keybase.API.Environment.EnsureInitialization"/>.</remarks>
			public static void Listen ([NotNull] Action<Message> onIncoming, [NotNull] Action onError)
			{
				Process process;
				if (null == (process = CreateProcess (kListenArguments)))
				{
					onError ();

					return;
				}

				process.OutputDataReceived += (sender, arguments) =>
				{
					if (null == process || null == arguments.Data)
					{
						return;
					}

#if DEBUG_API_TRANSMISSION
					Log.Message ("API.Chat.Listen process received data: {0}", arguments.Data);
#endif

					Message message = JsonSerializer.Deserialize<Incoming> (
						arguments.Data,
						StandardResolver.AllowPrivateCamelCase
					).ToMessage ();

#if DEBUG_API_TRANSMISSION
					Log.Message ("API.Chat.Listen invoking result handler: {0}", response.Message);
#endif

					onIncoming (message);
				};

				bool receivedStartMessage = false;

				process.ErrorDataReceived += (sender, arguments) =>
				{
					if (null == process || null == arguments.Data)
					{
						return;
					}

					if (!receivedStartMessage && arguments.Data.StartsWith (kListenOutputStart))
					{
						receivedStartMessage = true;
						return;
					}

					Log.Error ("API.Chat.Listen process received error output: {0}", arguments.Data);
				};

				process.Exited += (sender, arguments) =>
				{
					if (null == process)
					{
						return;
					}

#if DEBUG_API_TRANSMISSION
					Log.Message ("API.Chat.Listen process exit");
#endif

					onError ();

					process.EnableRaisingEvents = false;
					process.Dispose ();
					process = null;
				};

				if (!process.Start ())
				{
#if DEBUG_API_TRANSMISSION
					Log.Message ("API.Chat.Listen process failed to start");
#endif

					onError ();

					return;
				}

				process.BeginOutputReadLine ();
				process.BeginErrorReadLine ();
			}
		}
	}
}
