using System.IO;
using MR200.UI.MaintenanceSystem.Gmail;

namespace MR200.UI.MaintenanceSystem
{
    public sealed class MaintenanceEmailResult
    {
        public bool Sent { get; init; }
        public string Message { get; init; } = string.Empty;
        public bool ElementImageAttached { get; init; }
        public bool CatalogAttached { get; init; }
        public bool MachinePositionImageAttached { get; init; }

        /// <summary>Where the alert was written when it could not be sent.</summary>
        public string? SavedCopyPath { get; init; }

        /// <summary>Gmail's id for the sent message; also findable in the sender's Sent folder.</summary>
        public string? MessageId { get; init; }
    }

    /// <summary>
    /// Sends the maintenance alert through the Gmail API using OAuth 2.0.
    ///
    /// There is no Gmail password anywhere in this system. The account is connected
    /// once from the Maintenance screen; afterwards the stored refresh token lets alerts
    /// go out silently, with no browser and no user present.
    ///
    /// Sending is best-effort by design: a failure here is reported but never undoes
    /// the failure detection or the machine stop.
    /// </summary>
    public static class MaintenanceEmailService
    {
        /// <summary>
        /// Builds and sends the alert. Always returns a result; never throws.
        /// </summary>
        public static async Task<MaintenanceEmailResult> SendFailureAlertAsync(MaintenanceAlertContext context)
        {
            // Resolve attachments first, so the body can say what is actually included.
            ResolvedAsset? elementImage = null;
            ResolvedAsset? catalog = null;
            ResolvedAsset? positionImage = null;

            try
            {
                elementImage = await MaintenanceAssetResolver
                    .TryResolveElementImageAsync(context.FailedElement.ImageUrl).ConfigureAwait(false);
            }
            catch { /* never blocks the alert */ }

            try
            {
                positionImage = await MaintenanceAssetResolver
                    .TryResolveElementImageAsync(context.FailedElement.ImageOfElementAtMachine).ConfigureAwait(false);
            }
            catch { /* machine-position pictures are not supplied yet */ }

            try
            {
                catalog = MaintenanceAssetResolver.TryLoadCatalog(context.FailedElement.CatalogFileName);
            }
            catch { /* never blocks the alert */ }

            string subject = MaintenanceEmailBuilder.BuildSubject(context);
            string body = MaintenanceEmailBuilder.BuildHtmlBody(context,
                elementImage != null, positionImage != null);

            if (!MaintenanceSettings.EmailEnabled)
            {
                var path = TrySaveCopy(subject, body);
                return new MaintenanceEmailResult
                {
                    Sent = false,
                    Message = "Maintenance e-mail is disabled in configuration. The alert was generated but not sent.",
                    ElementImageAttached = elementImage != null,
                    CatalogAttached = catalog != null,
                    MachinePositionImageAttached = positionImage != null,
                    SavedCopyPath = path
                };
            }

            if (!MaintenanceSettings.AreCredentialsConfigured)
            {
                var path = TrySaveCopy(subject, body);
                return new MaintenanceEmailResult
                {
                    Sent = false,
                    Message = MaintenanceSettings.CredentialStatusMessage,
                    ElementImageAttached = elementImage != null,
                    CatalogAttached = catalog != null,
                    MachinePositionImageAttached = positionImage != null,
                    SavedCopyPath = path
                };
            }

            try
            {
                var messageId = await SendAsync(subject, body, elementImage, positionImage, catalog,
                    CancellationToken.None).ConfigureAwait(false);

                return new MaintenanceEmailResult
                {
                    Sent = true,
                    MessageId = messageId,
                    Message = $"Maintenance alert sent to {MaintenanceSettings.Recipient}"
                              + (messageId != null ? $" (Gmail id {messageId})." : "."),
                    ElementImageAttached = elementImage != null,
                    CatalogAttached = catalog != null,
                    MachinePositionImageAttached = positionImage != null
                };
            }
            catch (Exception ex)
            {
                var path = TrySaveCopy(subject, body);
                return new MaintenanceEmailResult
                {
                    Sent = false,
                    Message = "Could not send the maintenance alert: " + ex.Message,
                    ElementImageAttached = elementImage != null,
                    CatalogAttached = catalog != null,
                    MachinePositionImageAttached = positionImage != null,
                    SavedCopyPath = path
                };
            }
        }

        /// <summary>Shared instance so the token is loaded once per process.</summary>
        private static GmailEmailService CreateGmailService() => new(
            MaintenanceSettings.ClientSecretsPath,
            MaintenanceSettings.TokenFolder,
            MaintenanceSettings.ApplicationName);

        /// <summary>
        /// Opens Google's consent screen so the operator can connect the account. Called
        /// only from the Maintenance screen, never from an automatic alert.
        /// </summary>
        public static async Task<(bool Connected, string Message)> ConnectGmailAccountAsync(
            CancellationToken ct = default, Action<string>? onAuthorizationUrl = null)
        {
            try
            {
                var gmail = CreateGmailService();
                await gmail.AuthorizeInteractiveAsync(ct, onAuthorizationUrl).ConfigureAwait(false);
                return (true, "Gmail account connected. Maintenance alerts will be sent to "
                              + MaintenanceSettings.Recipient + ".");
            }
            catch (GmailNotAuthorizedException ex)
            {
                return (false, ex.Message);
            }
            catch (OperationCanceledException)
            {
                return (false, "Connecting the Gmail account was cancelled.");
            }
            catch (Exception ex)
            {
                return (false, "Could not connect the Gmail account: " + ex.Message);
            }
        }

        /// <summary>Forgets the stored authorisation so the account can be reconnected.</summary>
        public static async Task<string> DisconnectGmailAccountAsync()
        {
            try
            {
                await CreateGmailService().RevokeLocalAuthorizationAsync().ConfigureAwait(false);
                return "The stored Gmail authorisation was removed from this machine. "
                       + "Use \"Connect Gmail\" to authorise again.";
            }
            catch (Exception ex)
            {
                return "Could not remove the stored Gmail authorisation: " + ex.Message;
            }
        }

        private static async Task<string?> SendAsync(string subject, string htmlBody,
            ResolvedAsset? elementImage, ResolvedAsset? positionImage, ResolvedAsset? catalog,
            CancellationToken ct)
        {
            var gmail = CreateGmailService();

            var inlineImages = new List<EmailInlineImage>();
            if (elementImage != null)
                inlineImages.Add(new EmailInlineImage(
                    MaintenanceEmailBuilder.ElementImageContentId,
                    elementImage.FileName, elementImage.Content, elementImage.MediaType));
            if (positionImage != null)
                inlineImages.Add(new EmailInlineImage(
                    MaintenanceEmailBuilder.MachinePositionImageContentId,
                    positionImage.FileName, positionImage.Content, positionImage.MediaType));

            var attachments = new List<EmailAttachment>();
            // Only the catalogue of the failed element travels with the alert.
            if (catalog != null)
                attachments.Add(new EmailAttachment(catalog.FileName, catalog.Content, catalog.MediaType));
            // The picture also rides along as a real attachment, for clients that block
            // embedded images.
            if (elementImage != null)
                attachments.Add(new EmailAttachment(
                    elementImage.FileName, elementImage.Content, elementImage.MediaType));

            return await gmail.SendEmailAsync(
                MaintenanceSettings.Recipient, subject, htmlBody,
                isHtml: true, attachments, inlineImages, ct).ConfigureAwait(false);
        }

        private static IEnumerable<string> SplitRecipients(string recipients)
        {
            var parts = recipients.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0) yield return trimmed;
            }
        }

        /// <summary>
        /// Writes the rendered alert to disk when it cannot be sent, so the failure
        /// report is never lost. Returns null if even that fails.
        /// </summary>
        private static string? TrySaveCopy(string subject, string htmlBody)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "Reports", "MaintenanceAlerts");
                Directory.CreateDirectory(folder);

                string safeSubject = string.Join("_", subject.Split(Path.GetInvalidFileNameChars()));
                if (safeSubject.Length > 80) safeSubject = safeSubject.Substring(0, 80);

                string path = Path.Combine(folder, $"{DateTime.Now:yyyyMMdd_HHmmss}_{safeSubject}.html");
                File.WriteAllText(path, htmlBody);
                return path;
            }
            catch
            {
                return null;
            }
        }
    }
}
