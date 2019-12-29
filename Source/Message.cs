using System;
using Math = framebunker.Math;


namespace Keybase
{
	/// <summary>
	/// A message received by the Chat API, updated with edits as long as it fits in the log
	/// </summary>
	public struct Message : IEquatable<Message>, IEquatable<Message.ID>
	{
		/// <summary>
		/// A unique message identifier
		/// </summary>
		public struct ID : IEquatable<ID>, IEquatable<Message>
		{
			public static ID Invalid => new ID ();


			public bool Valid => MessageID > 0;


			// TODO: Determine if there is any actual need to keep the IDs around
			private string ConversationID { get; }
			internal ulong MessageID { get; }
			private int HashCode { get; }


			public ID ([NotNull] string conversationID, ulong messageID)
			{
				ConversationID = conversationID;
				MessageID = messageID;
				HashCode = Math.GetHashCode (conversationID, messageID);
			}


			public bool Equals (ID other) => HashCode == other.HashCode;
			public bool Equals (Message other) => Equals (other.Self);


			public override bool Equals (object obj) =>
				(obj is ID id && id.Equals (this)) || (obj is Message message && message.Self.Equals (this));
			public override int GetHashCode () => HashCode;


			public static bool operator == (ID a, ID b) => a.HashCode == b.HashCode;
			public static bool operator != (ID a, ID b) => a.HashCode != b.HashCode;


			public override string ToString () => ConversationID + ", " + MessageID;
		}


		/// <summary>
		/// A snapshot of the contents of a <see cref="Message"/>
		/// </summary>
		public struct Data
		{
			public string Channel { get; internal set; }
			public string Author { get; internal set; }
			public string Contents { get; internal set; }
		}


		public static Message Invalid => new Message ();


		public bool Valid => Self.Valid;


		internal ID Self { get; }


		public Message (ID id)
		{
			Self = id;
		}


		/// <summary>
		/// Read the message from the log, returning whether the read was successful, passing the data via out if so
		/// </summary>
		public bool TryRead (out Data result)
		{
			if (!Valid)
			{
				result = default;
				return false;
			}

			return API.Chat.TryReadFromLog (Self, out result);
		}


		/// <summary>
		/// Count the number of logged reactions to this message
		/// </summary>
		public int CountReactions ()
		{
			return API.Chat.CountReactions (Self);
		}


		/// <summary>
		/// Read the logged reactions to this message into the <see cref="destination"/> array,
		/// at an optional <see cref="offset"/>, returning available reactions count
		/// </summary>
		public int ReadReactions (Reaction[] destination, int offset = 0)
		{
			return API.Chat.ReadReactions (Self, destination, offset);
		}


		bool IEquatable<Message>.Equals (Message other) => other.Self == Self;
		bool IEquatable<ID>.Equals (ID other) => other == Self;


		public override bool Equals (object obj) =>
			((obj is ID id && id.Equals (Self)) || (obj is Message message && message.Self.Equals (Self)));

		public override int GetHashCode () => Self.GetHashCode ();


		public static bool operator == (Message a, ID b) => a.Self == b;
		public static bool operator != (Message a, ID b) => a.Self != b;

		public static bool operator == (ID a, Message b) => a == b.Self;
		public static bool operator != (ID a, Message b) => a != b.Self;
	}
}
