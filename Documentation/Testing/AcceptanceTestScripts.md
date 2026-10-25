# AmigoAlert: Acceptance Test Scripts

Run on environment **E2** ([TestPlan.md §3](TestPlan.md#3-environments)): the Android app on a physical phone, and the API, Web and WebJob running locally against a test database with `Twilio__UseMockSms=true`.

**Testers:**
- **U** acts as the user, on the phone.
- **C** acts as the emergency contact, on a second device's browser.

**Reading SMS:** with mock SMS, C reads each message from the API/WebJob console line `Mock SMS MK… (<type>) to <phone>: <body>`, or with:

```sql
SELECT TOP 5 s.TimeSent, t.Name AS Type, c.Phone, s.TwilioSid
FROM Sms s
JOIN SmsMessageTypes t ON t.Id = s.MessageTypeId
JOIN EmergencyContact c ON c.Id = s.EmergencyContactId
ORDER BY s.Id DESC;
```

C opens the consent link by copying it from the console line. The link points at `Consent:PageBaseUrl`, so for E2 set `Consent__PageBaseUrl=https://<pc-ip>:7229/consent/` on the API.

**Timing:** to shorten check-in scripts, register or update U with a check-in time 2–3 minutes ahead. The WebJob must run every minute. A simple loop works: `while ($true) { dotnet run --project AmigoAlert.WebJob; Start-Sleep 60 }`.

**Recording results:** fill in *Result* (Pass / Fail / Pass with deviation) and *Evidence* (screenshot file name in `Documentation/Testing/evidence/`). Log failures as defects in [TestReport.md](TestReport.md#4-defects).

---

### UAT-01: Register (US-01, US-02, DR-02) — High
- **Given** a freshly installed app.
- **When** U opens it and leaves Address empty, then presses Submit,
- **Then** an error says, in text, that Address is required.
- **When** U fills Name, Email, Address and Landmarks, enters the user phone as `0991234567` and the contact's phone as `+593 99 765 4321`, chooses two check-in times, and presses Submit,
- **Then** the Terms and Conditions page is shown (US-02).
- **When** U accepts the terms,
- **Then** the home screen opens, and the database shows both phones in E.164 (`+593991234567`, `+593997654321`).

| Result | Evidence | Notes |
|---|---|---|
| | | |

### UAT-02: Contact receives the consent request (US-03) — High
- **Given** UAT-01 has completed.
- **Then** one ConsentRequest SMS was sent to the contact's number, containing the user's name and a link.
- **Check:** compare the text with the SRS: "{userName} has added you as their emergency contact. Please access this link to accept: {link}." Record any difference. Known: D-01, D-02.

| Result | Evidence | Notes |
|---|---|---|
| | | |

### UAT-03: Home screen while awaiting consent (US-07) — Medium
- **Given** the contact has not answered.
- **Then** the home screen reads "Awaiting consent of emergency contact {contactName}.", the emergency button is visibly disabled, and an update-information button is available.
- **When** the check-in time passes,
- **Then** no check-in notification arrives.

| Result | Evidence | Notes |
|---|---|---|
| | | |

### UAT-04: Contact opts in (US-04, US-05) — Medium
- **Given** UAT-03.
- **When** C opens the link,
- **Then** the page names U and shows the terms, a checkbox, and Accept and Decline.
- **When** C presses Accept without ticking the checkbox,
- **Then** nothing is recorded and the form remains.
- **When** C ticks the checkbox and presses Accept,
- **Then** "Thank you. Your consent has been recorded." is shown.
- **When** U reopens the app,
- **Then** the home screen shows the next check-in and an enabled emergency button.
- **When** C reopens the same link,
- **Then** it shows that C is receiving notifications about U, with a "Withdraw consent" button and no Accept button.
- **When** C presses Withdraw consent,
- **Then** "Your consent has been withdrawn. You will no longer receive notifications." is shown.
- **When** U reopens the app,
- **Then** U is taken to the registration form with a notice that C withdrew consent; the emergency button is not available.
- **When** C opens the same link again, ticks the checkbox and presses Accept,
- **Then** "Thank you. Your consent has been recorded." is shown and U's app is unlocked again.

| Result | Evidence | Notes |
|---|---|---|
| | | |

### UAT-05: Contact opts out, user re-enters a contact (US-06, US-08, US-09) — Low
- **Given** a second registration (reinstall the app or use a second phone), with contact number N1.
- **When** C opens the link and presses Decline,
- **Then** "Your response has been recorded. You will not receive notifications." is shown.
- **When** U reopens the app,
- **Then** U is taken to the registration form to enter a new contact (US-08).
- **When** U re-enters N1,
- **Then** a text error says the contact declined, and nothing is sent (US-09).
- **When** U enters a new number N2,
- **Then** a consent request goes to N2.
- **Extra check (D-07):** after N2 consents, U changes the contact back to N1. **Expected per SRS:** refused. Record what happens and whether a consent SMS goes to N1.

| Result | Evidence | Notes |
|---|---|---|
| | | |

### UAT-06: Home screen when all is well (US-10, US-11) — High / Medium
- **Given** a consenting contact and status Okay.
- **When** U opens the app,
- **Then** the home screen shows "Next check-in at {time}" matching the nearer of U's two times, the emergency button, and the update-information button.

| Result | Evidence | Notes |
|---|---|---|
| | | |

### UAT-07: Check-in notification and "I'm okay" from the notification (US-15, US-16) — High
- **Given** U's check-in is set 2 minutes ahead, and the app is closed.
- **When** the time arrives,
- **Then** a notification arrives with "I'm okay" and "Emergency" actions.
- **When** U taps "I'm okay",
- **Then** the notification is replaced by a confirmation, the home screen shows Okay with the next check-in, no reminder arrives 10 minutes later, and no SMS is sent.
- **Check:** compare the SRS times (8:00 and 18:00) with the user-chosen times (D-04).

| Result | Evidence | Notes |
|---|---|---|
| | | |

### UAT-08: Pending check-in on the home screen (US-12) — High
- **Given** a check-in notification has arrived and is unanswered.
- **When** U opens the app instead of using the notification,
- **Then** it shows "Awaiting check-in, please indicate your status" and a green "I'm Okay" button.
- **When** U presses it,
- **Then** the home screen returns to Okay.

| Result | Evidence | Notes |
|---|---|---|
| | | |

### UAT-09: Reminder after a missed notification (US-19) — High
- **Given** a check-in notification is ignored.
- **Then** a second notification with the same actions arrives about 10 minutes later.

| Result | Evidence | Notes |
|---|---|---|
| | | |

### UAT-10: Escalation after two missed notifications (US-20, US-13) — High
- **Given** UAT-09's reminder is also ignored, with location permission granted.
- **Then** about 10 minutes after the reminder, one MissedCheckInEscalation SMS goes to the contact with U's name and a Google Maps link near the phone's position.
- **When** the test is repeated with location permission denied,
- **Then** the SMS says U may be having an emergency, without a link.
- **When** U opens the app,
- **Then** it shows "Previous check-in failed! {contactName} has been contacted. Click below to cancel the escalation", an orange "Cancel emergency, I am okay" button, and no emergency button.
- **Check:** compare the SMS text with the SRS (D-02).

| Result | Evidence | Notes |
|---|---|---|
| | | |

### UAT-11: Emergency from the home screen (US-17, US-18, US-14) — High
- **Given** status Okay.
- **When** U presses the emergency button,
- **Then** within a minute an EmergencyActivation SMS goes to the contact with U's name and a map link, or the registered address if location is off, and the home screen shows "You have pressed the emergency button! {contactName} has been contacted…" with the orange cancel button and no emergency or update button.

| Result | Evidence | Notes |
|---|---|---|
| | | |

### UAT-12: Emergency from the notification (US-17) — High
- **Given** a check-in notification has arrived.
- **When** U taps "Emergency" on it, without opening the app,
- **Then** the outcome is the same as UAT-11.

| Result | Evidence | Notes |
|---|---|---|
| | | |

### UAT-13: Cancel an emergency (US-21, US-22) — Medium / Low
- **Given** UAT-10 or UAT-11 has left U in Escalated or Emergency.
- **When** U presses "Cancel emergency, I am okay",
- **Then** the home screen returns to Okay, and the contact receives "{userName} has indicated that they are no longer in an emergency status."

| Result | Evidence | Notes |
|---|---|---|
| | | |

### UAT-14: Update information (US-23) — Medium
- **Given** status Okay with a consenting contact.
- **When** U changes their own address and the contact's name,
- **Then** the changes are saved and the contact stays consented, with no SMS.
- **When** U changes the contact's number,
- **Then** a new consent request goes to the new number and the home screen returns to "Awaiting consent…" (US-07).
- **When** U is in Emergency,
- **Then** the update button is hidden. Check D-06: the API itself still accepts updates.

| Result | Evidence | Notes |
|---|---|---|
| | | |

### UAT-15: Error and offline behaviour — supporting
- **Given** the API is stopped.
- **When** U presses the emergency button,
- **Then** a text error with a "Try again" button is shown and nothing is falsely reported as sent.
- **When** the API is restarted and U presses Try again,
- **Then** the emergency is sent.

| Result | Evidence | Notes |
|---|---|---|
| | | |

### UAT-16: Consent link expiry — supporting (US-04)
Expiry applies only to a request that has not been answered; once C has answered, the link stays valid so C can withdraw.
- **Given** a Pending contact whose `ConsentTokenExpires` has been set in the past in the test database.
- **When** C opens the link,
- **Then** "This consent link is invalid or has expired." is shown with no buttons.

| Result | Evidence | Notes |
|---|---|---|
| | | |

---

## Sign-off

| Role | Name | Date | Decision (Accept / Accept with deviations / Reject) |
|---|---|---|---|
| Acceptance tester (user) | | | |
| Acceptance tester (contact) | | | |
| Sponsor / instructor | | | |
