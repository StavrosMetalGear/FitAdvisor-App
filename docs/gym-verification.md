# Gym Session verification — 2026-10-14

The work remains uncommitted; nothing was pushed. See [behavior, attribution, licences and phone checklist](gym-sessions.md).

Core/storage verification: **50 checks passed** in Release with reflection-based JSON serialization disabled. Tests cover barbell and paired-dumbbell calculations; warm-up/pending exclusions; bodyweight, assistance, added load, duration and distance field contracts; nonfinite/irrelevant field rejection; timestamp pause/resume/expiry/extension; database reopen recovery; one active session; undo set completion; idempotent finish and one workout; equipment/convention/load-specific records; lower assistance; bilingual/accent search; exact technique variation fallback; explicit link/unlink and source-update compatibility; duplicate reference rejection; legacy edit/delete protection; undo finish; custom exercises/preferences/active timer encrypted roundtrip; plan replacement/restoration; schema-v4 additive migration; backup formats 1–4.

Existing regressions passed: offline workouts/Health Connect import identity and date-range/DST logic, authenticated encrypted backup errors and atomic rollback, planned-workout CRUD/totals and calorie isolation; habits; separate-process diary persistence; food catalogue comparisons against all 7,793 source records plus food snapshot migration/backup and macro calculations. The first catalogue invocation omitted its archive argument, then pointed to the incomplete original archive; the corrected run used `artifacts/usda-sr-legacy-2018-04-complete.zip` and passed. No source foods were changed.

Release builds for android-x64 and android-arm64 both passed with zero warnings and zero errors. The emulator uses Pixel_Phone, Android 36, x86_64. Its existing app was upgraded in place repeatedly without uninstalling or clearing data. ARM64 execution on a physical phone was not performed.

Release emulator observations:

- Home/menu navigation, Greek/dark and English/light gym controls worked. The catalogue picker wraps both languages and shows the exact equipment variation.
- Logged 8 reps at 50 kg total barbell load; one completed working set and 400 kg·reps volume were shown.
- Paused session time stayed at 00:05:06 through navigation, lock/reopen, force-stop and in-place upgrades. The completed set and rest preferences recovered.
- A running 90-second rest showed 58 seconds after lock/reopen. Paused at 53 seconds; force-stop/relaunch recovered 53 seconds. +30 showed 83 seconds; skip cleared it.
- Bench-press technique displayed the unavailable-guidance message. Greek and English floor push-up guidance, original illustration, source attribution, optional video button and return control rendered. Returning retained the completed set.
- At 200% system font scale, English/light guidance reflowed and scrolled to source/video/return buttons; the original 1.0 font scale was restored. This is visual/XML inspection, not a TalkBack session.
- Finish review showed completed set counts, 400 kg·reps and exclusion of pending sets. Finishing showed one completed workout in Workouts with detailed-set navigation. Opening that entry showed the saved sets; undo finish confirmed and resumed the same session.
- The resumed running session survived a further in-place update and recovered at 00:07:23 with its set intact. It was finished again using the same session identity.
- After refinish, final Workouts UI showed exactly one completed workout and 7.8 minutes, with manual strength logging and unavailable heart-rate/calorie labels. Screenshot/XML: `artifacts/gym-final-workout-totals.*`.

The timer display was changed to a two-second refresh and stops during action dialogs so Android UI inspection and dialog accessibility can reach a stable state. Timer accuracy still comes from stored timestamps, not UI ticks. The automated/UI checks do not prove physical vibration, audio, DND, OS process-kill policies, a phone reboot, or scheduled background alerts. Scheduled/background alerts are not implemented.

Imported linking tests used synthetic completed Health Connect strength fixtures in Core/Storage. A real Mi Band export/link was not exercised in the emulator. No live monitoring, heart-rate or calorie readings were implemented or claimed. Native document-picker export/restore was not repeated in this increment; encrypted roundtrip, validation and transactional restore were exercised in Core/Storage.

Delivery APK: `artifacts/FitAdvisor-0.5.0-gym-arm64.apk`, application version 0.5.0 / code 7. It is signed with the existing Android development certificate, not a newly configured production keystore. `apksigner` verifies v1/v2/v3 signatures; the certificate SHA-256 matches previous builds: `182049d70b00e02274148eb20c103d49d539c37b29372aef240def3d03dfae70`. Inspection confirms arm64-v8a, the existing Exercise/history permissions, normal VIBRATE permission, and no INTERNET or POST_NOTIFICATIONS permission. Android automatic backup remains disabled.

Final APK size: 20,098,304 bytes. SHA-256: `632E6360B63B211C874782973B91F97318493240705C89D753844E119339B1D3`.

Logs and emulator screenshots/XML are under `artifacts/gym-*`. Principal logs: `gym-checks.log`, `gym-offline-regression.log`, `gym-release-x64.log`, `gym-release-arm64.log`, `gym-signature.log`, and `gym-apk-badging.txt`.
