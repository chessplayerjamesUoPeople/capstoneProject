# AmigoAlert: System Test Cases

Automated cases live in `AmigoAlert.Tests/SystemTests/` and run with `dotnet test AmigoAlert.Tests --filter "Level=System"` on environment E1 (see [TestPlan.md](TestPlan.md#3-environments)).

Every automated case shares these preconditions:
- `IntegrationApiFactory.ResetAsync()` has migrated the database and re-seeded it.
- The case registers its own users in `@integration.test` and removes them afterwards.
- SMS is mocked and recorded.
- **T** is the user's first check-in time: a whole minute, 30 minutes before the test starts. The second check-in is T + 12 h.

## Automated: journeys (`SystemJourneyTests.cs`)

| ID | Requirements | Steps | Expected result |
|---|---|---|---|
| S-01 | US-01, US-03, US-04, US-05, US-07, US-15, DR-02 | 1. POST a user with the phone in local `09…` format. 2. POST the user in E.164. 3. GET the contact. 4. POST a contact (number normalised from `099 000 7777`). 5. GET `api/consent/{token}` from the SMS link. 6. Run the WebJob at T; press emergency. 7. PUT consent = granted; repeat the PUT. 8. Run the WebJob at T. | 1. 400. 2. 201, status Okay. 3. 404. 4. One consent SMS to the contact, ending in the token. 5. Names the user and contact, Pending, can respond. 6. No check-in; emergency 409. 7. 204, then Granted; the second PUT returns 404. 8. One check-in for T; status Pending1. Every SMS fits one segment. |
| S-02 | US-06, US-08, US-09 | 1. Register a user and add a contact. 2. The contact declines. 3. Run the WebJob at T; press emergency. 4. Submit the same number again under a new name. 5. Replace the contact with a new number, which accepts. 6. Run the WebJob at T. | 2. Rejected. 3. No check-in; 409. 4. 204; still Rejected; no SMS sent. 5. Pending, then Granted. 6. One check-in; the declined number was never messaged after declining. |
| S-03 | US-07 | 1. Register a user; add a contact who never answers. 2. Run the WebJob at T, T + 10 and T + 20. 3. Press emergency. | No check-ins, status Okay, emergency 409, no SMS. |
| S-04 | US-10, US-12, US-15, US-16, NF-11 | 1. Set up a consenting user. 2. Run the WebJob at T. 3. Device PUTs its location. 4. PUT response Okay; repeat it. 5. Run the WebJob at T + 10 and T + 20. | 2. One check-in; status Pending1. 3. Location row exists. 4. 204, status Okay, location cleared; the repeat returns 409. 5. No reminder, no SMS; the check-in shows Okay. |
| S-05 | US-13, US-19, US-20, US-21, US-22 | 1. Set up a consenting user. 2. Run the WebJob at T; device PUTs its location. 3. Run the WebJob at T + 9. 4. Run it at T + 10. 5. Run it at T + 20 and T + 21. 6. Cancel the emergency; cancel again. | 3. Still one check-in. 4. Two check-ins, status Pending2, no SMS. 5. Status Escalated; exactly one SMS to the contact, with the user's name and a Google Maps link to the reported location. 6. 204, status Okay, stand-down SMS ending "…has indicated that they are no longer in an emergency status."; the second cancel returns 409. |
| S-05b | US-20 | The same as S-05 without a location. | A single alert, "…may be having an emergency…", with no map link. The SMS type is MissedCheckInEscalation. |
| S-06 | US-14, US-17, US-18, US-21, US-22 | 1. Set up a consenting user. 2. Press emergency with lat/lon. 3. Cancel. 4. Press emergency without a location. | 2. 204, status Emergency; one SMS with the name, "NOT okay" and a map link; type EmergencyActivation. 3. Stand-down as in S-05. 4. The SMS carries the registered address. |
| S-07 | US-23, NF-11 (consent retention) | 1. Set up a consenting user. 2. PUT the user's details; PUT the contact with a new name only. 3. PUT the contact with a new number. 4. Press emergency. 5. The new contact accepts; press emergency. | 2. Both persisted; still Granted; no SMS. 3. Pending; one consent SMS to the new number; the old contact is inactive and keeps Granted. 4. 409. 5. 204; the alert goes to the new number. |
| S-08 | NF-11 | 1. Insert location logs at −91 and −89 days. 2. Run the WebJob. | The −91-day location is removed; the −89-day one is kept. |
| S-09 | NF-13 | 1. Register a user with a 50-char name and a 200-char address (the API maxima). 2. Run emergency without and with a location, each cancelled. 3. Let a check-in escalate. | 6 SMS in total, each ≤160 chars of printable ASCII, each with a mock (`MK…`) SID. |
| S-10 | NF-12 (integrity of the callback) | POST `api/sms/status` with no `X-Twilio-Signature`. | 403. |
| S-11 | US-03 | Add a contact using the deployed `Consent:PageBaseUrl`. | The consent SMS contains the user's full name. **Skipped: D-01.** |

## Automated: consent page (`ConsentPageTests.cs`)

These run against the real `AmigoAlert.Web` host and post the page's own forms with their antiforgery token and cookie, as a browser does. The contact is created through the API, so each link comes from a consent SMS.

| ID | Requirements | Steps | Expected result |
|---|---|---|---|
| C-01 | US-04 | Open `/consent/{token}`. | Shows "**{userName}** has asked you to be their emergency contact", the terms checkbox, and Accept and Decline. |
| C-02 | US-04 | Submit Accept without ticking the checkbox. | The form is shown again; the contact is still Pending. |
| C-03 | US-05 | Tick the checkbox, then submit Accept. Reopen the link. | "Thank you. Your consent has been recorded."; Granted; the reopened link shows "invalid or has expired". |
| C-04 | US-06 | Submit Decline. | "Your response has been recorded. You will not receive notifications."; Rejected. |
| C-05 | US-04 | Open a random token; open a token whose 72 h expiry has passed. | Both show "This consent link is invalid or has expired." with no buttons; the contact is still Pending. |

## Manual system checks

| ID | Requirement | Procedure | Expected result |
|---|---|---|---|
| M-01 | NF-10 | 1. In SSMS (or `sqlcmd`), connected as a database user without column master key access, run `SELECT TOP 5 Name, Email, Phone, Address, Landmarks FROM [User]` and `SELECT TOP 5 Name, Phone FROM EmergencyContact` on the Azure test database. 2. In the portal, check that Transparent Data Encryption is on for `AmigoAlertDB`. | Per the SRS, the PII columns are illegible (ciphertext). TDE alone protects the files, not query results, so plaintext here is a **fail** against the SRS's verification (see D-03). |
| M-02 | NF-12 (secrets) | Run `gitleaks detect --source . --log-opts="--all"`, or `git grep -nIE "(AC[0-9a-f]{32}\|SK[0-9a-f]{32}\|SharedAccessKey=\|AccountKey=\|Password=)" $(git rev-list --all)`, over the whole history. Check every `appsettings*.json` and `launchSettings.json`. | No credentials in source or history. Twilio values arrive only from Key Vault (`KeyVaultConfiguration.cs`); the Notification Hub connection string only from environment/App Service settings. |
| M-03 | NF-12 (TLS) | 1. `curl -I --tlsv1.2 --tls-max 1.2 https://<api-host>/api/users/<guid>` and the same for the Web host. 2. `curl -I --tls-max 1.1 https://<api-host>/`. 3. `curl -I http://<api-host>/`. 4. Check that `ApiSettings` in the mobile app uses `https://` only. | 1. Succeeds (TLS 1.2+). 2. Fails the handshake. 3. Redirects to HTTPS or is refused. 4. The App Service minimum TLS version is set to 1.2. |
| M-04 | US-03, US-04 (deployment) | On E3, open a consent link for a Pending test contact (created on E2 against the test DB, never production) through the deployed Web App, and check the Web App's configuration. | The page renders. The Web App has `Consent`/`Twilio` configuration or does not need it; see D-05. |
