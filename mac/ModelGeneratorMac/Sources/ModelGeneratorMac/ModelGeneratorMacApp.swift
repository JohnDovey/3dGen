import SwiftUI

@main
struct ModelGeneratorMacApp: App {
    @NSApplicationDelegateAdaptor(MacAppSupport.self) private var appDelegate
    @StateObject private var appModel = AppModel()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(appModel)
                .frame(minWidth: 1000, minHeight: 640)
                .onAppear {
                    appDelegate.appModel = appModel
                }
        }
        .commands {
            CommandGroup(replacing: .newItem) {
                Button("New") {
                    appModel.requestNewDocument()
                }
                .keyboardShortcut("n", modifiers: .command)

                Button("Open…") {
                    appModel.requestOpenDocument()
                }
                .keyboardShortcut("o", modifiers: .command)

                Divider()

                Button("Save") {
                    appModel.requestSave()
                }
                .keyboardShortcut("s", modifiers: .command)

                Button("Save As…") {
                    appModel.requestSaveAs()
                }
                .keyboardShortcut("s", modifiers: [.command, .shift])

                Divider()

                Button("Export STL…") {
                    appModel.exportSTL()
                }
                .keyboardShortcut("e", modifiers: [.command, .shift])
                .disabled(!appModel.canExport)
            }

            CommandGroup(replacing: .undoRedo) {
                Button("Undo") {
                    appModel.undo()
                }
                .keyboardShortcut("z", modifiers: .command)
                .disabled(!appModel.canUndo)

                Button("Redo") {
                    appModel.redo()
                }
                .keyboardShortcut("z", modifiers: [.command, .shift])
                .disabled(!appModel.canRedo)
            }
        }
        .defaultSize(width: 1200, height: 800)
    }
}
