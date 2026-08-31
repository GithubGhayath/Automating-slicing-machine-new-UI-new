# Sending Maintenance Alerts through Gmail (OAuth 2.0)

The MR200 maintenance system sends its alerts with the **Gmail API over OAuth 2.0**.
SMTP and Gmail App Passwords are not used and are not supported.

---

## 0. The distinction that matters most

These two things are constantly confused. They are not the same and are not
interchangeable.

| | **Your Gmail password** | **Google Cloud OAuth client** |
|---|---|---|
| What it is | The secret that logs a *human* into a Google account | A client ID + client secret that identify an *application* to Google |
| Who holds it | Only you, in your head / password manager | The application, in `credentials.json` |
| What it grants alone | Full access to your account | **Nothing.** It cannot touch any account until a human approves a consent screen |
| Does this app ever see it | **Never. Not once.** | Yes — that is its normal job |

The application never asks for, receives, or stores your Gmail password. When
authorisation is needed, it opens **Google's own** sign-in page in your browser. You type
your password into Google, not into this app. Google then hands the application a
**token** scoped to one narrow permission.

Even the "client secret" is not a password to your account. For desktop apps Google
explicitly documents it as **not confidential** — it cannot be used to access anything
without a user first consenting. Keep it out of Git anyway, as good practice.

---

## 1. Google Cloud configuration

Google reorganised this area in 2025: OAuth setup now lives under **Google Auth
Platform** rather than the old "OAuth consent screen" page. Steps below use the current
layout.

### 1.1 Create the project

1. Go to <https://console.cloud.google.com>.
2. Click the project selector in the top bar → **New project**.
3. Name it `MR200-Maintenance`. Leave *Location* as *No organisation*.
4. **Create**, then make sure it is the selected project in the top bar.

### 1.2 Enable the Gmail API

1. Left menu → **APIs & Services** → **Library**.
2. Search `Gmail API` → open it → **Enable**.

If you skip this, sending fails with *"Gmail API has not been used in project … before
or it is disabled."*

### 1.3 Configure the Google Auth Platform

Left menu → **Google Auth Platform** (or go to <https://console.cloud.google.com/auth>).
If it is not configured yet, click **Get started**.

- **Overview / Branding**
  - *App name*: `MR200 Maintenance Monitoring`
  - *User support email*: your Gmail address
  - *Developer contact information*: your Gmail address
- **Audience**
  - *User type*: **External** (required for a personal `@gmail.com` account; *Internal*
    only exists for Workspace organisations)
  - Under **Test users**, click **Add users** and add **the exact Gmail address that
    will send the alerts**. Skipping this causes `Error 403: access_denied`.
- **Data Access** → **Add or remove scopes** → filter for Gmail and tick **only**:

  ```
  https://www.googleapis.com/auth/gmail.send
  ```

  Do **not** add `gmail.readonly`, `gmail.modify` or `mail.google.com`. `gmail.send` is
  the narrowest scope that can send a message — it cannot read, search or delete mail.
  Save.

### 1.4 Create the OAuth client

1. **Google Auth Platform** → **Clients** → **Create client**
   (older layout: *APIs & Services* → *Credentials* → *Create credentials* → *OAuth client ID*).
2. **Application type: `Desktop app`** ← this is critical.
   - `Web application` will fail with `redirect_uri_mismatch`.
   - Desktop clients get loopback redirects (`http://127.0.0.1:<random port>`)
     automatically; you do **not** configure any redirect URI yourself.
3. Name: `MR200 Desktop`.
4. **Create** → in the dialog, **Download JSON**.

### 1.5 Testing vs Production — read this, it will save you a weekly annoyance

While the app's publishing status is **Testing**, Google expires refresh tokens after
**7 days**. Your alerts would silently stop working every week with `invalid_grant`.

For a machine that must send unattended alerts, go to **Google Auth Platform** →
**Audience** → **Publish app** → confirm.

- Refresh tokens then stop expiring.
- Because `gmail.send` is a restricted scope and the app is unverified, you will see a
  **"Google hasn't verified this app"** screen once — click **Advanced** → **Go to
  MR200 Maintenance Monitoring (unsafe)**. This is expected for a private in-house app
  and is capped at 100 users. No verification submission is required for personal use.

If you prefer to stay in Testing, everything works — you just have to click **Connect
Gmail** again every 7 days.

---

## 2. Where the credentials file goes

**Not in the repository.** Save the downloaded JSON as exactly:

```
C:\Users\<you>\AppData\Local\MR200\credentials.json
```

Create the folder first if needed:

```bash
mkdir "%LOCALAPPDATA%\MR200"
```

The app prints the exact expected path on the Maintenance screen if the file is missing.

To keep it somewhere else, either set `MaintenanceGmailCredentialsPath` in
`MR200.UI/App.config`, or set an environment variable:

```bash
setx MAINTENANCE_GMAIL_CREDENTIALS "D:\secure\credentials.json"
```

### What goes in Git and what does not

| File | Commit? |
|---|---|
| `MR200.UI/MaintenanceSystem/Gmail/*.cs` | Yes — code only |
| `MR200.UI/App.config` | Yes — contains no secrets |
| `credentials.json` | **Never** |
| `GmailToken/*.dat` (refresh token) | **Never** |

`.gitignore` already blocks all of them:

```
credentials.json
client_secret*.json
**/Credentials/
GmailToken/
*.token.json
token.json
```

Since both live under `%LOCALAPPDATA%`, they are outside the repo entirely — the
`.gitignore` rules are a second line of defence.

---

## 3. Token storage

`DpapiDataStore` replaces Google's stock `FileDataStore`, which writes refresh tokens as
**plain JSON**. Instead the token is encrypted with **Windows DPAPI**
(`DataProtectionScope.CurrentUser`) before hitting disk:

```
C:\Users\<you>\AppData\Local\MR200\GmailToken\*.dat
```

- Only the same Windows user on the same machine can decrypt it.
- Copying the file to another PC or reading it as another user yields nothing usable.
- A corrupt or foreign file is discarded automatically and the app just asks you to
  reconnect, rather than failing forever.

**To reset authorisation:** click **Disconnect** on the Maintenance screen, or delete
that folder. Either way the next alert reports that the account needs connecting.

**To revoke from Google's side** (recommended if you think the token leaked):
<https://myaccount.google.com/permissions> → *MR200 Maintenance Monitoring* → **Remove
access**. The stored token stops working immediately.

---

## 4. Project structure

```
SMRM new version/
├── MR200.UI/
│   ├── MaintenanceSystem/
│   │   ├── Gmail/
│   │   │   ├── GmailEmailService.cs      ← OAuth + Gmail API send
│   │   │   └── DpapiDataStore.cs         ← encrypted token store
│   │   ├── MaintenanceEmailService.cs    ← builds + dispatches the alert
│   │   ├── MaintenanceEmailBuilder.cs    ← HTML body
│   │   └── MaintenanceSettings.cs        ← config + readiness
│   ├── App.config                        ← non-secret settings only
│   └── MainWindow.xaml                   ← Connect Gmail / Disconnect buttons
│
└── %LOCALAPPDATA%\MR200\                 ← OUTSIDE the repository
    ├── credentials.json                  ← OAuth client
    └── GmailToken\*.dat                  ← DPAPI-encrypted refresh token
```

---

## 5. NuGet packages

| Package | Version | Why |
|---|---|---|
| `Google.Apis.Gmail.v1` | 1.75.0.4225 | The Gmail API client. Pulls in `Google.Apis.Auth`, which provides the OAuth flow, the loopback receiver and `UserCredential` |
| `MimeKit` | 4.17.0 | Builds correct MIME. Needed for HTML bodies, `cid:` embedded images and attachments — `System.Net.Mail` cannot be serialised to raw MIME for the Gmail API |
| `System.Security.Cryptography.ProtectedData` | 10.0.11 | DPAPI, so the refresh token is encrypted at rest instead of plain JSON |

Already installed. `Google.Apis.Auth` is transitive — do not add it separately.

---

## 6. Using it

### Code

```csharp
var gmail = new GmailEmailService(
    MaintenanceSettings.ClientSecretsPath,
    MaintenanceSettings.TokenFolder,
    MaintenanceSettings.ApplicationName);

// once, interactive
await gmail.AuthorizeInteractiveAsync();

// afterwards, silent
await gmail.SendEmailAsync("someone@example.com", "Subject", "<b>Hi</b>", isHtml: true);
```

The alert path uses the richer overload with attachments and inline images.

### In the app

The consent screen is **never** opened automatically. A bearing failing at 3 a.m. must
not block on a browser window, so:

- **Maintenance screen → Connect Gmail** — deliberate, one time, opens the browser.
- **Automatic alerts** — silent, using the stored refresh token only. If the account
  isn't connected, the alert is still generated and saved to
  `Desktop\Reports\MaintenanceAlerts\`, the machine still stops, and the on-screen
  failure dialog says so.

### First run, step by step

1. Click **Connect Gmail**.
2. Your default browser opens Google's account chooser.
3. Choose the sending account, type your password **into Google's page**.
4. Unverified-app warning → **Advanced** → **Go to … (unsafe)**.
5. Consent screen shows exactly one permission: **"Send email on your behalf"**.
   If you see anything about *reading* mail, the scope is wrong — stop and re-check §1.3.
6. Click **Continue**. The tab shows "MR200 is connected to Gmail"; close it.
7. The Maintenance screen now reads *"Gmail is connected."*

### Every run after that

No browser, no prompt. The refresh token is read from the encrypted store and swapped
for a fresh access token automatically.

### Test send

```bash
dotnet run --project "C:\Users\SHIHA_~1\AppData\Local\Temp\claude\C--Users-shiha-a8q6i0r-SMRM-new-version\3b5645ec-4c74-4ef6-a228-371a784e9d00\scratchpad\SendTest"
```

Prints the resolved paths, whether the account is connected, then sends a full alert to
`ghayathahmad2002@gmail.com` and reports which attachments made it.

---

## 7. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `Error 403: access_denied` | Account not in **Test users** while app is in Testing | Auth Platform → Audience → Test users → add the sending address |
| `Google hasn't verified this app` | Unverified app with a restricted scope | **Advanced** → *Go to … (unsafe)*. Normal for private apps |
| `invalid_client` | Wrong / stale `credentials.json`, or client deleted | Re-download JSON from Clients; confirm type is **Desktop app** |
| `redirect_uri_mismatch` | Client created as **Web application** | Delete it, create a **Desktop app** client, re-download |
| `invalid_grant` after ~7 days | App still in **Testing** | Publish the app (§1.5), then reconnect |
| `invalid_grant` immediately | Token revoked, or Google password changed | **Disconnect** then **Connect Gmail** again |
| `Gmail API has not been used in project…` | API not enabled | APIs & Services → Library → Gmail API → Enable |
| `403` mentioning insufficient scope | Token predates the scope change | Disconnect, reconnect to re-consent |
| Consent screen asks to *read* mail | Extra scopes configured | Data Access → remove all but `gmail.send`, reconnect |
| Browser never opens | Loopback port blocked | Allow the app through the firewall; try once with antivirus web-shield off |
| App Passwords page unavailable | Expected — irrelevant here | Nothing to do. This design deliberately does not use App Passwords |
| Token works for you, not another Windows user | DPAPI is per-user by design | Each Windows user connects once |

---

## 8. If a secret is ever committed

`credentials.json` in Git history is not catastrophic — it cannot access any account
without user consent — but clean it up:

1. **Google Cloud → Clients → delete the exposed client.** This is the step that
   actually matters; it invalidates it everywhere.
2. Create a new Desktop client, download the new JSON to `%LOCALAPPDATA%\MR200\`.
3. Remove it from the working tree and confirm it is ignored:

   ```bash
   git rm --cached MR200.UI/credentials.json
   ```

4. Purge it from history only if the repo is public — `git filter-repo
   --path credentials.json --invert-paths`, then force-push and tell collaborators to
   re-clone.
5. Revoke any tokens issued under it at <https://myaccount.google.com/permissions>.

**A leaked refresh token is the more serious case** — revoke it immediately at
<https://myaccount.google.com/permissions>.
