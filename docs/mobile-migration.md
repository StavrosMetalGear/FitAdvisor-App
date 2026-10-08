# FitAdvisor mobile migration

Status: Android confirmed by the user. The first mobile increment implements profile editing, manual meals, weight history, editing/deletion and today’s dashboard using an app-private SQLite database without an account. The existing web app is preserved. Calculation, persistence and web HTTP checks passed; Android Debug packaging and an initial Release ARM64 build succeeded. The final Debug APK build passed with zero warnings/errors and its signature was verified. A final Release rebuild after the compiled-binding changes is deferred to the next session. Native UI/airplane-mode/phone restart tests remain pending because no Android device or emulator is connected. No legacy SQL Server data conversion was performed. See [Android build and installation guide](android-build-and-run.md).

## Current project and reuse
- ASP.NET Core Razor Pages, net8.0, EF Core 8, Identity with ApplicationUser, SQL Server configured in Program.cs. SQLite package exists but is not configured. Existing server pages are not an offline phone application.
- Reuse C# formulas through FitAdvisor.Core, meal/weight concepts, useful page copy, and existing data through a later explicit export/import. Razor Pages, Identity, and SQL Server persistence cannot be carried directly into a native offline app. Existing Bootstrap responsiveness helps the interim web UI.
- ApplicationUser has name, gender, height, weight, target weight, but no age/activity/goals history. Height comment says cm or meters, while formulas require cm. Registration has required numeric inputs without meaningful ranges; weight logger accepts nonpositive weights. Meal entry stores name/calories/date only, no quantity/macros. Pages/WeightEntry.cs is a data model located in the UI directory.
- CaloriePlan hardcodes age 30, computes Mifflin-style resting energy, multiplies by 1.5 and 0.8, truncates, applies another 0.8, then easy/medium/hard factors. This produces about 0.96 × resting energy for the medium plan. LogMeal instead uses resting energy × 1.2 and calls it Easy. Target weight does not influence calorie targets. Preserve old outputs in the compatibility adapter; replace policy only with explicit inputs and migrated settings.
- Body fat uses adult Deurenberg BMI/age/sex formula, rounded to one decimal. It is not a circumference method. Target body fat is hypothetical at target weight. Old age is assumed and there is no estimate history/provenance. The new result carries version and actual inputs; persistence comes next.
- Multiple scaffolded Identity pages request UserManager<IdentityUser> or SignInManager<IdentityUser>, inconsistent with registered ApplicationUser. Audit sign-in, reset and account management before relying on these flows. No test project was present in the inspected source.

## Recommended approach
Use a separate .NET MAUI application with native XAML screens and MVVM, sharing an independently testable C# domain library. Start on the user's actual phone platform once confirmed. Keep the web application available during migration. A MAUI Blazor Hybrid UI could reuse future Razor components, but these current server PageModels are not portable components; native forms suit quick entry. Do not force authentication into the personal app.

Use local SQLite in the app's private data directory, repository interfaces, explicit transactional schema migrations, foreign keys, and uniqueness constraints. Decision for this increment: Microsoft.Data.Sqlite 8.0.27 with SQLitePCLRaw.bundle_e_sqlite3 2.1.13, explicit SQL and transactional schema versioning. This keeps the store independent of MAUI and testable in a desktop process. Platform startup and native library loading still require a phone test. Never use EnsureCreated as a substitute for evolving schema migrations. An app restart and airplane-mode smoke test are MVP release requirements.

Architecture: MAUI views -> viewmodels/application use cases -> Core domain/calculations; repositories -> local SQLite. Platform adapters implement optional health imports, notifications, file sharing and later GPS. Core must not depend on UI, Identity, EF or native APIs. Current shared library targets net8 for web compatibility; a newer MAUI shell can reference it. Select a supported stable MAUI toolchain rather than a preview, and plan web/runtime updates separately.

## Roadmap
| Stage | Deliverables and acceptance |
| --- | --- |
| MVP 0 (verified this increment) | Shared calculations and explicit-input web calculator; C# regression and HTTP validation checks pass. Historical double-deficit outputs labeled as known defects, not recommendations. |
| MVP 1 (current increment) | Separate Android MAUI app; versioned profile/calorie goals; app-private SQLite with transactional version-1 schema; quantity/calorie/optional-macro meals, dated weight/history, edit/delete and today dashboard. C# separate-process restart and CRUD checks pass. Phone UI/lifecycle verification pending. No accounts or integrations. |
| MVP 1 follow-up | Manual running and gym sets, then safe export/backup/restore and explicit legacy import. Keep the current profile/meals/weight increment focused; do not introduce device imports or advanced analytics yet. |
| MVP 2 | Weekly weight/day averages and running totals; previous exercise performance; basic exercise library and routines; editable campus route; source labels; reconcile calorie targets across diary/dashboard; simple smoking/alcohol only after core flows pass. |
| Phase two | Custom foods, favorites, recipes, copy meal; planned runs and half-marathon goal; additional measurements and versioned body-fat estimate history; smoking cravings/lapses/money, sleep/energy/soreness/water; optional reminders; route comparisons and exercise records; optional documented e1RM; health-platform proof of concept after device confirmation. |
| Future | Verified imports, smart scale, live GPS, evaluated food search/barcodes, optional accounts/cloud sync. Advanced analytics require adequate data and documented methods; no generic readiness or injury/diagnostic scoring. |

## Screens
- Today: consumed kcal, target kcal, estimated activity kcal, food allowance separately; meals/workouts/goals; one-tap add meal, weight, run, set.
- Profile & goals: kg/cm/date of birth or age, formula coefficient, activity accounting policy, weight/nutrition/running/strength goals; dated change history. Explain assumptions and exercise credit next to the setting.
- Food diary: date/meal group, quantity/unit/macros; reuse and copy later; edit/delete accessible on each record.
- Weight & measurements: quick dated entry and source, chart/weekly averages with coverage, optional waist and other measurements.
- Runs: quick distance/time/category/effort, editable route, optional elevation/HR/calories/notes; list and detail; planned vs completed later.
- Gym: choose exercise/routine, previous sets visible, quick working/warm-up set with reps/load/rest/RPE or RIR.
- Week: totals and coverage; observations only when enough comparable records exist.
- Habits: optional smoking/alcohol then recovery. Settings/data: unit display, reminder toggles, permissions, export, backup/restore.

## Proposed persisted models
All records use local UUID, created/updated timestamps, a local date plus offset/timezone for diary grouping, source, and optional external identity. Save canonical kg/cm/km/meters/seconds/kcal; convert only for display. Imported records retain original units/payload where useful.

| Model | Important fields |
| --- | --- |
| ProfileRevision | effective date, weight kg, height cm, birth date or age-at-date, formula sex, activity policy; revisions retained |
| GoalRevision | effective date, target weight, calorie adjustment/manual override, macro goals, running/strength targets, half-marathon date, multiplier, exercise-credit fraction; no retrospective recalculation |
| DailyTargetSnapshot | local date, profile/goal revision IDs, formula version, inputs, target; explicitly distinguish later edits from original context |
| MealEntry/MealItem | local date, meal/name, quantity/unit, consumed kcal/protein/carbs/fat snapshots, optional food/recipe reference; changing a recipe must not rewrite diary history |
| Food/Recipe/RecipeItem | nutritional reference per 100 g or defined serving, recipe yield, ingredients; version where needed |
| Measurement | date/time, type/value/unit, manual/algorithm/smart-scale source; for estimates algorithm version, full inputs and linked profile revision |
| RunWorkout | category (customizable), km, seconds, gain/loss meters, optional HR/calories, RPE 1–10, route revision, route/weather/feeling notes; optional planned-workout link |
| RouteRevision | name, distance/elevation, effective date; campus seed approximately 8 km and elevation range 120–160 m, editable, never silently treated as measured GPS |
| Exercise/Routine/RoutineExercise | stable exercise identity, load semantics/equipment, ordered exercises |
| StrengthWorkout/StrengthSet | time, exercise ID, order, reps, load kg, bodyweight at time if used, added/assisted load mode, warm-up flag, rest seconds, optional RPE/RIR |
| HabitEntry/SmokingGoal | cessation start, daily cigarettes incl. zero, cravings/lapses, pack size/price/currency/effective date; lapses never erase previous history |
| AlcoholEntry/RecoveryEntry | quantity/unit and optional ABV, sleep interval/duration, energy/soreness scale, optional water ml |
| ImportIdentity/SyncCursor | unique platform + origin app/device + external ID + record type; cursor advances only after successful transaction; update/delete propagation and revoked access tested |

Keep user-visible delete straightforward. Retain goal/profile revisions referenced by existing entries. Sync tombstones are needed only once synchronization is introduced. Never backfill an assumed age as measured data in a legacy import.

## Calculation and analytics rules
Resting kcal = 10 × kg + 6.25 × cm - 5 × years + sex coefficient (+5 male, -161 female). Maintenance = resting × selected activity multiplier; target = maintenance + signed goal adjustment. Defaults do not credit activity. Credit is permitted only with a baseline excluding exercise: allowance = target + fraction × activity kcal above baseline. Imported gross calories require review/conversion; rejecting incompatible settings alone does not establish that imported calories are net. Missing activity calories stay unknown. Never derive gym calories from tonnage.

Body fat = 1.2 × BMI + 0.23 × age - 10.8 × maleFlag - 5.4. Version identifier in code is deurenberg-adult-1991-v1. Store input snapshot; do not clamp implausible percentages, show unavailable. Adult-only input guard (18–120 years, 20–500 kg, 100–250 cm) is an application constraint, not validation of suitability across every accepted input. Rounded display does not imply precision. Existing target body fat is scenario output, not a goal measurement.

Pace = duration seconds / distance km, displayed min:sec/km; reject zero distance/time. Weekly run distance/time include completed runs only. Longest run is distance, and PRs must distinguish measured distance, manual route estimates and route versions. Compare same route/distance with elevation, effort, weather and HR alongside; do not invent an elevation correction. Half-marathon progress initially shows completed/planned training and distance goals, not a predicted finish time.

For weight weekly averages, first average multiple readings within each measured day, then average the daily means. Missing days remain absent, not zero, carried forward or interpolated. Show coverage, e.g. 3/7 days; no readings yields insufficient data. Nutrition missing diary days are unknown, not fasting.

Strength volume: for externally loaded working sets sum load kg × reps for the same exercise and load mode; warm-up inclusion is explicit. Bodyweight/added/assisted movements retain separate quantities and are not mixed with external-load tonnage without a documented comparable convention. Compare reps at the same load and load at the same reps. Optional Epley estimate = load × (1 + reps/30), only for eligible exercises/ranges selected and documented later; estimate never replaces recorded performance. Sleep/performance summaries show sample counts and comparable observations without causal language.

## Storage/export verification plan
SQLite transaction tests cover CRUD, constraints, restart/reopen, schema migration and rollback. JSON backup has schema/version/units and complete provenance; CSV export has explicit units and timestamps for analysis. Restore validates size/schema/enums/ranges/relationships/duplicates in a temporary database, then swaps only on successful validation with a pre-restore backup. Reject unsupported newer versions; never overwrite live data while parsing. User-selected export must include a note that health data are sensitive; select backup destination deliberately. Verify migration with copied legacy SQL Server exports before any switch. Mobile UI tests cover rapid repeat entry, decimals/locales, back navigation, validation and deletion. Platform import tests cover denied/revoked permissions, pagination, duplicates, updates/deletes and partial failure.

## Dependencies, costs and unresolved questions
MAUI/.NET, SQLite, optional MVVM tooling; no paid backend required for local personal use. Development needs the relevant SDK/workload and a physical device/emulator; iOS development requires a networked Mac with Xcode per Microsoft documentation. HealthKit can use .NET iOS APIs; a Health Connect Android binding/version and sample spike are required before claiming C# integration works. Store distribution: Google Play lists a US$25 one-time registration fee, Apple Developer Program US$99/year or local currency; hardware and future hosting/API costs are additional. Personal installation/distribution route must be chosen for the actual platform.

Health Connect supports separate workout, distance, energy, HR, sleep and measurement types with per-type permissions; availability of a type does not imply the band writes it. HealthKit requires capability/usage descriptions and authorization; privacy means lack of read data is not proof of denied permission. Request only chosen read types, no write/GPS/background access initially. A health-platform adapter should discover available data, preview imports and preserve source/external identifiers. Confirm phone OS/version, Mi Band exact model and companion app/version, then record a sample workout/sleep and inspect what appears in the phone health platform. Verify units, timestamps, HR detail and update/delete behavior. No direct Bluetooth or live HR promise. The user has not purchased a Withings scale; no scale integration is part of this increment.

Food API/barcode work is deferred: compare regional coverage, serving/unit quality, licensing/attribution/redistribution and cache rights, rate limits and API prices before choosing a provider. Map/GPS/background permissions, battery behavior, cloud privacy/conflict handling and reminder delivery also need separate evaluation.

## Current implementation and calculation differences

Projects: FitAdvisor.Core (formulas and diary contracts), FitAdvisor.Storage (SQLite repository and source-generated JSON snapshots), FitAdvisor.Mobile (Android MAUI views/viewmodels). The web project excludes mobile/storage/test source from its recursive compile/content globs and still references Core. A separate mobile solution is in FitAdvisor.Mobile/FitAdvisor.Mobile.slnx; the original web solution is unchanged.

Android screens: Today, Profile, Meals, Weight. The profile stores kg/cm, age-at-revision, formula coefficient, activity multiplier, signed kcal adjustment and optional target kg. Meal calories/macros are totals for the entered quantity; no automatic multiplication or database food lookup. Optional macros are nullable. Weight entries are manual kg and do not silently change profile calculation weight. Daily totals use explicit local diary dates; missing meals/weights remain unknown/absent.

Profile edits append revisions, update only that date’s target, and retain earlier snapshots. Same-date meal/weight edits preserve the original profile context; re-dating chooses the destination day’s snapshot context. New daily snapshots use the effective profile revision and retain the formula version and inputs. Current age is years at revision, not a birth date; update it explicitly on birthdays. Goal values are versioned within the profile revision in this increment; separate running/strength goal models follow later.

SQLite is stored as FileSystem.AppDataDirectory/fitadvisor.db. Writes are parameterized and transactional, database foreign keys are enabled, connections are unpooled/short-lived, and I/O runs on a worker thread behind a gate. Schema upgrades use PRAGMA user_version; newer schemas are rejected and failed initialization rolls back. JSON snapshots use source-generated serializers to avoid reflection-only serialization under trimming. Edit/delete command bindings are compiled as well. Source is manual for all current records; imported IDs and integrations are deferred. Data is not encrypted with a separate database key; it uses the phone’s app-private storage protections. Android automatic backup is disabled; a safe user-controlled export/backup/restore remains a follow-up requirement. Closing/restarting the app retains the file; uninstall/clear storage removes it.

### Legacy versus corrected calculations

The historical adapter is isolated as LegacyCaloriePlans; it is not used by the mobile app. For 80 kg, 180 cm, age 30 and the male coefficient, resting energy is 1780 kcal. Historical medium plan = truncation of truncation(1780 × 1.5 × 0.8) × 0.8 = 1708 kcal. A corrected single 20% deficit from 2670 kcal maintenance gives 2136 kcal (signed adjustment -534). The double reduction and premature truncation are known defects preserved only to compare old outputs, with prominent labels on the historical web page. “Recommended Plans” was removed from that page. Unrecognized legacy sex strings now stop calculation instead of silently selecting the female coefficient.

The old meal diary’s 2136 kcal for the same example comes from 1780 × 1.2 with no deficit; its numerical agreement with the corrected example is coincidental and has different assumptions. Its old “Easy Plan” label was replaced with an explicit historical diary target label. Existing meal records and the legacy target formula were not silently rewritten.

The new calculator/mobile use explicit age, kg/cm, a selected multiplier and a signed calorie adjustment. They retain floating-point precision until display and apply only the requested adjustment once. Target weight does not choose a deficit automatically. Mobile credits no exercise calories; allowance equals target. The shared calculator permits configurable exercise credit only with a baseline excluding exercise. Body-fat estimates remain approximate Deurenberg estimates; no new mobile body-fat logging UI is claimed.

### Verification and environment

The default command runner fails during sandbox setup with helper_unknown_error/setup refresh before even dotnet --info starts. The outside-sandbox comparison command succeeded, establishing a process-setup/environment boundary failure rather than a broken .NET installation. No sandbox configuration was changed. Approved commands outside that failing sandbox were used for builds/tests.

A first concurrent web/check build encountered a shared Core output lock; serialized builds resolved it. All subsequent dependent builds are sequential. HTTP validation found and fixed a prior-increment bug: integer RangeAttribute bounds on double-valued fields attempted integer conversion of NaN and threw OverflowException before the handler ran. Bounds are now double-valued, and the non-finite HTTP regression passes.

| Check | Result |
| --- | --- |
| Web build | Passed; 13 pre-existing nullable warnings, no errors |
| Core calculation executable | Passed, including explicit legacy/corrected expectations |
| SQLite Release executable | Passed: CRUD, invalid input rejection, nullable macros, SQL parameterization, local-date totals, goal revision preservation, same-day edits, future-schema refusal and transaction rollback |
| Separate-process restart | Passed: one process commits profile/meal/weight; another opens the database and verifies records/dashboard |
| Reflection-disabled serialization | Passed: persistence executable with JsonSerializerIsReflectionEnabledByDefault=false |
| NutritionCalculator HTTP | Passed: valid result, invalid units/settings/coefficient/non-finite data, antiforgery |
| Android Debug APK | Final standalone APK build passed: zero warnings/errors. apksigner verified v1/v2/v3 signatures (exit 0); emitted META-INF warnings for v1 signing. |
| Android Release ARM64 | Initial linking/AOT build passed with 8 binding compilation warnings. Source was updated to typed compiled bindings; final Release rebuild is deferred until the next session. |
| Browser visual check | Blocked: browser runtime initialized but reported no available browsers; discovery list was empty |
| Native Android UI/restart/airplane mode | Blocked: adb reported no connected devices; no running emulator is connected |

Initial Android build reported wasm-tools, but diagnostic output identified missing MonoTargets SDK 10.0.12 while installed packs included 10.0.8. Workload restore reconciled the manifests/packs; Debug packaging and Release ARM64 linking/AOT then succeeded. Android SDK platform 36 and build-tools 36.0.0 are installed. No missing-workload claim remains after the successful build. Native UI checks are still required on the user’s phone; the desktop database reopen check is not presented as a native app restart test.

Saved work is in the workspace, uncommitted. The final APK is FitAdvisor.Mobile/bin/Debug/net10.0-android/com.stavros.fitadvisor-Signed.apk (86,361,668 bytes). No device installation was attempted.

Build/run/install commands and the phone smoke-test sequence are in [android-build-and-run.md](android-build-and-run.md). No Mi Band, Withings, cloud, barcode or GPS work was implemented. Running and gym-set logging are next.

## Official sources reviewed
- Microsoft MAUI supported platforms: https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0
- Android Health Connect data types/permissions: https://developer.android.com/health-and-fitness/health-connect/data-types
- Apple HealthKit authorization: https://developer.apple.com/documentation/healthkit/authorizing-access-to-health-data
- Mifflin original publication: https://pubmed.ncbi.nlm.nih.gov/2305711/
- Deurenberg original publication (adult prediction standard error reported as 4.1 percentage points): https://pubmed.ncbi.nlm.nih.gov/2043597/
- Apple enrollment fees: https://developer.apple.com/help/account/membership/program-enrollment/
- Google registration fee: https://support.google.com/googleplay/android-developer/answer/6112435
