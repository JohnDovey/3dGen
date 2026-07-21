import AppKit
import SwiftUI

struct ImageLibrarySheet: View {
    @Environment(AppModel.self) private var appModel
    @Environment(\.dismiss) private var dismiss
    @State private var tagsDraft: String = ""
    @State private var taggingFileName: String?

    var body: some View {
        @Bindable var appModel = appModel

        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Text("Insert image bas-relief")
                    .font(.title2.weight(.semibold))
                Spacer()
                Button("Import…") {
                    appModel.importImageFilesFromPanel()
                }
            }

            HStack {
                Image(systemName: "magnifyingglass")
                    .foregroundStyle(.secondary)
                TextField("Search name or tag", text: $appModel.imageLibraryQuery)
                    .textFieldStyle(.roundedBorder)
                    .onChange(of: appModel.imageLibraryQuery) { _, _ in
                        Task { await appModel.refreshImageLibrary() }
                    }
            }

            if appModel.imageLibraryItems.isEmpty {
                Text("No images in the library. Import PNG/JPG files to get started.")
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView {
                    LazyVGrid(columns: [GridItem(.adaptive(minimum: 100), spacing: 12)], spacing: 12) {
                        ForEach(appModel.imageLibraryItems) { item in
                            ImageLibraryCell(
                                item: item,
                                thumbnail: appModel.imageThumbnailCache[item.fileName],
                                onSelect: { appModel.selectImageFromLibrary(fileName: item.fileName) },
                                onDelete: { appModel.deleteImageLibraryFile(item.fileName) },
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
                    appModel.showImageLibrarySheet = false
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
                TextField("portrait, logo, …", text: $tagsDraft)
                    .textFieldStyle(.roundedBorder)
                HStack {
                    Spacer()
                    Button("Cancel") { taggingFileName = nil }
                    Button("Save") {
                        if let name = taggingFileName {
                            appModel.setImageLibraryTags(fileName: name, tagsCSV: tagsDraft)
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

private struct ImageLibraryCell: View {
    let item: ImageLibraryItem
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
