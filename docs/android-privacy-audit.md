# Android offline MVP privacy audit — 2026-10-10

Scope: FitAdvisor.Mobile, FitAdvisor.Core and FitAdvisor.Storage, their resolved NuGet dependency graph, merged Android manifests and signed Debug/Release APK manifests inspected with Android SDK aapt. The separate ASP.NET web project is not referenced by the mobile app; its server configuration is outside this APK's data flow.

## Current feature increment

The findings and artifact hashes below describe historical builds. The current [0.4.2 diagnostics/history update](health-connect-diagnostics-and-plans.md) retains INTERNET removal, disabled cleartext/debugging/automatic backup, and the absence of app analytics, cloud uploads and remote crash reporting. It reads only Exercise sessions and optionally requests `android.permission.health.READ_HEALTH_DATA_HISTORY` on supported devices, in addition to `READ_EXERCISE`. There are no writes, routes, location or background permissions. No additional package dependency was added.

The new data flow is explicit: exporting sends a passphrase-encrypted copy of diary/workout data to the document URI selected by the user. FitAdvisor requests local-only documents, but the selected provider controls storage and may store the encrypted file outside the phone. Restore reads that chosen file. Health Connect transfers exercise-session metadata from Android's local health store into FitAdvisor's app-private database only after an Import tap, permission grant and confirmation. There is no application HTTP request or Internet permission. Imported exercise titles/notes, source package/record identifiers, dates/times and types are included in subsequent encrypted backups. No Health Connect grants are backed up or restored.

See [local data and workouts](offline-data-and-workouts.md) for fields, encryption, limits, source deletion/reimport behavior, and checks. Historical claims below that no export UI exists apply to the previous MVP.

Final feature APK inspection confirmed exactly READ_EXERCISE plus the original signature permission, no INTERNET, and allowBackup/debuggable/usesCleartextTraffic all false. Both builds passed with zero build warnings/errors and signatures verified under the existing certificate. Current hashes/installable files and emulator evidence are in [local data and workouts](offline-data-and-workouts.md#install-and-test-mi-fitness); the hashes below remain historical MVP evidence. No live packet capture, real-phone Mi Fitness import or complete third-party binary audit was performed.

## Historical MVP findings

- No HTTP/HTTPS request, socket, remote image URL, WebView, browser-launch, share/export or upload call was found in the mobile app or its two referenced projects. XML namespace URLs are identifiers, not requests.
- No analytics, advertising, remote crash-reporting or cloud-storage SDK was found in the resolved dependency graph. No API key, credential, bearer token, private key or service endpoint was found in the reviewed application source/configuration. This is a static inspection, not proof of absence of obfuscated secrets inside every third-party binary.
- Direct packages: Microsoft.Maui.Controls 10.0.20, Microsoft.Extensions.Logging.Debug 10.0.0, Microsoft.Data.Sqlite 8.0.27 and SQLitePCLRaw.bundle_e_sqlite3 2.1.13. Transitive packages supply MAUI/AndroidX UI, Microsoft configuration/DI/logging, SQLite, Glide image loading, Kotlin, Gson and cryptography support. Framework networking/image/authentication capabilities exist but no app code invokes them. Full resolved inventory: [android-audit-dependencies.txt](android-audit-dependencies.txt).
- Logging.AddDebug is compiled only under DEBUG; it is a development logging provider, not a configured remote reporter. UI exceptions become local status text. No app logging of diary values was found.
- The pre-change signed Release ARM64 APK requests only com.stavros.fitadvisor.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION, an AndroidX-generated signature permission. It has no INTERNET, ACCESS_NETWORK_STATE, contacts, SMS, phone, microphone, location, camera or storage permission. Keep the signature permission: it protects internal receiver delivery and grants no personal-data access. android.permission.DUMP on the ProfileInstaller receiver restricts callers; it is not a permission requested by the app.
- The old standalone Debug APK requests INTERNET for development support. The offline build now disables AndroidNeedsInternetPermission, AndroidIncludeDebugSymbols and android:debuggable, with an explicit manifest merge removal rule for INTERNET. IDE debugger attachment is disabled for these offline APKs. Cleartext traffic is explicitly disabled. Backup remains disabled. APK verification results are recorded below.
- Framework manifest components include a non-exported FileProvider, authentication intermediate activity and connectivity/battery receivers. They are not evidence of uploads or authentication being used. The app contains no sharing/authentication/connectivity calls. The exported launcher activity is expected; the ProfileInstaller receiver is protected by DUMP.

## Exactly what the app sends

No application-controlled outbound data path was found. There is no recipient, endpoint, upload payload, account synchronization, analytics event or crash-report submission in the reviewed MVP code. The inspected Release APK also lacks direct network permission. This conclusion is static evidence, not a packet-capture measurement.

The local app-private FileSystem.AppDataDirectory/fitadvisor.db stores name, calculation weight, height, age, formula sex, activity multiplier, calorie adjustment, optional target weight; retained profile revisions; dated calorie target/input/formula snapshots; meal names, quantities, units, kcal and optional macro totals; dated weight readings and notes; UUIDs, record links and creation/update timestamps. All database operations use Microsoft.Data.Sqlite with a local file path. No export or backup UI is implemented. No additional database encryption is configured.

## Limits

No phone/emulator was connected (adb devices returned an empty list). Live network capture, real-device permission checks, native runtime behavior and the installed APK's identity are unverified. Every third-party native/managed binary was not decompiled or independently audited. Signing certificates are public APK metadata, not embedded private signing keys.

Android/OEM diagnostics, keyboard services, screenshots, user/device administration tools and OEM device-to-device transfer can operate outside the app's own data paths. Their behavior was not measured. allowBackup=false is confirmed in the APK; that alone does not establish every OEM transfer behavior. This audit does not claim that no operating-system-controlled data ever leaves the phone.

## Final rebuilt APK verification

Both builds succeeded with zero compiler/build warnings and errors. Android SDK aapt inspected the manifests inside the signed APKs, not just intermediate XML: the only requested permission in each is the app-specific signature permission above. INTERNET and all Android platform permissions are absent. Both APKs explicitly set allowBackup=false, debuggable=false and usesCleartextTraffic=false.

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| FitAdvisor.Mobile/bin/Debug/net10.0-android/com.stavros.fitadvisor-Signed.apk | 86,357,572 | 46937A7EC72F6E96189696D13473C59C3D2D6F1EA6629244400345AEADAAE14B |
| FitAdvisor.Mobile/bin/Release/net10.0-android/android-arm64/com.stavros.fitadvisor-Signed.apk | 19,152,134 | BAC350D8D1E72560F26752EEA8F3B1A172F1BC78963289974A52C02A459EC5EE |

apksigner verified v1/v2/v3 signatures for both packages. It emitted warnings that third-party META-INF version/license/ProGuard entries are not protected by the v1 JAR signature. These warnings remain; verification did not fail. Neither package has a v4 signature or SourceStamp. They use the existing development signing setup; no production signing key was created or embedded. Build logs: artifacts/android-audit-debug-final.log and artifacts/android-audit-release-final.log. Release signature log: artifacts/android-audit-release-signature.log.

The permission fix changes build/manifest settings only. No database reset, migration, uninstall or device installation was performed. Older copied or installed APKs retain their old permissions until replaced; their identity and traffic are not verified by this rebuild.
