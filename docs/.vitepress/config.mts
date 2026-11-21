import { defineConfig } from "vitepress";

export default defineConfig({
    title: "Trivium",
    description: "Routing, dispatch and hosting for Telegram bots. One update, one handler — compiled at build time.",
    cleanUrls: true,
    // Contributor and agent docs stay in the repository; only user-facing pages are published.
    srcExclude: ["README.md", "fundamental/**", "plan/**", "reference/**"],
    head: [["link", { rel: "icon", type: "image/svg+xml", href: "/logo.svg" }]],
    themeConfig: {
        logo: "/logo.svg",
        nav: [
            {
                text: "Roadmap",
                link: "https://github.com/VodemSharp/trivium/blob/main/docs/plan/roadmap.md",
            },
            { text: "Status", link: "/#status" },
        ],
        footer: {
            message: "Released under the MIT license.",
            copyright: "Copyright © 2026 Trivium contributors",
        },
        socialLinks: [{ icon: "github", link: "https://github.com/VodemSharp/trivium" }],
    },
});
