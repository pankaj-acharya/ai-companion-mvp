#!/usr/bin/env python3
"""Create GitHub issues from a CSV file.

This script is intended to run inside a GitHub Actions workflow using the
built-in ``GITHUB_TOKEN``. The only required human input is the CSV file
itself; committing it to the watched path triggers issue creation.

CSV columns (header row required):
    title       (required) - the issue title
    body        (optional) - the issue body/description
    labels      (optional) - comma or semicolon separated label names
    assignees   (optional) - comma or semicolon separated usernames
    milestone   (optional) - milestone title (created if it does not exist)

Behaviour:
    * Empty rows are skipped; leading/trailing whitespace is trimmed.
    * Rows without a title are reported as errors and skipped.
    * Missing labels and milestones are created automatically.
    * Existing open/closed issues with the same title are skipped so re-runs
      are idempotent and do not create duplicates.
    * One failing row does not abort the batch; a summary is printed and
      written to the GitHub Actions job summary.
"""

from __future__ import annotations

import csv
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from typing import Any


API_ROOT = os.environ.get("GITHUB_API_URL", "https://api.github.com")
MAX_RETRIES = 3
RETRY_BACKOFF_SECONDS = 2


class GitHubClient:
    """Minimal GitHub REST client using only the standard library."""

    def __init__(self, token: str, repo: str) -> None:
        if not token:
            raise ValueError("A GitHub token is required (set GITHUB_TOKEN).")
        if "/" not in repo:
            raise ValueError("Repository must be in 'owner/name' format.")
        self.token = token
        self.repo = repo

    def _auth_header(self) -> str:
        return "Bearer " + self.token

    def _request(
        self,
        method: str,
        path: str,
        payload: dict[str, Any] | None = None,
    ) -> Any:
        url = path if path.startswith("http") else f"{API_ROOT}{path}"
        data = json.dumps(payload).encode("utf-8") if payload is not None else None
        last_error: Exception | None = None

        for attempt in range(1, MAX_RETRIES + 1):
            request = urllib.request.Request(url, data=data, method=method)
            request.add_header("Authorization", self._auth_header())
            request.add_header("Accept", "application/vnd.github+json")
            request.add_header("X-GitHub-Api-Version", "2022-11-28")
            request.add_header("User-Agent", "csv-to-issues-script")
            if data is not None:
                request.add_header("Content-Type", "application/json")

            try:
                with urllib.request.urlopen(request) as response:
                    body = response.read().decode("utf-8")
                    return json.loads(body) if body else None
            except urllib.error.HTTPError as error:
                # Retry on rate limiting and transient server errors.
                if error.code in (403, 429) or 500 <= error.code < 600:
                    reset = error.headers.get("X-RateLimit-Reset")
                    remaining = error.headers.get("X-RateLimit-Remaining")
                    if remaining == "0" and reset:
                        wait = max(0, int(reset) - int(time.time())) + 1
                    else:
                        wait = RETRY_BACKOFF_SECONDS * attempt
                    last_error = error
                    if attempt < MAX_RETRIES:
                        time.sleep(wait)
                        continue
                detail = error.read().decode("utf-8", errors="replace")
                raise RuntimeError(
                    f"GitHub API {method} {url} failed: {error.code} {detail}"
                ) from error
            except urllib.error.URLError as error:
                last_error = error
                if attempt < MAX_RETRIES:
                    time.sleep(RETRY_BACKOFF_SECONDS * attempt)
                    continue
                raise RuntimeError(
                    f"GitHub API {method} {url} failed: {error}"
                ) from error

        raise RuntimeError(f"GitHub API {method} {url} failed: {last_error}")

    def _paginated(self, path: str) -> list[Any]:
        results: list[Any] = []
        url: str | None = f"{API_ROOT}{path}"
        while url:
            request = urllib.request.Request(url, method="GET")
            request.add_header("Authorization", self._auth_header())
            request.add_header("Accept", "application/vnd.github+json")
            request.add_header("X-GitHub-Api-Version", "2022-11-28")
            request.add_header("User-Agent", "csv-to-issues-script")
            with urllib.request.urlopen(request) as response:
                body = response.read().decode("utf-8")
                results.extend(json.loads(body) if body else [])
                url = _next_link(response.headers.get("Link"))
        return results

    def existing_issue_titles(self) -> set[str]:
        issues = self._paginated(
            f"/repos/{self.repo}/issues?state=all&per_page=100"
        )
        # Pull requests are also returned by the issues endpoint; exclude them.
        return {
            issue["title"]
            for issue in issues
            if "pull_request" not in issue
        }

    def existing_labels(self) -> set[str]:
        labels = self._paginated(f"/repos/{self.repo}/labels?per_page=100")
        return {label["name"] for label in labels}

    def create_label(self, name: str) -> None:
        self._request(
            "POST",
            f"/repos/{self.repo}/labels",
            {"name": name, "color": "ededed"},
        )

    def milestones_by_title(self) -> dict[str, int]:
        milestones = self._paginated(
            f"/repos/{self.repo}/milestones?state=all&per_page=100"
        )
        return {m["title"]: m["number"] for m in milestones}

    def create_milestone(self, title: str) -> int:
        result = self._request(
            "POST",
            f"/repos/{self.repo}/milestones",
            {"title": title},
        )
        return result["number"]

    def create_issue(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", f"/repos/{self.repo}/issues", payload)


def _next_link(link_header: str | None) -> str | None:
    if not link_header:
        return None
    for part in link_header.split(","):
        section = part.split(";")
        if len(section) < 2:
            continue
        url = section[0].strip().strip("<>")
        for param in section[1:]:
            if param.strip() == 'rel="next"':
                return url
    return None


def _split_multi(value: str) -> list[str]:
    if not value:
        return []
    normalized = value.replace(";", ",")
    return [item.strip() for item in normalized.split(",") if item.strip()]


def parse_rows(csv_path: str) -> list[dict[str, str]]:
    with open(csv_path, newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames is None:
            raise ValueError(f"CSV file '{csv_path}' has no header row.")
        normalized_fields = {
            (name or "").strip().lower() for name in reader.fieldnames
        }
        if "title" not in normalized_fields:
            raise ValueError(
                f"CSV file '{csv_path}' must contain a 'title' column. "
                f"Found columns: {sorted(normalized_fields)}"
            )
        rows: list[dict[str, str]] = []
        for row in reader:
            cleaned = {
                (key or "").strip().lower(): (value or "").strip()
                for key, value in row.items()
            }
            rows.append(cleaned)
        return rows


def write_summary(lines: list[str]) -> None:
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    text = "\n".join(lines)
    print(text)
    if summary_path:
        with open(summary_path, "a", encoding="utf-8") as handle:
            handle.write(text + "\n")


def main() -> int:
    csv_path = os.environ.get("CSV_PATH", "issues/issues.csv")
    repo = os.environ.get("GITHUB_REPOSITORY", "")
    token = os.environ.get("GITHUB_TOKEN", "")

    if not os.path.isfile(csv_path):
        print(f"::error::CSV file not found: {csv_path}")
        return 1

    try:
        client = GitHubClient(token=token, repo=repo)
        rows = parse_rows(csv_path)
    except (ValueError, RuntimeError) as error:
        print(f"::error::{error}")
        return 1

    existing_titles = client.existing_issue_titles()
    existing_labels = client.existing_labels()
    milestone_lookup = client.milestones_by_title()

    created: list[str] = []
    skipped: list[str] = []
    failed: list[str] = []

    for index, row in enumerate(rows, start=2):  # row 1 is the header
        if not any(row.values()):
            continue  # blank row

        title = row.get("title", "")
        if not title:
            failed.append(f"Row {index}: missing required 'title'")
            print(f"::error::Row {index}: missing required 'title'")
            continue

        if title in existing_titles:
            skipped.append(f"'{title}' (already exists)")
            continue

        payload: dict[str, Any] = {"title": title}

        body = row.get("body", "")
        if body:
            payload["body"] = body

        labels = _split_multi(row.get("labels", ""))
        if labels:
            for label in labels:
                if label not in existing_labels:
                    try:
                        client.create_label(label)
                        existing_labels.add(label)
                    except RuntimeError as error:
                        print(f"::warning::Could not create label '{label}': {error}")
            payload["labels"] = labels

        assignees = _split_multi(row.get("assignees", ""))
        if assignees:
            payload["assignees"] = assignees

        milestone_title = row.get("milestone", "")
        if milestone_title:
            if milestone_title not in milestone_lookup:
                try:
                    number = client.create_milestone(milestone_title)
                    milestone_lookup[milestone_title] = number
                except RuntimeError as error:
                    print(
                        f"::warning::Could not create milestone "
                        f"'{milestone_title}': {error}"
                    )
            if milestone_title in milestone_lookup:
                payload["milestone"] = milestone_lookup[milestone_title]

        try:
            issue = client.create_issue(payload)
            existing_titles.add(title)
            created.append(f"#{issue['number']} {title}")
        except RuntimeError as error:
            failed.append(f"Row {index}: '{title}' -> {error}")
            print(f"::error::Row {index}: failed to create '{title}': {error}")

    summary = ["## CSV → Issues summary", ""]
    summary.append(f"- **Created:** {len(created)}")
    for item in created:
        summary.append(f"  - {item}")
    summary.append(f"- **Skipped:** {len(skipped)}")
    for item in skipped:
        summary.append(f"  - {item}")
    summary.append(f"- **Failed:** {len(failed)}")
    for item in failed:
        summary.append(f"  - {item}")
    write_summary(summary)

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
