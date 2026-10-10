# Local backup, workouts and Health Connect

This increment retains the privacy audit's offline protections: INTERNET is explicitly removed, cleartext traffic and Android debugging are disabled, automatic Android backup stays disabled, and there is no analytics, advertising, remote crash reporting or app cloud-upload service. The only new Android platform permission is `android.permission.health.READ_EXERCISE`.

## Backup and restore

Open **Data**, enter a passphrase of at least 12 characters, repeat it for export, and tap **Export encrypted backup**. Choose an on-device destination in Android's document picker. The picker requests local-only destinations; document providers ultimately control storage. If a provider stores files elsewhere, it receives only the encrypted backup. No storage permission, share-sheet recipient or Internet access is needed.

The `.fitbackup` file includes profile revisions, all saved daily target snapshots, meals, weights, manual/imported workouts, strength sets, UUIDs, timestamps and import provenance. It does not export permission grants, signing keys or an account. Encryption uses AES-256-GCM, a fresh 16-byte salt and 12-byte nonce, and PBKDF2-HMAC-SHA256 with 600,000 iterations. The versioned header/salt/nonce are authenticated. The passphrase is never saved; losing it makes the backup unrecoverable. The database itself remains app-private and is not separately encrypted by this increment.

For restore, enter the original passphrase, choose **Restore from backup**, and select the file. FitAdvisor authenticates/decrypts it and validates the format, values, unique IDs and profile references before showing record counts and asking whether to replace current data. Restore replaces rather than merges. Replacement is one database transaction: write failure rolls back all deleted/inserted records. IDs, historical targets, optional macros, notes, timestamps and provenance are preserved. Cached editing forms are cleared after successful restore. Cancellation, wrong passphrase, corruption or unsupported format does not replace records. There is a 32 MB file limit and 100,000-record validation limit. A failed export can leave an incomplete destination file; retry with a new file and delete the incomplete one.

## Manual workouts

Open **Workouts** to add a run or gym session with date, title, duration in minutes and notes. Runs require distance in km and show average pace. Gym sessions support ordered strength sets with an exercise name, whole-number reps, external load in kg and a warm-up flag. Save each pending set before saving the session. Edit/remove sets in the form; session edits keep the workout ID and existing set IDs. Deleting a saved session asks for confirmation.

Workouts store local date, UTC offset, stable UUIDs and creation/update timestamps. The additive schema-v2 migration creates a workout table and import uniqueness index without rewriting the schema-v1 diary tables. Workouts do not award exercise calories, alter profile weight, or change calorie targets/meal allowances. GPS, routes, workout recording, exercise catalogs, bodyweight/assistance models and health-data writes are outside this increment.

## Health Connect

This implementation uses the built-in Android 14+ Health Connect platform API, avoiding additional NuGet/network libraries. On older Android versions, import explains that it is unavailable; manual logging and backups still work.

Select dates within the last 30 days and tap **Import Health Connect sessions**. Only this action requests exercise read access. Denial stops import. Reads are paginated and limited to 10,000 sessions, with a 30-second timeout per page. All pages are read before asking you to confirm saving local copies. There is no startup/resume/background job or automatic retry. Import uses the currently granted access; Health Connect can restrict the readable history, so an empty result is not proof that no source sessions exist.

Only exercise session start/end-derived duration, start UTC offset/local date, exercise type, title, notes, source package, source record ID and modification timestamp are copied. No routes, GPS, heart rate, distance records, weight, steps, calorie records, history-extension permission, background permission or write permission is requested. Runs imported as sessions can therefore have unknown distance/pace; source exercise type is retained. Health Connect is an on-device source; FitAdvisor does not contact its source apps' cloud services.

Repeated imports match source package plus Health Connect record ID. Newer source revisions update the existing local copy while retaining its local UUID. Manual workouts are never overwritten. Different source IDs/apps can still represent the same real-world workout; this increment does not infer cross-provider duplicates. Imported copies are read-only: edit the original in its source app. Local deletion does not delete Health Connect data; a later explicit import can bring the copy back. Source deletion does not automatically remove a previous local copy. Revoking permission prevents subsequent reads but does not erase already imported local data. Use Health Connect settings to revoke access. Its permission-usage screen can show FitAdvisor's local privacy explanation.

## Checks and remaining device checks

Run:

```powershell
dotnet run --project verification/Offline/FitAdvisor.OfflineChecks.csproj -c Release -p:JsonSerializerIsReflectionEnabledByDefault=false
dotnet run --project verification/Persistence/FitAdvisor.PersistenceChecks.csproj -c Release -p:JsonSerializerIsReflectionEnabledByDefault=false
dotnet run --project verification/FitAdvisor.CalculationChecks.csproj
```

The offline checks cover authenticated backup roundtrip, wrong password/tamper/truncation, a Greek locale, retained history/IDs/timestamps/sets, invalid-reference rejection, atomic rollback of a deliberately failed restore, schema-v1 upgrade, workout validation/CRUD, source-ID collisions, repeated import/update and unchanged calorie allowances.

The read-only API 36.1 emulator smoke test verified launch, navigation through More to Data/Workouts, encrypted export to Downloads, passphrase-field clearing, restore record-count confirmation and replacement of a synthetic run, manual run save/reopen and 6 min/km pace for 5 km in 30 minutes. The native Health Connect screen offered only Exercise read access; denial left the manual run intact, and a subsequent grant returned an empty-session result through the real service callback. No AndroidRuntime fatal exception was observed. The later build adds an import status label beside the import fields.

Real-phone document providers, populated Health Connect/Mi Fitness imports, permission revocation, process death during pickers, Android 14 provider availability, older-Android fallback and optimized ARM64 runtime behavior still need device acceptance. Source-session pagination/update behavior was verified in storage checks, not against a populated Health Connect store. No existing phone data was cleared or uninstalled. The emulator was launched with -read-only/-no-snapshot so test changes do not replace its saved disk state.

## Install and test Mi Fitness

Prepared signed files:

| APK | Use | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| artifacts/FitAdvisor-offline-arm64.apk | ARM64 phone, optimized Release | 18,911,596 | 5CE8104A0229F0598B10A3EE0F4885C75616F40E2729E1C37CCC97E1867E2005 |
| artifacts/FitAdvisor-offline-universal.apk | Standalone Debug, multiple Android architectures | 86,521,412 | 3D57FC50A2DB6A2ECCF6447599FBE43E135D52E8B52558B1F572A42473837EE2 |

Both final APKs passed v1/v2/v3 signature verification and have the same certificate as the earlier personal build: SHA-256 `182049d70b00e02274148eb20c103d49d539c37b29372aef240def3d03dfae70`. This is development signing, not a new production key. The verifier continues to emit warnings for third-party META-INF entries not covered by v1 signatures. Both manifests were inspected inside the signed APK and checked against exactly READ_EXERCISE plus the app-specific signature permission; allowBackup, debuggable and usesCleartextTraffic are all false. No INTERNET or other platform permission is present. Both builds passed with zero build warnings/errors. Logs: artifacts/offline-features-debug-final.log and artifacts/offline-features-release-final.log.

1. Use an Android 14+ phone for Health Connect import. Connect it over USB, authorize debugging, then run `adb devices -l` and use that phone's serial. From the repo root: `adb -s PHONE_SERIAL install -r artifacts/FitAdvisor-offline-arm64.apk`. The universal APK is the alternative for another CPU architecture. Keep existing data: update in place; do not uninstall or clear storage. If the installed app has a different signing key, stop rather than uninstalling it.
2. Open FitAdvisor and check your existing profile/meals/weights. In More → Data, create an encrypted backup and keep its passphrase. Test destructive restore only with a disposable diary or when you intentionally want to replace current records.
3. Sync the watch/tracker to Mi Fitness. In Mi Fitness's available Health Connect integration, enable exercise sharing. In Android Health Connect, check that Mi Fitness can write exercise data, then verify that a recent workout actually appears in the health store with its source. Menu names depend on Android/Mi Fitness versions; their behavior on your phone is unverified.
4. In FitAdvisor, open More → Workouts, select the workout's dates within the last 30 days, and tap Import. Grant only Exercise read access. Confirm the session count and import; verify title/type/date/duration and source. Repeat the same import: existing source IDs should not duplicate. Run distance/pace, heart rate, steps, sleep and calories are not imported by this exercise-session-only slice.
5. If nothing appears, first check whether the workout exists in Health Connect and whether FitAdvisor has exercise read access. A workout visible only in Mi Fitness is not yet available to this importer. No direct Xiaomi account/cloud endpoint or Internet fallback is implemented. Mi Fitness's own network/cloud behavior is outside FitAdvisor's privacy protections.

Google's [Mi Fitness integration documentation](https://support.google.com/googlehealth/answer/14236613?hl=en-GB) identifies Health Connect as a supported route for Mi Fitness exercise summaries and emphasizes syncing the tracker into Mi Fitness first. This supports the integration approach; it does not verify your specific app version, region, watch or phone. No Google Health account/app is required by FitAdvisor's local importer.

References: [native Health Connect API](https://developer.android.com/reference/android/health/connect/HealthConnectManager), [permission/privacy rationale requirements](https://developer.android.com/health-and-fitness/health-connect/get-started), [Android document storage](https://developer.android.com/training/data-storage/shared/documents-files).
