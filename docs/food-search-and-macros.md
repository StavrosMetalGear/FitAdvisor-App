# Food search and daily macros — 0.4.1

## Diagnosis

Reproduced using the existing signed 0.4.0 Release APK on the Pixel_Phone Android emulator. No physical Pixel was connected. In Food Diary, the Meal or food name field is manual entry: typing chicken produces neither suggestions nor automatic nutrition. The separate catalogue loads chicken records after pressing Search, proving the embedded resource loads in the Release app. Typing into catalogue search alone gives no results; the old TextChanged handler only clears results. Entering 150 g after selecting a record still leaves the preview empty and Save disabled: the old grams handler invalidates the preview, and only the Preview button calculates nutrition. There was no spelling suggestion path for chichen.

## Changes

- Visible bilingual Search foods entry point and explicit manual-entry explanation.
- 300 ms debounce, accent/case normalization, Greek aliases, bounded one-edit spelling suggestions for single words of at least four characters. Suggestions never select a record. Generation checks discard stale responses.
- Results show the original English USDA preparation description, raw/cooked guidance and kcal per 100 g. Greek aliases cover common foods, not every dish; descriptions remain source English.
- Explicit selection opens a 100 g preview. Every valid gram change immediately recalculates all reported nutrients; blank/invalid grams clear the preview and disable Save. Review preview then Save. Missing macros are unavailable, never saved as zero; a record without calories cannot be saved as a catalogue meal.
- Catalogue edits retain the original source snapshot and record ID. Manual entries retain their entered totals and do not scale them by quantity.
- Daily calories and macro energy donut in Food Diary. For complete days, energy is `4 × protein grams + 4 × carbohydrate grams + 9 × fat grams`; each percentage is its contribution divided by that energy. This is a simplified convention. Rounding, fibre, alcohol and source conventions can make this differ from recorded calories.
- Text legend includes grams, energy percentages and color names; the graphic also has a semantic text description. Empty, zero-energy and incomplete days show an explanation instead of a misleading donut. Incomplete days show known gram totals, labeled incomplete, with no percentages or actual/target comparison.
- Optional percentage targets must be finite, within 0–100 and total 100%. Comparison shows actual/target and percentage-point difference. Targets, theme and language are device preferences, separate from encrypted diary backups; clearing targets does not affect diary records.
- Food UI and chart use the Settings food/chart/message language preference. Chart text refreshes when returning to the diary. Native text controls inherit light/dark styling; the chart uses distinguishable colors and text alternatives.

## Preservation and checks

No diary schema, backup format, habit storage, workout storage or import logic changes. Version code increases from 4 to 5. Install over 0.4.0; do not uninstall or clear app storage.

Catalogue verification compares all 7,793 records and nutrients against the USDA source archive, checks Greek/English chicken, accents/case, chichen suggestions, raw/cooked distinctions, portion scaling, missing nutrients, invalid quantities, frozen snapshots, saving/editing, totals, schema-v3 migration and encrypted backup restoration. Added independent 4/4/9 calculation, percentage sum, target validation and empty/incomplete/zero/single-macro cases. Offline, habits and persistence regressions cover older migrations, workouts/import deduplication, full backups and process restart.

Emulator evidence is under artifacts/food-fix-*. The Release UI test selected FDC 169841: 100 g = 297 kcal / 19.6 g protein / 18 g carbohydrates / 16.2 g fat; 150 g = 445.5 / 29.4 / 27 / 24.3; edit to 75 g = 222.75 / 14.7 / 13.5 / 12.15. Macro energy for 150 g is 444.3 kcal; shares are 26.47%, 24.31%, 49.22%. 30/40/30 targets compare as −3.53, −15.69, +19.22 percentage points.

The final Release update retained the 75 g saved entry, 30/40/30 targets and Greek/dark preferences. Adding a synthetic 100 kcal manual meal without macros updated recorded calories to 322.75 and displayed incomplete data, no donut, no percentages, and no actual/target comparison. A loaded empty day displayed zero entries and no chart. Health Connect was exercised through Home, Exercise permission denial and allowed-but-empty import; neither path imported data. No real phone or Mi Band test was performed.

Final signed ARM64 APK: 19,344,889 bytes; SHA-256 `EAB3AA5A11A78551091C51FF434BC9612324BEEFCFAD1BE7F83400DDD88D102D`. Both final Release builds succeeded with zero warnings and errors. Signature verifies with v1/v2/v3 schemes and certificate SHA-256 `182049d70b00e02274148eb20c103d49d539c37b29372aef240def3d03dfae70`, matching 0.4.0. No commit or push was performed.

## Pixel checklist

1. Export an encrypted diary backup, then install artifacts/FitAdvisor-0.4.1-arm64.apk over the existing app. Verify existing entries, habits and workouts.
2. Food Diary → Search foods. Type chicken, ΚΟΤΌΠΟΥΛΟ, κοτοπουλο and chichen; wait briefly for results. Check preparation and kcal/100 g. Confirm no food is selected until you tap Select.
3. Select your exact food; enter 100 then 150 g. Verify all four nutrient totals update immediately. Blank or zero grams must disable saving. Review the preview, Save, reopen Edit and change the weight; verify one updated entry and correct daily totals.
4. Review the daily donut and legend; set targets totaling 100%. Try an invalid total. Add a manual meal with missing macros: chart must show incomplete data. A day with no meals must show no chart. Check Greek/English and light/dark in Settings.
5. Verify Health Connect as below, then re-import the same date range and confirm no duplicates.

## Mi Fitness → Health Connect → FitAdvisor (checked 2026-10-14)

1. Sync the band into Mi Fitness and confirm a recent exercise session appears there. In Mi Fitness: Profile → Third-party data → Health Connect → Connect. Xiaomi documents this path; available data depends on the band and app version: https://www.mi.com/pk/support/faq/details/KA-728033/
2. On Android 14+, open Settings → Security and privacy → Privacy controls → Health Connect (or search Settings for Health Connect). App permissions → Mi Fitness: allow writing Exercise sessions/Exercise where offered. Verify a matching session exists in Health Connect; step data alone is insufficient. Google permission instructions: https://support.google.com/android/answer/12201227?hl=en-GB
3. FitAdvisor Home → Health Connect Import (also available under Workouts). Choose From/Through through today, tap Import and allow Exercise read access. Review the read count/source and confirm the local import. Version 0.4.2 follows Health Connect's first-grant history cutoff and offers optional older-history access; see the [current phone test](health-connect-diagnostics-and-plans.md). FitAdvisor reads exercise sessions on Android 14+, not steps, routes, heart rate or automatic calorie adjustments.
4. If no sessions appear, verify Mi Fitness wrote exercise data and both apps have appropriate permissions. Repeat import updates matching source IDs without duplicates. Do not infer successful Mi Band synchronization from a permission grant or an empty import.

Real Mi Band synchronization remains unverified until actual phone-origin exercise data is imported. The signed APK uses the same existing Android signing certificate as 0.4.0 (Android Debug certificate); it is a Release-configuration sideload artifact, not a Play Store production-signing change.
