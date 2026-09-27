using Caliburn.Micro;
using SimpleDnsCrypt.Models;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleDnsCrypt.Helper
{
    public static class ProcessHelper
    {
        private static readonly ILog Log = LogManagerHelper.Factory();

        /// <summary>
        ///		Execute process with arguments
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static ProcessResult ExecuteWithArguments(string filename, string arguments)
        {
            var processResult = new ProcessResult();
            try
            {
                const int timeout = 9000;
                using var process = new Process
                {
                    StartInfo =
                    {
                        FileName = filename,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                var output = new StringBuilder();
                var error = new StringBuilder();

                using var outputWaitHandle = new AutoResetEvent(false);
                using var errorWaitHandle = new AutoResetEvent(false);
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null)
                    {
                        outputWaitHandle.Set();
                    }
                    else
                    {
                        output.AppendLine(e.Data);
                    }
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null)
                    {
                        errorWaitHandle.Set();
                    }
                    else
                    {
                        error.AppendLine(e.Data);
                    }
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (process.WaitForExit(timeout) &&
                    outputWaitHandle.WaitOne(timeout) &&
                    errorWaitHandle.WaitOne(timeout))
                {
                    if (process.ExitCode == 0)
                    {
                        processResult.StandardOutput = output.ToString();
                        processResult.StandardError = error.ToString();
                        processResult.Success = true;
                    }
                    else
                    {
                        processResult.StandardOutput = output.ToString();
                        processResult.StandardError = error.ToString();
                        Log.Warn(processResult.StandardError);
                        processResult.Success = false;
                    }
                }
                else
                {
                    // Timed out.
                    throw new Exception("Timed out");
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception);
                processResult.StandardError = exception.Message;
                processResult.Success = false;
            }

            return processResult;
        }

        /// <summary>
        ///		Execute process with arguments
        /// </summary>
        public static async Task<ProcessResult> ExecuteWithArgumentsAsync(string filename, string arguments)
        {
            var processResult = new ProcessResult();
            try
            {
                using var timeoutCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(9));
                using var process = new Process
                {
                    StartInfo =
                    {
                        FileName = filename,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                var output = new StringBuilder();
                var error = new StringBuilder();

                using var outputWaitHandle = new SemaphoreSlim(0);
                using var errorWaitHandle = new SemaphoreSlim(0);
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null)
                    {
                        outputWaitHandle.Release();
                    }
                    else
                    {
                        output.AppendLine(e.Data);
                    }
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null)
                    {
                        errorWaitHandle.Release();
                    }
                    else
                    {
                        error.AppendLine(e.Data);
                    }
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await Task.WhenAll(process.WaitForExitAsync(timeoutCancellation.Token),
                        outputWaitHandle.WaitAsync(timeoutCancellation.Token),
                        errorWaitHandle.WaitAsync(timeoutCancellation.Token))
                   .ConfigureAwait(false);


                processResult.StandardOutput = output.ToString();
                processResult.StandardError = error.ToString();
                if (process.ExitCode == 0)
                {
                    processResult.Success = true;
                }
                else
                {
                    Log.Warn(processResult.StandardError);
                    processResult.Success = false;
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception);
                processResult.StandardError = exception.Message;
                processResult.Success = false;
            }

            return processResult;
        }
    }
}
