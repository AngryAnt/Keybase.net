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
using framebunker;


namespace Keybase
{
	partial class API
	{
		public static partial class Chat
		{
			private const string
				kAPIArguments = "chat api",
				kListenArguments = "chat api-listen",
				kListenOutputStart = "Listening for chat notifications.";
			private const int
				kAPIProcessPoolSize = 10,
				kMessageLogMax = 2048,
				kMessageLogResize = 1024,
				kIDListPoolSize = 32;
			private const double kAPIProcessTimeoutSeconds = 10;


			private static ListPool<Message.ID> s_IDListPool = new ListPool<Message.ID> (kIDListPoolSize);
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


			private static bool FindMessage (Message.ID id, out Message.Data result, bool pop)
			{
				Incoming.Message cache = s_Messages.Find (m => id == m.GetID (), pop: pop);

				if (null == cache)
				{
					result = default;
					return false;
				}

				result = new Message.Data
				{
					Channel = Channel.Deserialized (cache.GetChannel ()),
					Author = new User (cache.GetSender ()?.GetName () ?? ""),
					Contents = cache.GetContent ()?.GetText ()?.GetBody () ?? ""
				};

				return true;
			}


			private static bool FindReaction (Message.ID id, out Reaction.Data result, bool pop)
			{
				Incoming.Message cache = s_Messages.Find (m => id == m.GetID (), pop: pop);

				if (null == cache)
				{
					result = default;
					return false;
				}

				Incoming.Reaction reaction = cache.GetContent ()?.GetReaction ();

				result = new Reaction.Data
				{
					Channel = Channel.Deserialized (cache.GetChannel ()),
					Author = new User (cache.GetSender ()?.GetName () ?? ""),
					Target = cache.GetReactionTargetID (),
					Contents = reaction?.GetBody () ?? ""
				};

				return true;
			}


			/// <summary>
			/// Remove the message or reaction from the log, returning whether it was deleted or could not be found
			/// </summary>
			public static bool TryRemoveFromLog (Message.ID id) => FindMessage (id, out Message.Data result, pop: true);


			/// <summary>
			/// Read the message from the log, returning whether the read was successful, passing the data via out if so
			/// </summary>
			public static bool TryReadFromLog (Message.ID id, out Message.Data result) => FindMessage (id, out result, pop: false);


			/// <summary>
			/// Remove the message from the log, returning whether it was found, passing its data via out if so
			/// </summary>
			public static bool TryRemoveFromLog (Message.ID id, out Message.Data result) => FindMessage (id, out result, pop: true);


			/// <summary>
			/// Read the reaction from the log, returning whether the read was successful, passing the data via out if so
			/// </summary>
			public static bool TryReadFromLog (Message.ID id, out Reaction.Data result) => FindReaction (id, out result, pop: false);


			/// <summary>
			/// Remove the reaction from the log, returning whether it was found, passing its data via out if so
			/// </summary>
			public static bool TryRemoveFromLog (Message.ID id, out Reaction.Data result) => FindReaction (id, out result, pop: true);


			/// <summary>
			/// Returns the number of reactions to the given <see cref="id"/>
			/// </summary>
			public static int CountReactions (Message.ID id)
			{
				int result = 0;
				s_Messages.Foreach (m =>
				{
					if (m.GetReactionTargetID () == id)
					{
						++result;
					}
				});

				return result;
			}


			/// <summary>
			/// Read the logged reactions to this message into the <see cref="destination"/> array,
			/// at an optional <see cref="offset"/>, returning available reactions count
			/// </summary>
			public static int ReadReactions (Message.ID id, [NotNull] Reaction[] destination, int offset = 0)
			{
				int count = 0;
				offset = offset < 0 ? 0 : offset;

				s_Messages.Foreach (m =>
				{
					if (m.GetReactionTargetID() != id)
					{
						return;
					}

					int index = offset + count++;

					if (index < destination.Length)
					{
						destination[index] = new Reaction (m.GetID ());
					}
				});

				return count;
			}
		}
	}
}
