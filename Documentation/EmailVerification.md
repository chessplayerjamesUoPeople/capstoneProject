# AmigoAlert: Email log in and account recovery

The app identifies a user by the id the API returns at registration. It keeps that id in the phone's secure storage. Reinstalling the
app, moving to a new phone, or clearing the app's data loses it. The user gets it back by logging in with their email address and a
one-time code emailed to them. The same kind of code confirms the address at registration.

## 1. How it works

**First launch.** When no user is stored, Home sends the user to the Welcome screen, which offers **Log in** and **Register**.

**Register:**
1. The user fills in the registration form and accepts the terms.
2. The app calls `POST api/verification-codes` with purpose `Registration`, and a code is emailed.
3. On the **Check your email** screen, the code is sent with the details to `POST api/users`.
4. The emergency contact is added as before.
5. An address that is already registered gets a 409, and the app offers to log in instead.

**Log in:**
1. The user enters their email address.
2. The app calls `POST api/verification-codes` with purpose `Login`, and a code is emailed.
3. The code goes to `POST api/users/login`, which returns the user.
4. The app stores the id, registers the device for check-in notifications, and opens Home. Home picks up the account where it left off,
   including the emergency contact and its consent.

**Codes:**

| Rule | Value |
|---|---|
| Length | 6 random digits |
| Valid for | 10 minutes |
| Which code works | Only the newest code for an address and purpose |
| Wrong guesses allowed | 5. After that the code stops working and the user requests a new one. |
| Request limit | One per 60 seconds and 5 per hour per address and purpose. Over the limit the API returns 429 with `Retry-After`. |
| Storage | Only a salted SHA-256 hash is stored, in `EmailVerification` |
| Cleanup | The WebJob deletes rows 24 hours after they expire |

**Not a session.** Logging in returns the user's id, exactly as registration does. The API is still unauthenticated. See section 6 of
[PushNotifications.md](PushNotifications.md).

## 2. Setting up real email (Azure Communication Services)
The API signs in to Communication Services the same way it signs in to Key Vault: with `DefaultAzureCredential`.
- **In Azure:** it uses the App Service's managed identity.
- **On your PC:** it uses your Visual Studio or `az login` account.

So there is no key or connection string to store. You need two Azure resources, two role assignments and two settings.

### 2.1 Create the email domain
1. In the Azure portal, create an **Email Communication Services** resource:
   - Use the same resource group as the API, for example.
   - **Data location** must match the Communication Services resource in 2.2. Pick **United States** for both.
2. Open the new resource, go to **Provision domains**, and select **Add domain** then **Azure managed domain**.
   - It is free and needs no DNS setup.
   - After a minute or two its status is *Verified*.
3. Open the domain and note the **MailFrom address**, for example `DoNotReply@0a1b2c3d-….azurecomm.net`. This is the
   **sender address**.

An Azure managed domain sends a limited volume, which is plenty for testing. Mail from it may land in spam at first. Your own domain
(**Add domain > Custom domain**, verified with DNS records) delivers better and is the better choice for production.

### 2.2 Create the Communication Services resource and connect the domain
1. Create a **Communication Services** resource with the same **Data location** as in 2.1.
2. Open it, go to **Email > Domains**, select **Connect domain**, and pick the domain from 2.1.
3. On its **Overview** page, note the **Endpoint**, for example `https://amigoalert-acs.unitedstates.communication.azure.com/`.

### 2.3 Allow the API to send
On the **Communication Services** resource (not the Email Communication Services one), open **Access control (IAM)** and select
**Add > Add role assignment**.

1. **For the deployed API:**
   - **Role:** **Communication and Email Service Owner**. **Contributor** also works.
   - **Members:** choose *Managed identity*, then *App Service*, then the API's App Service.
   - If the App Service isn't listed, turn on its identity first under **App Service > Identity > System assigned > On**. Key Vault
     already uses this identity, so it is probably on.
2. **For running the API from Visual Studio on your PC:** add the same role for your own user account, the one you are signed in with
   in Visual Studio or `az login`. If your account is the subscription **Owner** it can already send, so you can skip this.

Assign the role directly to the identity, not through an Entra group. Group assignments are reported not to apply to managed identities.
New role assignments can take a few minutes to take effect.

### 2.4 Configure the API
The endpoint and sender address are not secrets, so they can live in `AmigoAlert_API/appsettings.json`:

```json
"Email": {
  "Endpoint": "https://amigoalert-acs.unitedstates.communication.azure.com/",
  "SenderAddress": "DoNotReply@0a1b2c3d-….azurecomm.net"
},
```

Alternatively, set them in the App Service under **Settings > Environment variables** as `Email__Endpoint` and
`Email__SenderAddress`. Those override the JSON for the deployed API only.

### 2.5 Check it
1. Run the API.
2. In the app, choose **Log in** or **Register** and enter an address you can read.
3. The code should arrive within a minute. Check the spam folder the first time.

If something is wrong, the app shows "The email could not be sent", the API returns 502, and the API log says why:
- `Email:Endpoint and Email:SenderAddress must both be configured.`: a setting from 2.4 is missing.
- An authentication error, or a 401 or 403 from Communication Services: the role from 2.3 is missing or hasn't taken effect yet. On your
  PC, it can also mean Visual Studio or `az login` is signed in to a different account.
- An error about the sender or the domain: the domain isn't connected (2.2), or the sender address doesn't match it.

## 3. Mock email (no Azure needed)
Setting `Email:UseMockEmail` to true swaps in a mock sender. It writes each code to the API console, for example
`Mock email mock-… to you@example.com: Your AmigoAlert code is 123456`, and sends nothing.

- **Turning it on for one run:** set the environment variable `Email__UseMockEmail=true`, or set it in the API's launch profile.
  `appsettings.Development.json` leaves it off, so local runs send real email.
- **Tests:** the integration tests always use the mock and read codes from `IntegrationApiFactory.SentEmail`. I-20 to I-29 cover the
  codes, log in and cleanup.

## 4. Clearing the app's data
The app stores only two values: the user id in secure storage, and a per-install push id in preferences. Everything else lives on the
server. Clearing the app's data loses those two values, and logging in restores them.

**Debug builds used to break.** Deploying a Debug build from Visual Studio uses *Fast Deployment*, which copies the .NET assemblies
(tens of MB) into the app's data folder instead of the APK. Clearing storage deleted them, and the app could no longer start.

**The fix.** The Android project now sets `EmbedAssembliesIntoApk` for Debug builds, so the assemblies stay in the APK. The cost is a
slower deploy. Release builds always embed them.
