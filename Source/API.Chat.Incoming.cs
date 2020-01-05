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
using framebunker;
using Utf8Json;
using Utf8Json.Resolvers;


namespace Keybase
{
	partial class API
	{
		partial class Chat
		{
			[Flags] public enum DeletePolicy
			{
				Remove = 1,
				Callback = 1 << 1,
				CallbackAndRemove = Remove | Callback
			}


			public interface IListener
			{
				DeletePolicy DeletePolicy { get; }


				void OnIncoming (Message message);
				void OnReaction (Reaction reaction);
				void OnDelete (Message.ID target);
				void OnError ();
			}


			/// <remarks>Runs <see cref="Keybase.API.Environment.EnsureInitialization"/>.</remarks>
			public static void Listen ([NotNull] IListener listener)
			{
				Process process;
				if (null == (process = CreateProcess (kListenArguments)))
				{
					listener.OnError ();

					return;
				}

				process.OutputDataReceived += (sender, arguments) =>
				{
					if (null == process || null == arguments.Data)
					{
						return;
					}

					Log.Debug (typeof (Chat), Log.Type.Message, "Incoming: {0}", arguments.Data);

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
								Log.Message ("API.Chat.Listen invoking OnIncoming: {0}", message);
#endif

								listener.OnIncoming (message);
							}
							else
							{
								Log.Error ("API.Chat.Listen failed to log an incoming message. Keeping busy?");
							}
						break;
						case Incoming.Content.Type.Reaction:
							if (!incoming.MarkReceipt ())
							{
								Log.Error ("API.Chat.Listen MarkReceipt failed. Incoming contained no message structure?");
								return;
							}

							Reaction reaction = incoming.ToReaction ();

							if (reaction.Valid)
							{
#if DEBUG_API_LISTEN
								Log.Message ("API.Chat.Listen invoking OnReaction: {0}", reaction);
#endif

								listener.OnReaction (reaction);
							}
							else
							{
								Log.Error ("API.Chat.Listen failed to log an incoming reaction. Keeping busy?");
							}
						break;
						case Incoming.Content.Type.Delete:
							DeletePolicy policy = listener.DeletePolicy;

							using (PooledList<Message.ID> targets = incoming.ToDeleteList ())
							{
								if (null == targets)
								{
									Log.Error ("API.Chat.Listen received delete with an invalid targets list");

									return;
								}

#if DEBUG_API_LISTEN
								Log.Message ("API.Chat.Listen handling delete of {0} messages", targets.Value.Count);
#endif

								if (policy.HasFlag (DeletePolicy.Callback))
								{
									foreach (Message.ID target in targets.Value)
									{
										listener.OnDelete (target);
									}
								}

								if (policy.HasFlag (DeletePolicy.Remove))
								{
									foreach (Message.ID target in targets.Value)
									{
										TryRemoveFromLog (target);
									}
								}
							}
						break;
						case Incoming.Content.Type.Unknown:
							Log.Error ("API.Chat.Listen received unknown content type. Incomplete API spec? Data:\n{0}", arguments.Data);
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

					Log.Debug (typeof (Chat), Log.Type.Message, "API.Chat.Listen process exit");

					listener.OnError ();

					process.EnableRaisingEvents = false;
					process.Dispose ();
					process = null;
				};

				if (!process.Start ())
				{
					Log.Error ("API.Chat.Listen process failed to start");

					listener.OnError ();

					return;
				}

				process.BeginOutputReadLine ();
				process.BeginErrorReadLine ();
			}
		}
	}
}
