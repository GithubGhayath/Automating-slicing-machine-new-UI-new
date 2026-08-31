using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using MimeKit;

namespace MR200.UI.MaintenanceSystem.Gmail
{
    /// <summary>A file attached to an outgoing message.</summary>
    public sealed record EmailAttachment(string FileName, byte[] Content, string MediaType);

    /// <summary>An image embedded in the HTML body and referenced as cid:ContentId.</summary>
    public sealed record EmailInlineImage(string ContentId, string FileName, byte[] Content, string MediaType);

    /// <summary>Raised when the account has not been connected, or the grant was revoked.</summary>
    public sealed class GmailNotAuthorizedException : Exception
    {
        public GmailNotAuthorizedException(string message, Exception? inner = null) : base(message, inner) { }
    }

    /// <summary>
    /// Sends mail through the Gmail API using OAuth 2.0.
    ///
    /// No Gmail password is ever involved. The application holds a Google Cloud OAuth
    /// *client* (client id + client secret, which identify the application, not the
    /// user) and asks Google to issue a token after the account owner approves the
    /// consent screen in their browser.
    ///
    /// The refresh token is kept in an encrypted store outside the repository, so the
    /// consent screen appears once and every later send is silent.
    ///
    /// Scope: gmail.send only - the narrowest scope that can send a message. It cannot
    /// read, search or delete mail.
    /// </summary>
    public sealed class GmailEmailService
    {
        private static readonly string[] Scopes = { GmailService.Scope.GmailSend };

        private readonly string _clientSecretsPath;
        private readonly DpapiDataStore _tokenStore;
        private readonly string _userId;
        private readonly string _applicationName;

        public GmailEmailService(string clientSecretsPath, string tokenFolder,
            string applicationName, string userId = "mr200-maintenance")
        {
            _clientSecretsPath = clientSecretsPath;
            _tokenStore = new DpapiDataStore(tokenFolder);
            _applicationName = applicationName;
            _userId = userId;

            // Repairs a token file that an earlier build left Hidden, which would make
            // the next refresh fail. Cheap, and saves the operator re-authorising.
            _tokenStore.RepairFileAttributes();
        }

        /// <summary>Where the encrypted refresh token lives, for display and manual reset.</summary>
        public string TokenFolderPath => _tokenStore.FolderPath;

        /// <summary>Path the OAuth client file is expected at.</summary>
        public string ClientSecretsPath => _clientSecretsPath;

        /// <summary>True when the OAuth client file has been placed on this machine.</summary>
        public bool HasClientSecrets => File.Exists(_clientSecretsPath);

        /// <summary>True when an authorisation token is already stored (no browser needed).</summary>
        public bool HasStoredAuthorization => _tokenStore.HasAnyToken;

        // ---------------------------------------------------------------- auth

        private async Task<GoogleAuthorizationCodeFlow> CreateFlowAsync(CancellationToken ct)
        {
            if (!HasClientSecrets)
            {
                throw new GmailNotAuthorizedException(
                    $"The Google OAuth client file was not found at \"{_clientSecretsPath}\". " +
                    "Download it from Google Cloud Console (APIs & Services -> Credentials -> " +
                    "your Desktop client -> Download JSON) and save it to that path.");
            }

            GoogleClientSecrets secrets;
            try
            {
                await using var stream = File.OpenRead(_clientSecretsPath);
                secrets = await GoogleClientSecrets.FromStreamAsync(stream, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new GmailNotAuthorizedException(
                    "The Google OAuth client file could not be read. Make sure it is the JSON " +
                    "downloaded from Google Cloud Console and was not edited. " + ex.Message, ex);
            }

            return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = secrets.Secrets,
                Scopes = Scopes,
                DataStore = _tokenStore
            });
        }

        /// <summary>
        /// Opens Google's consent screen in the default browser and stores the resulting
        /// refresh token. Call this once, deliberately - never from an automatic alert.
        /// </summary>
        /// <param name="onAuthorizationUrl">
        /// Receives the Google authorisation URL just before the browser is launched, so
        /// the caller can show it. If the browser fails to open, or the tab is closed by
        /// accident, the same URL can simply be pasted in again - the local listener is
        /// still waiting for the redirect.
        /// </param>
        public async Task AuthorizeInteractiveAsync(
            CancellationToken ct = default, Action<string>? onAuthorizationUrl = null)
        {
            var flow = await CreateFlowAsync(ct).ConfigureAwait(false);

            // Loopback redirect on 127.0.0.1 with a random free port: the flow Google
            // requires for installed / desktop applications.
            var receiver = new UrlReportingCodeReceiver(onAuthorizationUrl);

            try
            {
                await new AuthorizationCodeInstalledApp(flow, receiver)
                    .AuthorizeAsync(_userId, ct).ConfigureAwait(false);
            }
            catch (TokenResponseException ex)
            {
                throw new GmailNotAuthorizedException(DescribeTokenError(ex), ex);
            }
        }

        /// <summary>
        /// Wraps Google's loopback receiver purely to surface the authorisation URL
        /// before the browser opens. Behaviour is otherwise identical.
        /// </summary>
        private sealed class UrlReportingCodeReceiver : ICodeReceiver
        {
            private readonly LocalServerCodeReceiver _inner = new(
                "<html><body style='font-family:Segoe UI,Arial,sans-serif;text-align:center;padding:60px'>" +
                "<h2 style='color:#17463E'>MR200 is connected to Gmail</h2>" +
                "<p style='color:#6B7280'>You can close this tab and return to the application.</p>" +
                "</body></html>");

            private readonly Action<string>? _onAuthorizationUrl;

            public UrlReportingCodeReceiver(Action<string>? onAuthorizationUrl)
                => _onAuthorizationUrl = onAuthorizationUrl;

            public string RedirectUri => _inner.RedirectUri;

            public Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(
                AuthorizationCodeRequestUrl url, CancellationToken taskCancellationToken)
            {
                try { _onAuthorizationUrl?.Invoke(url.Build().AbsoluteUri); } catch { }
                return _inner.ReceiveCodeAsync(url, taskCancellationToken);
            }
        }

        /// <summary>
        /// Returns a usable credential without ever opening a browser, refreshing the
        /// access token if it has gone stale. Returns null when the account has not been
        /// connected yet or the grant was revoked.
        /// </summary>
        public async Task<UserCredential?> TryGetCredentialAsync(CancellationToken ct = default)
        {
            var flow = await CreateFlowAsync(ct).ConfigureAwait(false);

            TokenResponse? token;
            try { token = await flow.LoadTokenAsync(_userId, ct).ConfigureAwait(false); }
            catch { return null; }

            if (token == null || string.IsNullOrEmpty(token.RefreshToken)) return null;

            var credential = new UserCredential(flow, _userId, token);

            try
            {
                // Refreshes and re-persists automatically when the access token is stale.
                await credential.GetAccessTokenForRequestAsync(cancellationToken: ct).ConfigureAwait(false);
                return credential;
            }
            catch (TokenResponseException ex)
            {
                // Refresh token revoked, expired, or the OAuth client changed.
                throw new GmailNotAuthorizedException(DescribeTokenError(ex), ex);
            }
        }

        /// <summary>
        /// Forgets the stored authorisation. The next send will report that the account
        /// needs connecting again.
        /// </summary>
        public Task RevokeLocalAuthorizationAsync() => _tokenStore.ClearAsync();

        // ---------------------------------------------------------------- send

        /// <summary>Sends a message. Simple form.</summary>
        public Task<string?> SendEmailAsync(string recipient, string subject, string body, bool isHtml = false)
            => SendEmailAsync(recipient, subject, body, isHtml, null, null, CancellationToken.None);

        /// <summary>
        /// Sends a message with optional attachments and embedded images.
        /// Returns the Gmail message id, which also identifies the copy Gmail files in
        /// the sender's Sent folder.
        /// </summary>
        public async Task<string?> SendEmailAsync(
            string recipient,
            string subject,
            string body,
            bool isHtml,
            IEnumerable<EmailAttachment>? attachments,
            IEnumerable<EmailInlineImage>? inlineImages,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(recipient))
                throw new ArgumentException("At least one recipient is required.", nameof(recipient));

            var credential = await TryGetCredentialAsync(ct).ConfigureAwait(false)
                ?? throw new GmailNotAuthorizedException(
                    "The Gmail account is not connected yet. Open the Maintenance screen and " +
                    "use \"Connect Gmail\" to approve access once; alerts are sent silently afterwards.");

            using var service = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = _applicationName
            });

            var mime = BuildMimeMessage(recipient, subject, body, isHtml, attachments, inlineImages);

            using var buffer = new MemoryStream();
            await mime.WriteToAsync(buffer, ct).ConfigureAwait(false);

            var message = new Message { Raw = Base64UrlEncode(buffer.ToArray()) };

            try
            {
                // "me" = the account that granted the token. Gmail stamps the real From
                // when the header is absent, and files a copy in that account's Sent folder.
                var sent = await service.Users.Messages.Send(message, "me")
                    .ExecuteAsync(ct).ConfigureAwait(false);
                return sent?.Id;
            }
            catch (Google.GoogleApiException ex)
            {
                throw new InvalidOperationException(DescribeApiError(ex), ex);
            }
        }

        private MimeMessage BuildMimeMessage(
            string recipient, string subject, string body, bool isHtml,
            IEnumerable<EmailAttachment>? attachments,
            IEnumerable<EmailInlineImage>? inlineImages)
        {
            var message = new MimeMessage();

            // Only set From when a real sender address is configured, and only when it
            // parses. A fabricated or mismatched From makes the message fail DMARC at the
            // receiving end: Gmail still accepts the API call, but the mail is silently
            // dropped or spam-filtered. Leaving From unset makes Gmail stamp the
            // authorised account itself, which is always correctly aligned.
            var configuredSender = MaintenanceSettings.SenderAddress;
            if (!string.IsNullOrWhiteSpace(configuredSender)
                && MailboxAddress.TryParse(configuredSender, out var senderMailbox))
            {
                message.From.Add(new MailboxAddress(
                    MaintenanceSettings.SenderDisplayName, senderMailbox.Address));
            }

            foreach (var address in recipient.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = address.Trim();
                if (trimmed.Length > 0) message.To.Add(MailboxAddress.Parse(trimmed));
            }

            message.Subject = subject ?? string.Empty;

            var builder = new BodyBuilder();
            if (isHtml) builder.HtmlBody = body; else builder.TextBody = body;

            if (inlineImages != null && isHtml)
            {
                foreach (var image in inlineImages)
                {
                    try
                    {
                        var resource = builder.LinkedResources.Add(
                            image.FileName, image.Content, ContentType.Parse(image.MediaType));
                        resource.ContentId = image.ContentId;
                    }
                    catch { /* a bad image must never stop the alert */ }
                }
            }

            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    try
                    {
                        builder.Attachments.Add(
                            attachment.FileName, attachment.Content, ContentType.Parse(attachment.MediaType));
                    }
                    catch { /* a bad attachment must never stop the alert */ }
                }
            }

            message.Body = builder.ToMessageBody();
            return message;
        }

        /// <summary>Gmail expects base64url without padding.</summary>
        private static string Base64UrlEncode(byte[] data) =>
            Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        // ------------------------------------------------------------ diagnostics

        private static string DescribeTokenError(TokenResponseException ex)
        {
            var error = ex.Error?.Error ?? "unknown_error";
            var description = ex.Error?.ErrorDescription;

            return error switch
            {
                "invalid_grant" =>
                    "Google rejected the stored authorisation (invalid_grant). This usually means the " +
                    "token was revoked, the Google account password changed, or the app is still in " +
                    "\"Testing\" mode where refresh tokens expire after 7 days. Reconnect the account, " +
                    "and set the app to \"In production\" to stop this recurring.",
                "invalid_client" =>
                    "Google rejected the OAuth client (invalid_client). The credentials JSON does not " +
                    "match a live client - re-download it, and make sure the client type is \"Desktop app\".",
                "access_denied" =>
                    "Authorisation was denied on the consent screen. If the app is in Testing mode, the " +
                    "Google account must be listed under Audience -> Test users.",
                _ => $"Google authorisation failed ({error}). {description}"
            };
        }

        private static string DescribeApiError(Google.GoogleApiException ex)
        {
            var status = (int)ex.HttpStatusCode;
            return status switch
            {
                403 when ex.Message.Contains("has not been used", StringComparison.OrdinalIgnoreCase) ||
                         ex.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase) =>
                    "The Gmail API is not enabled for this Google Cloud project. Enable it under " +
                    "APIs & Services -> Library -> Gmail API, then retry.",
                403 =>
                    "Gmail refused the request (403). The token is probably missing the gmail.send " +
                    "scope - disconnect and reconnect the account to re-consent. " + ex.Message,
                401 =>
                    "Gmail rejected the credentials (401). Reconnect the Gmail account. " + ex.Message,
                429 =>
                    "Gmail rate limit reached (429). The alert was not sent; try again shortly.",
                _ => $"Gmail API error {status}: {ex.Message}"
            };
        }
    }
}
