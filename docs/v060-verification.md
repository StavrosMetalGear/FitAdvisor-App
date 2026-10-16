# 0.6.0 implementation and verification

Date: 2026-10-16. No commit or push was made. Physical-phone, Mi Fitness sync/export and ARM64 phone-runtime acceptance remain pending.

## Deliverable

`artifacts/FitAdvisor-0.6.0-arm64.apk`, 22,367,157 bytes.

SHA-256: `5C9093E5AC240C9F2422AA67A0B542D8098333FEE6A7A37C4B063244D54D4C7B`.

Package `com.stavros.fitadvisor`, versionName `0.6.0`, versionCode `8`, ARM64 only. Release builds for x64 emulator and ARM64 finished with zero warnings/errors. APK v1/v2/v3 signatures verify. Signer SHA-256 `182049d70b00e02274148eb20c103d49d539c37b29372aef240def3d03dfae70` matches the existing v0.5.0 signing identity. It is the existing Android Debug certificate, not a newly provisioned production certificate. Update installation preserves app identity/storage.

Manifest additions: INTERNET, READ_HEART_RATE and READ_ACTIVE_CALORIES_BURNED. Existing READ_EXERCISE, optional READ_HEALTH_DATA_HISTORY and VIBRATE remain. No camera, location, notification, total-energy or Health Connect write permission was added. Automatic Android backup remains disabled.

## Implemented behavior

- Search/favorites/recent foods lead food entry; manual totals and reusable package-label forms are separate pages. Local suggestions normalize Greek accents/case/final sigma and offer spelling matches; selection is explicit. Selecting a food focuses the portion/preview view. Macro targets/details are collapsed.
- Explicit OFF brand/name and barcode lookup, conservative request gating, missing/questionable nutrition handling, distinct source identities, g/ml basis and frozen snapshots. Selected products/favorites/custom labels are backed up. No network request is made while typing.
- One active exercise card, expandable history/edit/reorder controls, persisted sessions, reusable editable routines, repeat previous workout with fresh identities and pending suggestions, optional anchored subjective effort, and parent-first hardware Back navigation.
- 18 offline source-checked photo guides with Greek/English cues and accessible descriptions. Unsupported exact variants are labeled unavailable. See review scope and licensing in the source notes.
- Optional post-sync exact-source/time sensor snapshots after explicit import linking. Missing values remain unavailable; source-reported active energy is separate from totals and food allowance. Xiaomi training-effect scores are not supplied.

## Automated checks — passed

All suites ran in Release with reflection-based JSON disabled where applicable. Reproduce with `powershell -File verification/verify-feedback.ps1` from the repository root and a complete official USDA archive. The old truncated archive is preserved; use the `-complete.zip` file.

| Suite | Evidence / coverage |
|---|---|
| Calculations | `artifacts/v060-FitAdvisor.CalculationChecks.log` — existing targets/calculations |
| Persistence | `artifacts/v060-FitAdvisor.PersistenceChecks.log` — separate-process reopen, CRUD, targets/revisions, future-schema refusal, migration rollback |
| Offline | `artifacts/v060-FitAdvisor.OfflineChecks.log` — authenticated backup, wrong password/tamper, full history, plans/import deduplication, calorie isolation, dates/DST |
| Habits | `artifacts/v060-FitAdvisor.HabitChecks.log` — history, settings, legacy migration and encrypted roundtrip |
| Catalogue | `artifacts/v060-FitAdvisor.CatalogueChecks.log` — every one of 7,793 USDA records/nutrients compared with original JSON; portion math, aliases, snapshots and migration |
| Gym | `artifacts/v060-FitAdvisor.GymChecks.log` — 50 checks, timing/recovery, equipment conventions, idempotent finish, linking, historical sets and legacy encrypted formats 1–4 |
| Phone feedback | `artifacts/v060-feedback-checks-final.log` — 50 checks, real v5→v6 migration, unchanged historical meals/sets, old v5 and new v6 encrypted backups, OFF/custom/ml portions, missing/flagged nutrition, real staging response, routine resets, sensor source/time/energy deduplication, effort and rate-limit cooldown |
| Design | `artifacts/v060-FitAdvisor.DesignChecks.log` — 400 days of bilingual message/rollover checks |

Schema 6 is additive: two new payload tables (`saved_foods`, `gym_routines`); no historical meal/workout table rewrite. New fields on gym/snapshot JSON have defaults. Backup format 6 includes additions; formats 1–5 remain readable. AES-GCM/PBKDF2 envelope and passphrase rules are unchanged. Legacy migration test fixtures were updated to remove the new tables before intentionally simulating old schemas. `git diff --check` passed (only repository line-ending notices).

## Release emulator checks — observed

Android API 36.1 emulator `emulator-5554`, normal 1080×2400 / density 420; small 720×1280 / density 320; font_scale 1.0 and 2.0; light and dark. Emulator display/font/night settings were restored after checks. No data clear or uninstall was used.

- Installed v0.5.0 over the emulator's older app, created a saved active session, then updated with the Release x64 build. The session recovered. Logged one bench set (8 reps × 40 kg), viewed offline technique photos and returned without losing it.
- Entered local food text and observed explicit suggestions. Preview changed immediately from 100 g oats / 379 kcal to 50 g / 189.5 kcal. Saved one meal; diary showed one entry / 189.5 kcal. Favorite and meal survived subsequent APK update/reopen.
- Numeric keyboard left the normal-size portion preview/save readable. At 200% on the small display, button labels initially clipped; the shared styles were fixed and full wrapped labels visually rechecked. Long food descriptions with the enlarged keyboard require scrolling or keyboard dismissal; this is not a claim that every long suggestion fits simultaneously above that keyboard.
- Saved a routine, opened its editor, finished/reviewed the session with effort 7/10, then repeated it. The new timer began at 00:00:00, the 8×40 suggestion was pending, and working volume was zero. Force-stop/relaunch recovered that pending session.
- Combined Chest and Dumbbells filters produced one matching variation with both filters visible. Clear filters restored the catalogue. No exercise was silently selected.
- Inspected technique image descriptions in the accessibility tree. Hardware Back from a secondary page returned to its parent. TalkBack spoken output, switch access and physical touch ergonomics were not tested.

## Screenshots

- [Food diary, 200% dark](../artifacts/gym-v060-food-small200-dark-final.png)
- [Local favorites/search, 200%](../artifacts/gym-v060-search-small200-final.png)
- [Portion preview with numeric keyboard](../artifacts/gym-v060-portion-keyboard-final.png)
- [Recovered pending gym session](../artifacts/gym-v060-recovery-pending-final.png)
- [Completed gym session and effort](../artifacts/gym-v060-finished.png)
- [Repeat: pending suggestions and reset timer](../artifacts/gym-v060-repeat-pending.png)
- [Combined exercise filters](../artifacts/gym-v060-combined-filters.png)
- [Routine editor](../artifacts/gym-v060-routine-editor.png)
- [Offline technique setup](../artifacts/gym-v060-guide-final.png)
- [Small-screen gym controls, 200% light](../artifacts/gym-v060-gym-small200-light.png)

## Remaining acceptance checks and limitations

Native sensor permission denial/revocation, actual Mi Fitness Exercise/heart/active-energy exports, source attribution/coverage and supported phone versions require the actual phone. No successful band export, native sensor read, live monitoring or score-export test is claimed. Synthetic tests cover missing data, origin/time mismatches, partial/duplicate energy and preservation of an existing snapshot on rejected source changes. Native denial paths are implemented but were not exercised with a real linked Mi Fitness record.

Successful Greek branded-product search, availability of the user's exact package/barcode, service timeout/offline UI on the phone and cache reuse after real selection remain acceptance checks. A read-only staging product response was verified; HTTP 429/cooldown was exercised through a mock handler. There is no camera scanner. OFF limits can be stricter for a shared IP and the local gate resets with the process. No qualified trainer/clinical review is claimed; weighted/assisted/custom machine guides remain unavailable. Routines are edited independently of historical sessions. At 200% text, scrolling is necessary.

See [source/licence/privacy notes](v060-sources-and-privacy.md) and the [short phone checklist](v060-phone-checklist.md).
