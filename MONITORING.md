# Logs and monitoring

Every backend service writes the same kind of logs, set up in one place: [src/LandaDoc.ServiceDefaults/LandaDocLogging.cs](src/LandaDoc.ServiceDefaults/LandaDocLogging.cs). This guide covers what gets logged, how to send the logs to [Better Stack](https://betterstack.com) so you can search all services in one place, and how to get alerted when a service is down.

## Where logs go

| Destination | When | Notes |
|---|---|---|
| Console | Always | Render shows a short recent history per service, under the service → **Logs**. |
| Better Stack | When `BetterStack__SourceToken` is set | All 9 services in one searchable place. Set up below. |
| Seq | When `Seq__Url` is set | For local development, e.g. `Seq__Url=http://localhost:5341`. |

Every line carries a `Service` field (`Identity`, `Payment`, …), so you can read all services side by side or filter to one.

## What gets logged

**Every HTTP request**: one line with method, path, result code and duration, plus the user's id when they're signed in. For example: `HTTP POST /api/insurance-claims/…/approve responded 200 in 41 ms`. Health checks from uptime monitors are left out, and so are database queries.

**Business events**, one line each, with the ids needed to look things up:

| Area | Events |
|---|---|
| Accounts (Identity) | registered; logged in; login refused (with reason and client IP); password changed; account details edited (by the user or by an admin); too many attempts (rate limit hit, with client IP); logged out after inactivity, or from the inactivity prompt |
| Doctors and clinics (Admin) | doctor profile created; edited (by the doctor or an admin, with the names of the fields that changed); approved; suspended; clinic created or updated |
| Bookings (Appointment) | booked (or refused, with the reason); rescheduled; completed; confirmed after payment (and how it was paid); cancelled because payment failed; held during an insurance review; payment deadline after a declined claim; expired unpaid |
| Payments (Payment) | payment opened for a booking; **no fee known for a doctor** (a warning: the patient can't pay); card checkout opened; mobile money prompt sent; card and mobile money payments succeeded or failed; webhooks with a bad signature (warning) |
| Insurance (Payment) | insurer added or edited; claim filed; approved; declined; marked paid or rejected by the insurer |

**Deliberately not logged:** patient names, emails typed at login, phone numbers, insurance member numbers, reasons for visits, doctors' notes or reasons, and bios. Logs are kept by a third party and seen by more people than the database is, so they hold ids only. Look the details up in the apps from the ids.

## Connect Better Stack

1. Create an account at [betterstack.com](https://betterstack.com), then go to **Telemetry → Sources → Connect source**.
2. Name it `LandaDoc`, and pick **.NET** as the platform. Create the source.
3. On the source's page, note the **Source token** and the **Ingesting host** (something like `s1234567.eu-nbg-2.betterstackdata.com`).
4. In Render, open **Environment Groups → landadoc-shared** and add:

   | Key | Value |
   |---|---|
   | `BetterStack__SourceToken` | the source token |
   | `BetterStack__Endpoint` | the ingesting host (with or without `https://`) |

5. Redeploy every service (open each one → **Manual Deploy → Deploy latest commit**).
6. In Better Stack, open the source's **Live tail**. Within a minute you should see each service's start-up lines, like `Now listening on…` with `Service` set to each service's name.

Without these two keys nothing is sent and nothing breaks; logs simply stay in Render's console.

**Free plan limits:** Better Stack's free plan keeps logs for a short time (a few days) and caps the monthly volume. Check the current limits on their pricing page. If you need weeks of history for support, upgrade or choose a log service with longer free retention. Only `LandaDocLogging.cs` would need to change.

## Uptime alerts

Render's free services sleep after 15 minutes idle and occasionally fail to wake up. An uptime monitor catches that before patients do.

1. In Better Stack, go to **Uptime → Monitors → Create monitor**.
2. Choose **URL becomes unavailable** and enter the first address below. Pick how you want to be alerted (email, SMS, app).
3. Repeat for each service:

   ```
   https://landadoc-identity.onrender.com/health
   https://landadoc-appointment.onrender.com/health
   https://landadoc-availability.onrender.com/health
   https://landadoc-payment.onrender.com/health
   https://landadoc-notification.onrender.com/health
   https://landadoc-admin.onrender.com/health
   https://landadoc-search.onrender.com/health
   https://landadoc-document.onrender.com/health
   https://landadoc-review.onrender.com/health
   ```

   Use your actual service addresses if Render gave any of them a suffix.

A check every few minutes also keeps the services from sleeping. On Render's free plan, that uses up the shared 750 instance hours a month faster, so check interval and plan together.

## Finding things

Search by the id you have; most events mention several ids, so you can follow a story across services.

| You want to know | Search for |
|---|---|
| Everything that happened to one booking | the appointment id, e.g. `dfa1fb47-ffaa-4c45-8c2b-42903a5d940c` |
| What one person did | their user id (shown in the Admin app, or in their `logged in` line) |
| Why a patient couldn't pay | the appointment id; look for `No fee known for doctor` or a failed payment line |
| Where an insurance claim stands | the claim id, or `Insurance claim` with the appointment id |
| Who changed a doctor's profile | the doctor profile id; look for `edited by` |
| Someone guessing passwords | `Too many attempts`; the client IP is in the line |
| A single service | filter on `Service` = `Payment` (or another) |
| Errors only | filter on level `Error`; requests that failed with a 5xx status are logged as errors |
