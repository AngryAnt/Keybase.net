namespace Keybase
{
	/// <summary>
	/// A message received by the Chat API, updated with edits as long as it fits in the log
	/// </summary>
	public struct Message
	{
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


		public bool Valid => ID > 0;


		private ulong ID { get; }


		public Message (ulong id)
		{
			ID = id;
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

			return API.Chat.TryReadFromLog (ID, out result);
		}
	}
}
