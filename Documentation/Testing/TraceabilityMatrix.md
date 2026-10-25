# AmigoAlert: Requirements Traceability Matrix

Each row maps one SRS requirement (`Requirements.docx`) to the tests that verify it.

**Test ID prefixes:**
- **U-** unit (`AmigoAlert.Tests/Unit`)
- **I-** integration (`AmigoAlert.Tests/Integration`)
- **S-/C-** automated system (`AmigoAlert.Tests/SystemTests`)
- **M-** manual system check ([SystemTestCases.md](SystemTestCases.md))
- **UAT-** acceptance script ([AcceptanceTestScripts.md](AcceptanceTestScripts.md))

**Status:**
- **Pass**: automated and passing on E1, 2026-10-15.
- **Pending**: manual or acceptance test not yet run.
- **Deviation**: works, but differs from the SRS wording.
- **Fail**: does not meet the SRS.
- **Prev.**: verified in an earlier test cycle.

| Req | Priority | Summary | System tests | Acceptance | Other levels | Status |
|---|---|---|---|---|---|---|
| US-01 | High | Register | S-01 | UAT-01 | I-01–I-04 | Pass (system); UAT pending |
| US-02 | Low | Accept terms | — (client-only) | UAT-01 | I-01 (terms version stored) | UAT pending |
| US-03 | High | Consent request SMS | S-01, S-11 | UAT-02 | I-01 | **Fail: D-01**; Deviation: D-02 |
| US-04 | Medium | Opt-in/opt-out link | S-01, C-01, C-02, C-05, C-06, C-07, C-08, M-04 | UAT-04, UAT-16 | I-18, I-19 | Pass (system); UAT pending |
| US-05 | Medium | Consent given | S-01, C-03 | UAT-04 | I-13 | Pass (system); UAT pending |
| US-06 | Low | Consent rejected | S-02, C-04 | UAT-05 | — | Pass (system); UAT pending |
| US-07 | Medium | Locked until consent | S-01, S-03, S-07 | UAT-03 | I-12, I-13 | Pass (system); UAT pending |
| US-08 | Low | Back to registration on rejection | S-02 (server state) | UAT-05 | — | Pass (system); UAT pending |
| US-09 | Low | Cannot re-add a declined contact | S-02 | UAT-05 | — | Pass for the current contact; **open: D-07** |
| US-10 | High | Home screen | S-04 (status source) | UAT-06 | I-02 | UAT pending |
| US-11 | Medium | Time to next check-in | — (client-only) | UAT-06 | — | UAT pending |
| US-12 | High | Pending check-in view | S-04 | UAT-08 | I-07, I-08 | Pass (system); UAT pending |
| US-13 | High | Escalated view | S-05 | UAT-10 | — | Pass (system); UAT pending |
| US-14 | High | Emergency view | S-06 | UAT-11 | I-11 | Pass (system); UAT pending |
| US-15 | High | Check-in notification at 8:00 / 18:00 | S-01, S-04 | UAT-07 | I-14, I-15 | Pass (system); Deviation: D-04 |
| US-16 | High | "I'm okay" stands down | S-04 | UAT-07, UAT-08 | I-07, I-08 | Pass (system); UAT pending |
| US-17 | High | Emergency button (app or notification) | S-06 | UAT-11, UAT-12 | I-11 | Pass (system); UAT pending |
| US-18 | High | Emergency SMS to contact | S-06 | UAT-11 | I-11 | Pass (system); Deviation: D-02 |
| US-19 | High | Reminder after 10 min | S-05 | UAT-09 | — | Pass (system); UAT pending |
| US-20 | High | Escalation SMS after missed reminder | S-05, S-05b | UAT-10 | I-14 | Pass (system); Deviation: D-02 |
| US-21 | Medium | Cancel emergency (user) | S-05, S-06 | UAT-13 | — | Pass (system); UAT pending |
| US-22 | Low | Stand-down SMS (contact) | S-05, S-06 | UAT-13 | — | Pass (system, SRS text matches) |
| US-23 | Medium | Update information | S-07 | UAT-14 | I-05, I-06 | Pass (system); **open: D-06** |
| NF-01 | High | Missed check-in accuracy ≥95% | — | — | AccuracyTests | Prev. |
| NF-02 | High | Successful check-in accuracy ≥95% | — | — | AccuracyTests | Prev. |
| NF-03 | High | Escalation SMS ≤60 s | — | — | LatencyTests (L-xx) | Prev. |
| NF-04 | High | ≥90% notifications within 2 min | — | — | NotificationLatencyTests | Prev. |
| NF-05 | Medium | Portrait and landscape | — | — | Accessibility checklist | Prev. |
| NF-06 | Medium | Not colour alone | — | — | Accessibility checklist | Prev. |
| NF-07 | Medium | Contrast 7:1 / 4.5:1 | — | — | Accessibility checklist | Prev. |
| NF-08 | Medium | Text at 200% | — | — | Accessibility checklist | Prev. |
| NF-09 | Medium | Targets ≥48dp | — | — | Accessibility checklist | Prev. |
| NF-10 | High | PII encrypted at rest | M-01 | — | — | **Expected fail: D-03** (to confirm with M-01) |
| NF-11 | Low | Location minimisation, 90-day purge, consent retention | S-04, S-07, S-08 | — | — | Pass |
| NF-12 | High | Secrets in Key Vault; TLS ≥1.2 | S-10, M-02, M-03 | — | — | S-10 pass; M-02 preliminary scan clean; M-03 pending |
| NF-13 | Medium | SMS ≤160 chars | S-01, S-09 | — | U-SmsMessageComposerTests | Pass |
| DR-01 | — | Activity diagram | — | — | Design review | Not a test item |
| DR-02 | — | Phone as 09… or +5939…, stored in E.164 | S-01 | UAT-01 | U-PhoneNumberFormatTests | Pass (system); UAT pending |

**Coverage check:** every US, the in-scope NFRs (NF-10 to NF-13) and DR-02 have at least one system or acceptance test. NF-01 to NF-09 point to their earlier results.
