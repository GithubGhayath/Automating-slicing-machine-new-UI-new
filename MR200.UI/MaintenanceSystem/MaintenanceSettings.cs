using MR200.UI.Helpers;

namespace MR200.UI.MaintenanceSystem
{
    /// <summary>
    /// Every tunable value the maintenance system uses, resolved from App.config
    /// (non-secret settings) and from files kept outside the repository.
    ///
    /// SECURITY: no Gmail password exists anywhere in this system. Mail is sent through
    /// the Gmail API with OAuth 2.0. The only sensitive artefacts are the Google OAuth
    /// client JSON and the refresh token, and both live under %LOCALAPPDATA%\MR200 -
    /// never in source, never in App.config, never in the database, never in Git.
    /// </summary>
    public static class MaintenanceSettings
    {
        // ---- Environment variable names (values live outside the repository) ----

        /// <summary>Optional: overrides where the Google OAuth client JSON is read from.</summary>
        public const string EnvClientSecretsPath = "MAINTENANCE_GMAIL_CREDENTIALS";

        /// <summary>Optional: the address shown in the From header. Cosmetic only.</summary>
        public const string EnvSenderAddress = "MAINTENANCE_EMAIL_ADDRESS";

        /// <summary>Optional: overrides the recipient configured in App.config.</summary>
        public const string EnvRecipient = "MAINTENANCE_EMAIL_RECIPIENT";

        private static string? Env(string name)
        {
            // Process scope first, then the user's and machine's permanent variables,
            // so the app picks up a setx-ed value without needing a reboot.
            foreach (var scope in new[]
                     {
                         EnvironmentVariableTarget.Process,
                         EnvironmentVariableTarget.User,
                         EnvironmentVariableTarget.Machine
                     })
            {
                try
                {
                    var value = Environment.GetEnvironmentVariable(name, scope);
                    if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
                }
                catch { /* Machine scope can be denied on locked-down accounts. */ }
            }
            return null;
        }

        private static double ConfigDouble(string key, double fallback)
        {
            var raw = clsHelper.ReadFromConfiguration(key);
            return double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
        }

        private static int ConfigInt(string key, int fallback)
        {
            var raw = clsHelper.ReadFromConfiguration(key);
            return int.TryParse(raw, out var value) ? value : fallback;
        }

        private static bool ConfigBool(string key, bool fallback)
        {
            var raw = clsHelper.ReadFromConfiguration(key);
            return bool.TryParse(raw, out var value) ? value : fallback;
        }

        private static string ConfigString(string key, string fallback)
        {
            var raw = clsHelper.ReadFromConfiguration(key);
            return string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim();
        }

        // ---- Machine kinematics used to convert running time into element life ----

        /// <summary>Rotational speed of all four feeding shafts. Confirmed at 25 RPM.</summary>
        public static double FeedingShaftSpeedInRPM => ConfigDouble("FeedShaftSpeedRPM", 25);

        /// <summary>Linear speed of the A46 V-belts. Confirmed at 0.371 m/s.</summary>
        public static double BeltLinearSpeedInMeterPerSecond
            => ConfigDouble("VBeltLinearSpeedMeterPerSecond", 0.371);

        /// <summary>
        /// Pitch length of one A46 belt loop. One pass = one full circuit of this
        /// length. SKF PHG A46: 1200 mm pitch / 1168 mm inside length.
        /// </summary>
        public static double BeltPitchLengthInMeter
            => clsHelper.MillimeterToMeter(ConfigDouble("VBeltPitchLengthMillimeter", 1200));

        // ---- Predictive-maintenance thresholds ----

        /// <summary>Life-used percentage at which an element is reported as approaching failure.</summary>
        public static double WarningThresholdPercent
            => ConfigDouble("PredictiveMaintenanceWarningThreshold", 80);

        /// <summary>Life-used percentage at which an element is reported as critical.</summary>
        public static double CriticalThresholdPercent
            => ConfigDouble("PredictiveMaintenanceCriticalThreshold", 95);

        /// <summary>Window used for the failed element's recent maintenance table.</summary>
        public static int MaintenanceHistoryWindowInDays
            => ConfigInt("MaintenanceHistoryWindowDays", 30);

        // ---- Gmail API transport (no passwords anywhere) ----

        /// <summary>
        /// Google OAuth client file (client id + client secret). This identifies the
        /// APPLICATION to Google - it is not, and never contains, a Gmail password.
        /// Kept outside the repository; %LOCALAPPDATA%\MR200 by default.
        /// </summary>
        public static string ClientSecretsPath
        {
            get
            {
                var configured = Env(EnvClientSecretsPath)
                    ?? ConfigString("MaintenanceGmailCredentialsPath", string.Empty);

                if (!string.IsNullOrWhiteSpace(configured))
                    return Environment.ExpandEnvironmentVariables(configured);

                return System.IO.Path.Combine(DefaultSecureFolder, "credentials.json");
            }
        }

        /// <summary>Folder holding the DPAPI-encrypted OAuth token. Never inside the repository.</summary>
        public static string TokenFolder =>
            System.IO.Path.Combine(DefaultSecureFolder, "GmailToken");

        /// <summary>%LOCALAPPDATA%\MR200 - per-user, outside the project tree.</summary>
        private static string DefaultSecureFolder => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MR200");

        /// <summary>Application name reported to the Gmail API.</summary>
        public static string ApplicationName
            => ConfigString("MaintenanceGmailApplicationName", "MR200 Maintenance Monitoring");

        public static string SenderDisplayName
            => ConfigString("MaintenanceEmailSenderDisplayName", "MR200 Production Line Monitoring System");

        public static int ImageDownloadTimeoutSeconds
            => ConfigInt("MaintenanceElementImageDownloadTimeoutSeconds", 20);

        /// <summary>Whether the alert e-mail should be attempted at all.</summary>
        public static bool EmailEnabled => ConfigBool("MaintenanceEmailEnabled", true);

        // ---- E-mail identities ----

        /// <summary>
        /// Address used for the From display only. Gmail stamps the real sender from the
        /// authorised account, so this is cosmetic and is not a secret.
        /// </summary>
        public static string? SenderAddress
            => Env(EnvSenderAddress) ?? NullIfEmpty(ConfigString("MaintenanceEmailSenderAddress", string.Empty));

        /// <summary>Recipient. Environment variable wins, otherwise App.config. Not a secret.</summary>
        public static string Recipient
            => Env(EnvRecipient) ?? ConfigString("MaintenanceEmailRecipient", "ghayathahmad2002@gmail.com");

        private static string? NullIfEmpty(string value)
            => string.IsNullOrWhiteSpace(value) ? null : value;

        // ---- Readiness ----

        /// <summary>True once the OAuth client file exists and the account has been connected.</summary>
        public static bool AreCredentialsConfigured
        {
            get
            {
                try
                {
                    return System.IO.File.Exists(ClientSecretsPath)
                        && System.IO.Directory.Exists(TokenFolder)
                        && System.IO.Directory.EnumerateFiles(TokenFolder, "*.dat").Any();
                }
                catch { return false; }
            }
        }

        /// <summary>Operator-facing explanation of what is still missing. Safe to log or display.</summary>
        public static string CredentialStatusMessage
        {
            get
            {
                try
                {
                    if (!System.IO.File.Exists(ClientSecretsPath))
                        return "Gmail is not set up yet. Place the Google OAuth client file (credentials.json) at "
                               + ClientSecretsPath + ", then use \"Connect Gmail\" on the Maintenance screen.";

                    if (!AreCredentialsConfigured)
                        return "The Gmail account has not been connected yet. Use \"Connect Gmail\" on the "
                               + "Maintenance screen to approve access once; alerts are sent silently afterwards.";

                    return "Gmail is connected. Maintenance alerts will be sent to " + Recipient + ".";
                }
                catch (Exception ex)
                {
                    return "Gmail configuration could not be checked: " + ex.Message;
                }
            }
        }

    }
}
