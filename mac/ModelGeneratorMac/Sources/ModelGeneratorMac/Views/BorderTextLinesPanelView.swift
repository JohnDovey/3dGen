import SwiftUI

struct BorderTextLinesPanelView: View {
    @Environment(AppModel.self) private var appModel

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text("Border text")
                    .font(.headline)
                Spacer()
                Button {
                    appModel.addBorderTextLine()
                } label: {
                    Label("Add", systemImage: "plus")
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
            }

            Text("Lettering along the raised border (coin rim). Not drag-positioned.")
                .font(.caption)
                .foregroundStyle(.secondary)

            if appModel.model.borderTextLines.isEmpty {
                Text("No border text — click Add for rim lettering.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            ForEach(Array(appModel.model.borderTextLines.enumerated()), id: \.element.id) { index, _ in
                BorderTextLineEditorView(
                    index: index,
                    line: appModel.borderTextLineBinding(at: index),
                    canRemove: true
                )
                .id(appModel.model.borderTextLines[index].id)
            }
        }
    }
}

struct BorderTextLineEditorView: View {
    let index: Int
    @Binding var line: WireBorderTextLine
    let canRemove: Bool
    @Environment(AppModel.self) private var appModel

    @State private var contentDraft: String = ""
    @State private var contentCommitTask: Task<Void, Never>?

    private var modeBinding: Binding<BorderTextModeOption> {
        Binding(
            get: { self.line.modeOption },
            set: { self.line.modeOption = $0 }
        )
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text("Border line \(index + 1)")
                    .font(.subheadline.weight(.semibold))
                Spacer()
                Button(role: .destructive) {
                    appModel.removeBorderTextLine(at: index)
                } label: {
                    Image(systemName: "trash")
                }
                .buttonStyle(.borderless)
                .disabled(!canRemove)
                .help("Remove this border text line")
            }

            TextField("Text", text: $contentDraft)
                .textFieldStyle(.roundedBorder)
                .onSubmit { commitContentImmediately() }
                .onChange(of: contentDraft) { _, newValue in
                    scheduleContentCommit(newValue)
                }

            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 2) {
                    Text("Font").font(.caption).foregroundStyle(.secondary)
                    Picker("Font", selection: $line.fontName) {
                        ForEach(FontCatalog.families, id: \.self) { family in
                            Text(family).tag(family)
                        }
                    }
                    .labelsHidden()
                    .frame(maxWidth: .infinity)
                }

                VStack(alignment: .leading, spacing: 2) {
                    Text("Size").font(.caption).foregroundStyle(.secondary)
                    TextField(
                        "Size",
                        value: $line.fontSize,
                        format: .number.precision(.fractionLength(0...1))
                    )
                    .textFieldStyle(.roundedBorder)
                    .frame(width: 56)
                }
            }

            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text("Height (mm)").font(.caption).foregroundStyle(.secondary)
                    TextField(
                        "Height",
                        value: $line.height,
                        format: .number.precision(.fractionLength(0...2))
                    )
                    .textFieldStyle(.roundedBorder)
                    .frame(width: 72)
                }

                VStack(alignment: .leading, spacing: 2) {
                    Text("Mode").font(.caption).foregroundStyle(.secondary)
                    Picker("Mode", selection: modeBinding) {
                        ForEach(BorderTextModeOption.allCases) { mode in
                            Text(mode.description).tag(mode)
                        }
                    }
                    .labelsHidden()
                    .frame(width: 110)
                }

                VStack(alignment: .leading, spacing: 2) {
                    Text("Anchor°").font(.caption).foregroundStyle(.secondary)
                    TextField(
                        "Anchor",
                        value: $line.anchorAngleDegrees,
                        format: .number.precision(.fractionLength(0...1))
                    )
                    .textFieldStyle(.roundedBorder)
                    .frame(width: 64)
                    .help("0° = +X, 90° = top of shape (CCW)")
                }

                VStack(alignment: .leading, spacing: 2) {
                    Text("Color").font(.caption).foregroundStyle(.secondary)
                    ColorPicker(
                        "",
                        selection: Binding(
                            get: { Color(argb: self.line.colorArgb) },
                            set: { self.line.colorArgb = $0.argbInt }
                        ),
                        supportsOpacity: false
                    )
                    .labelsHidden()
                }

                Spacer()
            }
        }
        .padding(10)
        .background(RoundedRectangle(cornerRadius: 8).fill(Color(nsColor: .controlBackgroundColor)))
        .overlay(
            RoundedRectangle(cornerRadius: 8)
                .stroke(Color(nsColor: .separatorColor), lineWidth: 1)
        )
        .onAppear {
            contentDraft = line.content
        }
        .onChange(of: line.content) { _, newValue in
            if contentDraft != newValue {
                contentDraft = newValue
            }
        }
        .onDisappear {
            contentCommitTask?.cancel()
            commitContentImmediately()
        }
    }

    private func scheduleContentCommit(_ text: String) {
        contentCommitTask?.cancel()
        contentCommitTask = Task { @MainActor in
            try? await Task.sleep(nanoseconds: 700_000_000)
            guard !Task.isCancelled else { return }
            applyContentIfNeeded(text)
        }
    }

    private func commitContentImmediately() {
        contentCommitTask?.cancel()
        applyContentIfNeeded(contentDraft)
    }

    private func applyContentIfNeeded(_ text: String) {
        guard line.content != text else { return }
        var updated = line
        updated.content = text
        line = updated
    }
}
