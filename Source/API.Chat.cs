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
			private const int
				kAPIProcessPoolSize = 10,
				kMessageLogMax = 2048,
				kMessageLogResize = 1024;
			private const double kAPIProcessTimeoutSeconds = 10;


			// TODO: Since we have arrays in the mix, there is little point to convert to structs. Look at using pools and custom deserialization handlers in stead.


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


				public override string ToString ()
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
				public class Message
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


					public DateTime Received { get; internal set; }


					public Keybase.Message.ID GetID () => new Keybase.Message.ID (conversation_id, id);
					public Channel GetChannel () => channel;
					public Sender GetSender () => sender;
					public Content GetContent () => content;
				}


				public class Channel
				{
					private string
						name,
						members_type,
						topic_type/*,
						topic_name*/; // TODO: Optional


					public string GetName () => name;
				}


				public class Sender
				{
					private string
						uid,
						username,
						deviceid,
						device_name;


					public string GetName () => username;
				}


				public class Content
				{
					public enum Type
					{
						Unknown,
						Text,
						Reaction,
						Edit,
						Delete
					}


					private string type; // text/reaction/edit/delete
					private Text text; // optional
					private Reaction reaction; // optional
					private Delete delete; // optional
					private Text edit; // optional


					public Text GetText () => text;
					public Type GetContentType () => Enum.TryParse (typeof (Type), type, true, out object result) ? (Type)result : Type.Unknown;
				}


				public class Text
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


				public class Reaction
				{
					private ulong m;
					private string b;
				}


				public class Delete
				{
					private ulong[] messageIDs;
				}


				public class UserMention
				{
					private string
						text,
						uid;
				}


				public class TeamMention
				{
					private string
						name,
						channel;
				}


				public class Pagination
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


				public Content.Type GetContentType () => msg?.GetContent ()?.GetContentType () ?? Content.Type.Unknown;


				public Keybase.Message ToMessage ()
				{
					Keybase.Message.ID id;

					// Bail if this is not a valid message after all
					if (null == msg || !(id = msg.GetID ()).Valid)
					{
						return Keybase.Message.Invalid;
					}

					// If this message is logged, just return a wrapper for it
					if (s_Messages.Find (m => msg == m) == msg)
					{
						return new Keybase.Message (id);
					}

					// If we already hold a different version of this message, get rid of it (we assume this one is newer)
					s_Messages.Find (m => id == m.GetID (), pop: true);

					// TODO: When we have reactions and such integrated, we should merge that part of the popped message into the new one

					// Try to add the new message, resizing the log if necessary
					if (!s_Messages.Add (msg))
					{
						ReduceMessageLog ();

						if (!s_Messages.Add (msg))
						{
							return Keybase.Message.Invalid;
						}
					}

					// Clean up the old instance if found, return new wrapper
					return new Keybase.Message (id);
				}


				public bool MarkReceipt ()
				{
					if (null == msg)
					{
						return false;
					}

					msg.Received = DateTime.Now;

					return true;
				}
			}
#pragma warning restore CS0169, CS0649


			private static Pool<PooledProcess> s_APIProcessPool = null;
			private static Pool<Incoming.Message> s_Messages =
				new Pool<Incoming.Message> (kMessageLogMax, p => new Incoming.Message ());


			[CanBeNull] private static PooledProcess RetainAPIProcess ()
			{
				return (s_APIProcessPool ??= PooledProcess.CreatePool (kAPIProcessPoolSize, kAPIArguments))?.Retain ();
			}


			private static void ReduceMessageLog ()
			{
				if (kMessageLogResize > s_Messages.Stored)
				{
					return;
				}

				// Run through the log to determine min & max receipt time, then determine the medium value as halfway between the two //

				long
					min = DateTime.MaxValue.Ticks,
					max = DateTime.MinValue.Ticks;

				s_Messages.Foreach (m =>
				{
					long current = m.Received.Ticks;

					if (min > current)
					{
						min = current;
					}

					if (max < current)
					{
						max = current;
					}
				});

				long mid = min + (max - min) / 2;

				// While log size is still above target, try to remove messages older than medium
				while (s_Messages.Stored > kMessageLogResize && null != s_Messages.Find (m => m.Received.Ticks < mid, pop: true))
				{}
			}


			/// <summary>
			/// Read the message from the log, returning whether the read was successful, passing the data via out if so
			/// </summary>
			public static bool TryReadFromLog (Message.ID id, out Message.Data result)
			{
				Incoming.Message cache = s_Messages.Find (m => id == m.GetID ());

				if (null == cache)
				{
					result = default;
					return false;
				}

				result = new Message.Data
				{
					Channel = cache.GetChannel ()?.GetName () ?? "",
					Author = cache.GetSender ()?.GetName () ?? "",
					Contents = cache.GetContent ()?.GetText ()?.GetBody () ?? ""
				};

				return true;
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

#if DEBUG_API_LISTEN
					Log.Message ("API.Chat.Listen process received data: {0}", arguments.Data);
#endif

					Incoming incoming = JsonSerializer.Deserialize<Incoming> (
						arguments.Data,
						StandardResolver.AllowPrivateCamelCase
					);

					Incoming.Content.Type type = incoming.GetContentType ();
					switch (type)
					{
						case Incoming.Content.Type.Text:
							if (!incoming.MarkReceipt ())
							{
								Log.Error ("API.Chat.Listen MarkReceipt failed. Incoming contained no message structure?");
								return;
							}

							Message message = incoming.ToMessage ();

							if (message.Valid)
							{
#if DEBUG_API_LISTEN
								Log.Message ("API.Chat.Listen invoking result handler: {0}", response.Message);
#endif

								onIncoming (message);
							}
							else
							{
								Log.Error ("API.Chat.Listen failed to log an incoming message. Keeping busy?");
							}
						break;
						case Incoming.Content.Type.Unknown:
							Log.Error ("API.Chat.Listen received unknown content type. Incomplete API spec?", arguments.Data);
						return;
						default:
							Log.Warning ("API.Chat.Listen received unhandled content type: {0}", type);
						break;
					}
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

#if DEBUG_API_LISTEN
					Log.Message ("API.Chat.Listen process exit");
#endif

					onError ();

					process.EnableRaisingEvents = false;
					process.Dispose ();
					process = null;
				};

				if (!process.Start ())
				{
#if DEBUG_API_LISTEN
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
