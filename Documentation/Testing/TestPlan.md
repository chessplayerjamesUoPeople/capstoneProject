# AmigoAlert: System and Acceptance Test Plan

| | |
|---|---|
| Version | 1.0 (2026-10-15) |
| Requirements baseline | `Requirements.docx` (US-01 to US-23, NF-01 to NF-13, DR-01, DR-02) |
| Related documents | [TraceabilityMatrix.md](TraceabilityMatrix.md), [SystemTestCases.md](SystemTestCases.md), [AcceptanceTestScripts.md](AcceptanceTestScripts.md), [TestReport.md](TestReport.md) |

## 1. Purpose and scope

This plan covers the last two test levels for AmigoAlert:

- **System testing** checks the integrated system against the functional and in-scope nonfunctional requirements, using only its public surfaces. The integrated system is the API, the WebJob, the consent web page, SQL Server and the SMS gateway (mocked).
- **Acceptance testing** checks that the product does what the SRS's users need. It uses the real Android app, with a tester acting as the user and another acting as the emergency contact.

**In scope:** US-01 to US-23, NF-10 to NF-13, and DR-02.

**Out of scope:**
- NF-01 to NF-09 are already verified. Their existing results are carried into the report and not re-run:
  - NF-01/02 are the accuracy suite.
  - NF-03/04 are the latency suites.
  - NF-05 to NF-09 are the accessibility checks.
- DR-01 is a design artefact (the activity diagram), reviewed rather than tested.
- Performance under load, and iOS (no iOS head exists).

## 2. Test levels

| Level | What is exercised | How | Where |
|---|---|---|---|
| Unit | Pure rules: SMS composition (NF-13), phone normalisation (DR-02) | xUnit, `[Trait("Level","Unit")]` | `AmigoAlert.Tests/Unit/` |
| Integration | One API area at a time against SQL Server (I-01 to I-17) | xUnit + `WebApplicationFactory` | `AmigoAlert.Tests/Integration/` (existing) |
| **System** | End-to-end journeys across the API, WebJob, consent page and DB (S-01 to S-11, C-01 to C-05), plus manual system checks (M-01 to M-04) | xUnit, `[Trait("Level","System")]`; manual for M-xx | `AmigoAlert.Tests/SystemTests/`, [SystemTestCases.md](SystemTestCases.md) |
| **Acceptance** | User stories on the Android app (UAT-01 to UAT-16) | Scripted manual Given/When/Then, with screenshots | [AcceptanceTestScripts.md](AcceptanceTestScripts.md) |

### System test design

The journeys drive the system only the way its actors do:
- **The device** goes through the HTTP API.
- **The emergency contact** goes through the link in the consent SMS, either via the API or via the real Blazor consent page with its antiforgery-protected forms.
- **The scheduler** goes through the WebJob's own entry point, `AmigoAlert.WebJob.Program.RunAsync`. It is run at chosen instants (the occurrence, +10 min and +20 min) with a fixed `TimeProvider`, so a full check-in cycle runs in seconds.

The database is read directly only to check things no public surface exposes: location rows, inactive contacts and `Sms` rows. Twilio is replaced with `MockSmsService` through the same `Twilio:UseMockSms` switch a local run uses, so no SMS leaves the machine and every message is recorded for assertion.

## 3. Environments

| Environment | Used for | Configuration |
|---|---|---|
| **E1 — Automated** | Unit, integration and system tests | LocalDB database (`AMIGOALERT_TEST_SQL`), migrated and seeded by the fixture; mock SMS; no push. |
| **E2 — Local acceptance** | UAT-01 to UAT-16 | API (`https://<pc-ip>:7012`), Web (`https://localhost:7229`) and WebJob on the developer PC; dedicated test database; `Twilio__UseMockSms=true`; Notification Hub credentials set (see `Documentation/PushNotifications.md` §3.2, §4). Android phone with `ApiSettings.UseLocalApi = true`. |
| **E3 — Deployed smoke** | M-03, M-04 | The Azure App Services, read-only checks only. No test data is written to the production database. |

Run the automated levels with:

```powershell
$env:AMIGOALERT_TEST_SQL = "Server=(localdb)\MSSQLLocalDB;Database=AmigoAlertSystemTest;Trusted_Connection=True;TrustServerCertificate=True"
dotnet test AmigoAlert.Tests --filter "Level=System"                          # system level
dotnet test AmigoAlert.Tests --filter "Category!=LiveSms&Category!=TwilioRejection"   # everything that sends no real SMS
```

The WebJob is never pointed at the production database from a test. `NotificationLatencyTests` already refuses to run against `AmigoAlertDB`.

## 4. Entry and exit criteria

**Entry (system):**
- The solution builds.
- The integration suite (I-01 to I-17) passes on E1.

**Entry (acceptance):**
- All automated system tests pass, or fail only on a logged defect.
- E2 is reachable from the phone.
- A push notification has arrived end to end once.

**Exit:**
- Every High-priority user story has at least one passing system or acceptance test.
- Every Medium/Low story passes, or has a logged defect with an agreed resolution (fix, SRS amendment, or accepted).
- NF-10 to NF-13 pass, or have an accepted deviation.
- No open Critical defects.
- The traceability matrix has no requirement without a test.
- The report is signed off.

## 5. Roles

| Role | Person | Responsibility |
|---|---|---|
| Test lead / developer | James Scarberry | Writes and runs system tests, fixes defects, maintains these documents |
| Acceptance tester (user) | A volunteer from the target group, or the developer acting as them | Executes UAT scripts as the user |
| Acceptance tester (contact) | A second person with their own phone | Receives SMS, uses the consent page |
| Acceptance sign-off | Capstone instructor / project sponsor | Reviews the report and accepts or rejects |

## 6. Defect management

Defects are logged in [TestReport.md §4](TestReport.md#4-defects) as D-nn, each with the requirement, steps and evidence. Severity:

| Severity | Meaning |
|---|---|
| Critical | A safety function fails: an emergency or missed check-in does not reach a consenting contact, or a non-consenting person is messaged. |
| High | A High-priority requirement is not met, or a contact or user is misinformed. |
| Medium | A requirement is partly met, or a workaround exists. |
| Low | Wording, cosmetic, or a deviation better resolved by amending the SRS. |

A failing automated test caused by an open defect is kept, marked `Skip = "D-nn: ..."`, so the suite stays green and the defect stays visible in every run. The skip is removed when the defect is fixed.

## 7. Risks

| Risk | Mitigation |
|---|---|
| The API has no authentication (`Documentation/PushNotifications.md` §6) | Out of SRS scope. Recorded as a known risk; acceptance runs only on a private network. |
| Live SMS costs money and reaches real people | Mock SMS everywhere except the opt-in NF-03 phases (already done, capped at 12 sends). In UAT the contact reads messages from the API log or the `Sms` table instead of their phone. |
| Push delivery depends on a physical phone, Firebase and the Notification Hub | UAT scripts allow for delay. NF-04 already measured delivery. |
| Check-in timing makes UAT slow (10 + 10 minutes) | Schedule the user's check-in 2–3 minutes ahead. The WebJob runs every minute. |
| The SRS's message texts predate the 160-character limit (NF-13) | Treated as SRS-amendment candidates (D-02), not code defects, unless the sponsor decides otherwise. |
