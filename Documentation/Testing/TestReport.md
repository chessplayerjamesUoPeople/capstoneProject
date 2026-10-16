# AmigoAlert: System and Acceptance Test Report

| | |
|---|---|
| Build | branch `claude/system-acceptance-testing-plan-f5384c` (based on `feature/integrationTests` a1fab32) |
| Plan | [TestPlan.md](TestPlan.md) |
| Status | System level executed (automated). Manual system checks and acceptance level **pending**. |

## 1. Summary

| Level | Run | Passed | Failed | Skipped (open defect) | Pending |
|---|---|---|---|---|---|
| Unit (`Level=Unit`) | 2026-10-15, E1 | 18 | 0 | 0 | — |
| Integration (I-01 to I-17) | 2026-10-15, E1 | all | 0 | 0 | — |
| System, automated (`Level=System`) | 2026-10-16, E1 | 16 | 0 | 1 (S-11, D-01) | — |
| System, manual (M-01 to M-04) | — | — | — | — | 4 |
| Acceptance (UAT-01 to UAT-16) | — | — | — | — | 16 |

The full non-live suite (`--filter "Category!=LiveSms&Category!=TwilioRejection"`) ran 59 tests: 58 passed, 1 skipped, 0 failed, in 26 s on LocalDB. No real SMS was sent: every `Sms` row has a mock `MK…` SID (asserted in S-09).

## 2. System test results (automated)

The latest run (2026-10-16, LocalDB, mock SMS) wrote its results to [results/system-tests.html](results/system-tests.html), a readable report, and [results/system-tests.trx](results/system-tests.trx), the Visual Studio / Azure DevOps format. That run had 17 tests: 16 passed, 1 skipped, 0 failed. To regenerate them:

```powershell
dotnet test AmigoAlert.Tests --filter "Level=System" --results-directory Documentation\Testing\results `
  --logger "console;verbosity=normal" --logger "trx;LogFileName=system-tests.trx" --logger "html;LogFileName=system-tests.html"
```

| ID | Result | ID | Result |
|---|---|---|---|
| S-01 | Pass | S-07 | Pass |
| S-02 | Pass | S-08 | Pass |
| S-03 | Pass | S-09 | Pass |
| S-04 | Pass | S-10 | Pass |
| S-05 | Pass | S-11 | Skipped, D-01 |
| S-05b | Pass | C-01 to C-05 | Pass |
| S-06 | Pass | | |

### Manual system checks

| ID | Result | Notes |
|---|---|---|
| M-01 (NF-10) | Pending | The code review found no column-level encryption (no Always Encrypted or app-side encryption on `User`/`EmergencyContact`), so a fail is expected. See D-03. |
| M-02 (NF-12, secrets) | Preliminary pass | `git grep` of the working tree for Twilio and Notification Hub credentials and passwords found only option names and Key Vault secret names. Still to do: a full history scan with gitleaks. |
| M-03 (NF-12, TLS) | Pending | Needs the deployed hosts. |
| M-04 (consent page deployed) | Pending | See D-05. |

## 3. Previously verified requirements (carried over)

| Req | Evidence |
|---|---|
| NF-01, NF-02 | `AccuracyTests` CSV (`nf-accuracy-<timestamp>.csv`) from the earlier cycle |
| NF-03 | `LatencyTests` CSVs (`nf03-latency-*.csv`), live phases |
| NF-04 | `NotificationLatencyTests` CSV (`nf04-notification-latency-*.csv`) |
| NF-05 to NF-09 | Accessibility checklist results from the earlier cycle |

Attach or link those artefacts here when the report is finalised.

## 4. Defects

| ID | Sev. | Requirement | Description | Evidence | Proposed resolution | Status |
|---|---|---|---|---|---|---|
| D-01 | High | US-03 | The consent SMS cuts the user's name to about 5 characters ("Integ…"). The deployed `Consent:PageBaseUrl` (73 chars) plus a 32-char token leaves almost no room in 160 chars (`SmsMessageComposer.ConsentRequest`). The contact cannot tell who is asking, which undermines informed consent. | S-11 (skipped); S-01 run output: `"AmigoAlert: Integ added you as their emer…"` | Shorten the link: a custom short domain or path (e.g. `/c/`), and/or encode the token as 22-char base64url instead of 32-char hex. Remove the skip on S-11 once it passes. | Open |
| D-02 | Low | US-03, US-15, US-18, US-20 | The message texts differ from the SRS. US-03: "{name} added you as their emergency contact: {link}". US-18: "{name} is NOT okay and is having an emergency. Location: {url}" / "…Address: {address}" (the SRS says location unavailable, not address). US-20: "{name} did not check in at HH:mm. Last known location (HH:mm): {url}" / "…They may be having an emergency. Please call or visit them." Notification action "Emergency" vs the SRS's "Emergency!". US-22 matches exactly. | S-01, S-05, S-05b, S-06; `SmsMessageComposer.cs` | Most changes follow from NF-13's 160-char limit. Amend the SRS to the implemented texts, or confirm each change with the sponsor. | Open, decision needed |
| D-03 | High | NF-10 | No application-level or column-level encryption of PII (name, email, phone, address, landmarks, contact name/phone). Azure SQL TDE encrypts the storage, but a `SELECT` returns plaintext, which fails the SRS's verification ("Attempt to view plaintext database data; it should be illegible"). | Code review (`AmigoAlert.Data/Configurations`), M-01 pending | Use Always Encrypted (deterministic for `Email`, which is looked up; randomized for the rest) with the column master key in Key Vault. Alternatively, amend NF-10 to "encrypted at rest via TDE" if the sponsor accepts that. | Open, decision needed |
| D-04 | Low | US-15 | The SRS fixes check-ins at 8:00 and 18:00; the implementation lets the user choose two times at registration. | S-01, S-04 (user-chosen times); `CreateUserDto` | Amend the SRS (user-chosen times are a superset), or default the pickers to 8:00/18:00. | Open |
| D-05 | Medium | US-04 (deployment) | `AmigoAlert.Web/Program.cs` never calls `AddAmigoAlertKeyVault`, and its `appsettings.json` has no `Twilio`/`Consent` sections. `ConsentService` still resolves `ISmsService`, so on Azure the real `SmsService` is built with empty Twilio credentials. Responding to consent does not send SMS, so it probably works, but this is unverified. | Code review; C-tests use the mock | Verify with M-04. Either call `AddAmigoAlertKeyVault` in the Web host, or split consent recording out of `ConsentService` so the Web host does not depend on `ISmsService`. | Open, verify |
| D-06 | Low | US-23 | "Except during an emergency" is enforced only by the app hiding the button (`HomePage.xaml.cs:219`). `PUT api/users/{id}` and `PUT …/emergency-contact` succeed while the user is in Emergency/Escalated, and a contact change mid-emergency moves the alert's recipient to a non-consenting number. | Code review of `UsersController`, `ConsentService`; UAT-14 | Reject both PUTs with 409 when the status is Escalated or Emergency; add a system test. | Open |
| D-07 | Medium | US-09 | A declined contact is blocked only while they are the *current* contact (`RegistrationPage.xaml.cs:138`; the server keeps no list of declined numbers). Changing to N2 and then back to N1 sends N1 a new consent SMS. | Code review of `ConsentService.SetContactAsync`; UAT-05 extra check | Refuse in `ConsentService` when the user has any contact row with that phone at `Rejected` (409), and show the existing error in the app. | Open, confirm in UAT-05 |

## 5. Known risks (not defects against the SRS)

- The API has no authentication (`Documentation/PushNotifications.md` §6). Any client that knows a user id can raise or cancel that user's emergency.
- The leftover `GET /weatherforecast` endpoint and the unused NavMenu links remain from the templates.
- The consent page's terms text is placeholder lorem ipsum (`Consent.razor:22`). It must be replaced before acceptance; this is a blocker for UAT-04 sign-off.

## 6. Acceptance results

Fill in from [AcceptanceTestScripts.md](AcceptanceTestScripts.md).

| UAT | Result | Defects |
|---|---|---|
| UAT-01 to UAT-16 | Pending | |

## 7. Sign-off

| Role | Name | Date | Decision |
|---|---|---|---|
| Test lead | James Scarberry | | |
| Sponsor / instructor | | | |
