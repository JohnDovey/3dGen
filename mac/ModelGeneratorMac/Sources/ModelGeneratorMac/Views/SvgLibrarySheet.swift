import AppKit
import SwiftUI

struct SvgLibrarySheet: View {
    @Environment(AppModel.self) private var appModel
    @Environment(\.dismiss) private var dismiss
    @State private var tagsDraft: String = ""
    @State private var taggingFileName: String?

    var body: some View {
        @Bindable var appModel = appModel

        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Text(appModel.svgLibraryPurpose == .insert ? "Insert SVG" : "Custom shape SVG")
                    .font(.title2.weight(.semibold))
                Spacer()
                Button("Import…") {
                    appModel.importSvgFilesFromPanel()
                }
            }

            HStack {
                Image(systemName: "magnifyingglass")
                    .foregroundStyle(.secondary)
                TextField("Search name or tag", text: $appModel.svgLibraryQuery)
                    .textFieldStyle(.roundedBorder)
                    .onChange(of: appModel.svgLibraryQuery) { _, _ in
                        Task { await appModel.refreshSvgLibrary() }
                    }
            }

            if appModel.svgLibraryItems.isEmpty {
                Text("No SVG files in the library. Import some to get started.")
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView {
                    LazyVGrid(columns: [GridItem(.adaptive(minimum: 100), spacing: 12)], spacing: 12) {
                        ForEach(appModel.svgLibraryItems) { item in
                            SvgLibraryCell(
                                item: item,
                                thumbnail: appModel.svgThumbnailCache[item.fileName],
                                onSelect: { appModel.selectSvgFromLibrary(fileName: item.fileName) },
                                onDelete: { appModel.deleteSvgLibraryFile(item.fileName) },
                                onTags: {
                                    taggingFileName = item.fileName
                                    tagsDraft = item.keywords.joined(separator: ", ")
                                }
                            )
                        }
                    }
                    .padding(.vertical, 4)
                }
            }

            HStack {
                Spacer()
                Button("Cancel") {
                    dismiss()
                    appModel.showSvgLibrarySheet = false
                }
                .keyboardShortcut(.cancelAction)
            }
        }
        .padding()
        .frame(minWidth: 560, minHeight: 420)
        .sheet(isPresented: Binding(
            get: { taggingFileName != nil },
            set: { if !$0 { taggingFileName = nil } }
        )) {
            VStack(alignment: .leading, spacing: 12) {
                Text("Tags for \(taggingFileName ?? "")")
                    .font(.headline)
                Text("Comma-separated keywords")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                TextField("logo, badge, …", text: $tagsDraft)
                    .textFieldStyle(.roundedBorder)
                HStack {
                    Spacer()
                    Button("Cancel") { taggingFileName = nil }
                    Button("Save") {
                        if let name = taggingFileName {
                            appModel.setSvgLibraryTags(fileName: name, tagsCSV: tagsDraft)
                        }
                        taggingFileName = nil
                    }
                    .buttonStyle(.borderedProminent)
                }
            }
            .padding()
            .frame(width: 360)
        }
    }
}

private struct SvgLibraryCell: View {
    let item: SvgLibraryItem
    let thumbnail: Data?
    let onSelect: () -> Void
    let onDelete: () -> Void
    let onTags: () -> Void

    var body: some View {
        VStack(spacing: 6) {
            Group {
                if let thumbnail, let ns = NSImage(data: thumbnail) {
                    Image(nsImage: ns)
                        .resizable()
                        .aspectRatio(contentMode: .fit)
                } else {
                    Image(systemName: "photo")
                        .foregroundStyle(.secondary)
                }
            }
            .frame(width: 72, height: 72)
            .background(Color(nsColor: .controlBackgroundColor))
            .clipShape(RoundedRectangle(cornerRadius: 6))

            Text(item.fileName)
                .font(.caption)
                .lineLimit(2)
                .multilineTextAlignment(.center)

            if !item.keywords.isEmpty {
                Text(item.keywords.joined(separator: ", "))
                    .font(.caption2)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }

            HStack(spacing: 4) {
                Button("Use", action: onSelect)
                    .buttonStyle(.borderedProminent)
                    .controlSize(.mini)
                Button("Tags", action: onTags)
                    .controlSize(.mini)
                Button(role: .destructive, action: onDelete) {
                    Image(systemName: "trash")
                }
                .controlSize(.mini)
            }
        }
        .padding(8)
        .background(RoundedRectangle(cornerRadius: 8).stroke(Color(nsColor: .separatorColor)))
    }
}
