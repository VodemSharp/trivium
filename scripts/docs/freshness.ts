#!/usr/bin/env bun

/**
 * Reports which guides may be out of date.
 *
 * Every guide under `docs/` declares `last_reviewed` and, where the mapping is tight, `code_globs`.
 * A row is "behind" when the newest commit touching those globs is newer than the review date.
 *
 * The report is advisory: it never fails the build unless `--fail` is passed (or a guide is
 * missing its frontmatter, with `--fail`), because a glob match means "re-read me", not "I am wrong".
 *
 * Exit codes: 0 normally, 1 only with --fail.
 */

import { execFileSync } from "node:child_process";
import { appendFileSync, readFileSync } from "node:fs";
import { relative, resolve } from "node:path";

import { listMarkdownFiles } from "./files";
import { parseFrontmatter } from "./markdown";

const root = resolve(import.meta.dir, "../..");
const failOnStale = process.argv.includes("--fail");
const MillisecondsPerDay = 86_400_000;

interface Report {
    file: string;
    reviewed: string;
    codeChanged: string | null;
    daysBehind: number | null;
}

interface FreshnessReport {
    rows: Report[];
    problems: string[];
}

function isDocsFile(file: string): boolean {
    const firstSegment = relative(root, file).split(/[\\/]/)[0];
    return firstSegment === "docs";
}

function latestCommitSeconds(paths: string[]): number | null {
    try {
        const output = execFileSync("git", ["log", "-1", "--format=%ct", "--", ...paths], {
            cwd: root,
            encoding: "utf8",
            stdio: ["ignore", "pipe", "ignore"],
        }).trim();

        if (output === "") {
            return null;
        }

        return Number(output);
    } catch {
        return null;
    }
}

function isShallowRepository(): boolean {
    try {
        const output = execFileSync("git", ["rev-parse", "--is-shallow-repository"], {
            cwd: root,
            encoding: "utf8",
            stdio: ["ignore", "pipe", "ignore"],
        }).trim();

        return output === "true";
    } catch {
        return false;
    }
}

function formatDate(seconds: number | null): string | null {
    if (seconds === null) {
        return null;
    }

    return new Date(seconds * 1000).toISOString().slice(0, 10);
}

function buildReport(): FreshnessReport {
    const rows: Report[] = [];
    const problems: string[] = [];

    if (isShallowRepository()) {
        problems.push("shallow clone: code-change dates are unreliable (run `git fetch --unshallow`)");
    }

    for (const file of listMarkdownFiles(root).filter(isDocsFile)) {
        const frontmatter = parseFrontmatter(readFileSync(file, "utf8"));
        if (!frontmatter.present) {
            // Not a managed guide; docs/index.md and future scratch notes are simply skipped.
            continue;
        }

        const relativePath = relative(root, file);
        const reviewed = frontmatter.lastReviewed;
        if (reviewed === null) {
            problems.push(`${relativePath}: missing "last_reviewed" frontmatter`);
            continue;
        }

        if (frontmatter.codeGlobs.length === 0) {
            rows.push({ file: relativePath, reviewed, codeChanged: null, daysBehind: null });
            continue;
        }

        const reviewedAt = Date.parse(reviewed);
        if (Number.isNaN(reviewedAt)) {
            problems.push(`${relativePath}: last_reviewed "${reviewed}" is not an ISO date (YYYY-MM-DD)`);
            continue;
        }

        const codeChangedSeconds = latestCommitSeconds(frontmatter.codeGlobs);
        const daysBehind =
            codeChangedSeconds === null
                ? null
                : Math.floor((codeChangedSeconds * 1000 - reviewedAt) / MillisecondsPerDay);

        rows.push({
            file: relativePath,
            reviewed,
            codeChanged: formatDate(codeChangedSeconds),
            daysBehind: daysBehind !== null && daysBehind > 0 ? daysBehind : null,
        });
    }

    return { rows, problems };
}

function formatTable(rows: Report[]): string {
    const lines: string[] = ["| document | reviewed | code changed | status |", "| --- | --- | --- | --- |"];

    for (const row of rows) {
        const status = row.daysBehind === null ? "current" : `**${row.daysBehind}d behind**`;
        const reviewed = row.reviewed;
        const codeChanged = row.codeChanged ?? "—";
        lines.push(`| \`${row.file}\` | ${reviewed} | ${codeChanged} | ${status} |`);
    }

    return lines.join("\n");
}

function writeStepSummary(table: string, problems: string[]): void {
    const summaryPath = process.env.GITHUB_STEP_SUMMARY;
    if (summaryPath === undefined) {
        return;
    }

    appendFileSync(summaryPath, `## Documentation freshness\n\n${table}\n\n`);
    if (problems.length === 0) {
        return;
    }

    const bullets = problems.map((problem) => `- ${problem}`).join("\n");
    appendFileSync(summaryPath, `${bullets}\n\n`);
}

const report = buildReport();
report.rows.sort((left, right) => (right.daysBehind ?? -1) - (left.daysBehind ?? -1));

const staleRows = report.rows.filter((row) => row.daysBehind !== null);
const table = formatTable(report.rows);

console.log(table);
if (report.problems.length > 0) {
    console.log(`\n${report.problems.join("\n")}`);
}
console.log(
    `\n${report.rows.length} documents, ${staleRows.length} possibly stale. ` +
        "Bump last_reviewed after re-reading one.",
);

writeStepSummary(table, report.problems);

if (failOnStale && (staleRows.length > 0 || report.problems.length > 0)) {
    process.exitCode = 1;
}
