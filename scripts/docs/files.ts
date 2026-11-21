/**
 * Writes the list of Markdown files the docs tooling should look at.
 *
 * The source of truth is `.gitignore`: `git ls-files` returns tracked and untracked files and,
 * with `--exclude-standard`, drops everything ignored (node_modules, bin, obj, .obsidian, ...).
 * Deleted-but-still-tracked files are filtered out, because the tooling reads every path it gets.
 *
 * Outside a git checkout (a plain tarball) a small directory walk is used instead.
 */

import { execFileSync } from "node:child_process";
import { existsSync, readdirSync } from "node:fs";
import { extname, join } from "node:path";

const FallbackSkippedDirectories = new Set([
    "node_modules",
    ".git",
    ".obsidian",
    ".trash",
    ".opencode",
    "bin",
    "obj",
    "artifacts",
    "TestResults",
    "coverage",
    "dist",
]);

const MaxGitOutputBytes = 64 * 1024 * 1024;

export function listMarkdownFiles(root: string): string[] {
    let output: string;

    try {
        output = execFileSync("git", ["ls-files", "-co", "--exclude-standard", "--deduplicate", "-z", "--", "*.md"], {
            cwd: root,
            encoding: "utf8",
            stdio: ["ignore", "pipe", "ignore"],
            maxBuffer: MaxGitOutputBytes,
        });
    } catch {
        return walkDirectories(root);
    }

    const files: string[] = [];
    for (const entry of output.split("\0")) {
        if (entry === "") {
            continue;
        }

        const file = join(root, entry);
        if (existsSync(file)) {
            files.push(file);
        }
    }

    return files;
}

function walkDirectories(directory: string, collected: string[] = []): string[] {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
        if (entry.isDirectory()) {
            if (!FallbackSkippedDirectories.has(entry.name)) {
                walkDirectories(join(directory, entry.name), collected);
            }
            continue;
        }

        // Symlinks report isDirectory() === false, so a link cycle cannot make this recurse forever.
        if (entry.isFile() && extname(entry.name) === ".md") {
            collected.push(join(directory, entry.name));
        }
    }

    return collected;
}
