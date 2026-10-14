# Health Connect diagnostics and planned workouts — 0.4.2

## Findings and changes

The reported permissions and Pixel 10 Pro XL active-calorie entries do not establish that Mi Fitness exported an Exercise session. No screenshot file was available in this workspace; the source attribution is based on the phone findings supplied by the user. FitAdvisor reads only `ExerciseSessionRecord` using Android's local Health Connect manager, across all source packages. It does not read steps/calories or infer workouts from them. A session visible only inside Mi Fitness is unavailable to this importer. There is no evidence yet that this phone's band → Mi Fitness → Health Connect Exercise export works.

The old rolling last-30-days validation was incorrect and has been removed, including its arbitrary 30-day range-length cap. Standard history is anchored 30 days before the first Health Connect permission grant to FitAdvisor, rather than before today. Android enforces the actual cutoff; FitAdvisor does not invent a grant date from install time or preferences. Reinstallation and a new grant reset the default access baseline. Older-history access does not recover data never exported by Mi Fitness.

The optional checkbox requests `READ_HEALTH_DATA_HISTORY` only when the user selects it and taps Import. It is supported on API 35+ or Android 14 with U extension 13+. Exercise permission is requested separately. A missing/denied optional grant uses standard access and explains possible omission or rejection of earlier data. The history grant extends the permitted time range, not the allowed record types: only Exercise is read. The manifest still excludes Internet, writes, routes, steps, calories and background access. Imported data is only saved after confirmation.

Diagnostics distinguish denied/revoked Exercise access, optional history support/grant state, zero returned sessions, service error codes, rate limits and timeouts. A security or invalid-argument error can have multiple causes; the app labels history restrictions as possible, not proven. Successful queries show selected dates, current phone time zone, count and source packages. Saved imported rows also show source package and exercise type.

From/Through are inclusive local calendar dates in the phone's current zone. The query uses midnight From through exclusive midnight after Through, resolving each boundary independently for DST, including invalid/ambiguous midnight handling. A source record's own offset determines its saved date; the phone zone is the fallback when absent. Travel can therefore produce a saved date different from the query's phone-zone date. Import pickers and reader both prohibit future dates because these are recorded Exercise sessions.

Pagination retains 500 records/page, a 30-second/page timeout, a 10,000-session safety limit, repeated-token detection and foreground/permission checks. A native API defect was fixed: explicit ascending order is now set only on the first request; continuation requests use only the page token. Android forbids setting both together. This defect could affect a populated second page; it is not evidence that the reported first-page empty result was a query bug.

## Planned manual workouts and data preservation

Manual dates can be in the future; selecting one sets Planned. Enter the activity/title and planned duration; run distance is optional for a plan. Plans have a visible Planned label and sort ahead of completed entries. They support Edit and Delete. Mark completed asks for the actual date (today or earlier), actual duration, and actual distance for a run, then asks for confirmation. Completion updates the same local ID and creation timestamp. The date's current phone-zone offset is recalculated. Planned records do not automatically become completed when their date passes.

Completed totals filter by explicit completed status. No workout trend chart currently exists; plans do not feed nutrition/weight/habit trends or calorie targets. Existing manual and imported payloads lacking status deserialize as completed. Imported records cannot have planned status. No database table is rebuilt or cleared: status is an additive property in the existing JSON payload. Source-package + record-ID duplicate prevention, manual/import isolation and atomic import transactions are unchanged. A manual completion and an independently exported Health Connect session are separate records; cross-source duplicate matching is not inferred.

Encrypted backup format 4 preserves plan status, future date and duration. Formats 1–3 remain readable and existing workouts remain completed. Older builds reject new format-4 backups instead of silently converting plans into completed workouts. Use this version or later to restore a new backup.

## Short phone acceptance test

1. Update in place with `artifacts/FitAdvisor-0.4.2-arm64.apk` (same signing certificate). Do not uninstall or clear storage. Optionally export an encrypted backup first.
2. Record a new short workout on the band, finish/save it, then sync into Mi Fitness and check its date, type and duration there.
3. Open Android Settings → Health Connect → Data and access (or Browse data) → Activity → **Exercise**. Locate that exact new session, open its details, and confirm its source is **Mi Fitness**, with matching time/type/duration. Activity write permission alone is insufficient. Pixel-sourced active calories are a different record type. If the Exercise session is absent, stop: FitAdvisor cannot import it. Record band model, Mi Fitness version/region and Exercise permission state for upstream investigation.
4. In FitAdvisor → Health Connect Import, select that local date for both From and Through, tap Import, grant Exercise read if requested, and keep FitAdvisor foregrounded. Read the count/source diagnostics and confirm saving. Compare the row's source package, type, date and duration. Repeat the dates: the unchanged source ID must add zero copies. Force-stop/reopen and check it remains. This newly exported session avoids an older-history cutoff as the primary test variable.
5. For older history, first verify the old session actually exists under Health Connect Exercise. Select its dates, optionally enable the older-history checkbox, and retry. Denial or unsupported history retains standard access; it cannot establish whether Mi Fitness ever exported an absent old run. Revoke/deny Exercise to test the separate permission message; stored workouts must remain.
6. Create a future Other activity with a title and planned minutes. Confirm Planned status, no increase in completed totals, and persistence after reopening. Edit it, export/restore an encrypted backup, then Mark completed with a past/today date and a different actual duration. Confirm the same entry becomes Completed and only actual minutes enter totals. Create/delete another plan. Import dates must still stop at today.

## Verification and limits

The offline verification suite covers legacy status defaults, future-plan validation, edit/reopen, completion identity/date/duration, invalid status transitions, deletion, completed-only totals, encrypted backup/restore, import deduplication/provenance, rollback, legacy schema migration and unchanged calorie targets. Date checks cover inclusive Through, reversed/future import rejection, older ranges and Athens 23/25-hour DST days.

Release ARM64 builds passed with zero warnings/errors. Packaged manifest verification permits exactly Exercise read, optional history read, and the existing app-specific signature receiver permission; Internet is absent and backup/debugging/cleartext flags remain false. APK signatures verify under v1/v2/v3 with the existing development certificate SHA-256 `182049d70b00e02274148eb20c103d49d539c37b29372aef240def3d03dfae70`. The signer retains the existing third-party META-INF/JAR-entry warnings; verification succeeds. Build, regression, manifest, badging, signature and APK hash evidence use `artifacts/health-diagnostics-*` filenames.

Real-phone Mi Fitness export, populated native pagination, optional history grant/denial on supported and unsupported devices, and the native UI acceptance steps above still require phone testing. No successful Mi Band synchronization is claimed.

## Android references

- [Read history and first-grant rules](https://developer.android.com/health-and-fitness/health-connect/read-data)
- [Native history permission availability](https://developer.android.com/reference/android/health/connect/HealthPermissions#READ_HEALTH_DATA_HISTORY)
- [Native pagination: token and sort-order restriction](https://developer.android.com/reference/android/health/connect/ReadRecordsRequestUsingFilters.Builder)
