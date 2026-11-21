import { describe, expect, test } from "bun:test";

import { parseDocument, parseFrontmatter, splitLines } from "./markdown";

describe("splitLines", () => {
    test("strips CRLF so exact matches keep working on a Windows checkout", () => {
        const lines = splitLines("---\r\ntitle: X\r\n---\r\n");

        expect(lines).toEqual(["---", "title: X", "---", ""]);
    });
});

describe("parseFrontmatter", () => {
    test("reads last_reviewed and the code_globs block list", () => {
        const source = [
            "---",
            "title: API Layer",
            "last_reviewed: 2026-10-17",
            "code_globs:",
            "  - src/Trivium.Generators/**",
            "  - samples/Trivium.AppHost/Program.cs",
            "---",
            "",
            "# API Layer",
        ].join("\n");

        const frontmatter = parseFrontmatter(source);

        expect(frontmatter.present).toBe(true);
        expect(frontmatter.title).toBe("API Layer");
        expect(frontmatter.lastReviewed).toBe("2026-10-17");
        expect(frontmatter.codeGlobs).toEqual(["src/Trivium.Generators/**", "samples/Trivium.AppHost/Program.cs"]);
    });

    test("reads quoted values", () => {
        const frontmatter = parseFrontmatter('---\ntitle: "Security & Sessions"\n---\n');

        expect(frontmatter.title).toBe("Security & Sessions");
    });

    test("reports a missing block instead of guessing", () => {
        const frontmatter = parseFrontmatter("# Just a heading\n");

        expect(frontmatter.present).toBe(false);
        expect(frontmatter.lastReviewed).toBeNull();
        expect(frontmatter.codeGlobs).toEqual([]);
    });

    test("tolerates trailing spaces on the delimiter", () => {
        const frontmatter = parseFrontmatter("---   \nlast_reviewed: 2026-10-17\n---   \n");

        expect(frontmatter.present).toBe(true);
        expect(frontmatter.lastReviewed).toBe("2026-10-17");
    });
});

describe("parseDocument headings", () => {
    test("keeps a literal hash that is glued to the heading text", () => {
        const document = parseDocument("## C#\n");

        expect(document.headings).toHaveLength(1);
        expect(document.headings[0].text).toBe("C#");
        expect(document.headings[0].github).toBe("c");
    });

    test("strips a closing hash sequence", () => {
        const document = parseDocument("## Title ##\n");

        expect(document.headings[0].text).toBe("Title");
    });

    test("leaves snake_case alone and still removes real emphasis", () => {
        const document = parseDocument("## snake_case and _emphasis_\n");

        expect(document.headings[0].text).toBe("snake_case and emphasis");
    });

    test("removes inline code and strong emphasis", () => {
        const document = parseDocument("## **Bold** `Configuration` heading\n");

        expect(document.headings[0].text).toBe("Bold Configuration heading");
    });

    test("numbers duplicate headings like GitHub does", () => {
        const document = parseDocument("## Setup\n\n## Setup\n");

        expect(document.headings.map((heading) => heading.github)).toEqual(["setup", "setup-1"]);
    });

    test("skips the frontmatter block", () => {
        const document = parseDocument("---\ntitle: '# Not a heading'\n---\n\n## Real heading\n");

        expect(document.headings).toHaveLength(1);
        expect(document.headings[0].text).toBe("Real heading");
    });
});

describe("parseDocument fenced blocks", () => {
    test("ignores links, comments and hashes inside a fence", () => {
        const source = [
            "# Before",
            "",
            "```md",
            "<!-- unterminated comment inside a sample",
            "[not a link](nope.md)",
            "## not a heading",
            "```",
            "",
            "[real link](api.md)",
        ].join("\n");

        const document = parseDocument(source);

        expect(document.problems).toHaveLength(0);
        expect(document.headings.map((heading) => heading.text)).toEqual(["Before"]);
        expect(document.links.map((link) => link.target)).toEqual(["api.md"]);
    });

    test("requires the closing fence to use the same marker", () => {
        const source = [
            "~~~",
            "[not a link](nope.md)",
            "```",
            "[still not a link](nope.md)",
            "~~~",
            "[link](a.md)",
        ].join("\n");

        const document = parseDocument(source);

        expect(document.links.map((link) => link.target)).toEqual(["a.md"]);
    });

    test("reports an unterminated fence", () => {
        const document = parseDocument("```\n[not a link](nope.md)\n");

        expect(document.problems).toHaveLength(1);
        expect(document.problems[0].message).toContain("unterminated code fence");
        expect(document.links).toHaveLength(0);
    });
});

describe("parseDocument comments", () => {
    test("reads the heading after a leading comment", () => {
        const source = "<!-- Source of truth: IIdentityApi.cs -->\n\n# Endpoint Inventory\n";

        const document = parseDocument(source);

        expect(document.headings[0].text).toBe("Endpoint Inventory");
    });

    test("drops single-line and multi-line comments", () => {
        const source = [
            "# Title",
            "<!-- hidden [link](nope.md) -->",
            "<!--",
            "[hidden](nope.md)",
            "-->",
            "[shown](a.md)",
        ].join("\n");

        const document = parseDocument(source);

        expect(document.links.map((link) => link.target)).toEqual(["a.md"]);
        expect(document.problems).toHaveLength(0);
    });

    test("reports an unterminated comment instead of silently dropping the file", () => {
        const document = parseDocument("# Title\n<!-- never closed\n[invisible](a.md)\n");

        expect(document.problems).toHaveLength(1);
        expect(document.problems[0].message).toContain("unterminated HTML comment");
        expect(document.links).toHaveLength(0);
    });
});

describe("parseDocument links", () => {
    test("collects links and images, and skips inline code", () => {
        const source = "Use `[not a link](nope.md)` and [yes](api.md) and ![image](diagram.png)\n";

        const document = parseDocument(source);

        expect(document.links.map((link) => link.target)).toEqual(["api.md", "diagram.png"]);
    });

    test("reports line numbers", () => {
        const document = parseDocument("# Title\n\n[a](a.md)\n\n[b](b.md)\n");

        expect(document.links.map((link) => link.line)).toEqual([3, 5]);
    });
});
