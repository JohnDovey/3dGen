import SwiftUI

struct OpenModelSheet: View {
    @Environment(AppModel.self) private var appModel
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Open Model")
                .font(.title2.weight(.semibold))

            if appModel.modelSummaries.isEmpty {
                Text("No saved models yet.")
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                Table(appModel.modelSummaries) {
                    TableColumn("Name") { row in
                        Text(row.name)
                    }
                    TableColumn("Shape") { row in
                        Text(row.shapeLabel)
                    }
                    TableColumn("Modified") { row in
                        if let d = row.modifiedDate {
                            Text(d, style: .date)
                        } else {
                            Text("—")
                        }
                    }
                    TableColumn("") { row in
                        HStack {
                            Button("Open") {
                                appModel.openModel(id: row.id)
                            }
                            .buttonStyle(.borderedProminent)
                            .controlSize(.small)

                            Button("Delete", role: .destructive) {
                                appModel.deleteModelSummary(id: row.id)
                            }
                            .controlSize(.small)
                        }
                    }
                    .width(140)
                }
            }

            HStack {
                Spacer()
                Button("Cancel") {
                    dismiss()
                    appModel.showOpenSheet = false
                }
                .keyboardShortcut(.cancelAction)
            }
        }
        .padding()
        .frame(minWidth: 560, minHeight: 360)
    }
}
