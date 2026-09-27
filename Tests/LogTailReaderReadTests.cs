using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SimpleDnsCrypt.Utils;

namespace Tests
{
    /// <summary>
    /// Exercises the tail-follow read against a real file on disk, including the case that made
    /// upstream #287 unrecoverable without a restart: the log file becoming shorter than the
    /// offset the reader had reached, because dnscrypt-proxy rotated it.
    /// </summary>
    public class LogTailReaderReadTests
    {
        private string path;

        [SetUp]
        public void SetUp()
        {
            path = Path.Combine(Path.GetTempPath(), $"sdc-tail-test-{System.Guid.NewGuid():N}.log");
            File.WriteAllText(path, string.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private List<string> ReadOnce(ref long offset)
        {
            using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite));
            return LogTailReader.ReadNewLines(reader, ref offset).ToList();
        }

        [Test]
        public void FirstReadFromEndOfExistingFile_ReturnsNothing()
        {
            File.AppendAllText(path, "line one\nline two\n");

            var offset = new FileInfo(path).Length;

            Assert.IsEmpty(ReadOnce(ref offset));
        }

        [Test]
        public void AppendedLines_AreReturnedOnceAndAdvanceTheOffset()
        {
            var offset = 0L;
            File.AppendAllText(path, "a\n");

            Assert.AreEqual(new[] { "a" }, ReadOnce(ref offset));

            // nothing new yet
            Assert.IsEmpty(ReadOnce(ref offset));

            File.AppendAllText(path, "b\nc\n");
            Assert.AreEqual(new[] { "b", "c" }, ReadOnce(ref offset));
        }

        [Test]
        public void FileTruncatedAfterRotation_ResumesFromStartInsteadOfStalling()
        {
            var offset = 0L;
            File.AppendAllText(path, "old-a\nold-b\nold-c\n");
            ReadOnce(ref offset);
            Assert.Greater(offset, 0L, "precondition: offset advanced past the old content");

            // simulate rotation: the proxy replaces the file with a much shorter one
            File.WriteAllText(path, "new-a\n");

            var afterRotation = ReadOnce(ref offset);

            Assert.AreEqual(new[] { "new-a" }, afterRotation);
        }

        [Test]
        public void RepeatedReadsAfterRotation_KeepSeeingNewLines()
        {
            var offset = 0L;
            File.AppendAllText(path, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n");
            ReadOnce(ref offset);

            File.WriteAllText(path, "rotated-1\n");
            Assert.AreEqual(new[] { "rotated-1" }, ReadOnce(ref offset));

            File.AppendAllText(path, "rotated-2\n");
            Assert.AreEqual(new[] { "rotated-2" }, ReadOnce(ref offset));
        }
    }
}
