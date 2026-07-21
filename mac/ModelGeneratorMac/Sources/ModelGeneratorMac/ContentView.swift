import SwiftUI

struct ContentView: View {
    @Environment(AppModel.self) private var appModel

    var body: some View {
        @Bindable var appModel = appModel

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
                .environment(appModel)
        }
        .sheet(isPresented: $appModel.showSvgLibrarySheet) {
            SvgLibrarySheet()
                .environment(appModel)
        }
        .sheet(isPresented: $appModel.showImageLibrarySheet) {
            ImageLibrarySheet()
                .environment(appModel)
        }
        .sheet(isPresented: $appModel.showSaveNameSheet) {
            // Separate view + local draft so typing the name does not re-render the main UI.
            SaveModelNameSheet()
                .environment(appModel)
        }
    }
}

/// Save-name UI keeps draft text in `@State` so keystrokes never touch AppModel until Save.
private struct SaveModelNameSheet: View {
    @Environment(AppModel.self) private var appModel
    @State private var nameDraft: String = ""
    @FocusState private var nameFocused: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Save Model")
                .font(.headline)
            TextField("Model name", text: $nameDraft)
                .textFieldStyle(.roundedBorder)
                .focused($nameFocused)
                .onSubmit { commit() }
            HStack {
                Spacer()
                Button("Cancel") { appModel.cancelSaveName() }
                    .keyboardShortcut(.cancelAction)
                Button("Save") { commit() }
                    .keyboardShortcut(.defaultAction)
                    .buttonStyle(.borderedProminent)
            }
        }
        .padding()
        .frame(width: 360)
        .onAppear {
            nameDraft = appModel.saveNameDraft
            nameFocused = true
        }
    }

    private func commit() {
        appModel.saveNameDraft = nameDraft
        appModel.confirmSaveName()
    }
}
