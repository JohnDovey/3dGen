import SwiftUI

struct SvgInsertsPanelView: View {
    @EnvironmentObject private var appModel: AppModel

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text("SVG inserts")
                    .font(.headline)
                Spacer()
                Button {
                    appModel.openSvgLibrary(for: .insert)
                } label: {
                    Label("Insert…", systemImage: "plus")
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
            }

            if appModel.model.svgInserts.isEmpty {
                Text("No SVG inserts — use Insert to browse the library.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            ForEach(Array(appModel.model.svgInserts.enumerated()), id: \.element.id) { index, _ in
                SvgInsertEditorView(
                    index: index,
                    insert: appModel.svgInsertBinding(at: index)
                )
            }
        }
    }
}

struct SvgInsertEditorView: View {
    let index: Int
    @Binding var insert: WireSvgInsert
    @EnvironmentObject private var appModel: AppModel
    @State private var thumbnail: Data?

    private var positionMode: Binding<PositionModeOption> {
        Binding(
            get: { self.insert.positionModeOption },
            set: { self.insert.positionModeOption = $0 }
        )
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text(insert.sourceFileName ?? "SVG \(index + 1)")
                    .font(.subheadline.weight(.semibold))
                    .lineLimit(1)
                Spacer()
                Button(role: .destructive) {
                    appModel.removeSvgInsert(at: index)
                } label: {
                    Image(systemName: "trash")
                }
                .buttonStyle(.borderless)
            }

            HStack(alignment: .top, spacing: 10) {
                thumbnailView
                    .frame(width: 48, height: 48)
                    .background(Color(nsColor: .controlBackgroundColor))
                    .clipShape(RoundedRectangle(cornerRadius: 4))

                VStack(alignment: .leading, spacing: 6) {
                    HStack {
                        labeledField("Scale", value: $insert.scale)
                        labeledField("Emboss", value: $insert.embossHeight)
                        ColorPicker(
                            "",
                            selection: Binding(
                                get: { Color(argb: self.insert.colorArgb) },
                                set: { self.insert.colorArgb = $0.argbInt }
                            ),
                            supportsOpacity: false
                        )
                        .labelsHidden()
                        .frame(width: 36)
                    }

                    Picker("Position", selection: positionMode) {
                        ForEach(PositionModeOption.allCases) { mode in
                            Text(mode.description).tag(mode)
                        }
                    }
                    .pickerStyle(.segmented)

                    HStack {
                        coord("X", $insert.positionX, enabled: insert.positionModeOption.showsXYZ)
                        coord("Y", $insert.positionY, enabled: insert.positionModeOption.showsXYZ)
                        coord("Z", $insert.positionZ, enabled: insert.positionModeOption.showsXYZ)
                        coord("Rot°", $insert.rotationZ, enabled: true)
                    }
                }
            }
        }
        .padding(10)
        .background(RoundedRectangle(cornerRadius: 8).fill(Color(nsColor: .controlBackgroundColor)))
        .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color(nsColor: .separatorColor), lineWidth: 1))
        .task(id: insert.svgContent) {
            await loadThumb()
        }
    }

    @ViewBuilder
    private var thumbnailView: some View {
        if let thumbnail, let ns = NSImage(data: thumbnail) {
            Image(nsImage: ns)
                .resizable()
                .aspectRatio(contentMode: .fit)
        } else {
            Image(systemName: "photo")
                .foregroundStyle(.secondary)
        }
    }

    private func labeledField(_ title: String, value: Binding<Float>) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title).font(.caption2).foregroundStyle(.secondary)
            TextField(title, value: value, format: .number.precision(.fractionLength(0...2)))
                .textFieldStyle(.roundedBorder)
                .frame(width: 64)
        }
    }

    private func coord(_ title: String, _ value: Binding<Float>, enabled: Bool) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title).font(.caption2).foregroundStyle(.secondary)
            TextField(title, value: value, format: .number.precision(.fractionLength(0...2)))
                .textFieldStyle(.roundedBorder)
                .disabled(!enabled)
                .opacity(enabled ? 1 : 0.45)
        }
    }

    private func loadThumb() async {
        guard !insert.svgContent.isEmpty else {
            thumbnail = nil
            return
        }
        thumbnail = await appModel.renderSvgContentThumbnail(insert.svgContent)
    }
}


