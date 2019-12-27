using System;
using Math = framebunker.Math;


namespace Keybase
{
	/// <summary>
	/// A message received by the Chat API, updated with edits as long as it fits in the log
	/// </summary>
	public struct Message
	{
		/// <summary>
		/// A unique message identifier
		/// </summary>
		public struct ID : IEquatable<ID>
		{
			public static ID Invalid => new ID ();


			public bool Valid => MessageID > 0;


			// TODO: Determine if there is any actual need to keep the IDs around
			private string ConversationID { get; }
			private ulong MessageID { get; }
			private int HashCode { get; }


			public ID ([NotNull] string conversationID, ulong messageID)
			{
				ConversationID = conversationID;
				MessageID = messageID;
				HashCode = Math.GetHashCode (conversationID, messageID);
			}


			public bool Equals (ID other) => HashCode == other.HashCode;


			public override bool Equals (object obj) => null != obj && obj is ID other && HashCode == other.HashCode;
			public override int GetHashCode () => HashCode;


			public static bool operator == (ID a, ID b) => a.HashCode == b.HashCode;
			public static bool operator != (ID a, ID b) => a.HashCode != b.HashCode;
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


		private ID Self { get; }


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
	}
}
