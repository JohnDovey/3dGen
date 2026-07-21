import Foundation

/// Loads `docs/HOW_TO_USE.md` (+ images) for the in-app Help viewer.
/// Resolution order:
/// 1. App bundle `Contents/Resources/Help/` (packaged release)
/// 2. Repository `docs/` (development via `swift run` from the monorepo)
enum HelpContent {
    static let appVersion = "0.9.0"
    static let copyright = "Copyright © John Dovey <dovey.john@gmail.com>"

    /// Directory containing HOW_TO_USE.md and optional `images/`.
    static func helpDirectory() -> URL? {
        if let bundled = Bundle.main.resourceURL?.appendingPathComponent("Help", isDirectory: true) {
            let md = bundled.appendingPathComponent("HOW_TO_USE.md")
            if FileManager.default.fileExists(atPath: md.path) {
                return bundled
            }
        }

        // Dev: walk up from CWD looking for docs/HOW_TO_USE.md
        var dir = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        for _ in 0..<12 {
            let candidate = dir.appendingPathComponent("docs")
            let md = candidate.appendingPathComponent("HOW_TO_USE.md")
            if FileManager.default.fileExists(atPath: md.path) {
                return candidate
            }
            // Also try ../../docs from mac/ModelGeneratorMac
            let parent = dir.deletingLastPathComponent()
            if parent.path == dir.path { break }
            dir = parent
        }

        // Explicit relative fallbacks
        for rel in ["docs", "../docs", "../../docs", "../../../docs"] {
            let candidate = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
                .appendingPathComponent(rel)
                .standardized
            let md = candidate.appendingPathComponent("HOW_TO_USE.md")
            if FileManager.default.fileExists(atPath: md.path) {
                return candidate
            }
        }
        return nil
    }

    static func loadMarkdown() throws -> (text: String, baseURL: URL) {
        guard let dir = helpDirectory() else {
            throw HelpError.notFound
        }
        let md = dir.appendingPathComponent("HOW_TO_USE.md")
        let text = try String(contentsOf: md, encoding: .utf8)
        return (text, dir)
    }

    /// Minimal Markdown → HTML for the How to Use guide (headings, lists, images, code, paragraphs).
    static func markdownToHTML(_ markdown: String, baseURL: URL) -> String {
        let lines = markdown.components(separatedBy: "\n")
        var html: [String] = []
        html.append("""
        <!DOCTYPE html><html><head><meta charset="utf-8">
        <style>
          body { font-family: -apple-system, BlinkMacSystemFont, sans-serif; margin: 24px; line-height: 1.45; color: #1d1d1f; }
          h1 { font-size: 1.6em; } h2 { font-size: 1.3em; margin-top: 1.4em; } h3 { font-size: 1.1em; }
          code { background: #f2f2f7; padding: 1px 4px; border-radius: 3px; font-size: 0.92em; }
          pre { background: #f2f2f7; padding: 12px; border-radius: 8px; overflow-x: auto; }
          img { max-width: 100%; height: auto; border: 1px solid #d2d2d7; border-radius: 6px; margin: 8px 0; }
          ul, ol { padding-left: 1.4em; }
          a { color: #0066cc; }
        </style></head><body>
        """)

        var inCode = false
        var inList = false
        var para: [String] = []

        func flushPara() {
            if !para.isEmpty {
                html.append("<p>" + para.joined(separator: " ") + "</p>")
                para.removeAll()
            }
        }
        func closeList() {
            if inList {
                html.append("</ul>")
                inList = false
            }
        }

        for raw in lines {
            let line = raw
            if line.hasPrefix("```") {
                flushPara()
                closeList()
                if inCode {
                    html.append("</code></pre>")
                    inCode = false
                } else {
                    html.append("<pre><code>")
                    inCode = true
                }
                continue
            }
            if inCode {
                html.append(escape(line) + "\n")
                continue
            }

            if line.trimmingCharacters(in: .whitespaces).isEmpty {
                flushPara()
                closeList()
                continue
            }

            if line.hasPrefix("#") {
                flushPara()
                closeList()
                let level = line.prefix(while: { $0 == "#" }).count
                if level >= 1 && level <= 6 {
                    let title = String(line.dropFirst(level).trimmingCharacters(in: .whitespaces))
                    let h = min(level, 3)
                    html.append("<h\(h)>\(inline(title))</h\(h)>")
                    continue
                }
            }

            if line.range(of: #"^\s*[-*]\s+"#, options: .regularExpression) != nil {
                flushPara()
                if !inList {
                    html.append("<ul>")
                    inList = true
                }
                let item = line.replacingOccurrences(of: #"^\s*[-*]\s+"#, with: "", options: .regularExpression)
                html.append("<li>\(inline(item))</li>")
                continue
            }

            // Image: ![alt](path)
            if let img = firstMatch(line, pattern: #"!\[([^\]]*)\]\(([^)]+)\)"#) {
                flushPara()
                closeList()
                let alt = escape(img.0)
                let src = img.1
                // Resolve relative to help directory for file:// loading
                let resolved: String
                if src.hasPrefix("http") {
                    resolved = src
                } else {
                    resolved = baseURL.appendingPathComponent(src).absoluteString
                }
                html.append("<p><img src=\"\(escape(resolved))\" alt=\"\(alt)\"></p>")
                continue
            }

            closeList()
            para.append(inline(line))
        }
        flushPara()
        closeList()
        if inCode { html.append("</code></pre>") }
        html.append("</body></html>")
        return html.joined(separator: "\n")
    }

    private static func inline(_ s: String) -> String {
        var t = escape(s)
        // **bold**
        t = t.replacingOccurrences(of: #"\*\*([^*]+)\*\*"#, with: "<strong>$1</strong>", options: .regularExpression)
        // `code`
        t = t.replacingOccurrences(of: #"`([^`]+)`"#, with: "<code>$1</code>", options: .regularExpression)
        // [text](url)
        t = t.replacingOccurrences(of: #"\[([^\]]+)\]\(([^)]+)\)"#, with: "<a href=\"$2\">$1</a>", options: .regularExpression)
        return t
    }

    private static func escape(_ s: String) -> String {
        s.replacingOccurrences(of: "&", with: "&amp;")
            .replacingOccurrences(of: "<", with: "&lt;")
            .replacingOccurrences(of: ">", with: "&gt;")
            .replacingOccurrences(of: "\"", with: "&quot;")
    }

    private static func firstMatch(_ s: String, pattern: String) -> (String, String)? {
        guard let re = try? NSRegularExpression(pattern: pattern) else { return nil }
        let range = NSRange(s.startIndex..., in: s)
        guard let m = re.firstMatch(in: s, range: range), m.numberOfRanges >= 3,
              let r1 = Range(m.range(at: 1), in: s),
              let r2 = Range(m.range(at: 2), in: s)
        else { return nil }
        return (String(s[r1]), String(s[r2]))
    }
}

enum HelpError: LocalizedError {
    case notFound

    var errorDescription: String? {
        "Could not find docs/HOW_TO_USE.md. Packaged apps ship it under Resources/Help/; in development run from the monorepo so docs/ is discoverable."
    }
}
