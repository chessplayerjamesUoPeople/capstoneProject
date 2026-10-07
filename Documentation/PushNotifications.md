# Push notifications: setup and security

This guide sets up the services behind AmigoAlert's check-in notifications: a Firebase project, which delivers to Android devices, and an
Azure Notification Hub, which the backend sends through. It also covers how every credential involved is stored and protected. Follow the
sections in order.

| Value used below | In this guide |
|---|---|
| Android package name | `com.amigoalert.app` (the `package` in `MobileApplication.Droid/AndroidManifest.xml`) |
| Resource group / region | `AmigoAlert_ResourceGroup` / West US (where the App Service `amigoalertapi` runs) |
| Notification Hubs namespace | `amigoalert-ns` (must be globally unique; substitute your own) |
| Hubs | `amigoalert-hub` (production), `amigoalert-hub-dev` (local development) |
| Hub access policy | `AmigoAlertBackend` |
| Key Vault / secret | `amigoalert-kv` (globally unique; substitute) / `NotificationHubConnectionString` |

## How it works

```
 WebJob                                                        Android device
 EscalationService ──push, tag user:{id}──► Notification Hub ──► FCM ──► AmigoAlertFirebaseMessagingService
                                                                          │ posts the notification
                                                                          ▼
                                                    [ I'm okay ]  [ Emergency ]   (CheckInActionReceiver)
                                                          │              │
 API  ◄── PUT /api/checkins/{id}/response ────────────────┘              │
 API  ◄── POST /api/users/{id}/emergency ────────────────────────────────┘

 Registration: app launch / new FCM token ──► PUT /api/users/{id}/devices/{installationId} ──► API creates the hub installation
```

* **When pushes are sent.** The WebJob pushes the first notification when a check-in falls due, and the reminder 10 minutes later if it is
  unanswered. The SMS escalation to the emergency contact is unchanged.
* **Failures.** A push that fails is logged and changes nothing, so escalation never depends on a push arriving.
* **What a push carries.** Identifiers only: the check-in id, whether it is the reminder, and when it expires. There is no name, phone
  number or message text.
* **Message type.** Pushes are FCM *data* messages, so the app draws the notification, with its buttons, whether it is open, in the
  background or closed.
* **Expiry.** A notification removes itself once its check-in would have been escalated.
* **Who receives a push.** The API sets each device's tag, `user:{userId}`; the app cannot choose it. Tags hold only the opaque user id,
  because Notification Hubs does not encrypt tags at rest.
* **The buttons.** Each button calls the API without opening the app.
  * While the call is sent, the buttons are replaced, so they can't be tapped twice.
  * If the call fails, nothing is retried automatically. The notification tells the user to open the app, which shows their current
    status.

---

## 1. Firebase project

### 1.1 Create the project
1. Go to <https://console.firebase.google.com> and choose **Create a project**. Name it `AmigoAlert`.
2. Turn **Google Analytics off**. The app does not use it, and leaving it off avoids collecting usage data from users (LOPDP).

### 1.2 Register the Android app
1. Get the SHA-1 fingerprint of the debug signing key. Visual Studio's JDK includes `keytool`, under `C:\Program Files\Microsoft\jdk-*\bin\`
   if it isn't on the PATH:
   ```powershell
   & keytool -list -v -alias androiddebugkey -storepass android -keypass android `
     -keystore "$env:LOCALAPPDATA\Xamarin\Mono for Android\debug.keystore"
   ```
2. In the Firebase project, select **Add app → Android**.
   * **Package name:** `com.amigoalert.app`. It must match exactly, and it cannot be changed later.
   * **Debug signing certificate SHA-1:** the value from step 1.
3. **Download `google-services.json`** and save it as `MobileApplication/MobileApplication.Droid/google-services.json`.
   * Firebase considers this file safe to commit. What protects it is the key restriction in section 1.5.
   * If you would rather keep it out of the repository, add it to `.gitignore`; the build includes it only when the file is present.
   * Without it the app builds and runs but receives no notifications. logcat (tag `AmigoAlert.Push`) shows `Push registration failed`.
4. Skip the "Add Firebase SDK" steps. The `Xamarin.Firebase.Messaging` package already does that.
5. Before releasing, add the release signing key's SHA-1 under **Project settings → General → Your apps → Add fingerprint**.

### 1.3 Confirm the FCM API is enabled
Open **Project settings → Cloud Messaging** and check that **Firebase Cloud Messaging API (V1)** shows *Enabled*. The legacy API has been
shut down, and nothing here uses it.

### 1.4 Create a dedicated service account for Notification Hubs
Notification Hubs authenticates to FCM with a Google service account key. Firebase's **Generate new private key** button gives the
`firebase-adminsdk` account's key, which can administer the whole project. **Don't use it.** Create an account that can only send
messages:

1. Open <https://console.cloud.google.com> and select the AmigoAlert project.
2. Go to **IAM & Admin → Service Accounts → Create service account**. Name it `amigoalert-notification-hub`, and grant only the
   **Firebase Cloud Messaging API Admin** role. Not Owner, not Editor.
3. Open the account and go to **Keys → Add key → Create new key → JSON**.
   * **The downloaded file is a credential.** Anyone who has it can push to every device of the app.
   * Never save it inside the repository folder.
   * You'll paste it into the hub in section 2.2 and then delete it.

If key creation is blocked by *"iam.disableServiceAccountKeyCreation"*, the Google Cloud organisation enforces a policy; organisations
created since May 2024 do so by default. An organisation admin has to allow key creation for this project. Personal accounts with no
organisation aren't affected.

### 1.5 Restrict the Android API key
1. In the Google Cloud console, go to **APIs & Services → Credentials** and open **Android key (auto created by Firebase)**.
2. Under **Application restrictions**, choose **Android apps** and add `com.amigoalert.app` with the debug SHA-1. Add the release SHA-1 once
   you have it.
3. Under **API restrictions**, choose **Restrict key** and select only:
   * **Firebase Installations API**
   * **FCM Registration API**
4. Save. The change can take a few minutes to apply.
5. Launch the app and check that registration still succeeds (section 4).
   * If logcat shows `PERMISSION_DENIED` or "API key not valid", a fingerprint or API is missing from the restriction.

---

## 2. Azure Notification Hub

### 2.1 Create the namespace and hubs
1. In the Azure portal, go to **Create a resource → Notification Hub**.
   * **Resource group:** `AmigoAlert_ResourceGroup`. **Location:** West US.
   * **Namespace:** `amigoalert-ns`. **Hub:** `amigoalert-hub`.
   * **Pricing tier:**
     * **Free** gives 1 million pushes a month and 500 devices, but has **no SLA** and Microsoft does not recommend it for production.
     * **Basic** costs about USD 10 a month and adds an SLA. Use it once real users depend on AmigoAlert.
2. In the namespace, add a second hub, `amigoalert-hub-dev`, for local development. Development test sends then can't reach real users, and
   a leaked development key exposes no production devices.

### 2.2 Connect each hub to Firebase
1. Open the hub and go to **Settings → Google (FCM v1)**. Copy these fields from the service account JSON file:
   * **Private Key:** the whole `private_key` value, from `-----BEGIN PRIVATE KEY-----` to `-----END PRIVATE KEY-----`.
   * **Client Email:** `client_email`.
   * **Project ID:** `project_id`.
2. Save, and do the same for the other hub.
3. **Then delete the JSON file** and empty the Recycle Bin. The hubs now hold the key, and you can create a new one at any time
   (section 3.3).

### 2.3 Create the backend's access policy
Don't use the hub's `DefaultListenSharedAccessSignature` or `DefaultFullSharedAccessSignature`. The full one can also change the hub and
its Firebase credentials. Instead:

1. In each hub, go to **Settings → Access Policies → Add**.
   * **Name:** `AmigoAlertBackend`.
   * **Permissions:** **Listen** and **Send** only. **Leave Manage unticked.**
   * Listen lets the API register devices, and Send lets the WebJob push check-ins. Nothing needs Manage.
2. Copy the policy's **Primary Connection String** straight to where section 3 says. Don't paste it anywhere else.
3. **No hub connection string ever goes into the mobile app.** The API registers devices, so the app never talks to the hub.

### 2.4 Test delivery
Once a device is registered (section 4), open the hub and go to **Support + troubleshooting → Test Send**.
* **Platform:** Google (FCM v1).
* **Tag expression:** `user:10000000-0000-0000-0000-000000000001`, or the user you are testing with.
* **Payload:** use a real unanswered check-in id from `GET /api/users/{id}/checkins`:
  ```json
  {"message":{"android":{"priority":"high"},"data":{"checkInId":"1","isReminder":"false"}}}
  ```

---

## 3. Securing the connection string

**Rules that apply everywhere:**
* Never put the connection string in `appsettings*.json` (they are committed), in the mobile app, in a log, or in chat, issues or pull
  requests.
* The code reads it from `NotificationHub:ConnectionString` and the hub name from `NotificationHub:HubName`.
* If they are missing, pushes are skipped and an error is logged. Check-ins, escalation and the API keep working.

### 3.1 Production: Key Vault and the App Service's managed identity
App Service reads the secret from Key Vault with its own identity, so the value never appears in app settings, the repository or a
pipeline. Notification Hubs only supports shared access signatures, not managed identities, so a connection string is unavoidable. Key
Vault is where it belongs.

1. **Create the vault.** Go to **Create a resource → Key Vault** and use these settings:
   * **Name:** `amigoalert-kv`. **Resource group:** `AmigoAlert_ResourceGroup`. **Region:** West US. **Tier:** Standard.
   * **Access configuration:** Azure role-based access control.
   * **Soft-delete:** on (the default). **Purge protection:** on, so a deleted secret can be recovered and can't be destroyed early.
2. **Let yourself add secrets.** In the vault, go to **Access control (IAM) → Add role assignment → Key Vault Secrets Officer → your
   account**. In RBAC mode even a subscription Owner needs this.
3. **Store the secret.** Go to **Objects → Secrets → Generate/Import**.
   * **Name:** `NotificationHubConnectionString`.
   * **Value:** the production hub's `AmigoAlertBackend` primary connection string.
4. **Turn on the app's identity.** Open the App Service `amigoalertapi` and go to **Settings → Identity → System assigned → On**. It is
   probably on already, because the SQL connection string uses `Authentication=Active Directory Default`.
5. **Let the app read the secret.** In the vault, go to **Access control (IAM) → Add role assignment → Key Vault Secrets User**, then
   **Managed identity → App Service → amigoalertapi**. This role can read secret values and nothing else. For the narrowest access, assign
   it on the single secret instead of the vault.
6. **Point the app at the secret.** In the App Service, go to **Settings → Environment variables → App settings** and add:

   | Name | Value |
   |---|---|
   | `NotificationHub__HubName` | `amigoalert-hub` |
   | `NotificationHub__ConnectionString` | `@Microsoft.KeyVault(VaultName=amigoalert-kv;SecretName=NotificationHubConnectionString)` |

   * The double underscore stands for `:` and works on both Windows and Linux plans.
   * Leave out the secret version, as above, so a rotated secret is picked up.
   * **Apply.** The app restarts.
7. **Check that it resolved.** Open the setting again. Its source should read **Key vault Reference** with a green tick. A red cross gives
   the reason; it is usually a role assignment that hasn't propagated yet, so wait a few minutes and restart.
8. **Check the WebJob too.** The WebJob runs in the same App Service and gets the same settings. After its next run, check its log
   (**WebJobs → the job → Logs**).
   * If the reference didn't resolve for it, the log shows *"...is an unresolved Key Vault reference..."*. The value itself is never
     logged.
   * The fallback is to read the vault directly from `AmigoAlert.WebJob/Program.cs`: the
     `Azure.Extensions.AspNetCore.Configuration.Secrets` provider with `DefaultAzureCredential`, and a secret named
     `NotificationHub--ConnectionString`.

The Twilio `AuthToken` should move to Key Vault the same way: secret `TwilioAuthToken`, app setting `Twilio__AuthToken`.

### 3.2 Local development: environment variables
Use the **development hub only**; never put the production connection string on a development machine. Set it as user environment
variables, which keeps it outside the repository and out of shell history. Both the API and the WebJob read them.

1. Open Windows **Settings**, search for **Edit environment variables for your account**, and add:
   * `NotificationHub__HubName` = `amigoalert-hub-dev`
   * `NotificationHub__ConnectionString` = the development hub's `AmigoAlertBackend` primary connection string
2. Restart Visual Studio and any terminals so they pick the variables up.
3. To stop using the hub locally, delete the two variables. Pushes are then skipped with a logged error.

### 3.3 Rotating keys
* **Hub connection string** (rotate at least yearly, and immediately if it may have leaked):
  1. In the hub, go to **Access Policies → AmigoAlertBackend** and copy the **Secondary** connection string.
  2. Save it as a new version of the Key Vault secret.
  3. Restart the App Service, or wait up to 24 hours for it to pick up the new version. Confirm pushes still work.
  4. **Regenerate the primary key**, so the old value opens nothing. Next time, rotate the other way round.
  5. If a leak is suspected, regenerate both keys at once and update Key Vault.
* **FCM service account key:**
  1. In Google Cloud, go to **Service Accounts → amigoalert-notification-hub → Keys → Add key**.
  2. Enter the new key in each hub's **Google (FCM v1)** settings.
  3. Delete the old key in Google Cloud, and delete the downloaded file.

---

## 4. Testing end to end locally
1. **Emulator.** Use an Android emulator whose system image includes **Google Play**; plain AOSP images have no FCM. A physical phone works
   too, but Debug builds call the local API at `10.0.2.2`, which only the emulator can reach. On a phone, set `ApiSettings.UseLocalApi` to
   `false` to use the deployed API instead.
2. **Firebase config.** Place `google-services.json` (section 1.2). The package name changed from the template's
   `com.companyname.mobileapplication`, so uninstall the old app from the emulator.
3. **API.** Set the environment variables (section 3.2) and start the API with the `https` profile.
4. **Register.** Launch the Android app and allow notifications when asked.
   * The API log should show `PUT /api/users/…/devices/…` answered with 204.
   * logcat (tag `AmigoAlert.Push`) should show no `Push registration failed`.
5. **Real check-in.** Set the test user's `Notification1Time` a minute or two ahead, in Ecuador time (UTC-5). Once it has passed, run the
   WebJob.
   * Set `DOTNET_ENVIRONMENT=Development` first, so it uses the mock SMS service like the API does.
   * The notification appears at the top of the screen.
   * A run 10 minutes later sends the reminder, which replaces it.
6. **Try each case:**
   * **I'm okay:** the notification says the check-in was recorded, and the app shows Okay.
   * **Emergency:** the app shows Emergency, and the mock SMS appears in the log.
   * **App states:** the notification appears with the app open, in the background, and after swiping the app away. A *force-stopped* app
     receives nothing until it is opened again; that is Android's rule.
   * **Offline:** with airplane mode on, a tap shows "Could not reach AmigoAlert…".

## 5. Deployment checklist
- [ ] Production hub and Key Vault set up as in section 3.1, and the reference status is green.
- [ ] Release signing key SHA-1 added in Firebase (section 1.2) and to the API key restriction (section 1.5).
- [ ] The WebJob is scheduled to run about every minute, with **Always On** enabled on the App Service (App Service requires it for
      cron-triggered WebJobs). Pushes are only as timely as the schedule.
- [ ] After the first scheduled run, the WebJob log shows no Notification Hubs errors.

## 6. Known risks and limitations
* **The API has no authentication.** This is the most serious open risk.
  * Anyone who knows a user's id can register their own phone to receive that user's check-ins.
  * Because check-in ids are sequential, anyone can answer any check-in as "okay".
  * Device tags are set by the server, which is as far as protection goes without authentication.
  * The fix is user authentication, for example Entra External ID with JWT bearer tokens, plus ownership checks on the `api/users/{id}` and
    `api/checkins/{id}` routes.
* **A late "I'm okay".** `RecordCheckInResponseAsync` sets the status to Okay even after the contact has been alerted, without sending
  the stand-down SMS.
  * The notification removes itself before escalation, which makes this unlikely from a notification.
  * A server-side check (refuse once the status is Escalated or Emergency) would close it.
* **Repeated emergencies.** `ActivateEmergencyAsync` doesn't check whether an emergency is already active, so each call texts the contact.
  The notification removes its buttons on the first tap and never retries by itself, but the endpoint itself has no protection.
* **Free tier.** No SLA. Move to Basic before relying on it.
* **Firebase token deprecation.** Firebase Messaging 25.1 deprecated `getToken` and `onNewToken` in favour of Firebase installation IDs.
  Notification Hubs still addresses FCM v1 devices by registration token, so the app keeps using tokens. Revisit when Notification Hubs
  supports installation IDs, or if Firebase announces a removal date.
* **Notifications switched off.** If the user denies the permission, check-ins arrive silently; a missed one is still escalated.
* **Location is best effort.** The emergency button, the notification's Emergency action and a missed check-in all include a map link
  when the phone can supply a location, and fall back to the registered address (or the plain missed-check-in wording) when it can't.
  * The app asks for location on first launch and needs **Allow all the time** (`ACCESS_BACKGROUND_LOCATION`) for the notification action
    and the check-in capture to work while the app is closed. Without it only the in-app button gets a fix.
  * The Emergency button and action wait at most 3 seconds for a fix, then send without one. A last-known fix older than 10 minutes is not used.
  * When a check-in or reminder push arrives, the phone uploads its location to `PUT api/checkins/{id}/location`. The API stores it as a
    status log row under the user's pending status. An okay answer deletes those rows; otherwise they stay until the 90-day location
    purge. If the check-in is escalated, the newest fix goes into the SMS with its time, and into the escalation's status log.
  * Like the other check-in routes, the location route is unauthenticated, so see the first risk above.
* **Best-effort delivery.** FCM and the hub can delay or drop a push, which is why escalation never depends on one.
