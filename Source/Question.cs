/*
 *
Copyright 2020 Emil "AngryAnt" Johansen

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
using System.Linq;
using System.Threading.Tasks;


namespace Keybase
{
	// TODO: Have this support sending a list of Question.Options in stead - with a string value as well as an index, resulting in that being returned rather than just a string
	// TODO: Consider more allocation-friendly options than just a basic class like this
	public class Question
	{
		private const string kDefaultPrompt = ":speech_balloon:";


		public enum PromptBehaviour
		{
			/// <summary>
			/// Delete the message & associated responses before re-sending that same message
			/// </summary>
			Replace,
			/// <summary>
			/// Delete the message & associated responses
			/// </summary>
			Delete,
			/// <summary>
			/// Take no action - leaving the message & associated responses in the chat
			/// </summary>
			Leave
		}


		public struct Response
		{
			public User From { get; internal set; }
			[NotNull] public string Text { get; internal set; }


			public bool Valid => From.Valid;


			public override string ToString () => Text;
		}


		public bool Valid
		{
			get => null != Options;
			set
			{
				if (!value)
				{
					Options = null;
				}
			}
		}
		public User User { get; private set; }
		public Channel Channel { get; private set; }
		[CanBeNull] public string Text { get; private set; }
		[NotNull] public string[] Options { get; private set; }


		private TaskCompletionSource<Message> m_RequestReceived;
		private Message m_RequestMessage;
		private TaskCompletionSource<Response> m_OptionChosen;


		public Question (Channel destination, User user, [NotNull] IEnumerable<string> options)
			: this (destination, user, options.ToArray ())
		{}


		public Question (Channel destination, User user, [NotNull] string text, [NotNull] IEnumerable<string> options)
			: this (destination, user, text, options.ToArray ())
		{}


		public Question (Channel destination, User user, Message message, [NotNull] IEnumerable<string> options)
			: this (destination, user, message, options.ToArray ())
		{}


		public Question (Channel destination, User user, [NotNull] string text, params string[] options)
			: this (destination, user, options)
		{
			Text = text;
		}


		public Question (Channel destination, User user, Message message, params string[] options)
			: this (destination, user, options)
		{
			if (message.TryRead (out Message.Data data))
			{
				Text = data.Contents;
				m_RequestMessage = message;
			}
			else
			{
				Valid = false;
			}
		}


		public Question (Channel destination, User user, params string[] options)
		{
			User = user;
			Channel = destination;
			Options = options;

			m_RequestMessage = Message.Invalid;
			Text = null;
		}


		/// <summary>
		/// Send the prompt message to the specified chat and set up reactions to it for the response options
		/// </summary>
		/// <returns>Whether all involved operations executed successfully</returns>
		public async Task<bool> AskAsync ()
		{
			if (!Valid)
			{
				return false;
			}

			if (!m_RequestMessage.Valid)
			{
				m_RequestReceived = new TaskCompletionSource<Message> ();

				if (!await API.Chat.MessageAsync (Channel, Text ?? kDefaultPrompt))
				{
					Log.Error ("Question.SetUpAsync: MessageAsync failed on '{0}' of '{1}'.", Channel, Text);
					return false;
				}

				m_RequestMessage = await m_RequestReceived.Task;
				m_RequestReceived = null;

				if (!m_RequestMessage.Valid)
				{
					return false;
				}
			}

			await Task.WhenAll (Options.Select (o => API.Chat.ReactAsync (Channel, m_RequestMessage, o)));

			return true;
		}


		/// <summary>
		/// Get the response to this question provided by the target user
		/// </summary>
		/// <param name="behaviour">Configure how the prompt message should be handled at flow end</param>
		/// <returns>A representation of the chosen option as well as the chooser</returns>
		/// <remarks>Caller should test <see cref="Response.Valid"/> of the return value</remarks>
		public async Task<Response> GetResponseAsync (PromptBehaviour behaviour = PromptBehaviour.Replace)
		{
			if (!Valid)
			{
				return new Response ();
			}

			m_OptionChosen = new TaskCompletionSource<Response> ();

			Response response = await m_OptionChosen.Task;

			if (PromptBehaviour.Leave == behaviour)
			{
				return response;
			}

			await API.Chat.DeleteAsync (Channel, m_RequestMessage);

			if (
				null != Text && // Never replace the default prompt
				PromptBehaviour.Replace == behaviour && // Only replace other prompts if instructed to
				response.Valid && // Do not replace after invalid response
				m_RequestMessage.TryRead (out Message.Data data)
			)
			{
				await API.Chat.MessageAsync (Channel, data.Contents);
			}

			return response;
		}


		/// <summary>
		/// Handle the provided incoming message if applicable
		/// </summary>
		/// <returns>Whether or not the message was handled</returns>
		public bool ConsiderIncoming (Message message)
		{
			return
				message.TryRead (out Message.Data data) &&
				Channel == data.Channel &&
				User == API.Environment.User &&
				HandleIncoming (message, data);
		}


		/// <summary>
		/// Handle the provided incoming reaction if applicable
		/// </summary>
		/// <returns>Whether or not the reaction was handled</returns>
		public bool ConsiderIncoming (Reaction reaction)
		{
			return
				reaction.TryRead (out Reaction.Data data) &&
				Channel == data.Channel &&
				User == data.Author &&
				HandleIncoming (reaction, data);
		}


		/// <summary>
		/// Directly handle a reaction already determined to have been sent as a possible response to this question
		/// </summary>
		public void OnOtherReaction (Reaction reaction, Reaction.Data data) => HandleIncoming (reaction, data);


		/// <summary>
		/// Directly handle a message already determined to have been sent by <see cref="API.Environment.User"/>,
		/// in the same context as this question
		/// </summary>
		public void OnSelfMessage (Message message, Message.Data data) => HandleIncoming (message, data);


		public void OnCancel ()
		{
			m_OptionChosen?.TrySetResult (new Response ());
			m_RequestReceived?.TrySetResult (Message.Invalid);
		}


		private bool HandleIncoming (Message message, Message.Data data)
		{
			return
				null != m_RequestReceived &&
				data.Contents == (Text ?? kDefaultPrompt) &&
				m_RequestReceived.TrySetResult (message);
		}


		private bool HandleIncoming (Reaction reaction, Reaction.Data data)
		{
			return
				null != Options &&
				null != m_OptionChosen &&
				Options.Any (o => o.Equals (data.Contents, StringComparison.InvariantCultureIgnoreCase)) &&
				m_OptionChosen.TrySetResult (
					new Response
					{
						From = data.Author,
						Text = data.Contents
					}
				);
		}
	}
}
