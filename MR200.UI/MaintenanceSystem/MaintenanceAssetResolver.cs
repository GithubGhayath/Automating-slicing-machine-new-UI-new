using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace MR200.UI.MaintenanceSystem
{
    /// <summary>One downloaded or loaded binary asset destined for the alert e-mail.</summary>
    public sealed class ResolvedAsset
    {
        public ResolvedAsset(byte[] content, string fileName, string mediaType)
        {
            Content = content;
            FileName = fileName;
            MediaType = mediaType;
        }

        public byte[] Content { get; }
        public string FileName { get; }
        public string MediaType { get; }
    }

    /// <summary>
    /// Finds the two attachments the maintenance e-mail needs: the picture of the
    /// failed element and its manufacturer catalogue.
    ///
    /// Catalogues ship with the application under MaintenanceAssets\Catalogs and are
    /// referenced by file name from ElementsInformation.Catalog. Pictures are stored
    /// as product-page links and downloaded at send time.
    ///
    /// Nothing here is allowed to throw: a missing image or catalogue must never stop
    /// the alert from going out.
    /// </summary>
    public static class MaintenanceAssetResolver
    {
        private const string AssetFolderName = "MaintenanceAssets";
        private const string CatalogSubFolderName = "Catalogs";
        private const string ImageSubFolderName = "Images";

        private static readonly HttpClient Http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
            {
                Timeout = TimeSpan.FromSeconds(Math.Max(5, MaintenanceSettings.ImageDownloadTimeoutSeconds))
            };
            // Several manufacturer sites reject requests without a browser user agent.
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
                "text/html,application/xhtml+xml,image/avif,image/webp,image/apng,*/*;q=0.8");
            return client;
        }

        /// <summary>Folder the catalogue PDFs are deployed to, next to the executable.</summary>
        public static string CatalogFolder =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AssetFolderName, CatalogSubFolderName);

        /// <summary>Folder element and machine-position pictures are deployed to.</summary>
        public static string ImageFolder =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AssetFolderName, ImageSubFolderName);

        /// <summary>
        /// Loads the manufacturer catalogue for an element. Returns null when the
        /// element has no catalogue configured or the file is not deployed.
        /// </summary>
        public static ResolvedAsset? TryLoadCatalog(string? catalogFileName)
        {
            if (string.IsNullOrWhiteSpace(catalogFileName)) return null;

            try
            {
                // Guard against a stored value trying to escape the catalogue folder.
                var safeName = Path.GetFileName(catalogFileName);
                if (string.IsNullOrWhiteSpace(safeName)) return null;

                var fullPath = Path.Combine(CatalogFolder, safeName);
                if (!File.Exists(fullPath)) return null;

                return new ResolvedAsset(File.ReadAllBytes(fullPath), safeName, "application/pdf");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Downloads the picture of an element. The stored value is usually a product
        /// page, so the page is fetched and its main product image resolved from the
        /// Open Graph / Twitter card metadata. A direct image URL is used as-is.
        /// Returns null on any failure.
        /// </summary>
        public static async Task<ResolvedAsset?> TryResolveElementImageAsync(string? imageReference)
        {
            if (string.IsNullOrWhiteSpace(imageReference)) return null;
            var reference = imageReference.Trim();

            // A locally deployed picture always wins: it cannot be blocked, rate-limited
            // or taken offline. Retailer product pages frequently refuse automated
            // requests, so a file is the dependable option.
            var local = TryLoadLocalImage(reference);
            if (local != null) return local;

            if (!Uri.TryCreate(reference, UriKind.Absolute, out var uri)) return null;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

            try
            {
                using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return null;

                var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

                // The link already pointed straight at an image.
                if (mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    return BuildImageAsset(bytes, uri, mediaType);
                }

                // Otherwise treat it as a product page and look for its main image.
                if (!mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)) return null;

                var html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var imageLink = ExtractPrimaryImageUrl(html, response.RequestMessage?.RequestUri ?? uri);
                if (imageLink == null) return null;

                using var imageResponse = await Http.GetAsync(imageLink).ConfigureAwait(false);
                if (!imageResponse.IsSuccessStatusCode) return null;

                var imageMediaType = imageResponse.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                if (!imageMediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return null;

                var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                return BuildImageAsset(imageBytes, imageLink, imageMediaType);
            }
            catch
            {
                // Offline, blocked by the site, timed out - the alert still goes out.
                return null;
            }
        }


        /// <summary>
        /// Resolves a picture held on this machine. Accepts a bare file name (looked up
        /// in MaintenanceAssets\Images) or an absolute path. Returns null when the
        /// reference is a URL or the file is absent.
        /// </summary>
        private static ResolvedAsset? TryLoadLocalImage(string reference)
        {
            try
            {
                if (reference.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    reference.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return null;

                string? path = null;

                if (Path.IsPathRooted(reference))
                {
                    if (File.Exists(reference)) path = reference;
                }
                else
                {
                    var candidate = Path.Combine(ImageFolder, Path.GetFileName(reference));
                    if (File.Exists(candidate)) path = candidate;
                }

                if (path == null) return null;

                var extension = Path.GetExtension(path).ToLowerInvariant();
                var mediaType = extension switch
                {
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".bmp" => "image/bmp",
                    ".svg" => "image/svg+xml",
                    _ => "image/jpeg"
                };

                return new ResolvedAsset(File.ReadAllBytes(path), Path.GetFileName(path), mediaType);
            }
            catch
            {
                return null;
            }
        }

        private static ResolvedAsset? BuildImageAsset(byte[] bytes, Uri source, string mediaType)
        {
            if (bytes.Length == 0) return null;

            var extension = mediaType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/svg+xml" => ".svg",
                _ => ".jpg"
            };

            var name = Path.GetFileNameWithoutExtension(source.AbsolutePath);
            if (string.IsNullOrWhiteSpace(name)) name = "element";
            name = Regex.Replace(name, @"[^A-Za-z0-9_\-]", "_");
            if (name.Length > 40) name = name.Substring(0, 40);

            return new ResolvedAsset(bytes, name + extension, mediaType);
        }

        /// <summary>
        /// Pulls the main product image out of a product page: Open Graph first, then
        /// the Twitter card, then a link rel=image_src.
        /// </summary>
        private static Uri? ExtractPrimaryImageUrl(string html, Uri pageUri)
        {
            string[] patterns =
            {
                @"<meta[^>]+property\s*=\s*[""']og:image(?::secure_url)?[""'][^>]+content\s*=\s*[""']([^""']+)[""']",
                @"<meta[^>]+content\s*=\s*[""']([^""']+)[""'][^>]+property\s*=\s*[""']og:image(?::secure_url)?[""']",
                @"<meta[^>]+name\s*=\s*[""']twitter:image[""'][^>]+content\s*=\s*[""']([^""']+)[""']",
                @"<link[^>]+rel\s*=\s*[""']image_src[""'][^>]+href\s*=\s*[""']([^""']+)[""']"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                if (!match.Success) continue;

                var candidate = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
                if (candidate.StartsWith("//")) candidate = pageUri.Scheme + ":" + candidate;

                if (Uri.TryCreate(pageUri, candidate, out var resolved) &&
                    (resolved.Scheme == Uri.UriSchemeHttp || resolved.Scheme == Uri.UriSchemeHttps))
                {
                    return resolved;
                }
            }

            return null;
        }
    }
}
