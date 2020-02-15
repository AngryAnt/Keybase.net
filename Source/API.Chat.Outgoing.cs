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
using System.Threading.Tasks;
using System.Timers;
using Utf8Json;
using Utf8Json.Resolvers;


namespace Keybase
{
	partial class API
	{
		partial class Chat
		{
			private const string
				kMessageResult = "message sent",
				kReactionResult = "message reacted to",
				kDeleteResult = "message deleted";


			/// <remarks>Runs <see cref="Keybase.API.Environment.EnsureInitialization"/>.</remarks>
			/// <remarks>For available requests and responses, see keybase chat api -h</remarks>
			public static void Request ([NotNull] string json, [NotNull] Action<string> onResult, [NotNull] Action onError)
			{
				Log.Debug (typeof (Chat), Log.Type.Message, "Outgoing: {0}", json);

				PooledProcess pooledProcess;
				if (null == (pooledProcess = RetainAPIProcess ()))
				{
					Log.Error ("API.Chat.Request: Failed to retain a process from the pool");
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

						if (response.IsError)
						{
							Log.Error ("API.Chat.Request received error response: {0}", response.ToString ());
							onError ();
						}
						else if (!response.Valid)
						{
							Log.Error ("API.Chat.Request received invalid response: {0}", arguments.Data);
						}
						else
						{
#if DEBUG_API_TRANSMISSION
							Log.Message ("API.Chat.Request invoking result handler: {0}", response.ToString ());
#endif

							onResult (response.ToString ());
						}

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

						Log.Error ("API.Chat.Request process " + (arguments is ElapsedEventArgs ? "timeout" : "unexpected exit"));

						onError ();

						pooledProcess.Dispose ();
						pooledProcess = null;
					}
				).Timeout = kAPIProcessTimeoutSeconds * 1000;

#if DEBUG_API_TRANSMISSION
				Log.Message ("API.Chat.Request: Writing to process: {0}", json);
#endif

				pooledProcess.StandardInput.Write (json);
			}


			[NotNull] public static Task<bool> MessageAsync (Channel destination, User target, [NotNull] string text)
			{
				return MessageAsync (destination, destination == target.Channel ? text : "@" + target + ": " + text);
			}


			[NotNull] public static Task<bool> MessageAsync (Channel destination, [NotNull] string text)
			{
				TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool> ();

				SanitiseMessageContents (ref text);

				Request (
					json: "{\"method\": \"send\", \"params\": {\"options\": {\"channel\": " + destination.ToOutgoingJSON () + ", \"message\": {\"body\": \"" + text + "\"}}}}",
					onResult: result => ValidateResult (result, kMessageResult, ref completionSource),
					onError: () => completionSource.SetResult (false)
				);

				return completionSource.Task;
			}


			[NotNull] public static async Task<bool> ReplyAsync (Message message, [NotNull] string text)
			{
				if (!message.TryRead (out Message.Data data))
				{
					return false;
				}

				SanitiseMessageContents (ref text);

				TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool> ();

				Request (
					json: "{\"method\": \"send\", \"params\": {\"options\": {\"channel\": " + data.Channel.ToOutgoingJSON () + ", \"message\": {\"body\": \"" + text + "\"}, \"reply_to\": " + message.Self.MessageID + "}}}",
					onResult: result => ValidateResult (result, kMessageResult, ref completionSource),
					onError: () => completionSource.SetResult (false)
				);

				return await completionSource.Task;
			}


			[NotNull] public static Task<bool> ReactAsync (Channel destination, Message message, [NotNull] string reaction)
			{
				return ReactAsync (destination, message.Self, reaction);
			}


			[NotNull] public static Task<bool> ReactAsync (Channel destination, Message.ID messageID, [NotNull] string reaction)
			{
				TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool> ();

				SanitiseMessageContents (ref reaction);

				Request (
					json: "{\"method\": \"reaction\", \"params\": {\"options\": {\"channel\": " + destination.ToOutgoingJSON () +
						", \"message_id\": " + messageID.MessageID +", \"message\": {\"body\": \"" + reaction + "\"}}}}",
					onResult: result => ValidateResult (result, kReactionResult, ref completionSource),
					onError: () => completionSource.SetResult (false)
				);

				return completionSource.Task;
			}


			[NotNull] public static Task<bool> DeleteAsync (Channel destination, Message message)
			{
				return DeleteAsync (destination, message.Self);
			}


			[NotNull] public static Task<bool> DeleteAsync (Channel destination, Message.ID messageID)
			{
				TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool> ();

				Request (
					json: "{\"method\": \"delete\", \"params\": {\"options\": {\"channel\": " + destination.ToOutgoingJSON () +
						", \"message_id\": " + messageID.MessageID +"}}}",
					onResult: result => ValidateResult (result, kDeleteResult, ref completionSource),
					onError: () => completionSource.SetResult (false)
				);

				return completionSource.Task;
			}


			private static void ValidateResult (
				[CanBeNull] string result,
				[NotNull] string expected,
				[CanBeNull] ref TaskCompletionSource<bool> handler
			)
			{
				if (null == handler)
				{
					return;
				}

				bool valid = null != result && result.Equals (expected, StringComparison.InvariantCultureIgnoreCase);

				if (!valid)
				{
					Log.Error (
						"API.Chat.ValidateResult: Unexpected result (in quotes):\n\"{0}\"\nFrom:\n{1}",
						result ?? "(null)",
						System.Environment.StackTrace
					);
				}

				handler.SetResult (valid);
				handler = null;
			}


			private static void SanitiseMessageContents ([NotNull] ref string text)
			{
				// TODO: This should probably be a StringBuilder parameter which we run a regex replace on
				text = text.Replace ("\n", "\\n").Replace ("\t", "\\t").Replace ("\"", "\\\"");
			}
		}
	}
}
