# Food catalogue history audit — 2026-10-13

Investigated before habit implementation, from clean checkout `06c7ccd` at `D:\repos\FitAdvisor`.

- Local branches: `master`; remote-tracking branches: `origin/master`. `git ls-remote --heads origin` confirms only remote `master`, also at `06c7ccd`.
- `git log --all`, reflog, and `git show --stat 4574a90` show that commit `4574a909ddf97f2361760e2a005d5c7969c24bd9`, despite its title “Add offline food search with Greek aliases and portion calculations”, changed **only** `docs/food-search-progress.md` (13 lines).
- That document explicitly reports no implementation at baseline `2f21791`. Its title in Git is misleading; it is not evidence that catalogue code was committed.
- Current tracked tree and meal UI use manually entered nutrition. No catalogue service, database, aliases, portion calculator, or search UI is present.
- `git fsck --full --no-reflogs --unreachable` found one unreachable commit, `3285ba55b5a34964889d91fcc25eef9f118389bb`: the pre-rebase Android baseline. Inspection of its changed files and the three unreachable blobs found baseline web registration/navigation and a README merge-conflict draft, not food search.
- The existing ignored `artifacts/usda-sr-legacy-2018-04.zip` (2,342,638 bytes) is preserved untouched. The original audit document is preserved unchanged. No branches, Git objects, archives or historical source were removed, rewritten or replaced.

Conclusion: no previous food catalogue implementation was recoverable from this checkout's available history, objects or remote branches. A separate uncommitted copy or another repository would require further evidence. Food search remains absent; this increment does not silently recreate it.

The repository move does not explain absence from the tracked Git tree. It did leave copied Android build intermediates with old `C:\Users\stavr\source\repos\FitAdvisor` paths; package verification therefore requires a full rebuild from `D:`. Android SDK and AVD are now at `D:\Android\Sdk` and `D:\Android\avd`.
