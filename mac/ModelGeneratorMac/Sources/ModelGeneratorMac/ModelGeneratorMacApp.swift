import SwiftUI

@main
struct ModelGeneratorMacApp: App {
    @StateObject private var appModel = AppModel()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(appModel)
                .frame(minWidth: 1000, minHeight: 640)
        }
        .commands {
            CommandGroup(replacing: .newItem) { }

            CommandGroup(after: .newItem) {
                Button("Export STL…") {
                    appModel.exportSTL()
                }
                .keyboardShortcut("e", modifiers: [.command, .shift])
                .disabled(!appModel.canExport)
            }
        }
        .defaultSize(width: 1200, height: 800)
    }
}
