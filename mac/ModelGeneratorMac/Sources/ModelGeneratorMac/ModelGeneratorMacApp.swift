import AppKit
import SwiftUI

@main
struct ModelGeneratorMacApp: App {
    @NSApplicationDelegateAdaptor(MacAppSupport.self) private var appDelegate
    @StateObject private var appModel = AppModel()
    @State private var showHelp = false
    @State private var showAbout = false

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(appModel)
                .frame(minWidth: 1000, minHeight: 640)
                .onAppear {
                    appDelegate.appModel = appModel
                    // Ensure window is key when launched via `swift run` from a terminal.
                    NSApp.setActivationPolicy(.regular)
                    NSApp.activate(ignoringOtherApps: true)
                }
                .sheet(isPresented: $showHelp) {
                    HelpView()
                }
                .sheet(isPresented: $showAbout) {
                    AboutView()
                }
        }
        .commands {
            CommandGroup(replacing: .appInfo) {
                Button("About 3D Model Generator") {
                    showAbout = true
                }
            }

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

                Button("Export Project…") {
                    appModel.exportProject()
                }
                .keyboardShortcut("p", modifiers: [.command, .shift])

                Button("Import Project…") {
                    appModel.requestImportProject()
                }
                .keyboardShortcut("i", modifiers: [.command, .shift])

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

            CommandGroup(replacing: .help) {
                Button("How to Use 3D Model Generator") {
                    showHelp = true
                }
                .keyboardShortcut("/", modifiers: [.command, .shift])
            }
        }
        .defaultSize(width: 1200, height: 800)
    }
}
