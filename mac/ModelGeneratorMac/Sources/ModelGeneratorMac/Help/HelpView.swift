import SwiftUI
import WebKit

struct HelpView: View {
    @Environment(\.dismiss) private var dismiss
    @State private var html: String = "<p>Loading…</p>"
    @State private var baseURL: URL = URL(fileURLWithPath: "/")
    @State private var errorText: String?

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Text("How to Use")
                    .font(.title2.weight(.semibold))
                Spacer()
                Button("Done") { dismiss() }
                    .keyboardShortcut(.cancelAction)
            }
            .padding()

            Divider()

            if let errorText {
                Text(errorText)
                    .foregroundStyle(.red)
                    .padding()
                Spacer()
            } else {
                HelpWebView(html: html, baseURL: baseURL)
            }
        }
        .frame(minWidth: 720, minHeight: 520)
        .onAppear(perform: load)
    }

    private func load() {
        do {
            let (md, base) = try HelpContent.loadMarkdown()
            baseURL = base
            html = HelpContent.markdownToHTML(md, baseURL: base)
        } catch {
            errorText = error.localizedDescription
        }
    }
}

struct HelpWebView: NSViewRepresentable {
    let html: String
    let baseURL: URL

    func makeNSView(context: Context) -> WKWebView {
        let config = WKWebViewConfiguration()
        let view = WKWebView(frame: .zero, configuration: config)
        view.setValue(false, forKey: "drawsBackground")
        return view
    }

    func updateNSView(_ webView: WKWebView, context: Context) {
        webView.loadHTMLString(html, baseURL: baseURL)
    }
}

struct AboutView: View {
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("3D Model Generator")
                .font(.title.weight(.semibold))
            Text("Version \(HelpContent.appVersion)")
                .foregroundStyle(.secondary)
            Text("Design simple embossed 3D-printable models: shapes, text, SVG graphics, and photo bas-reliefs — then export STL.")
                .fixedSize(horizontal: false, vertical: true)
            Text(HelpContent.copyright)
                .font(.caption)
                .foregroundStyle(.secondary)
            HStack {
                Spacer()
                Button("OK") { dismiss() }
                    .keyboardShortcut(.defaultAction)
                    .buttonStyle(.borderedProminent)
            }
        }
        .padding(24)
        .frame(width: 400)
    }
}
