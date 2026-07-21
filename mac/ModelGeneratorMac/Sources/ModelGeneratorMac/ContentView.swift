import SwiftUI

struct ContentView: View {
    @EnvironmentObject private var appModel: AppModel

    var body: some View {
        VStack(spacing: 0) {
            HSplitView {
                ShapeInspectorView()
                    .frame(minWidth: 300, idealWidth: 360, maxWidth: 420)

                SceneViewportView()
                    .frame(minWidth: 400, maxWidth: .infinity, maxHeight: .infinity)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)

            StatusBarView()
        }
        .navigationTitle(appModel.windowTitle)
        .onAppear {
            appModel.startIfNeeded()
        }
        .alert("Error", isPresented: Binding(
            get: { appModel.alertMessage != nil },
            set: { if !$0 { appModel.alertMessage = nil } }
        )) {
            Button("OK", role: .cancel) { appModel.alertMessage = nil }
        } message: {
            Text(appModel.alertMessage ?? "")
        }
        .confirmationDialog(
            "Save changes to '\(appModel.model.name)' first?",
            isPresented: Binding(
                get: { appModel.pendingDiscardAction != nil },
                set: { if !$0 { appModel.confirmDiscardCancel() } }
            ),
            titleVisibility: .visible
        ) {
            Button("Save") { appModel.confirmDiscardSave() }
            Button("Don't Save", role: .destructive) { appModel.confirmDiscardDontSave() }
            Button("Cancel", role: .cancel) { appModel.confirmDiscardCancel() }
        }
        .sheet(isPresented: $appModel.showOpenSheet) {
            OpenModelSheet()
                .environmentObject(appModel)
        }
        .sheet(isPresented: $appModel.showSvgLibrarySheet) {
            SvgLibrarySheet()
                .environmentObject(appModel)
        }
        .sheet(isPresented: $appModel.showImageLibrarySheet) {
            ImageLibrarySheet()
                .environmentObject(appModel)
        }
        .sheet(isPresented: $appModel.showSaveNameSheet) {
            VStack(alignment: .leading, spacing: 16) {
                Text("Save Model")
                    .font(.headline)
                TextField("Model name", text: $appModel.saveNameDraft)
                    .textFieldStyle(.roundedBorder)
                    .onSubmit { appModel.confirmSaveName() }
                HStack {
                    Spacer()
                    Button("Cancel") { appModel.cancelSaveName() }
                        .keyboardShortcut(.cancelAction)
                    Button("Save") { appModel.confirmSaveName() }
                        .keyboardShortcut(.defaultAction)
                        .buttonStyle(.borderedProminent)
                }
            }
            .padding()
            .frame(width: 360)
        }
    }
}
