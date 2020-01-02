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
	public struct User : IEquatable<User>
	{
		[NotNull] public string Name { get; }
		public bool Valid => !string.IsNullOrWhiteSpace (Name);
		public Channel Channel => Valid ? Channel.Direct (this) : default;


		public User ([NotNull] string name)
		{
			Name = name.ToLower ();
		}


		public override string ToString () => Name;
		public override int GetHashCode () => Name.GetHashCode ();

		public bool Equals (User other) => Name.Equals (other.Name, StringComparison.InvariantCultureIgnoreCase);
		public override bool Equals (object obj) => obj != null && obj is User other && ((IEquatable<User>)this).Equals (other);

		public static bool operator== (User a, User b) => ((IEquatable<User>)a).Equals (b);
		public static bool operator!= (User a, User b) => !((IEquatable<User>)a).Equals (b);
	}
}
