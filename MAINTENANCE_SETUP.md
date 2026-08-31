# MR200 — Maintenance & Predictive Maintenance System

This document explains how to configure the maintenance system that was added to the
existing MR200 application. It contains **no credentials** and never will.

---

## 1. What was added

A predictive-maintenance layer that watches the life of the 16 physical elements the
machine runs on, stops the machine when one reaches the end of its rated life, and
e-mails the maintenance department with everything they need to act.

It is additive: RUN, STOP, End Process, the force calculations, the History dashboard
and the PDF export all behave exactly as before.

---

## 2. Database

Three new tables, created by the `AddMaintenanceSystem` migration. No existing table is
altered by it.

| Table | Purpose |
|---|---|
| `ElementsInformation` | Catalogue data for one element: name, rated life, life unit, price, picture link, catalogue file |
| `Elements` | One physical element on the machine: description, cumulative consumed life, machine-position picture, position number, failure latch |
| `Maintenance` | One maintenance / replacement operation: who, costs, date, element |

Relationships:

```
Element  1 ────── 1  ElementInformation     (unique FK on Elements.ElementInformationId)
Element  1 ────── *  Maintenance            (FK on Maintenance.ElementId)
```

`Elements ↔ ElementsInformation` is a **true one-to-one**: each of the 16 physical
elements owns its own information row, so a single bearing can be re-rated or given a
different picture without affecting its siblings.

Apply the migration with:

```bash
dotnet ef database update --project DataAccess
```

The application also applies it automatically at startup via `DbInitializer.Initialize()`.

---

## 3. The 16 monitored elements

`OrderOfElementAtMachine` is the number an operator uses to find the part on the machine.

| # | Type | Position |
|---|---|---|
| 1 | Bearing 16009 | Feeding shaft 1 (first from right) — at the end of the shaft |
| 2 | Bearing 16009 | Feeding shaft 1 — next to #1 |
| 3 | Bearing 16009 | Feeding shaft 2 — at the end of the shaft |
| 4 | Bearing 16009 | Feeding shaft 2 — next to #3 |
| 5 | Bearing 16009 | Feeding shaft 3 — at the end of the shaft |
| 6 | Bearing 16009 | Feeding shaft 3 — next to #5 |
| 7 | Bearing 16009 | Feeding shaft 4 — at the end of the shaft |
| 8 | Bearing 16009 | Feeding shaft 4 — next to #7 |
| 9 | Bearing 16007 | Top cutting shaft |
| 10 | Bearing 16008 | Top cutting shaft |
| 11 | Bearing 16007 | Bottom cutting shaft |
| 12 | Bearing 16008 | Bottom cutting shaft |
| 13 | A46 V-Belt | Drives feeding shaft 1 |
| 14 | A46 V-Belt | Drives feeding shaft 2 |
| 15 | A46 V-Belt | Drives feeding shaft 3 |
| 16 | A46 V-Belt | Drives feeding shaft 4 |

Rated lives and prices:

| Type | Rated life | Unit | Price |
|---|---|---|---|
| Bearing 16009 | 4,492,100,000 | revolutions | $47 |
| Bearing 16008 | 2,628,100,000 | revolutions | $36 |
| Bearing 16007 | 2,197,000,000 | revolutions | $31 |
| A46 V-Belt | 288,000,000 | passes | $10 |

---

## 4. How life is consumed

Life is always `rate × real elapsed seconds`. It never depends on timer ticks, frame
rate or CPU speed.

| Element group | Rate | Source |
|---|---|---|
| Bearings #1–#8 (feeding shafts) | `FeedShaftSpeedRPM / 60` = 0.41667 rev/s | `FeedShaftSpeedRPM` = 25 |
| Bearings #9–#12 (cutting shafts) | `RPM / 60` ≈ 58.21 rev/s | the application's own `NumberOfRotations_Unit_RPM(...)` |
| Belts #13–#16 | `beltSpeed / beltPitchLength` = 0.371 / 1.2 = 0.30917 passes/s | `VBeltLinearSpeedMeterPerSecond`, `VBeltPitchLengthMillimeter` |

**Belt passes.** One *pass* is one complete circuit of the belt around its loop, so the
belt travels its own pitch length once per pass. The SKF PHG A46 has a 1200 mm pitch
length, therefore `passes/s = 0.371 m/s ÷ 1.2 m`. Belt passes are stored and compared in
their own unit and are never treated as bearing revolutions.

**Cutting-shaft speed is never hard-coded.** It is read from the running calculation, so
changing `MaxCuttingVelocity` or `BladeDiameter` in `App.config` automatically changes
how fast those bearings age.

---

## 5. When life is saved

| Machine action | Effect on element life |
|---|---|
| **Start** (RUN) | Loads the 16 elements and their persistent `ConsumedLife` from the database **once**, then begins accumulating in memory |
| **Stop** (PAUSE) | Freezes the counters. **Nothing is written to the database.** Pressing Start again resumes the same operation and keeps what was accumulated |
| **End Process** (production complete) | Adds the accumulated life onto `ConsumedLife` in one transaction and ends the session, so the next RUN reloads and continues from there |
| **Element failure** | Persists the accumulated life immediately, so the failure survives a restart |

The database is read once per production operation and written once per production
operation. Nothing queries it from inside the simulation loop.

---

## 6. Failure behaviour

When `ConsumedLife >= DefaultLife` for any element:

1. The machine stops through the application's **existing** `StopMachine()` path — the
   same code the Stop button runs.
2. The accumulated life and a `FailureDetected` latch are written to the database, so
   the event is raised exactly once, even across restarts.
3. An animated alert appears in the application showing the failed element, its life
   figures, the machine condition at failure, and the e-mail status live.
4. The maintenance alert e-mail is generated and sent in the background.

If the e-mail cannot be sent, the machine still stops and the alert still appears; the
rendered e-mail is saved to `Desktop\Reports\MaintenanceAlerts\` so nothing is lost.

---

## 7. E-mail configuration

Alerts are sent through the **Gmail API using OAuth 2.0**. There is no Gmail password
anywhere in this system, and SMTP is not used.

Full walkthrough: **[GMAIL_OAUTH_SETUP.md](GMAIL_OAUTH_SETUP.md)**.

In short:

1. Create a Google Cloud project, enable the **Gmail API**.
2. Configure Google Auth Platform with the single scope
   `https://www.googleapis.com/auth/gmail.send`.
3. Create an OAuth client of type **Desktop app**, download the JSON.
4. Save it as `%LOCALAPPDATA%\MR200\credentials.json` (outside the repository).
5. Run the app, open **Maintenance**, click **Connect Gmail**, approve once.

The refresh token is stored DPAPI-encrypted in `%LOCALAPPDATA%\MR200\GmailToken\`, so
later alerts send silently with no browser and no user present.

### Non-secret settings — `MR200.UI/App.config`

```xml
<add key="MaintenanceEmailEnabled" value="true" />
<add key="MaintenanceEmailRecipient" value="ghayathahmad2002@gmail.com" />
<add key="MaintenanceEmailSenderAddress" value="GhayathAlali2003@gmail.com" />
```

Optional environment overrides: `MAINTENANCE_GMAIL_CREDENTIALS`,
`MAINTENANCE_EMAIL_RECIPIENT`, `MAINTENANCE_EMAIL_ADDRESS`.

To reset: **Disconnect** on the Maintenance screen, or revoke at
<https://myaccount.google.com/permissions>.

## 8. Catalogues and pictures

- **Catalogues** ship in `MR200.UI/MaintenanceAssets/Catalogs/` and are copied next to
  the executable at build time. `ElementsInformation.Catalog` stores only the file name,
  and the resolver refuses any value trying to escape that folder. Only the failed
  element's catalogue is attached.

- **Element pictures** (`ElementsInformation.Image`) and **machine-position pictures**
  (`Elements.ImageOfElementAtMachine`) both accept either form:

  | Stored value | Behaviour |
  |---|---|
  | `16009.jpg` | Loaded from `MaintenanceAssets/Images/` — **most reliable** |
  | `D:\photosp09.jpg` | Loaded from that absolute path |
  | `https://…` | Fetched at send time; a product page is parsed for its `og:image` |

  A local file always wins over a URL, because it cannot be blocked or taken offline.

  **Known limitation with retailer URLs.** Verified against the live sites:

  | Element | Source | Result |
  |---|---|---|
  | Bearing 16007 | nskbearingcatalogue.com | works |
  | A46 V-Belt | dieselbelting.com | works |
  | Bearing 16009 | amazon.it | blocked (bot protection) |
  | Bearing 16008 | nl.rs-online.com | blocked (bot protection) |

  Amazon and RS Online refuse automated requests, so those two pictures cannot be
  fetched. Drop image files into `MR200.UI/MaintenanceAssets/Images/` and set
  `ElementsInformation.Image` to the file name to fix it permanently.

  A missing picture never blocks the alert — it sends without it and the ATTACHMENTS
  section says so.

## 9. Maintenance screen

`Maintenance` in the sidebar shows:

- Predictive health counters — monitored / warning / critical / failed — against the
  configurable warning threshold
- Every element's consumed life, rated life, remaining life, life-used percentage,
  status and last maintenance date
- The full maintenance history from the database
- A **Maintain** action per element that records a maintenance operation and, when the
  element was replaced, resets `ConsumedLife` to 0 in the same transaction

Historical maintenance records are never deleted, including after a replacement.

---

## 10. Tunable thresholds

```xml
<add key="PredictiveMaintenanceWarningThreshold" value="80" />
<add key="PredictiveMaintenanceCriticalThreshold" value="95" />
<add key="MaintenanceHistoryWindowDays" value="30" />
```

The warning threshold is read from configuration in one place and is never hard-coded
across the application.

---

## 11. Testing end-of-life behaviour

At the real rated lives nothing fails for years of running, so end-of-life must be
exercised by temporarily lowering a rated life:

```sql
UPDATE ElementsInformation SET DefaultLife = 1000 WHERE Id = 9;
```

Select a wood type → Calculate Forces → Start. Element #9 turns at ~58.2 rev/s, so it
reaches 1000 revolutions in about 17 seconds. Expect the machine to stop by itself, the
alert to appear once, and the e-mail to be generated once.

Restore the real value afterwards:

```sql
UPDATE ElementsInformation SET DefaultLife = 2197000000 WHERE Name = 'Bearing 16007';
```
