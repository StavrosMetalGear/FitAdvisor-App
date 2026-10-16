# FitAdvisor

FitAdvisor includes the original ASP.NET Core Razor Pages web application and a separate Android application built with .NET MAUI.

## Web application

The original web application is preserved. It supports:

- Secure registration and login.
- Personal details, including name, gender, weight, height and target weight.
- Calorie plans.
- Meal logging and daily calorie balance.
- Weight tracking and weekly progress.

Web data is stored in SQL Server using Entity Framework Core.

The `/NutritionCalculator` page uses explicit inputs but does not save them to a web profile. See the migration document for historical calculation defects and differences from the corrected mobile targets.

Build the web application:

```cmd
dotnet build FitAdvisor.csproj
```

## Android application

The Android application supports:

- Offline profile and calorie targets.
- Manual meal logging.
- Weight history.
- Editing and deleting entries.
- Today's dashboard.
- Offline food search, reusable custom foods/favorites and optional Open Food Facts lookup.
- Persisted gym sessions, routines, repeat workouts and offline exercise photo guides.
- Optional post-sync Health Connect enrichment and encrypted backups.

Data is stored locally using SQLite. No account is required.

Android solution: `FitAdvisor.Mobile/FitAdvisor.Mobile.slnx`

### Build the APK

From the repository root:

```cmd
dotnet build FitAdvisor.Mobile/FitAdvisor.Mobile.csproj -c Debug -t:SignAndroidPackage -p:EmbedAssembliesIntoApk=true
```

APK location:

```text
FitAdvisor.Mobile/bin/Debug/net10.0-android/com.stavros.fitadvisor-Signed.apk
```

### Install on an Android phone

Enable USB debugging, connect the phone and authorize the computer. With Android SDK platform-tools on PATH, run:

```cmd
adb devices -l
adb install -r FitAdvisor.Mobile/bin/Debug/net10.0-android/com.stavros.fitadvisor-Signed.apk
```

Open FitAdvisor on the phone after installation.

## Verification

Run calculation and persistence checks sequentially:

```cmd
dotnet run --project verification/FitAdvisor.CalculationChecks.csproj
dotnet run --project verification/Persistence/FitAdvisor.PersistenceChecks.csproj -c Release -p:JsonSerializerIsReflectionEnabledByDefault=false
```

Android APK packaging and automated storage checks have passed. Native UI, offline operation and app restart persistence still require testing on a connected phone or emulator.

Passphrase-encrypted backup/export and restore are available from Backup / Restore. Uninstalling the app or clearing its storage removes local data. Install same-certificate APK updates without uninstalling.

The 0.6.0 phone-feedback update, signed ARM64 APK details, Release emulator evidence and remaining phone checks are documented in [verification](docs/v060-verification.md), [source/licence/privacy notes](docs/v060-sources-and-privacy.md), and the [phone checklist](docs/v060-phone-checklist.md).

## Documentation

- [Migration assessment, roadmap and verification](docs/mobile-migration.md)
- [Android APK build, installation and device tests](docs/android-build-and-run.md)

## Planned features

- Additional device compatibility and reviewed exercise variations.
- iOS support.
