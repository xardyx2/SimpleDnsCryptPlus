using System.Collections.Generic;
using System.IO;

namespace SimpleDnsCrypt.Utils
{
    /// <summary>
    /// Decides where a log tail reader resumes. Kept free of UI concerns so the rotation case
    /// is testable.
    /// </summary>
    public static class LogTailReader
    {
        /// <summary>
        /// Offset to read from next. dnscrypt-proxy rotates and truncates its own log files, so a
        /// file that is now shorter than the last offset we reached means the old content is gone;
        /// resuming at that offset would seek past EOF and yield nothing forever.
        /// </summary>
        public static long ResolveResumeOffset(long currentLength, long lastReadOffset)
        {
            return currentLength < lastReadOffset ? 0L : lastReadOffset;
        }

        public static IReadOnlyList<string> ReadNewLines(StreamReader reader, ref long lastReadOffset)
        {
            var lines = new List<string>();
            var currentLength = reader.BaseStream.Length;

            if (currentLength == lastReadOffset)
            {
                return lines;
            }

            // resume where we left off, or back to the start if the proxy rotated the file
            // shorter than that offset - seeking past EOF would stall the tail forever
            reader.BaseStream.Seek(ResolveResumeOffset(currentLength, lastReadOffset), SeekOrigin.Begin);

            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }

            lastReadOffset = reader.BaseStream.Position;
            return lines;
        }
    }
}
