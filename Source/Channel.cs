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

namespace Keybase
{
	// TODO: Add intellisense comments
	public struct Channel : IEquatable<Channel>
	{
		internal static Channel Deserialized ([CanBeNull] API.Chat.Incoming.Channel channel)
		{
			if (null == channel)
			{
				return Invalid;
			}

			Channel result = new Channel ();

			string
				name = channel.GetName (),
				team = channel.GetTeam ();

			if (!string.IsNullOrWhiteSpace (team))
			{
				result.Team = Team.Deserialized (team);
			}

			result.Name = name;

			return result;
		}


		public static Channel Invalid => new Channel ();
		public static Channel Self () => Direct (API.Environment.User);
		public static Channel Direct (User other) => new Channel { Name = other + "," + API.Environment.User };
		public static Channel InTeam (Team team, [NotNull] string name) => new Channel { Team = team, Name = name };


		public Team Team { get; private set; }
		[NotNull] public string Name { get; private set; }
		public bool Valid => !string.IsNullOrWhiteSpace (Name);
		public bool IsTeam => Team.Valid;
		public bool IsDirect => !IsTeam && Name.Contains (',');


		public bool TryGetDirectRecipient (out User other)
		{
			if (!IsDirect)
			{
				other = default;
				return false;
			}

			other = new User (Name.Substring (0, Name.IndexOf (',')));
			return true;
		}


		public string ToOutgoingJSON () => Team.Valid
			? "{\"name\": \"" + Team.Name + "\", \"members_type\": \"team\", \"topic_name\": \"" + Name + "\"}"
			: "{\"name\": \"" + Name + "\"}";


		public override string ToString () => Team.Valid ? Team + "#" + Name : Name;
		public override int GetHashCode () => ToString ().GetHashCode ();

		public bool Equals (Channel other) =>
			Team.Equals (other.Team) &&
			Name.Equals (other.Name, StringComparison.InvariantCultureIgnoreCase);
		public override bool Equals (object obj) => obj != null && obj is Channel other && ((IEquatable<Channel>)this).Equals (other);

		public static bool operator== (Channel a, Channel b) => ((IEquatable<Channel>)a).Equals (b);
		public static bool operator!= (Channel a, Channel b) => !((IEquatable<Channel>)a).Equals (b);
	}
}
