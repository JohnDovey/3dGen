import SwiftUI

struct TextLinesPanelView: View {
    @Environment(AppModel.self) private var appModel

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text("Text lines")
                    .font(.headline)
                Spacer()
                Button {
                    appModel.addTextLine()
                } label: {
                    Label("Add", systemImage: "plus")
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
            }

            if appModel.model.textLines.isEmpty {
                Text("No text lines — click Add to emboss text.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            ForEach(Array(appModel.model.textLines.enumerated()), id: \.element.id) { index, _ in
                TextLineEditorView(
                    index: index,
                    line: appModel.textLineBinding(at: index),
                    canRemove: true
                )
                .id(appModel.model.textLines[index].id)
            }
        }
    }
}

struct TextLineEditorView: View {
    let index: Int
    @Binding var line: WireTextLine
    let canRemove: Bool
    @Environment(AppModel.self) private var appModel

    /// Local draft so keystrokes don't rewrite model every character.
    @State private var contentDraft: String = ""
    @State private var contentCommitTask: Task<Void, Never>?

    private var positionMode: Binding<PositionModeOption> {
        Binding(
            get: { self.line.positionModeOption },
            set: { self.line.positionModeOption = $0 }
        )
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text("Line \(index + 1)")
                    .font(.subheadline.weight(.semibold))
                Spacer()
                Button(role: .destructive) {
                    appModel.removeTextLine(at: index)
                } label: {
                    Image(systemName: "trash")
                }
                .buttonStyle(.borderless)
                .disabled(!canRemove)
                .help("Remove this text line")
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
                    Text("Emboss (mm)").font(.caption).foregroundStyle(.secondary)
                    TextField(
                        "Emboss",
                        value: $line.textHeight,
                        format: .number.precision(.fractionLength(0...2))
                    )
                    .textFieldStyle(.roundedBorder)
                    .frame(width: 72)
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

            VStack(alignment: .leading, spacing: 4) {
                Text("Position").font(.caption).foregroundStyle(.secondary)
                Picker("Position", selection: positionMode) {
                    ForEach(PositionModeOption.allCases) { mode in
                        Text(mode.description).tag(mode)
                    }
                }
                .pickerStyle(.segmented)
            }

            HStack {
                coordField("X", value: $line.positionX, enabled: line.positionModeOption.showsXYZ)
                coordField("Y", value: $line.positionY, enabled: line.positionModeOption.showsXYZ)
                coordField("Z", value: $line.positionZ, enabled: line.positionModeOption.showsXYZ)
                coordField("Rot°", value: $line.rotationZ, enabled: true)
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
            // External load / undo — sync draft without re-entrancy loop.
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
            // Wait until typing pauses before committing + regenerating mesh.
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

    private func coordField(_ title: String, value: Binding<Float>, enabled: Bool) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title).font(.caption2).foregroundStyle(.secondary)
            TextField(
                title,
                value: value,
                format: .number.precision(.fractionLength(0...2))
            )
            .textFieldStyle(.roundedBorder)
            .disabled(!enabled)
            .opacity(enabled ? 1 : 0.45)
        }
    }
}
