# Habits & Recovery — 0.3.0

Implemented in stages: local records/dashboard and automated storage/calculation checks first; then source-reviewed, bundled education. Existing food-search investigation is in [food-search-history-audit.md](food-search-history-audit.md).

All three trackers and educational cards default to off. Enable them on Habits & Recovery (Home or Menu). The existing Settings message-language choice also selects Greek/English habit wording. Turning a tracker off retains its data. New controls reuse the existing themed cards and buttons, explicit field labels, 52 dp minimum controls, wrapping text and scrolling layouts. History is paginated in groups of 20.

Smoking stores one editable total per local calendar day, optional craving 0–10 and trigger notes. Zero must be explicitly logged. A quit date may be historical or planned. Estimated savings are `sum(max(0, baseline cigarettes/day − logged cigarettes) × user-entered cost/cigarette)` on logged dates from the quit date. Unlogged days contribute nothing; a lapse does not erase earlier smoke-free days or savings. Baseline/cost edits recalculate this estimate; it is not a bank balance or historical price ledger.

Alcohol records beverage type, volume in ml, ABV percentage and optional notes. Ethanol grams are `ml × ABV/100 × 0.789`; 500 ml at 5% = 19.725 g. This is an approximate mass calculation, not a generic drink count, blood-alcohol estimate, driving clearance or safe-consumption threshold.

Eating goals are user-defined, with a fixed unit (e.g. occasions, portions, ml) and an at-most/at-least weekly target. A goal's unit cannot change after creation, avoiding reinterpretation of history. Record amounts refer to that unit. Delete a goal only after its linked records are deleted or reassigned through editing. Existing food-diary calories and targets are independent of habits.

Four Monday–Sunday weekly totals show trends and personal goal progress. The current week is partial; missing dates remain unknown. No streak resets, automatic diagnoses, calorie penalties, risk predictions or food moral ratings are applied.

Schema v3 adds habit records, goals and settings transactionally to v1/v2 diaries without changing existing records. Parameterized local SQLite operations use the existing background worker/gate. Backup format v2 extends the existing authenticated, passphrase-encrypted envelope; exports include habit records, goals and settings in the same snapshot. Restores validate IDs, amounts, dates, unique smoking days and goal references before atomic replacement. Legacy v1 backups are readable; they restore no habits and disabled trackers. The restore screen explains replacement and previews record counts. Older app versions reject v2 backups. No permissions or network calls were added; reference URLs are displayed as text and do not automatically open or fetch anything.

## Education review

Reviewed against live sources on 2026-10-13. This is an implementation/source review, not an independent clinical sign-off. Every optional card includes a source title, URL and publication/update date, or explicitly says the source has no displayed date. Both language versions use original summaries and supportive wording. Sources:

- [WHO — Tobacco and nicotine](https://www.who.int/news-room/fact-sheets/detail/tobacco), 2026-06-26: established health effects and cessation support.
- [US NCI Smokefree — How Smoking Affects Your Workout](https://smokefree.gov/quit-smoking/why-you-should-quit/how-smoking-affects-your-workout), no date displayed: oxygen delivery, lungs, endurance and manageable activity for cravings.
- [Smoking and incidence of insomnia: systematic review of cohort studies](https://pubmed.ncbi.nlm.nih.gov/34507139/), 2021: association, observational limits, no individual prediction.
- [WHO — Alcohol](https://www.who.int/news-room/fact-sheets/detail/alcohol), 2024-06-28: established effects, amount, pattern and context; personal goals are not safe limits.
- [The effect of alcohol on subsequent sleep in healthy adults: systematic review and meta-analysis](https://pubmed.ncbi.nlm.nih.gov/39631226/), April 2025 issue: experimental REM findings, dose/timing, no sleep forecast.
- [The Effects of Alcohol Consumption on Recovery Following Resistance Exercise: A Systematic Review](https://pmc.ncbi.nlm.nih.gov/articles/PMC7739274/), 2019-06-26: small acute studies, often high doses, limited protein-synthesis findings, inconsistent functional outcomes and uncertain long-term implications.
- [NHS — Alcohol-use disorder](https://www.nhs.uk/conditions/alcohol-use-disorder/), reviewed 2026-04-24: professional help before reducing/stopping when dependence/withdrawal is possible; emergency guidance for severe withdrawal. No self-detox regimen.
- [WHO — Healthy diet](https://www.who.int/news-room/fact-sheets/detail/healthy-diet), 2026-01-26: adequacy, variety, portions and frequency in overall dietary patterns.

No precise performance loss, recovery deadline or individual risk estimate is claimed. The app does not diagnose illness or treat addiction. Cards can be hidden independently of tracking.

## Verification and limitations

See `artifacts/habits-checks.log`, `habits-offline-regression.log`, `habits-persistence-regression.log`, `habits-calculation-regression.log` and the Android build logs. Automated checks cover grams, savings after lapse, missing days, date/week/year boundaries, invalid inputs, CRUD, disabling/reopening, schema upgrade, Greek-locale serialization, authenticated backup roundtrip, legacy restore and atomic restore failure. Emulator results and final APK identity are recorded below after verification.

Remaining limitations: four-week totals are textual; unlogged days cannot establish goal achievement. Smoking supports cigarettes, not vape/nicotine conversions. Craving is a subjective daily score. There is no historical pricing ledger, notification/reminder, addiction treatment or individual performance prediction. Reference pages may change after the review date. Some inherited shell/backup labels and validation detail remain English. Real-device and ARM64 runtime acceptance are pending; do not uninstall or clear storage to resolve a signing mismatch.

## Verified artifacts — 2026-10-13

- Final signed Release ARM64 APK: `artifacts/FitAdvisor-habits-arm64.apk` (19,446,163 bytes), version 0.3.0 / code 3. SHA-256: `92B59D85A61EE2B76DDDC37676C170CCA6ABF5D2B6B5BF2B5050E4E07631E90E`.
- Full rebuild from `D:` succeeded with zero warnings/errors: `artifacts/habits-release-final.log`. v1/v2/v3 APK signature verification passed: `artifacts/habits-signature-final.log`. Existing development certificate retained, SHA-256 `182049d70b00e02274148eb20c103d49d539c37b29372aef240def3d03dfae70`. This is a development-signed Release build, not a production/Play signing identity.
- APK badging verifies `arm64-v8a` only. Permissions remain the existing Health Connect exercise read and signature-only internal receiver permission; no Internet or new habit permissions. See `artifacts/habits-apk-badging.txt`.
- First dashboard build, before educational content: zero warnings/errors (`habits-stage1-build.log`). Final standalone x86_64 Debug rebuild: zero warnings/errors (`habits-emulator-rebuild.log`). A stale initial debug APK failed with missing fast-deployment assemblies after the disk move; the full rebuild resolved launch. Neither uninstall nor storage clear was used.
- Habit checks, baseline calculation checks, persistence checks, and offline backup/workout regressions passed. Additional habit checks verify leap dates, future quit dates, negative savings clamp, smoking edit collisions, malformed/duplicate backup entries, and v3 migration rollback without losing existing meals.

## Emulator results

Existing API 36.1 x86_64 AVD at `D:\Android\avd\Medium_Phone.avd` ran with `-read-only -no-snapshot`; its saved disk is preserved. The rebuilt x86_64 Debug APK was installed using `install -r`. Results:

- Launch, Home entry and menu navigation passed; trackers/cards initially disabled.
- Alcohol form displayed ml/ABV labels, source-independent grams preview and explanatory formula. Synthetic 500 ml at 5% previewed/saved 19.73 g; editing to 330 ml yielded 13.02 g. History retained date, volume, ABV and calculated grams without duplicating the entry.
- After force-stop/relaunch, 13.02 g and opt-in/education settings remained. Deletion required confirmation and removed only the new synthetic alcohol record. Education hiding is independent of trackers.
- Alcohol cards displayed evidence qualifiers, source title/date/URL, sleep/recovery limits and professional/emergency alcohol-withdrawal support.
- Greek/dark dashboard rendered correctly. At 320×568 dp and 200% font scale, body headings, supportive text, tracker controls, quit-date and baseline/cost labels wrapped and were reachable by scrolling. Toolbar title ellipsizes; full title appears in the page. Display overrides were reset after the check.
- Eating goal creation passed with a synthetic user-defined goal (`Snacks`, `times`, at most 2/week); its linked record updated current progress to 1/2. Greek goal/unit/direction fields and edit/delete controls were inspected. Android's stylus tutorial briefly intercepted test input; dismissing it resolved that issue.
- Screen-reader field descriptions and switch states appear in the UI hierarchy. TalkBack interaction was not tested. Windows capture failed because the emulator window crop was outside the captured monitor; Android screenshots/hierarchy were used instead.

Evidence screenshots: `artifacts/habits-default.png`, `habits-alcohol-preview.png`, `habits-history.png`, `habits-greek-dark.png`, `habits-small-greek-dark.png`, `habits-small-controls.png`, `habits-small-cost.png`, and `habits-eating-progress.png`. No real-device testing is claimed. ARM64 runtime/trimmed-release behavior remains for phone acceptance; emulator runtime coverage used standalone Debug x86_64. Android document-provider export/restore UI and complete smoking CRUD were not re-exercised end-to-end in this emulator run; the habit storage/calculation/backup suites cover those data paths.
