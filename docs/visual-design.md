# FitAdvisor mobile visual design

## Scope and preservation

The Android home screen is the starting destination. Its labeled feature cards and the labeled navigation drawer lead to the existing dashboard, manual food diary, weight history, running/gym workouts, Health Connect import, encrypted backup/restore, profile and new local settings. Android Back returns from a destination to Home; Back on Home keeps Android's usual behavior. Import opens the existing date-selection section; it does not request access automatically.

This checkout has no implemented food catalog/search, so the card says **Food Diary**. No catalog search, activity detection, new workout kinds, automatic importing, new permissions or notifications are advertised or added.

SQLite paths, schema, IDs, calorie calculations, diary commands and encrypted backup format remain unchanged. App ID and signing identity stay the same. Use an in-place APK update, never uninstall or clear storage to upgrade. New presentation preferences use MAUI Preferences keys beginning `design.`; diary restore does not overwrite these preferences, and diary backups do not include them.

## Design

Muted teal accents, pale gray-green page backgrounds, rounded bordered cards, 16 sp body text, 14 sp supporting text, 20–28 sp headings, and 10–20 dp spacing. All primary buttons have a 52 dp minimum height, all switches have a 48 dp minimum target, and navigation rows have a 56 dp minimum. Labels wrap and scroll containers grow with scaled text. Form labels remain visible above fields. Record actions stack vertically. Light, dark and Follow device modes are selectable in Settings. Decorative assets are excluded from the accessibility tree; actions have visible text.

Home card titles wrap independently beside compact **Open** buttons, whose accessibility descriptions identify their destinations. Form actions use concise wording such as **Save set**, **Save changes**, **Clear form**, and **Import**, preserving their existing commands. This avoids native Android clipping of long button labels at 200% text size.

Calculated relative-luminance contrast is recorded in `artifacts/design-contrast.txt`; all text pairs exceed 4.5:1. Decorative artwork is deliberately lower emphasis and communicates no required information.

## Original artwork and license

All eight SVGs in `FitAdvisor.Mobile/Resources/Images/` were drawn specifically for this change: `running.svg`, `gym_training.svg`, `muay_thai.svg`, `cycling.svg`, `hiking.svg`, `programming.svg`, `music.svg`, and `pull_ups.svg`. They have a shared 64 × 64 viewBox, rounded 2.5-unit strokes, and no raster source, external downloads, fonts, or asset dependencies. Their source is this repository's original vector geometry. These eight illustrations are dedicated to the public domain under **CC0 1.0 Universal**; see `FitAdvisor.Mobile/Resources/Images/LICENSE.txt`. No third-party artwork or invented attribution is used. MAUI converts the SVGs to packaged platform image resources at build time.

## Decorative Home animation

One 64 dp activity illustration appears at a time in the compact Home header, between the short subtitle and Menu button. The brand has its own full-width row so enlarged text does not split the app name. Every six seconds, the illustration fades out for 220 ms, changes to the next of the eight activities, and fades in for 220 ms. There are no downloaded images, Lottie dependency or continuous rendering loop.

Animation pauses when leaving Home, opening the navigation drawer or deactivating the app window. Android's `animator_duration_scale` and `transition_animation_scale` are checked before each transition; zero values (including Remove animations) select the static runner. Settings can also disable animation. Static fallback is the first illustration. The artwork does not inspect or detect user activity. The local-day message refreshes on foreground activation, Home's six-second timer and Dashboard's 30-second timer. Both timers stop when their page is hidden or the window is deactivated.

## Original daily encouragement

Twelve original English/Greek pairs live in `FitAdvisor.Core/DailyEncouragement.cs`, bundled with the app. A deterministic selection uses the local calendar day's day number. The selected date and **both complete texts** are saved as one preference snapshot. The snapshot retains wording for that day even if the catalog is updated. Language selection shows the corresponding translation, rather than selecting a new message. The next local date selects a new pair; messages repeat after twelve days. Invalid or out-of-date snapshots safely fall back to the bundled pair.

Messages focus on flexible consistency, gradual progress, enjoyment and recovery. They make no promises of results, use no real-person attribution, and contain no guilt, body shaming or advice to train through pain. The message is initially visible and can be hidden in Settings. Language defaults to English and can be changed to Ελληνικά. All behavior is local, without network calls, alarms, notifications or new permissions.

## Verification

The dashboard first-pass preview was built and shown from the emulator before extending the style to other screens: `artifacts/design-dashboard-preview.png`.

Run deterministic-message checks with:

```powershell
dotnet run --project verification/Design/FitAdvisor.DesignChecks.csproj -c Release
```

These cover 400 successive dates, stable bilingual selection, saved wording, snapshot recovery, date rollover and local midnight independent of UTC date.

### Emulator and regression results (2026-10-11)

Passed on the API 36.1 x86_64 emulator, using in-place updates with the existing certificate:

- Home starts after force-stop. All seven feature cards open their working destinations; Profile remains available from the menu. Android Back returns to Home, retaining its scroll position.
- Health Connect Import opens the existing date selection and import button without requesting permission. An empty workout list now displays a compact empty-state label rather than a 360 dp blank area.
- Light, dark and device themes; English and Greek translations; message visibility; animation on/off; preferences and unchanged daily wording across force-stop and APK updates.
- Normal 411 × 914 dp viewport at 100% text, plus 320 × 568 dp at 200% text. Home, drawer, Settings and all existing screens were inspected with enlarged text. An initial split Home title and oversized welcome area were corrected and rechecked.
- All eight poses observed at fixed six-second screenshot intervals. Static image comparisons confirm reduced-motion and animation-off behavior. Before/after image comparisons confirm no pose advancement while the drawer is open or the app is backgrounded.
- Primary buttons measure at least 52 dp; switches and the gym warm-up checkbox 48 dp; navigation rows 56 dp. Text color pairs exceed 4.5:1. Native input underlines follow the high-contrast text color. Switches have explicit checked and unchecked track colors; their off-state palettes exceed 3:1 against the surface and thumb. The gym form's warm-up label wraps beside its checkbox at 200% scale.

Screenshots: `artifacts/design-home-light-final.png`, `artifacts/design-home-dark-final.png`, `artifacts/design-home-small-200-final.png`, `artifacts/design-menu-small-200-final.png`, and `artifacts/design-illustrations.png`. Runtime check results are in `artifacts/design-emulator-results.txt`.

Calculation, separate-process persistence and offline regression checks passed. They cover diary CRUD and revisions, schema/restore rollback, encrypted backup validation and roundtrip, Greek numeric locale, workout sets, import deduplication/provenance and calorie isolation. Logs: `artifacts/design-message-checks.log`, `artifacts/design-calculation-checks.log`, `artifacts/design-persistence-checks.log`, and `artifacts/design-offline-checks.log`. The calculation project excludes the new Design check sources so the two console entry points remain independent.

No personal diary was restored, deleted or cleared during emulator testing. ARM64 hardware execution and TalkBack walkthrough remain separate device acceptance checks; the emulator is x86_64.

### Final signed ARM64 APK

- Download artifact: `artifacts/FitAdvisor-design-arm64.apk`.
- Build output: `FitAdvisor.Mobile/bin/Release/net10.0-android/android-arm64/com.stavros.fitadvisor-Signed.apk`.
- Size: **19,372,435 bytes**.
- SHA-256: `5A6EA7EEC66BABCE9641441C07510A4ADAC5EFCFE339BC7B9E3FC544F2C7A9C2`.
- Release ARM64 build: **zero warnings, zero errors**; log `artifacts/design-release-arm64.log`.
- Signature verification: v1/v2/v3 passed with the existing development certificate SHA-256 `182049d70b00e02274148eb20c103d49d539c37b29372aef240def3d03dfae70`. This is the existing development signing identity, not a newly configured production keystore.
- The verifier emits 90 third-party `META-INF` entry-coverage warnings for v1 signing, also seen in earlier release audits. v2/v3 whole-APK signature verification succeeds. License and dependency metadata were retained.
- Native libraries: **arm64-v8a only**. Eight illustrations are packaged in five density variants (40 small PNG resources).
- Manifest retains only the existing Health Connect exercise-read and generated internal receiver permissions. **INTERNET and POST_NOTIFICATIONS are absent.** Audit logs: `artifacts/design-release-signature.txt` and `artifacts/design-release-permissions.txt`.

Install as an update with the same signature to retain the diary:

```powershell
& 'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe' -s PHONE_SERIAL install -r artifacts/FitAdvisor-design-arm64.apk
```

The final x86_64 emulator build also passed with zero warnings/errors (`artifacts/design-scaled-actions-build.log`). The final Home cards, Android Back, large-text Home and gym actions were checked after shortening action labels. Emulator font scale, display overrides and reduced-motion test settings were restored afterward.
