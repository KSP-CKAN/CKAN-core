using System;
using System.IO;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace CKANTests
{
    [TestFixture]
    public class Cache
    {
        private readonly string cache_dir = Path.Combine(Tests.TestData.DataDir(), "cache_test");
        private readonly string cache_dir_move_test = Path.Combine(Tests.TestData.DataDir(), "cache_test_moved");

        private CKAN.NetFileCache cache;

        [SetUp]
        public void MakeCache()
        {
            Directory.CreateDirectory(cache_dir);
            cache = new CKAN.NetFileCache(cache_dir);
        }

        [TearDown]
        public void RemoveCache()
        {
            Directory.Delete(cache_dir, true);

            if (Directory.Exists(cache_dir_move_test))
            {
                Directory.Delete(cache_dir_move_test, true);
            }
        }

        [Test]
        public void Sanity()
        {
            Assert.IsInstanceOf<CKAN.NetFileCache>(cache);
            Assert.IsTrue(Directory.Exists(cache.GetCachePath()));
        }

        [Test]
        public void StoreRetrieve()
        {
            Uri url = new Uri("http://example.com/");
            string file = Tests.TestData.DogeCoinFlagZip();

            // Sanity check, our cache dir is there, right?
            Assert.IsTrue(Directory.Exists(cache.GetCachePath()));

            // Our URL shouldn't be cached to begin with.
            Assert.IsFalse(cache.IsCached(url));

            // Store our file.
            cache.Store(url, file);

            // Now it should be cached.
            Assert.IsTrue(cache.IsCached(url));

            // Check contents match.
            string cached_file = cache.GetCachedFilename(url);
            FileAssert.AreEqual(file, cached_file);
        }

        [Test, TestCase("cheesy.zip","cheesy.zip"), TestCase("Foo-1:2.3","Foo-1-2.3"),
            TestCase("Foo-1:2:3","Foo-1-2-3"), TestCase("Foo/../etc/passwd","Foo-..-etc-passwd")]
        public void NamingHints(string hint, string appendage)
        {
            Uri url = new Uri("http://example.com/");
            string file = Tests.TestData.DogeCoinFlagZip();

            Assert.IsFalse(cache.IsCached(url));
            cache.Store(url, file, hint);

            StringAssert.EndsWith(appendage, cache.GetCachedFilename(url));
        }

        [Test]
        public void StoreRemove()
        {
            Uri url = new Uri("http://example.com/");
            string file = Tests.TestData.DogeCoinFlagZip();

            Assert.IsFalse(cache.IsCached(url));
            cache.Store(url, file);
            Assert.IsTrue(cache.IsCached(url));

            cache.Remove(url);

            Assert.IsFalse(cache.IsCached(url));
        }

        [Test]
        public void MoveCacheFailsForNull()
        {
            Uri url = new Uri("http://example.com/");
            string file = Tests.TestData.DogeCoinFlagZip();

            // Cache the file.
            cache.Store(url, file);

            // Move the cache.
            Assert.IsFalse(cache.MoveDefaultCache(null));
        }

        [Test]
        public void MoveCacheFailsForEmptyOrWhiteSpace()
        {
            Uri url = new Uri("http://example.com/");
            string file = Tests.TestData.DogeCoinFlagZip();

            // Cache the file.
            cache.Store(url, file);

            // Move the cache.
            Assert.IsFalse(cache.MoveDefaultCache(""));
            Assert.IsFalse(cache.MoveDefaultCache(" "));
            Assert.IsFalse(cache.MoveDefaultCache("                                 "));
        }

        [DllImport ("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int sys_chmod (string path, uint mode);

        private static uint S_IRUSR = 0000400;
        private static uint S_IWUSR = 0000200;
        private static uint S_IXUSR = 0000100;

        [Test]
        public void MoveCacheFailsForNoAccess()
        {
            // The following will only work on POSIX systems.
            if (CKAN.Platform.IsWindows)
            {
                Assert.IsTrue(false);
            }

            Uri url = new Uri("http://example.com/");
            string file = Tests.TestData.DogeCoinFlagZip();

            // Cache the file.
            cache.Store(url, file);

            // Make sure the new directory is created.
            Directory.CreateDirectory(cache_dir_move_test);

            // Change the permissions (Disable read/write).
            Assert.AreEqual(sys_chmod(cache_dir_move_test, S_IXUSR), 0);

            // Move the cache.
            Assert.IsFalse(cache.MoveDefaultCache(cache_dir_move_test));

            // Change the permissions (Enable read).
            Assert.AreEqual(sys_chmod(cache_dir_move_test, S_IRUSR), 0);

            // Move the cache.
            Assert.IsFalse(cache.MoveDefaultCache(cache_dir_move_test));

            // Change the permissions (Enable write).
            Assert.AreEqual(sys_chmod(cache_dir_move_test, S_IWUSR), 0);

            // Move the cache.
            Assert.IsFalse(cache.MoveDefaultCache(cache_dir_move_test));

            // Enable all permissions.
            Assert.AreEqual(sys_chmod(cache_dir_move_test, S_IRUSR | S_IWUSR | S_IXUSR), 0);

            // Move the cache.
            Assert.IsTrue(cache.MoveDefaultCache(cache_dir_move_test));
        }

        [Test]
        public void MoveCache()
        {
            Uri url = new Uri("http://example.com/");
            string file = Tests.TestData.DogeCoinFlagZip();

            // Cache the file.
            string path = cache.Store(url, file);

            // Check that file exists in the old cache dir.
            Assert.IsTrue(File.Exists(path));

            // Move the cache.
            Assert.IsTrue(cache.MoveDefaultCache(cache_dir_move_test));

            // Make sure we still have the file cached.
            string new_path = cache.GetCachedFilename(url);
            Assert.IsTrue(File.Exists(new_path));
            Assert.IsFalse(File.Exists(path));
        }

        [Test]
        public void CacheKraken()
        {
            string dir = "/this/path/better/not/exist";

            try
            {
                new CKAN.NetFileCache(dir);
            }
            catch (CKAN.DirectoryNotFoundKraken kraken)
            {
                Assert.AreSame(dir,kraken.directory);
            }
        }

        [Test]
        public void DoubleCache()
        {
            // Store and flip files in our cache. We should always get
            // the most recent file we store for any given URL.

            Uri url = new Uri("http://Double.Rainbow.What.Does.It.Mean/");
            Assert.IsFalse(cache.IsCached(url));

            string file1 = Tests.TestData.DogeCoinFlagZip();
            string file2 = Tests.TestData.ModuleManagerZip();

            cache.Store(url, file1);
            FileAssert.AreEqual(file1, cache.GetCachedFilename(url));

            cache.Store(url, file2);
            FileAssert.AreEqual(file2, cache.GetCachedFilename(url));

            cache.Store(url, file1);
            FileAssert.AreEqual(file1, cache.GetCachedFilename(url));
        }

        [Test]
        public void ZipValidation()
        {
            // We could use any URL, but this one is awesome. <3
            Uri url = new Uri("http://kitte.nz/");

            Assert.IsFalse(cache.IsCachedZip(url));

            // Store a bad zip.
            cache.Store(url, Tests.TestData.DogeCoinFlagZipCorrupt());

            // Make sure it's stored, but not valid as a zip
            Assert.IsTrue(cache.IsCached(url));
            Assert.IsFalse(cache.IsCachedZip(url));

            // Store a good zip.
            cache.Store(url, Tests.TestData.DogeCoinFlagZip());

            // Make sure it's stored, and valid.
            Assert.IsTrue(cache.IsCached(url));
            Assert.IsTrue(cache.IsCachedZip(url));
        }
    }
}

