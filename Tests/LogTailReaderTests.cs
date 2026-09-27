using NUnit.Framework;
using SimpleDnsCrypt.Utils;

namespace Tests
{
    /// <summary>
    /// Upstream issue #287: "Query log is broken until Simple DNSCrypt is restarted".
    /// The tail loops in the log views seek to the offset they last reached, but
    /// dnscrypt-proxy rotates and truncates its own log files. Once the file is shorter than
    /// that stored offset, the seek lands past EOF, ReadLine returns null immediately, and the
    /// loop keeps recording the same stale offset forever - so the view silently stops updating
    /// until the process restarts.
    /// </summary>
    public class LogTailReaderTests
    {
        [Test]
        public void LogGrewSinceLastRead_ResumesAtLastOffset()
        {
            Assert.AreEqual(40, LogTailReader.ResolveResumeOffset(currentLength: 100, lastReadOffset: 40));
        }

        [Test]
        public void LogUnchangedSinceLastRead_ResumesAtLastOffset()
        {
            Assert.AreEqual(40, LogTailReader.ResolveResumeOffset(currentLength: 40, lastReadOffset: 40));
        }

        [Test]
        public void LogRotatedToShorterFile_ResumesAtStart()
        {
            Assert.AreEqual(0, LogTailReader.ResolveResumeOffset(currentLength: 20, lastReadOffset: 40));
        }

        [Test]
        public void LogTruncatedToEmptyFile_ResumesAtStart()
        {
            Assert.AreEqual(0, LogTailReader.ResolveResumeOffset(currentLength: 0, lastReadOffset: 40));
        }
    }
}
