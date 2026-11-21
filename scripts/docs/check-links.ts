#!/usr/bin/env bun

/**
 * Verifies every relative link and heading anchor in the repository's Markdown.
 *
 * Rules:
 *   - a relative link must point at an existing file, not a directory;
 *   - a `#anchor` must match a heading, either by its literal text (the Obsidian form, used in the
 *     vault) or by its GitHub slug (used in README.md and dotfolders, which Obsidian never shows).
 *
 * Exit codes: 0 when only warnings are left, 1 when there are errors (or warnings under --strict).
 */

import { readFileSync, statSync, type Stats } from "node:fs";
import { dirname, extname, join, relative, resolve } from "node:path";

import { listMarkdownFiles } from "./files";
import { type ParsedDocument, parseDocument } from "./markdown";

const root = resolve(import.meta.dir, "../..");
const strictMode = process.argv.includes("--strict");

const ExternalTargetPattern = /^(https?:|mailto:|tel:|data:|\/\/|\{|<)/;
const HeadingMarkupPattern = /[`[\]]/;
const WhitespacePattern = /\s+/g;

type Severity = "error" | "warning";

interface Diagnostic {
    file: string;
    line: number;
    severity: Severity;
    message: string;
}

interface AnchorFinding {
    severity: Severity;
    message: string;
}

const documents = new Map<string, ParsedDocument>();
const diagnostics: Diagnostic[] = [];

function decodeAnchor(value: string): string {
    try {
        return decodeURIComponent(value);
    } catch {
        // A malformed escape stays as written; the anchor simply will not match.
        return value;
    }
}

// A file Obsidian never shows: README (a GitHub page) or anything under a dotfolder.
function isGithubOnlyFile(file: string): boolean {
    const path = relative(root, file);
    if (path === "README.md") {
        return true;
    }

    return path.split(/[\\/]/).some((segment) => segment.startsWith("."));
}

function isExternalTarget(target: string): boolean {
    return target === "" || ExternalTargetPattern.test(target);
}

function resolveTarget(sourceFile: string, pathPart: string): string {
    if (pathPart === "") {
        return sourceFile;
    }
    if (pathPart.startsWith("/")) {
        return join(root, pathPart.slice(1));
    }

    return resolve(dirname(sourceFile), pathPart);
}

function inspectAnchor(sourceFile: string, targetFile: string, anchor: string): AnchorFinding | null {
    const document = documents.get(targetFile);
    if (document === undefined) {
        return {
            severity: "warning",
            message: `anchor #${decodeAnchor(anchor)} points into a file the checker does not index`,
        };
    }

    const decoded = decodeAnchor(anchor).replace(WhitespacePattern, " ").trim();
    const literalHeading = document.headings.find(
        (candidate) => candidate.text.toLowerCase() === decoded.toLowerCase(),
    );
    const slugHeading =
        literalHeading === undefined ? document.headings.find((candidate) => candidate.github === decoded) : undefined;

    if (literalHeading === undefined && slugHeading === undefined) {
        return { severity: "error", message: `anchor #${decoded} matches no heading` };
    }

    const githubOnly = isGithubOnlyFile(sourceFile);

    if (githubOnly && literalHeading !== undefined) {
        return {
            severity: "warning",
            message:
                `GitHub-only doc (Obsidian never shows it): use the slug #${literalHeading.github} ` +
                `so the link jumps on GitHub, not the heading text`,
        };
    }

    if (!githubOnly && slugHeading !== undefined) {
        return {
            severity: "warning",
            message:
                `Obsidian note: use the heading text (#${encodeURIComponent(slugHeading.text)}) ` +
                `so the link jumps in the vault, not the GitHub slug`,
        };
    }

    if (literalHeading !== undefined && HeadingMarkupPattern.test(literalHeading.raw)) {
        return {
            severity: "warning",
            message:
                `heading "${literalHeading.text}" contains inline code or brackets, which an Obsidian anchor ` +
                `cannot reproduce reliably; reword the heading`,
        };
    }

    return null;
}

function loadDocuments(): void {
    for (const file of listMarkdownFiles(root)) {
        const document = parseDocument(readFileSync(file, "utf8"));
        documents.set(file, document);

        for (const problem of document.problems) {
            diagnostics.push({ file, line: problem.line, severity: "warning", message: problem.message });
        }
    }
}

function checkLinks(): number {
    let checkedLinkCount = 0;

    for (const [file, document] of documents) {
        for (const link of document.links) {
            const target = link.target;
            if (isExternalTarget(target)) {
                continue;
            }
            checkedLinkCount += 1;

            const hashIndex = target.indexOf("#");
            const withoutAnchor = hashIndex === -1 ? target : target.slice(0, hashIndex);
            const rawAnchor = hashIndex === -1 ? null : target.slice(hashIndex + 1);
            const pathPart = decodeAnchor(withoutAnchor.split("?")[0]);
            const resolved = resolveTarget(file, pathPart);

            let targetStats: Stats | null = null;
            try {
                targetStats = statSync(resolved);
            } catch {
                targetStats = null;
            }

            if (targetStats === null) {
                diagnostics.push({ file, line: link.line, severity: "error", message: `missing target ${pathPart}` });
                continue;
            }

            if (targetStats.isDirectory()) {
                diagnostics.push({
                    file,
                    line: link.line,
                    severity: "error",
                    message: `target ${pathPart} is a directory`,
                });
                continue;
            }

            if (rawAnchor === null || extname(resolved) !== ".md") {
                continue;
            }

            const finding = inspectAnchor(file, resolved, rawAnchor);
            if (finding !== null) {
                diagnostics.push({ file, line: link.line, severity: finding.severity, message: finding.message });
            }
        }
    }

    return checkedLinkCount;
}

function printDiagnostics(): void {
    diagnostics.sort((left, right) => {
        const byFile = left.file.localeCompare(right.file);
        if (byFile !== 0) {
            return byFile;
        }

        return left.line - right.line;
    });

    for (const diagnostic of diagnostics) {
        const location = `${relative(root, diagnostic.file)}:${diagnostic.line}`;
        console.log(`${location} [${diagnostic.severity}] ${diagnostic.message}`);
    }
}

loadDocuments();
const checkedLinkCount = checkLinks();
printDiagnostics();

const errors = diagnostics.filter((diagnostic) => diagnostic.severity === "error");
const warnings = diagnostics.filter((diagnostic) => diagnostic.severity === "warning");

console.log(
    `\nChecked ${checkedLinkCount} internal links across ${documents.size} files: ` +
        `${errors.length} error(s), ${warnings.length} warning(s).`,
);

if (errors.length > 0 || (strictMode && warnings.length > 0)) {
    process.exitCode = 1;
}
