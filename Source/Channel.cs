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
using System.Collections.Generic;
using framebunker;


namespace Keybase
{
	// TODO: Add intellisense comments
	public abstract class Channel : API.Chat.IListener
	{
		public interface IListener
		{
			void OnMessage (Message message);
		}


		private List<WeakReference<IListener>> m_Listeners = new List<WeakReference<IListener>> ();
		private bool m_Listening = false;


		[NotNull] public abstract string Name { get; }


		API.Chat.DeletePolicy API.Chat.IListener.DeletePolicy => API.Chat.DeletePolicy.Remove;


		public void AddListener ([NotNull] IListener listener)
		{
			m_Listeners.Add (new WeakReference<IListener> (listener));
			ReviewListening ();
		}


		public void RemoveListener ([NotNull] IListener listener)
		{
			m_Listeners.RemoveAll (r => !r.TryGetTarget (out IListener l) || l == listener);
			ReviewListening ();
		}


		public sealed override string ToString ()
		{
			return Name;
		}


		private void ReviewListening ()
		{
			if (m_Listening || m_Listeners.Count < 1)
			{
				return;
			}

			Log.Message ("With {0} listeners, {1} now starts listening", m_Listeners.Count, Name);

			API.Chat.Listen (this);
			m_Listening = true;
		}


		void API.Chat.IListener.OnIncoming (Message message)
		{
			// TODO: Actually store the incoming message? Or should we be able to get a Channel from the API and query its log the same way?
			if (!message.TryRead (out Message.Data data))
			{
				Log.Message ("{0} received a message, but was unable to read it", Name);

				return;
			}

			if (!data.Channel.Equals (Name, StringComparison.InvariantCultureIgnoreCase))
			{
				Log.Message ("{0} ignored incoming message from {1} in {2}", Name, data.Author, data.Channel);

				return;
			}

			for (int index = m_Listeners.Count - 1; index >= 0; --index)
			{
				if (!m_Listeners[index].TryGetTarget (out IListener listener))
				{
					Log.Message ("{0} cleaned up dead listener", Name);

					m_Listeners.FastRemoveAt (index);
					continue;
				}

				listener.OnMessage (message);
			}
		}


		void API.Chat.IListener.OnDelete (Message.ID target)
		{}


		void API.Chat.IListener.OnError ()
		{
			// Bail if we experienced an error while attempting to set up listening - no infinite recursion kthx
			if (!m_Listening)
			{
				return;
			}

			m_Listening = false;
			ReviewListening ();
		}
	}
}
