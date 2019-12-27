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


namespace Keybase
{
	// TODO: Add intellisense comments
	public class Connection : IDisposable
	{
		private const string kMessageSentResult = "message sent";


		private class SelfChannel : Channel
		{
			private Connection m_Owner = null;


			public override string Name => m_Owner.User.Name;


			public SelfChannel ([NotNull] Connection owner) => m_Owner = owner;
		}


		private class DirectChannel : Channel
		{
			private Connection m_Owner = null;
			private User m_Target = null;


			public override string Name => m_Owner.User + "," + m_Target;


			public DirectChannel ([NotNull] Connection owner, [NotNull] User target)
			{
				m_Owner = owner;
				m_Target = target;
			}
		}


		private static void SanitiseMessageContents ([NotNull] ref string text)
		{
			// TODO: This should probably be a StringBuilder parameter which we run a regex replace on
			text = text.Replace ("\n", "\\n").Replace ("\t", "\\t").Replace ("\"", "\\\"");
		}


		// TODO: Consider pros & cons of maintaining some sort of channel list in stead of just creating them - any downsides?


		[NotNull] public User User { get; private set; }
		[NotNull] public Channel Self { get; private set; }


		public Connection ()
		{
			Self = new SelfChannel (this);
			API.Environment.EnsureInitialization ();
			User = new User (API.Environment.Username);
		}


		[NotNull] public Channel CreateChannel ([NotNull] User target)
		{
			return new DirectChannel (this, target);
		}


		[NotNull] public Task<bool> MessageAsync ([NotNull] Channel destination, [NotNull] string text)
		{
			TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool> ();

			SanitiseMessageContents (ref text);
			text = text.Replace ("\n", "\\n").Replace ("\t", "\\t").Replace ("\"", "\\\"");

			API.Chat.Request (
				json: "{\"method\": \"send\", \"params\": {\"options\": {\"channel\": {\"name\": \"" + destination + "\"}, \"message\": {\"body\": \"" + text + "\"}}}}",
				onResult: result =>
				{
					if (completionSource == null)
					{
						return;
					}

					completionSource.SetResult (result != null && result.Equals (kMessageSentResult, StringComparison.InvariantCultureIgnoreCase));
					completionSource = null;
				},
				onError: () => completionSource.SetResult (false)
			);

			return completionSource.Task;
		}


		void IDisposable.Dispose ()
		{
			Self = null;
			User = null;
		}
	}
}
