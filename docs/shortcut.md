# Tracking work in Shortcut

Workspace: [siraraone](https://app.shortcut.com/siraraone) · team "Sirara One's Team" · workflow "Standard".

`graph_loop_template/state/graph_state.json` stays the source of truth for the build
loop; Shortcut mirrors it so progress and effort are visible.

## Structure

- **Epics:** Ledger (S01–S06) · Authentication & authorization (S07–S08) ·
  Maker-checker (S09–S12) · Platform & extraction readiness (S13 + platform chores).
- **One story per slice**, named `Sxx · <name>`, label `slice`. The description holds
  the slice's build; the **tasks are the definition of done**, one checkbox per
  condition from `context/slice-plan.md`.
- **Dependencies** from `graph_state.json` `depends_on` are "blocks" relations.
- **Carried-forward items** from `state/scratchpad.md` are chores labelled
  `carried-forward`, related to the slice they are due in.
- **Estimates** use the workspace scale (1, 2, 4, 8): slices 2–8, chores 1–2.

| Slice | Story | Slice | Story |
|---|---|---|---|
| S01 | sc-37 | S08 | sc-44 |
| S02 | sc-38 | S09 | sc-45 |
| S03 | sc-39 | S10 | sc-46 |
| S04 | sc-40 | S11 | sc-47 |
| S05 | sc-41 | S12 | sc-48 |
| S06 | sc-42 | S13 | sc-49 |
| S07 | sc-43 | | |

Carried-forward chores: sc-104 (statement order by `sequence_number`), sc-105
(direction-enum drift test), sc-106 (line-to-chart-node coherence), sc-107 (E5 as a
real gate).

## Workflow states

Backlog → To Do (next up) → In Progress (being built) → In Review (PR open) →
Done (integrated, every DoD task ticked).

## GitHub link

1. In Shortcut: Settings → Integrations → GitHub → install on
   `victorotene80/sirara-cba-api`.
2. Name branches `sc-<id>/<slug>`, e.g. `sc-40/post-journal-entry`. Shortcut
   attaches the branch and moves the story to In Progress.
3. Put `[sc-<id>]` in commit messages and the PR title. Opening the PR moves the
   story to In Review; merging moves it to Done (set in the integration's workflow
   settings).

## After each slice

- Tick each DoD task as its test goes green.
- Comment on the story: tests added / total, retries, mutation-check results,
  decisions recorded, commit or PR.
- Move to Done only after integration. New carried-forward items become
  `carried-forward` chores.
