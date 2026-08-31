using System.IO;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Json;
using Google.Apis.Util.Store;

namespace MR200.UI.MaintenanceSystem.Gmail
{
    /// <summary>
    /// Token store for the Google auth libraries that keeps OAuth tokens outside the
    /// project folder and encrypted at rest.
    ///
    /// Google's stock <c>FileDataStore</c> writes the refresh token as plain JSON. This
    /// replacement writes the same content through Windows DPAPI with
    /// <see cref="DataProtectionScope.CurrentUser"/>, so the file can only be read back
    /// by the same Windows user on the same machine. Copying it to another PC, or
    /// opening it as a different user, yields nothing usable.
    ///
    /// Location: %LOCALAPPDATA%\MR200\GmailToken (never inside the repository).
    /// </summary>
    public sealed class DpapiDataStore : IDataStore
    {
        private readonly string _folder;

        public DpapiDataStore(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                throw new ArgumentException("Token folder must be provided.", nameof(folder));

            _folder = folder;
            Directory.CreateDirectory(_folder);
        }

        /// <summary>Folder the encrypted tokens live in. Shown to the operator so they can clear it.</summary>
        public string FolderPath => _folder;

        /// <summary>True when at least one token file is present.</summary>
        public bool HasAnyToken =>
            Directory.Exists(_folder) && Directory.EnumerateFiles(_folder, "*.dat").Any();

        /// <summary>
        /// Repairs token files left Hidden by an earlier version of this class, which
        /// would otherwise make the next token refresh fail. Safe to call at any time.
        /// </summary>
        public void RepairFileAttributes()
        {
            try
            {
                if (!Directory.Exists(_folder)) return;
                foreach (var file in Directory.EnumerateFiles(_folder, "*.dat"))
                    ClearBlockingAttributes(file);
            }
            catch { }
        }

        private string FilePathFor<T>(string key)
        {
            var raw = $"{typeof(T).FullName}-{key}";
            foreach (var invalid in Path.GetInvalidFileNameChars())
                raw = raw.Replace(invalid, '_');
            return Path.Combine(_folder, raw + ".dat");
        }

        public Task StoreAsync<T>(string key, T value)
        {
            var json = NewtonsoftJsonSerializer.Instance.Serialize(value);
            var plain = Encoding.UTF8.GetBytes(json);
            var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);

            WriteAtomically(FilePathFor<T>(key), encrypted);
            return Task.CompletedTask;
        }

        public Task<T> GetAsync<T>(string key)
        {
            var path = FilePathFor<T>(key);
            if (!File.Exists(path)) return Task.FromResult<T>(default!);

            try
            {
                var encrypted = File.ReadAllBytes(path);
                var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(plain);
                return Task.FromResult(NewtonsoftJsonSerializer.Instance.Deserialize<T>(json));
            }
            catch
            {
                // Written by another user, copied from another machine, or corrupted.
                // Drop it so the next call simply re-authorises instead of failing forever.
                TryDelete(path);
                return Task.FromResult<T>(default!);
            }
        }

        public Task DeleteAsync<T>(string key)
        {
            TryDelete(FilePathFor<T>(key));
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            if (Directory.Exists(_folder))
                foreach (var file in Directory.EnumerateFiles(_folder, "*.dat"))
                    TryDelete(file);

            return Task.CompletedTask;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                ClearBlockingAttributes(path);
                File.Delete(path);
            }
            catch { }
        }

        /// <summary>
        /// Replaces the token file in one step.
        ///
        /// The write goes to a temporary file first and is then moved into place, so a
        /// crash midway cannot leave a half-written token that would force the operator
        /// to authorise again.
        ///
        /// Attributes are cleared beforehand because Windows refuses to recreate a
        /// Hidden or ReadOnly file through FileMode.Create - which is what
        /// File.WriteAllBytes uses. An earlier version of this class marked the token
        /// Hidden, and that made every token REFRESH fail with UnauthorizedAccessException
        /// roughly an hour after authorising, while the first write appeared to succeed.
        /// The file is deliberately left with normal attributes now: DPAPI encryption is
        /// what protects the contents, not obscurity.
        /// </summary>
        private static void WriteAtomically(string path, byte[] content)
        {
            ClearBlockingAttributes(path);

            var temp = path + ".tmp";
            ClearBlockingAttributes(temp);
            File.WriteAllBytes(temp, content);

            try
            {
                File.Move(temp, path, overwrite: true);
            }
            catch
            {
                // Move can fail if something else holds the target open; fall back to a
                // direct write so the refreshed token is not lost.
                try { File.WriteAllBytes(path, content); } finally { TryDeleteRaw(temp); }
            }
        }

        /// <summary>
        /// Removes attributes that block rewriting a file. Also repairs token files left
        /// Hidden by an earlier version, so no re-authorisation is needed.
        /// </summary>
        private static void ClearBlockingAttributes(string path)
        {
            try
            {
                if (!File.Exists(path)) return;

                const FileAttributes blocking =
                    FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System;

                var attributes = File.GetAttributes(path);
                if ((attributes & blocking) != 0)
                    File.SetAttributes(path, attributes & ~blocking);
            }
            catch { }
        }

        private static void TryDeleteRaw(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
