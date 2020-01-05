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


#if DEBUG
	#define LOG_MESSAGE
	#define LOG_WARNING
	#define LOG_ERROR
#endif


#if LOG_MESSAGE || LOG_WARNING || LOG_ERROR
	#define ANY_LOGGING
#endif


using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;


namespace Keybase
{
	public static class Log
	{
		public static class Settings<T>
		{
			public static bool Enabled =>
#if ANY_LOGGING
				Settings.Enabled (typeof (T))
#else
					false
#endif
			;
		}


		public static class Settings
		{
			private const string
				kNamespaceStrike = "KEYBASE_",
				kNamePrefix = "KEYBASE_NET_DEBUG_";


			[CanBeNull] private static string GetName ([NotNull] System.Type type)
			{
				string name = type.FullName?.ToUpper ()?.Replace ('.', '_')?.Replace ('+', '_');
				if (name.StartsWith (kNamespaceStrike))
				{
					name = name.Substring (kNamespaceStrike.Length);
				}

				return name;
			}


			[CanBeNull] private static string GetName ([NotNull] Delegate method)
			{
				MethodInfo info = method.Method;
				System.Type type = info.DeclaringType;

				return null == type ? null : GetName (type) + "_" + info.Name.ToUpper ();
			}


			public static bool Enabled ([NotNull] System.Type type)
			{
#if ANY_LOGGING
				string name = GetName (type);
				return !string.IsNullOrEmpty (name) && Enabled (name);
#else
				return false;
#endif
			}


			public static bool Enabled ([NotNull] Delegate method)
			{
#if ANY_LOGGING
				string name = GetName (method);
				return !string.IsNullOrEmpty (name) && Enabled (name);
#else
				return false;
#endif
			}


			public static bool Enabled ([NotNull] string level)
			{
#if ANY_LOGGING
				return null != Environment.GetEnvironmentVariable (kNamePrefix + level.ToUpper ());
#else
				return false;
#endif
			}
		}


		public enum Type
		{
			Message,
			Warning,
			Error
		}


		private static event Action<Type, string> s_OnSendSimple;
		private static event Action<Type, string, object[]> s_OnSendParameterised;


		private static void Send (Type type, [NotNull] string content)
		{
			if (null == s_OnSendSimple)
			{
				Console.WriteLine (content);
			}
			else
			{
				s_OnSendSimple.Invoke (type, content);
			}
		}


		private static void Send (Type type, [NotNull] string content, [NotNull] params object[] parameters)
		{
			if (null == s_OnSendParameterised)
			{
				Console.WriteLine (content, parameters);
			}
			else
			{
				s_OnSendParameterised.Invoke (type, content, parameters);
			}
		}


		//[Conditional ("ANY_LOGGING")]
		public static void Debug ([NotNull] System.Type source, Type type, [NotNull] string content)
		{
			if (Settings.Enabled (source))
			{
				Send (type, content);
			}
		}


		//[Conditional ("ANY_LOGGING")]
		public static void Debug ([NotNull] System.Type source, Type type, [NotNull] string content, [NotNull] params object[] parameters)
		{
			if (Settings.Enabled (source))
			{
				Send (type, content, parameters);
			}
		}


		//[Conditional ("ANY_LOGGING")]
		public static void Debug<T> (Type type, [NotNull] string content)
		{
			if (Settings<T>.Enabled)
			{
				Send (type, content);
			}
		}


		//[Conditional ("ANY_LOGGING")]
		public static void Debug<T> (Type type, [NotNull] string content, [NotNull] params object[] parameters)
		{
			if (Settings<T>.Enabled)
			{
				Send (type, content, parameters);
			}
		}


		//[Conditional ("ANY_LOGGING")]
		public static void Debug ([NotNull] Delegate source, Type type, [NotNull] string content)
		{
			if (Settings.Enabled (source))
			{
				Send (type, content);
			}
		}


		//[Conditional ("ANY_LOGGING")]
		public static void Debug ([NotNull] Delegate source, Type type, [NotNull] string content, [NotNull] params object[] parameters)
		{
			if (Settings.Enabled (source))
			{
				Send (type, content, parameters);
			}
		}


		//[Conditional ("ANY_LOGGING")]
		public static void RegisterHandlers ([NotNull] Action<Type, string> simpleHandler, [NotNull] Action<Type, string, object[]> parameterisedHandler)
		{
			s_OnSendSimple -= simpleHandler;
			s_OnSendSimple += simpleHandler;
			s_OnSendParameterised -= parameterisedHandler;
			s_OnSendParameterised += parameterisedHandler;
		}


		//[Conditional ("ANY_LOGGING")]
		public static void UnregisterHandlers ([NotNull] Action<Type, string> simpleHandler, [NotNull] Action<Type, string, object[]> parameterisedHandler)
		{
			s_OnSendSimple -= simpleHandler;
			s_OnSendParameterised -= parameterisedHandler;
		}


		//[Conditional ("LOG_MESSAGE")]
		public static void Message ([NotNull] string content)
		{
			Send (Type.Message, content);
		}


		//[Conditional ("LOG_MESSAGE")]
		public static void Message ([NotNull] string content, [NotNull] params object[] parameters)
		{
			Send (Type.Message, content, parameters);
		}


		//[Conditional ("LOG_WARNING")]
		public static void Warning ([NotNull] string content)
		{
			Send (Type.Warning, content);
		}


		//[Conditional ("LOG_WARNING")]
		public static void Warning ([NotNull] string content, [NotNull] params object[] parameters)
		{
			Send (Type.Warning, content, parameters);
		}


		//[Conditional ("LOG_ERROR")]
		public static void Error ([NotNull] string content)
		{
			Send (Type.Error, content);
		}


		//[Conditional ("LOG_ERROR")]
		public static void Error ([NotNull] string content, [NotNull] params object[] parameters)
		{
			Send (Type.Error, content, parameters);
		}
	}
}
