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
        .onDisappear {
            // Window close is not app quit; host stays up for multi-window later.
        }
        .alert("Error", isPresented: Binding(
            get: { appModel.alertMessage != nil },
            set: { if !$0 { appModel.alertMessage = nil } }
        )) {
            Button("OK", role: .cancel) { appModel.alertMessage = nil }
        } message: {
            Text(appModel.alertMessage ?? "")
        }
    }
}
