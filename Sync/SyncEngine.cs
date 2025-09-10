using System.Security.Cryptography;
using System.Text;
using FolderSync.Logging;

namespace FolderSync.Sync
{
    public sealed class SyncEngine
    {
        private readonly Logger _log;

        public SyncEngine(Logger logger)
        {
            _log = logger;
        }

        public async Task RunOnceAsync(string sourcePath, string replicaPath, bool useHashCompare, CancellationToken ct)
        {
            Directory.CreateDirectory(replicaPath);

            var srcFiles = EnumerateFiles(sourcePath);
            var dstFiles = EnumerateFiles(replicaPath);

            foreach (var kv in srcFiles) //copying or/and updating
            {
                var relPath   = kv.Key;
                var srcMeta   = kv.Value;
                var dstExists = dstFiles.TryGetValue(relPath, out var dstMeta);
                var target    = Path.Combine(replicaPath, relPath);

                if (!dstExists)
                {
                    await SafeCopyAsync(srcMeta.FullPath, target, ct);
                    _log.Info($"[new] {relPath}");
                }
                else if (NeedsUpdate(srcMeta, dstMeta, useHashCompare))
                {
                    await SafeCopyAsync(srcMeta.FullPath, target, ct);
                    _log.Info($"[update] {relPath}");
                }
            }

            foreach (var relPath in dstFiles.Keys.Except(srcFiles.Keys)) //del files
            {
                var fullDst = Path.Combine(replicaPath, relPath);
                try
                {
                    File.Delete(fullDst);
                    _log.Info($"[del file] {relPath}");
                }
                catch (Exception ex)
                {
                    _log.Error($"failed to delete '{relPath}' - {ex.Message}");
                }
            }

            foreach (var dir in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                var relDir    = GetRelativePath(sourcePath, dir);
                var targetDir = Path.Combine(replicaPath, relDir);
                Directory.CreateDirectory(targetDir);
            }
          
            var allDstDirs = Directory.EnumerateDirectories(replicaPath, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Count(c => c == Path.DirectorySeparatorChar));
            
            foreach (var dir in allDstDirs) //del folders
            {
                var relDir = GetRelativePath(replicaPath, dir);
                var srcDirFull = Path.Combine(sourcePath, relDir);
                if (!Directory.Exists(srcDirFull) && !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    try
                    {
                        Directory.Delete(dir);
                        _log.Info($"[del folder] {relDir}");
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"failed to delete '{relDir}' - {ex.Message}");
                    }
                }
            }
        }

        public async Task SafeCopyAsync(string source, string target, CancellationToken ct)
        {
         
            if (Directory.Exists(source) || !File.Exists(source)) //
                return;

            var tmp = target + ".tmp_copy";
            try
            {
                if (File.Exists(tmp))
                    File.Delete(tmp);

                using var inStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var outStream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None);

                await inStream.CopyToAsync(outStream, 1024 * 1024, ct);
                File.SetLastWriteTimeUtc(tmp, File.GetLastWriteTimeUtc(source));

                var parent = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);

                if (File.Exists(target))
                    File.Replace(tmp, target, null, ignoreMetadataErrors: true);
                else
                    File.Move(tmp, target);
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } //clean up on error
                throw;
            }
        }

        private static bool NeedsUpdate(FileMeta src, FileMeta dst, bool useHash)
        {
            var delta = Math.Abs((src.LastWriteUtc - dst.LastWriteUtc).TotalSeconds);
            if (delta > 2)
                return true;

            if (!useHash)
                return false;

            return !HashesEqual(src.FullPath, dst.FullPath);
        }

        private static bool HashesEqual(string a, string b)
        {
            using var md5 = MD5.Create();
            return ComputeHashHex(md5, a).Equals(ComputeHashHex(md5, b), StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeHashHex(HashAlgorithm algo, string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = algo.ComputeHash(fs);
            var sb   = new StringBuilder(hash.Length * 2);
            foreach (var bt in hash)
                sb.Append(bt.ToString("x2"));

            return sb.ToString();
        }

        private static Dictionary<string, FileMeta> EnumerateFiles(string root)
        {
            var dict = new Dictionary<string, FileMeta>(StringComparer.OrdinalIgnoreCase);
            foreach (var full in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var rel = GetRelativePath(root, full);
                var fi  = new FileInfo(full);
                dict[rel] = new FileMeta(full, fi.Length, fi.LastWriteTimeUtc);
            }

            return dict;
        }

        private static string GetRelativePath(string root, string fullPath) //
        {
            var basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(fullPath);

            return full.StartsWith(basePath, StringComparison.OrdinalIgnoreCase) ? full[basePath.Length..] : Path.GetRelativePath(basePath, full);
        }

        private readonly record struct FileMeta(string FullPath, long Length, DateTime LastWriteUtc);
    }
}
