import AppKit
import Foundation

/// Installed font family names for the text-line picker (same role as WinForms
/// `InstalledFontCollection` / owner-drawn font combo).
enum FontCatalog {
    /// Prefer a family that exists on both Windows samples and macOS when possible.
    static var preferredDefaultFamily: String {
        let preferred = ["Arial", "Helvetica Neue", "Helvetica", "San Francisco", ".AppleSystemUIFont"]
        let available = Set(families)
        for name in preferred where available.contains(name) {
            return name
        }
        return families.first ?? "Helvetica"
    }

    /// Sorted list of system font family names.
    static let families: [String] = {
        let names = NSFontManager.shared.availableFontFamilies
        return names.sorted { $0.localizedCaseInsensitiveCompare($1) == .orderedAscending }
    }()

    /// If `name` is not installed, return a sensible fallback.
    static func resolve(_ name: String) -> String {
        if families.contains(name) { return name }
        return preferredDefaultFamily
    }
}
