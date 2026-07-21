import SwiftUI

struct ShapeInspectorView: View {
    @EnvironmentObject private var appModel: AppModel

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                Text("Shape")
                    .font(.headline)

                // Two rows so Custom SVG fits without crushing the segmented control
                Picker("Type", selection: Binding(
                    get: { appModel.shapeType },
                    set: { appModel.shapeType = $0 }
                )) {
                    ForEach(ShapeTypeOption.allCases) { option in
                        Text(option.description).tag(option)
                    }
                }
                .pickerStyle(.menu)

                labeledSlider("Size (mm)", value: appModel.bindingShapeSize(), range: 5...500)

                if appModel.shapeType == .rectangle {
                    labeledSlider("Height (mm)", value: appModel.bindingShapeHeight(), range: 5...500)
                }

                labeledSlider("Thickness (mm)", value: appModel.bindingThickness(), range: 0.5...100)
                labeledSlider("Border thickness (mm)", value: appModel.bindingBorderThickness(), range: 0.1...50)
                labeledSlider("Border height (mm)", value: appModel.bindingBorderHeight(), range: 0.1...50)

                HStack {
                    ColorPicker("Base", selection: appModel.bindingBaseColor(), supportsOpacity: false)
                    ColorPicker("Border", selection: appModel.bindingBorderColor(), supportsOpacity: false)
                }

                if appModel.shapeType == .customSvg {
                    customShapeSection
                }

                Divider()

                TextLinesPanelView()

                Divider()

                SvgInsertsPanelView()

                Divider()

                Button {
                    appModel.exportSTL()
                } label: {
                    Label("Export STL…", systemImage: "square.and.arrow.up")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.borderedProminent)
                .disabled(!appModel.canExport)

                if !appModel.hostVersion.isEmpty {
                    Text("Host v\(appModel.hostVersion)")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                Spacer(minLength: 0)
            }
            .padding()
        }
        .background(.background)
    }

    private var customShapeSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Custom shape SVG")
                .font(.subheadline.weight(.semibold))
            HStack(spacing: 10) {
                Group {
                    if let data = appModel.customShapeThumbnail, let ns = NSImage(data: data) {
                        Image(nsImage: ns)
                            .resizable()
                            .aspectRatio(contentMode: .fit)
                    } else {
                        Image(systemName: "square.dashed")
                            .foregroundStyle(.secondary)
                    }
                }
                .frame(width: 40, height: 40)
                .background(Color(nsColor: .controlBackgroundColor))
                .clipShape(RoundedRectangle(cornerRadius: 4))

                VStack(alignment: .leading, spacing: 2) {
                    Text(appModel.model.customShapeSourceFileName ?? "No SVG selected")
                        .font(.caption)
                        .lineLimit(1)
                    HStack {
                        Button("Choose…") {
                            appModel.openSvgLibrary(for: .customShape)
                        }
                        .controlSize(.small)
                        if appModel.model.customShapeSvgContent != nil {
                            Button("Clear", role: .destructive) {
                                appModel.clearCustomShape()
                            }
                            .controlSize(.small)
                        }
                    }
                }
            }
        }
        .padding(8)
        .background(RoundedRectangle(cornerRadius: 8).fill(Color(nsColor: .controlBackgroundColor)))
    }

    private func labeledSlider(_ title: String, value: Binding<Double>, range: ClosedRange<Double>) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack {
                Text(title)
                Spacer()
                Text(value.wrappedValue, format: .number.precision(.fractionLength(1)))
                    .monospacedDigit()
                    .foregroundStyle(.secondary)
            }
            Slider(value: value, in: range)
        }
    }
}
