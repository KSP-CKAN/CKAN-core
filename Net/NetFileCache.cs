﻿using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ChinhDo.Transactions;
using ICSharpCode.SharpZipLib.Zip;
using log4net;

namespace CKAN
{
    /*
     * This class allows us to cache downloads by URL
     * It works using two directories - one to store downloads in-progress, and one to commit finished downloads
     * URLs are cached by hashing them by taking the first 8 chars of the url's SHA1 hash.
     * 
     * To use this class the user would have to:
     * - Obtain a temporary download path by calling GetTemporaryPathForURL(url)
     * - Initiate his download in this temporary path
     * - Call CommitDownload(url, desired_filename) which will move the temporary file to the final location
     * - The final file will be named such as <hash>-<filename>.zip
     * - The user can call IsCached(url) to check if a particular url exists in the cache
     * and GetCachedFilename() to get its filename
     */
    public class NetFileCache
    {
        private string cache_path;
        private static readonly TxFileManager tx_file = new TxFileManager();
        private static readonly ILog log = LogManager.GetLogger(typeof (NetFileCache));
   
        public NetFileCache(string requested_cache_path = null)
        {
            // Check the input.
            if (!string.IsNullOrWhiteSpace(requested_cache_path))
            {
                // Basic validation, our cache has to exist.
                if (!Directory.Exists(requested_cache_path))
                {
                    throw new DirectoryNotFoundKraken(requested_cache_path, "Cannot find cache directory, please create it when calling the cache with a specific path.");
                }

                cache_path = requested_cache_path;
            }
            else
            {
                // No specific cache requested, fall back to the global from the registry.
                cache_path = GetCachePathFromRegistry();

                // If no path was stored in the registry, get the system default.
                if (string.IsNullOrWhiteSpace(cache_path))
                {
                    cache_path = GetSystemDefaultCachePath();
                }
            }
        }

        public string GetCachePath()
        {
            return cache_path;
        }

        /// <summary>
        /// Gets the cache path from registry.
        /// </summary>
        /// <returns>The cache path from registry.</returns>
        public string GetCachePathFromRegistry()
        {
            // Create a new instance of the registry and request the cache path.
            Win32Registry registry = new Win32Registry();

            return registry.GetCachePath();
        }

        /// <summary>
        /// Gets the system default cache path.
        /// </summary>
        /// <returns>The system default cache path.</returns>
        public string GetSystemDefaultCachePath()
        {
            string base_directory = null;

            // Check which platform we are on, as the path depends on this.
            if (!Platform.IsWindows)
            {
                // Linux or OS X, attempt to use the XDG standard for getting the path.
                base_directory = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");

                // If the above did not work, fall back to "~/.local/".
                if (string.IsNullOrWhiteSpace(base_directory))
                {
                    base_directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local/share");
                }
            }
            else
            {
                // Windows.
                base_directory = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            }

            // Append the CKAN folder structure.
            string directory = Path.Combine(base_directory, "CKAN/downloads");

            // Create the folder if it doesn't exist.
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return directory;
        }

        // returns true if a url is already in the cache
        public bool IsCached(Uri url)
        {
            return GetCachedFilename(url) != null;
        }

        // returns true if a url is already in the cache
        // returns the filename in the outFilename parameter
        public bool IsCached(Uri url, out string outFilename)
        {
            outFilename = GetCachedFilename(url);

            return outFilename != null;
        }

        /// <summary>
        /// Returns true if our given URL is cached, *and* it passes zip
        /// validation tests. Prefer this over IsCached when working with
        /// zip files.
        /// </summary>
        public bool IsCachedZip(Uri url)
        {
            return GetCachedZip(url) != null;
        }

        /// <summary>>
        /// Returns the filename of an already cached url or null otherwise
        /// </summary>
        public string GetCachedFilename(Uri url)
        {
            log.DebugFormat("Checking cache for {0}", url);

            string hash = CreateURLHash(url);

            foreach (string file in Directory.GetFiles(cache_path))
            {
                string filename = Path.GetFileName(file);
                if (filename.StartsWith(hash))
                {
                    return file;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the filename for a cached URL, if and only if it
        /// passes zipfile validation tests. Prefer this to GetCachedFilename
        /// when working with zip files. Returns null if not available, or
        /// validation failed.
        /// </summary>
        public string GetCachedZip(Uri url)
        {
            string filename = GetCachedFilename(url);

            if (filename == null)
            {
                return null;
            }

            try
            {
                using (ZipFile zip = new ZipFile (filename))
                {
                    // Perform CRC check.
                    if (zip.TestArchive(true))
                    {
                        return filename;
                    }
                }
            }
            catch (ZipException)
            {
                // We ignore these; it just means the file is borked,
                // same as failing validation.
            }

            return null;
        }

        /// <summary>
        /// Stores the results of a given URL in the cache.
        /// Description is appended to the file hash when saving. If not present, the filename will be used.
        /// If `move` is true, then the file will be moved; otherwise, it will be copied.
        /// 
        /// Returns a path to the newly cached file.
        /// 
        /// This method is filesystem transaction aware.
        /// </summary>
        public string Store(Uri url, string path, string description = null, bool move = false)
        {
            log.DebugFormat("Storing {0}", url);

            // Make sure we clear our cache entry first.
            Remove(url);

            string hash = CreateURLHash(url);

            description = description ?? Path.GetFileName(path);

            string fullName = String.Format("{0}-{1}", hash, Path.GetFileName(description));
            string targetPath = Path.Combine(cache_path, fullName);

            log.DebugFormat("Storing {0} in {1}", path, targetPath);

            if (move)
            {
                tx_file.Move(path, targetPath);
            }
            else
            {
                tx_file.Copy(path, targetPath, true);
            }

            return targetPath;
        }

        /// <summary>
        /// Removes the given URL from the cache.
        /// Returns true if any work was done, false otherwise.
        /// This method is filesystem transaction aware.
        /// </summary>
        public bool Remove(Uri url)
        {
            string file = GetCachedFilename(url);

            if (file != null)
            {
                tx_file.Delete(file);
                return true;
            }

            return false;
        }

        // returns the 8-byte hash for a given url
        public static string CreateURLHash(Uri url)
        {
            using (var sha1 = new SHA1Managed())
            {
                byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(url.ToString()));

                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 8);
            }
        }
    }
}
