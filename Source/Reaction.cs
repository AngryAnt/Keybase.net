namespace Keybase
{
	public struct Reaction
	{
		/// <summary>
		/// A snapshot of the contents of a <see cref="Reaction"/>
		/// </summary>
		public struct Data
		{
			public string Channel { get; internal set; }
			public string Author { get; internal set; }
			public Message.ID Target { get; internal set; }
			public string Contents { get; internal set; }
		}


		public static Reaction Invalid => new Reaction ();


		public bool Valid => Self.Valid;


		private Message.ID Self { get; }


		public Reaction (Message.ID id)
		{
			Self = id;
		}


		/// <summary>
		/// Read the reaction from the log, returning whether the read was successful, passing the data via out if so
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
