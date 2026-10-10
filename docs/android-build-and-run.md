# Android build, install and verification

FitAdvisor.Mobile targets Android first (net10.0-android, minimum Android 6 / API 23). It is a separate MAUI app; FitAdvisor.csproj remains the web application. iOS is deferred. The app requires no account, server, health permissions, location or network connection for its profile, diary or weight flows.

## Requirements
Install .NET 10 SDK, a matching MAUI/Android workload, Android SDK platform 36/build-tools, and the JDK supported by that .NET Android workload. Visual Studio with the .NET MAUI workload can provision these. Check:

    dotnet --info
    dotnet workload list
    dotnet workload restore FitAdvisor.Mobile/FitAdvisor.Mobile.csproj

Use the installed Android SDK/JDK configuration. If discovery fails, supply -p:AndroidSdkDirectory="your SDK path" and -p:JavaSdkDirectory="your JDK path" to the build. Do not change application storage paths to fix SDK issues.

This session's initial SDK 10.0.204 build reported NETSDK1147 / wasm-tools. Diagnostics showed the real missing pack was Microsoft.NET.Runtime.MonoTargets.Sdk.net10 10.0.12, with only 10.0.8 installed. Workload restore reconciled the installed manifests/packs and the Android build then succeeded. It also refreshed already-installed Visual Studio platform workloads; this does not add iOS as an app target. Do not install WebAssembly tooling speculatively for this project.

## Build an installable personal debug APK
From the repository root:

    dotnet build FitAdvisor.Mobile/FitAdvisor.Mobile.csproj -c Debug -t:SignAndroidPackage -p:EmbedAssembliesIntoApk=true

Find the signed APK in FitAdvisor.Mobile/bin/Debug/net10.0-android/ (filename com.stavros.fitadvisor-Signed.apk). Use the signed variant. EmbedAssembliesIntoApk makes this a standalone APK instead of relying on Visual Studio fast deployment. The offline build disables Android debugging/debug-symbol support and explicitly removes INTERNET during manifest merging; IDE debugger attachment is unavailable. The workout increment adds only Health Connect READ_EXERCISE, requested at runtime by the Import button. Debug signing is suitable for personal development/testing; no Play account is needed for USB installation. It is not a production release distribution signature. See the [Android privacy audit](android-privacy-audit.md) for dependency, data-flow and artifact verification details.

## Install on your phone
1. Enable Developer options (typically tap Build number seven times), then USB debugging. Connect the phone by USB and accept its debugging authorization prompt.
2. Use adb from your Android SDK's platform-tools directory, or add that directory to PATH. Run:

       adb devices -l
       adb install -r FitAdvisor.Mobile/bin/Debug/net10.0-android/com.stavros.fitadvisor-Signed.apk

3. Open FitAdvisor from the phone's app launcher. On Windows, if adb is not on PATH, invoke its full path with PowerShell's call operator, for example:

       & 'C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe' devices -l

Alternatively, copy the signed APK to the phone, open it from a file manager and allow that installer app to install unknown apps if Android asks. Turn that installer permission off afterward if desired. Keep the same application ID and signing key for upgrades. adb install -r updates in place; uninstalling or clearing app storage removes the local database. The Data tab now supports encrypted export and validated restore; see [local data and workouts](offline-data-and-workouts.md).

## Current closeout status (2026-10-09)

The final Release ARM64 linking/AOT build and standalone signed Debug APK rebuild both passed with zero warnings/errors. The previous eight binding warnings are resolved. The rebuilt Debug APK's v1/v2/v3 signatures verified successfully with no verifier warnings this time. Earlier calculation/persistence/web results are retained; those checks were not rerun because no application code changed. See [MVP closeout](mvp-closeout.md) for the review, pending phone checklist and unimplemented running/gym proposal. Real-device acceptance is pending.

Exact Debug APK path:

    C:\Users\stavr\source\repos\FitAdvisor\FitAdvisor.Mobile\bin\Debug\net10.0-android\com.stavros.fitadvisor-Signed.apk

Historical 2026-10-09 artifact: size 86,018,730 bytes; SHA-256 `521964EDF7EAE716B355B6846D7A190429E42D7FC72C6C13E8771BAC35093935`. The 2026-10-10 privacy rebuild replaces this file; use the current hashes in the [Android privacy audit](android-privacy-audit.md).
Debug signing certificate SHA-256: `182049d70b00e02274148eb20c103d49d539c37b29372aef240def3d03dfae70`.
The historical APK was built from reviewed source at HEAD `32e44a0195646ad6100732a79c5b29a9a70658ea`. The 2026-10-10 rebuild adds offline manifest/build settings; application data and calculation code are unchanged.

Exact PowerShell installation commands (substitute the phone serial shown by devices):

```powershell
$adb = 'C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe'
& $adb devices -l
& $adb -s PHONE_SERIAL install -r 'C:\Users\stavr\source\repos\FitAdvisor\FitAdvisor.Mobile\bin\Debug\net10.0-android\com.stavros.fitadvisor-Signed.apk'
& $adb -s PHONE_SERIAL shell monkey -p com.stavros.fitadvisor -c android.intent.category.LAUNCHER 1
```

Use the explicit phone serial if the emulator is also running. Do not uninstall or clear storage. Export and verify an encrypted backup before risking existing data.

Final Release verification command:

    dotnet build FitAdvisor.Mobile/FitAdvisor.Mobile.csproj -c Release -r android-arm64 -p:AndroidPackageFormats=apk

Release artifact: `FitAdvisor.Mobile/bin/Release/net10.0-android/android-arm64/com.stavros.fitadvisor-Signed.apk` (historical 2026-10-09 size: 18,813,292 bytes; replaced by the 2026-10-10 privacy rebuild, with current hash/size in the [Android privacy audit](android-privacy-audit.md)). This build used development signing defaults; no user-owned production keystore was configured. Do not treat it as production distribution or as tested ARM64 runtime behavior. Historical logs: `artifacts/mvp-release-arm64.log` and `artifacts/mvp-debug-apk.log` (ignored build artifacts).

Existing warnings are separate: the previous web build reported 13 nullable warnings (web was not rebuilt here); the prior APK verifier emitted v1 META-INF warnings (not reproduced here). Linker advice about testing optimized assemblies is informational. Emulator startup snapshot/INI/Qt/offline messages are emulator diagnostics, not application build warnings.

## First use
- Profile: enter name, calculation weight in kg, height in cm, age in whole years, formula sex coefficient, activity multiplier, optional target weight and signed daily kcal adjustment. Save. Zero adjustment means estimated maintenance; negative means a deficit. Target weight does not automatically select a deficit.
- Meals: choose the diary date, enter a name, quantity/unit, total kcal for that entire quantity, and optional macro totals in g. Add. Quantity does not multiply the kcal/macros. Blank macros mean unknown, not zero. Use Show meals to load another date.
- Weight: enter a date and kg, with optional notes. History shows newest dates first and labels the source manual. Edit or delete from each record; deletion asks for confirmation.
- Today: target, logged calories and remaining allowance refresh when returning to the tab, or with Refresh today. A negative remainder is shown as kcal over allowance. Empty diary means no logged data, not confirmed fasting. No exercise calories are credited in this increment.
- Profile edits create retained revisions and update the target for that date. Earlier target snapshots remain intact, and existing entries retain their original profile revision on same-date edits. Moving an entry to another date adopts the destination date's context. Weight entries do not silently change the profile calculation weight; update that field explicitly.

Data is in FileSystem.AppDataDirectory/fitadvisor.db, an app-private SQLite file. Android automatic backup is disabled. The Data tab exports a passphrase-encrypted file to your chosen document destination and restores validated backups after confirmation. Workouts use an additive schema-v2 migration; existing diary records and targets are preserved. Health Connect import on Android 14+ requests only exercise read access when tapped. INTERNET remains absent. Uninstall and clear-storage actions delete the local database; ordinary app closure, force-stop, phone restart and same-signature APK upgrades should retain it. The separate-process storage test verified reopen persistence; the following phone test remains necessary for native lifecycle/UI verification.

## Checks
Run builds sequentially because they share Core output files:

    dotnet build FitAdvisor.csproj
    dotnet run --project verification/FitAdvisor.CalculationChecks.csproj
    dotnet run --project verification/Persistence/FitAdvisor.PersistenceChecks.csproj -c Release -p:JsonSerializerIsReflectionEnabledByDefault=false

For the web form, start the app in one terminal, then run HTTP checks in another:

    dotnet run --project FitAdvisor.csproj --no-launch-profile --urls http://127.0.0.1:5187
    dotnet run --project verification/Web/FitAdvisor.WebChecks.csproj

The web app's saved-profile/meal routes still need its existing SQL Server configuration; the standalone NutritionCalculator does not access that database. These HTTP checks verify responses and form validation, not visual layout.

Phone acceptance test (not completed in this session; no device/emulator connected):

1. Enable airplane mode and create a profile (80 kg, 180 cm, age 30, male coefficient, multiplier 1.2, adjustment -300). Today should show 1836 kcal target.
2. Add one meal: 80 g oats, 300 total kcal, optional macros. Today should show 300 consumed and 1536 remaining. Leaving macros blank must display unknown. Verify a comma decimal if your phone uses a comma locale.
3. Edit meal calories to 350; total should change without adding a second entry. Change its date and confirm the daily list and total move correctly. Cancel a delete, then confirm a delete.
4. Log and edit a weight. Check newest-first history and explicit kg labels; zero/invalid weight and height entered in meters must be rejected.
5. Keep a saved meal and weight, force-stop FitAdvisor using phone settings (or adb shell am force-stop com.stavros.fitadvisor), then relaunch. Profile, meal, weight and dashboard should be unchanged. Repeat after a phone restart.
6. Edit today's profile goal. Verify today's target changes but prior date snapshots and meal context remain retained. Repeatedly tap Save to check that the disabled/busy state prevents duplicate taps.

## Optional signed release APK
Use a private keystore for release updates, stored outside the repository. Create it once with keytool, which prompts for a password:

    keytool -genkeypair -v -keystore "C:\private\fitadvisor.keystore" -alias fitadvisor -keyalg RSA -keysize 2048 -validity 10000

Supply key/store passwords via file: or env: properties instead of literal command-line passwords. Example using an existing private password file:

    dotnet publish FitAdvisor.Mobile/FitAdvisor.Mobile.csproj -f net10.0-android -c Release -p:AndroidPackageFormats=apk -p:AndroidKeyStore=true -p:AndroidSigningKeyStore="C:\private\fitadvisor.keystore" -p:AndroidSigningKeyAlias=fitadvisor -p:AndroidSigningKeyPass=file:C:\private\fitadvisor-password.txt -p:AndroidSigningStorePass=file:C:\private\fitadvisor-password.txt

Keep the keystore/password securely backed up. Release artifacts are under bin/Release/net10.0-android/publish/. A release key differs from the debug key, so an installed debug build cannot be updated with that release signature. Do not uninstall a debug build containing needed personal records just to change signatures before export/backup is available. A user-owned release key and a signed release installation were not created in this session.

Sources: [Microsoft's MAUI Android publishing guide](https://learn.microsoft.com/en-us/dotnet/maui/android/deployment/publish-cli?view=net-maui-10.0) and [Android's ADB documentation](https://developer.android.com/tools/adb).
