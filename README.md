# ai-companion-mvp
AI Companion MVP – A personalized AI companion with memory, multi-modal interaction, and real-time conversation

## Creating GitHub issues from a CSV file

This repository can create GitHub issues automatically from a CSV file — the
CSV is the only input required. Commit or update a CSV file under the
`issues/` directory and a GitHub Actions workflow
([`.github/workflows/create-issues.yml`](.github/workflows/create-issues.yml))
runs [`.github/scripts/create_issues.py`](.github/scripts/create_issues.py) to
create the issues. No secrets or manual setup are needed; the workflow uses the
built-in `GITHUB_TOKEN`.

### CSV format

The first row must be a header. Columns:

| Column      | Required | Description                                              |
|-------------|----------|----------------------------------------------------------|
| `title`     | Yes      | Issue title.                                             |
| `body`      | No       | Issue description.                                       |
| `labels`    | No       | Comma or semicolon separated label names.                |
| `assignees` | No       | Comma or semicolon separated GitHub usernames.           |
| `milestone` | No       | Milestone title.                                         |

Example (`issues/issues.csv`):

```csv
title,body,labels,assignees,milestone
Set up project skeleton,Create the initial folder structure.,"enhancement;setup",,MVP
Fix audio playback glitch,Audio stutters during conversation.,bug,,
```

### Behaviour

- Empty rows are skipped and surrounding whitespace is trimmed.
- Rows missing a `title` are reported and skipped.
- Referenced labels and milestones are created automatically if they do not
  already exist.
- Issues whose title already exists (open or closed) are skipped, so re-running
  the workflow does not create duplicates.
- A per-run summary (created / skipped / failed) is written to the workflow
  logs and the Actions job summary.

### Triggering

- **Automatic:** push a change to any `issues/**.csv` file.
- **Manual:** run the *Create issues from CSV* workflow via
  **Actions → Run workflow**, optionally overriding the CSV path.

