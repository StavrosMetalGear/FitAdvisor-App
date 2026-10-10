# MVP 1 closeout — 2026-10-09

Implementation review found no unfinished source requirements within the agreed profile, calorie target, meals, weight history, edit/delete and Today scope. Native acceptance remains pending; this is a development/build closeout, not a device acceptance sign-off. No running/gym features, formula changes or database migrations were added.

| Area | Reviewed behavior |
| --- | --- |
| Profile/targets | Validated kg/cm/adult age/coefficient/multiplier/signed adjustment; retained profile/goal revisions and daily input/formula snapshots. Target weight does not select a deficit. |
| Meals | Dated quantity/unit, total kcal, nullable macro totals; stable-ID edits, re-dating and confirmed deletion. Quantity does not multiply nutrition. |
| Weight | Dated kg/notes, newest-first history, multiple readings per day, edit/delete. Does not alter profile calculation weight. |
| Today | Local date, consumed/target/remaining or over allowance; absent-data wording, navigation and refresh. No exercise credit. Refresh on tab appearance or explicitly; no midnight timer or explicit resume handler is implemented. |
| Storage | Existing app-private fitadvisor.db, schema version 1, transactions, retained revisions and same-day entry context. No reset/uninstall required. |

The broader roadmap's charts, weekly averages, birth-date aging, separate goal models, imports, workouts and export/backup are future scope, not completed MVP features. Profile revisions are retained rather than exposed for deletion because existing entries reference them. No additional calculation/persistence checks were rerun: this closeout changed documentation only. Previously recorded desktop results remain historical evidence, not native lifecycle results.

## Pending phone checklist

Use synthetic records or take care not to delete personal records. Every item below is **pending on the real device**.

1. Save/reopen profile: 80 kg, 180 cm, age 30, male, multiplier 1.2, adjustment -300 => target 1836 kcal. Reject invalid/zero/non-finite values; rapid Save taps must not duplicate revisions. Change today's goal and verify earlier dates retain targets.
2. Meal CRUD: add 80 g oats with 300 **total** kcal and blank macros; read back unknown macros; Today shows 300 consumed/1536 remaining. Edit to 350 => 1486 remaining without a duplicate. Move date and verify both daily lists/totals. Cancel deletion, then delete a test meal and verify totals.
3. Weight CRUD: add two dated readings, verify newest-first order and kg/notes, edit/re-date without duplication, cancel then confirm test-record deletion. Confirm profile calculation weight is unchanged.
4. Dashboard: return after each save/edit/delete, verify target/consumed/remaining and over-allowance wording; test Refresh today and empty diary/profile states.
5. Airplane mode: save profile, meal and weight with networking off; navigate/read/edit without a server or account.
6. Force-stop/reopen and phone restart: retain saved profile, meals, weights and daily targets. Update with the same signed Debug APK using `adb install -r` and verify existing data remains.
7. Date rollover: cross local midnight (or use an emulator with a controlled clock); tap Refresh today/leave and return to Today. Verify new local date, its target and only new-day meals; previous-day entries remain. Check resume after midnight and explicitly select the new date in Meals/Weight: those forms retain their selected date while the app stays open.
8. Greek locale: set Greek (Greece), reopen, enter `80,5` kg, `1,2` multiplier, fractional meal quantity/kcal/macros, save/edit/reopen and verify decimals were not scaled/truncated. Check the keyboard offers a comma and signed adjustment is usable; invalid separators should show validation rather than corrupt data.

**Backup/export/restore is not implemented.** Android automatic backup is disabled. Preserve the current signing key/application ID; do not uninstall or clear storage. A differently signed APK cannot update the installed app in place.

## Proposed next increment — not implemented

After device acceptance, add manual workout CRUD only, in two small slices:

- Runs screen: dated list and add/edit form for distance km, duration minutes/seconds and optional notes; confirm delete. Display pace derived from positive km/seconds. `RunWorkout`: UUID, local date, captured UTC offset, distance km, duration seconds, notes, source `manual`, created/updated UTC timestamps.
- Gym screen: dated session list, session editor and set list/editor. `StrengthWorkout`: UUID, local date, captured UTC offset, notes, created/updated UTC timestamps. `StrengthSet`: UUID, workout foreign key, exercise name snapshot, order, reps, external load kg, warm-up flag, optional notes, timestamps. Start with external-load exercises; bodyweight/assistance modes require an explicit later design.

Use additive transactional schema-v2 tables/indexes, preserve all version-1 records/snapshots, and check upgrade with a copied v1 database before shipping. Keep UUIDs stable on edits. No GPS, integrations, routines, exercise catalog, e1RM, calorie credit or target-policy changes in this increment. Today may gain a count/link to manually logged workouts after the CRUD slices pass. Safe backup/export remains an outstanding follow-up, especially before collecting substantial personal history.
