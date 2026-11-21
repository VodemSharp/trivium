/**
 * Pure Markdown parsing for the docs tooling: frontmatter, headings and links.
 *
 * Nothing here touches the file system or git, so every rule below is unit-testable
 * (see markdown.test.ts). The scan is deliberately line-oriented: the docs are prose with
 * fenced samples, not arbitrary CommonMark.
 */

const FrontmatterDelimiter = "---";
const CommentOpen = "<!--";
const CommentClose = "-->";

const HeadingPattern = /^(#{1,6})\s+(.*?)(?:\s+#+)?\s*$/;
const FenceOpenPattern = /^\s*(`{3,}|~{3,})/;
const LinkPattern = /!?\[[^\]\n]*\]\(([^)\n]*)\)/g;
const InlineCodeMaskPattern = /`[^`]*`/g;

const ImagePattern = /!\[[^\]]*\]\([^)]*\)/g;
const InlineLinkPattern = /\[([^\]]*)\]\([^)]*\)/g;
const InlineCodePattern = /`([^`]*)`/g;
const HtmlTagPattern = /<\/?[^>]+>/g;
const StrongEmphasisPattern = /(?<!\w)(\*\*|__)(?=\S)(.+?)(?<=\S)\1(?!\w)/g;
const EmphasisPattern = /(?<!\w)(\*|_)(?=\S)(.+?)(?<=\S)\1(?!\w)/g;

const FrontmatterPairPattern = /^([\w-]+):\s*(.*)$/;
const FrontmatterListItemPattern = /^\s*-\s+(.*)$/;
const QuotedValuePattern = /^(["'])(.*)\1$/;

export interface Heading {
    text: string;
    raw: string;
    github: string;
}

export interface MarkdownLink {
    target: string;
    line: number;
}

export interface ParseProblem {
    line: number;
    message: string;
}

export interface Fence {
    marker: string;
    length: number;
}

export interface ParsedDocument {
    headings: Heading[];
    links: MarkdownLink[];
    problems: ParseProblem[];
}

export interface DocFrontmatter {
    present: boolean;
    title: string | null;
    lastReviewed: string | null;
    codeGlobs: string[];
}

export function splitLines(source: string): string[] {
    // Splitting on a bare \n would leave a trailing \r on every line of a CRLF checkout,
    // which silently defeats exact matches such as the frontmatter delimiter.
    return source.split(/\r?\n/);
}

export function findFrontmatterEnd(lines: string[]): number {
    if (lines.length === 0) {
        return -1;
    }

    if (lines[0].trim() !== FrontmatterDelimiter) {
        return -1;
    }

    for (let index = 1; index < lines.length; index += 1) {
        if (lines[index].trim() === FrontmatterDelimiter) {
            return index;
        }
    }

    return -1;
}

export function parseFrontmatter(source: string): DocFrontmatter {
    const frontmatter: DocFrontmatter = {
        present: false,
        title: null,
        lastReviewed: null,
        codeGlobs: [],
    };

    const lines = splitLines(source);
    const end = findFrontmatterEnd(lines);

    if (end === -1) {
        return frontmatter;
    }

    frontmatter.present = true;
    let currentKey: string | null = null;

    for (let index = 1; index < end; index += 1) {
        const line = lines[index];

        const listItem = FrontmatterListItemPattern.exec(line);
        if (listItem !== null) {
            if (currentKey === "code_globs") {
                frontmatter.codeGlobs.push(unquote(listItem[1].trim()));
            }
            continue;
        }

        const pair = FrontmatterPairPattern.exec(line);
        if (pair === null) {
            continue;
        }

        currentKey = pair[1];
        const value = unquote(pair[2].trim());
        if (value === "") {
            continue;
        }

        if (currentKey === "title") {
            frontmatter.title = value;
        }
        if (currentKey === "last_reviewed") {
            frontmatter.lastReviewed = value;
        }
    }

    return frontmatter;
}

export function parseDocument(source: string): ParsedDocument {
    const lines = splitLines(source);
    const headings: Heading[] = [];
    const links: MarkdownLink[] = [];
    const problems: ParseProblem[] = [];
    const slugOccurrences = new Map<string, number>();

    let fence: Fence | null = null;
    let insideComment = false;

    const frontmatterEnd = findFrontmatterEnd(lines);
    const firstContentLine = frontmatterEnd === -1 ? 0 : frontmatterEnd + 1;

    for (let index = firstContentLine; index < lines.length; index += 1) {
        const lineNumber = index + 1;
        const rawLine = lines[index];

        // Fenced blocks win over everything: their content is sample code, never markdown.
        if (fence !== null) {
            if (isFenceClose(rawLine, fence)) {
                fence = null;
            }
            continue;
        }

        const openedFence = matchFenceOpen(rawLine);
        if (openedFence !== null) {
            fence = openedFence;
            continue;
        }

        // HTML comments are invisible to the renderers, so their content is not markdown either.
        const commentResult = stripHtmlComments(rawLine, insideComment);
        insideComment = commentResult.inComment;
        const line = commentResult.text;

        const headingMatch = HeadingPattern.exec(line);
        if (headingMatch !== null) {
            headings.push(createHeading(headingMatch[2], slugOccurrences));
        }

        collectLinks(line, lineNumber, links);
    }

    if (insideComment) {
        problems.push({ line: lines.length, message: "unterminated HTML comment; the rest of the file was skipped" });
    }
    if (fence !== null) {
        problems.push({ line: lines.length, message: "unterminated code fence; the rest of the file was skipped" });
    }

    return { headings, links, problems };
}

export function headingToText(raw: string): string {
    return raw
        .replace(ImagePattern, "")
        .replace(InlineLinkPattern, "$1")
        .replace(InlineCodePattern, "$1")
        .replace(HtmlTagPattern, "")
        .replace(StrongEmphasisPattern, "$2")
        .replace(EmphasisPattern, "$2")
        .trim();
}

export function githubSlug(text: string): string {
    return text
        .trim()
        .toLowerCase()
        .replace(/<[^>]*>/g, "")
        .replace(/[^\p{L}\p{N}\s_-]/gu, "")
        .replace(/ /g, "-");
}

function unquote(value: string): string {
    const match = QuotedValuePattern.exec(value);
    if (match === null) {
        return value;
    }
    return match[2];
}

function createHeading(raw: string, slugOccurrences: Map<string, number>): Heading {
    const text = headingToText(raw);
    const base = githubSlug(text);
    const seen = slugOccurrences.get(base) ?? 0;
    slugOccurrences.set(base, seen + 1);
    const suffix = seen === 0 ? "" : `-${seen}`;

    return { text, raw, github: `${base}${suffix}` };
}

function collectLinks(line: string, lineNumber: number, links: MarkdownLink[]): void {
    // Blank out inline code while keeping the offsets, so `[x](y.md)` inside backticks is not a link.
    const masked = line.replace(InlineCodeMaskPattern, (code) => " ".repeat(code.length));

    LinkPattern.lastIndex = 0;
    let match = LinkPattern.exec(masked);
    while (match !== null) {
        links.push({ target: match[1].trim(), line: lineNumber });
        if (match.index === LinkPattern.lastIndex) {
            // The pattern cannot match empty text today; this keeps the loop bounded if it ever does.
            LinkPattern.lastIndex += 1;
        }
        match = LinkPattern.exec(masked);
    }
}

function matchFenceOpen(line: string): Fence | null {
    const match = FenceOpenPattern.exec(line);
    if (match === null) {
        return null;
    }

    const run = match[1];
    return { marker: run.charAt(0), length: run.length };
}

function isFenceClose(line: string, fence: Fence): boolean {
    const trimmed = line.trim();
    if (trimmed.length < fence.length) {
        return false;
    }

    for (const character of trimmed) {
        if (character !== fence.marker) {
            return false;
        }
    }

    return true;
}

function stripHtmlComments(line: string, inComment: boolean): { text: string; inComment: boolean } {
    let text = line;
    let insideComment = inComment;

    while (true) {
        if (insideComment) {
            const closingIndex = text.indexOf(CommentClose);
            if (closingIndex === -1) {
                return { text: "", inComment: true };
            }

            text = text.slice(closingIndex + CommentClose.length);
            insideComment = false;
            continue;
        }

        const openingIndex = text.indexOf(CommentOpen);
        if (openingIndex === -1) {
            return { text, inComment: false };
        }

        const closingIndex = text.indexOf(CommentClose, openingIndex + CommentOpen.length);
        if (closingIndex === -1) {
            return { text: text.slice(0, openingIndex), inComment: true };
        }

        text = text.slice(0, openingIndex) + text.slice(closingIndex + CommentClose.length);
    }
}
