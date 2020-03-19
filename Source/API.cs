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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using framebunker;


namespace Keybase
{
	// TODO: Generally wrap all decode use of Utf8Json in a utility decode call which catches and logs exceptions
	public static partial class API
	{
		private const string
			kPingArguments = "ping",
			kPingReturnSuccessPostfix = " is up";


		public class PooledProcess : PoolItem
		{
			[CanBeNull] public static Pool<PooledProcess> CreatePool (int size, [CanBeNull] string arguments = "")
			{
				if (!Environment.EnsureInitialization ())
				{
					return null;
				}

				return Pool<PooledProcess>.Restrained (size, p => new PooledProcess (p, arguments));
			}


			[CanBeNull] private Process m_Process;
			[CanBeNull] private string m_Arguments;
			[CanBeNull] private DataReceivedEventHandler
				m_OnStandardOutput,
				m_OnStandardError;
			[CanBeNull] private EventHandler m_OnExit;
			[CanBeNull] private Timer m_KillTimer;


			public StreamWriter StandardInput => m_Process?.StandardInput;


			public double Timeout
			{
				get => m_KillTimer?.Interval ?? 0;
				set
				{
					if (null != m_KillTimer)
					{
						m_KillTimer.Dispose ();
						m_KillTimer = null;
					}

					if (value <= 0)
					{
						return;
					}

					m_KillTimer = new Timer (value);
					m_KillTimer.Elapsed += OnTimeout;
					m_KillTimer.AutoReset = false;
					m_KillTimer.Enabled = true;
				}
			}


			private PooledProcess ([NotNull] IPool pool, [CanBeNull] string arguments) : base (pool)
			{
				m_Arguments = arguments;
			}


			/// <summary>
			/// (Re)start this process, with the given event handlers
			/// </summary>
			public PooledProcess Initialize
			(
				[CanBeNull] DataReceivedEventHandler onStandardOutput,
				[CanBeNull] DataReceivedEventHandler onStandardError,
				[CanBeNull] EventHandler onExit
			)
			{
				bool freshStart = null == m_Process || m_Process.HasExited;

				if (freshStart)
				{
					ResetProcess ();
				}
				else
				{
					ResetConnection ();
				}

				m_OnStandardOutput = onStandardOutput;
				m_OnStandardError = onStandardError;
				m_OnExit = onExit;
				m_Process.EnableRaisingEvents = true;

				if (freshStart)
				{
					m_Process.Start ();
					m_Process.BeginOutputReadLine ();
					m_Process.BeginErrorReadLine ();
				}

				return this;
			}


			/// <summary>
			/// Terminate this process without disposing it
			/// </summary>
			public void Kill ()
			{
				Timeout = 0;

				if (null == m_Process || m_Process.HasExited)
				{
					return;
				}

				m_Process.EnableRaisingEvents = false;
				m_Process.Kill ();
				m_Process.Dispose ();
				m_Process = null;
			}


			private void ResetProcess ()
			{
				Timeout = 0;

				if (null != m_Process)
				{
					ResetConnection ();
					m_Process.Kill ();
					m_Process.Dispose ();
				}

				m_Process = CreateProcess (Environment.BinaryPath, m_Arguments);
				m_Process.OutputDataReceived += OnStandardOutput;
				m_Process.ErrorDataReceived += OnStandardError;
				m_Process.Exited += OnExit;
			}


			private void ResetConnection ()
			{
				m_OnStandardOutput = null;
				m_OnStandardError = null;
				m_OnExit = null;

				m_Process?.StandardInput.Flush ();
			}


			public override void OnRelease ()
			{
				Timeout = 0;

				if (null != m_Process)
				{
					m_Process.EnableRaisingEvents = false;
				}
				ResetConnection ();

				base.OnRelease();
			}


			private void OnStandardOutput ([NotNull] object sender, [NotNull] DataReceivedEventArgs arguments)
			{
				m_OnStandardOutput?.Invoke (sender, arguments);
			}


			private void OnStandardError ([NotNull] object sender, [NotNull] DataReceivedEventArgs arguments)
			{
				m_OnStandardError?.Invoke (sender, arguments);
			}


			private void OnExit ([NotNull] object sender, [NotNull] EventArgs arguments)
			{
				m_OnExit?.Invoke (sender, arguments);
			}


			private void OnTimeout ([NotNull] object sender, [NotNull] ElapsedEventArgs arguments)
			{
				Kill ();
				m_OnExit?.Invoke (sender, arguments);
			}
		}


		[NotNull] private static Process CreateProcess ([NotNull] string binaryPath, [CanBeNull] string arguments)
		{
			return new Process
			{
				StartInfo =
				{
					FileName = binaryPath,
					Arguments = arguments,
					CreateNoWindow = true,
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
				},
				EnableRaisingEvents = true
			};
		}


		/// <remarks>Runs <see cref="Keybase.API.Environment.EnsureInitialization"/>.</remarks>
		[CanBeNull] private static Process CreateProcess ([CanBeNull] string arguments = "")
		{
			return !Environment.EnsureInitialization () ? null : CreateProcess (Environment.BinaryPath, arguments);
		}


		/// <summary>
		/// Tests whether the API connection is live. Runs <see cref="EnsureInitialization"/>.
		/// </summary>
		[NotNull] public static async Task<bool> Ping ()
		{
			Process process = CreateProcess (kPingArguments);

			if (null == process)
			{
				return false;
			}

			TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool> ();

			process.Exited += (sender, arguments) => completionSource.TrySetResult (false);

			process.ErrorDataReceived += (sender, arguments) =>
			{
				if (
					null != arguments?.Data &&
					arguments.Data.Trim ().EndsWith
					(
						kPingReturnSuccessPostfix,
						StringComparison.InvariantCultureIgnoreCase
					)
				)
				{
#if DEBUG_API_PING
					Log.Message ("API.Ping input: '{0}'", arguments.Data);
#endif

					completionSource.TrySetResult (true);
				}
			};

			if (!process.Start ())
			{
#if DEBUG_API_PING
				Log.Message ("API.Ping process failed to start");
#endif

				return false;
			}

			process.BeginErrorReadLine ();

			return await completionSource.Task;
		}
	}
}
