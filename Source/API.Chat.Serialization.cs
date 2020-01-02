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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using framebunker;


// TODO: Move the To[Something] methods out of this file - as well as the MarkReceipt one


namespace Keybase
{
	partial class API
	{
		partial class Chat
		{
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
					public Keybase.Message.ID GetReactionTargetID () =>
						new Keybase.Message.ID (conversation_id, content?.GetReaction ()?.GetTargetMessageID () ?? 0);
					public string GetConversationID () => conversation_id;
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


					public Type GetContentType () => Enum.TryParse (typeof (Type), type, true, out object result) ? (Type)result : Type.Unknown;
					public Text GetText () => text;
					public Reaction GetReaction () => reaction;
					public Delete GetDelete () => delete;
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


					public ulong GetTargetMessageID () => m;
					public string GetBody () => b;
				}


				public class Delete
				{
					private ulong[] messageIDs;


					public ulong[] GetIDs () => messageIDs;
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


				public Keybase.Reaction ToReaction ()
				{
					Keybase.Message.ID id;

					// Bail if this is not a valid reaction after all
					if (null == msg || !(id = msg.GetID ()).Valid)
					{
						return Keybase.Reaction.Invalid;
					}

					// If this reaction is logged, just return a wrapper for it
					if (s_Messages.Find (m => msg == m) == msg)
					{
						return new Keybase.Reaction (id);
					}

					// If we already hold a different version of this reaction, get rid of it (we assume this one is newer)
					s_Messages.Find (m => id == m.GetID (), pop: true);

					// Try to add the new reaction, resizing the log if necessary
					if (!s_Messages.Add (msg))
					{
						ReduceMessageLog ();

						if (!s_Messages.Add (msg))
						{
							return Keybase.Reaction.Invalid;
						}
					}

					// Clean up the old instance if found, return new wrapper
					return new Keybase.Reaction (id);
				}


				[CanBeNull] public PooledList<Keybase.Message.ID> ToDeleteList ()
				{
					ulong[] messageIDs = msg?.GetContent ()?.GetDelete ()?.GetIDs ();

					if (null == messageIDs)
					{
						return null;
					}

					PooledList<Keybase.Message.ID> result = s_IDListPool.Retain ();

					result.Value.AddRange (messageIDs.Select (messageID => new Keybase.Message.ID (msg.GetConversationID (), messageID)));

					return result;
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
		}
	}
}
