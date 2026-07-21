import Foundation
import os

/// Launches and owns the `ModelGenerator.Host` process that listens on a Unix socket.
@MainActor
final class HostProcess {
    private(set) var socketPath: String
    private var process: Process?
    private let log = Logger(subsystem: "ModelGeneratorMac", category: "HostProcess")

    init(socketPath: String? = nil) {
        if let socketPath {
            self.socketPath = socketPath
        } else {
            let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
            let dir = support.appendingPathComponent("ModelGenerator", isDirectory: true)
            try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
            self.socketPath = dir.appendingPathComponent("host.sock").path
        }
    }

    var isRunning: Bool { process?.isRunning == true }

    /// Starts the host if not already running. Returns when the socket accepts a ping, or throws.
    func ensureRunning() async throws {
        if isRunning, FileManager.default.fileExists(atPath: socketPath) {
            if await canPing() { return }
        }
        try startProcess()
        try await waitUntilReady(timeout: 45)
    }

    func stop() {
        guard let process else { return }
        if process.isRunning {
            process.terminate()
            DispatchQueue.global().async {
                process.waitUntilExit()
            }
        }
        self.process = nil
        try? FileManager.default.removeItem(atPath: socketPath)
    }

    private func startProcess() throws {
        stop()

        let hostBinary = try Self.resolveHostExecutable()
        let proc = Process()
        proc.executableURL = hostBinary
        proc.arguments = ["serve", "--socket", socketPath]

        let err = Pipe()
        proc.standardError = err
        proc.standardOutput = Pipe()

        proc.terminationHandler = { p in
            // Intentionally not capturing self strongly across threads.
            NSLog("ModelGenerator.Host exited with status %d", p.terminationStatus)
        }

        try proc.run()
        process = proc
        log.notice("Started host: \(hostBinary.path, privacy: .public) socket=\(self.socketPath, privacy: .public)")
    }

    private func waitUntilReady(timeout: TimeInterval) async throws {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            if process?.isRunning != true {
                throw HostError.hostExitedEarly
            }
            if FileManager.default.fileExists(atPath: socketPath) {
                if await canPing() { return }
            }
            try await Task.sleep(nanoseconds: 200_000_000)
        }
        throw HostError.hostNotReady
    }

    private func canPing() async -> Bool {
        do {
            let client = HostClient(socketPath: socketPath)
            _ = try await client.ping()
            return true
        } catch {
            return false
        }
    }

    /// Resolves the host executable:
    /// 1. `MODELGENERATOR_HOST` env var
    /// 2. Sibling `ModelGenerator.Host` next to this binary (`.app/Contents/MacOS/`)
    /// 3. `Contents/Resources/ModelGenerator.Host` inside the app bundle
    /// 4. `dotnet run --project …/ModelGenerator.Host` via a temp launcher (dev)
    static func resolveHostExecutable() throws -> URL {
        if let env = ProcessInfo.processInfo.environment["MODELGENERATOR_HOST"], !env.isEmpty {
            let url = URL(fileURLWithPath: env)
            if FileManager.default.isExecutableFile(atPath: url.path) {
                return url
            }
            throw HostError.hostNotFound("MODELGENERATOR_HOST=\(env) is not executable")
        }

        // Packaged .app: Contents/MacOS/ModelGenerator.Host next to the UI binary
        if let exeDir = Bundle.main.executableURL?.deletingLastPathComponent() {
            let sibling = exeDir.appendingPathComponent("ModelGenerator.Host")
            if FileManager.default.isExecutableFile(atPath: sibling.path) {
                return sibling
            }
        }

        // Alternate: Contents/Resources/ModelGenerator.Host
        if let resources = Bundle.main.resourceURL {
            let embedded = resources.appendingPathComponent("ModelGenerator.Host")
            if FileManager.default.isExecutableFile(atPath: embedded.path) {
                return embedded
            }
        }

        let repoRoot = try findRepoRoot()
        let projectPath = repoRoot.appendingPathComponent("src/ModelGenerator.Host/ModelGenerator.Host.csproj")
        guard FileManager.default.fileExists(atPath: projectPath.path) else {
            throw HostError.hostNotFound("Could not find ModelGenerator.Host.csproj at \(projectPath.path)")
        }

        guard let dotnet = which("dotnet") else {
            throw HostError.hostNotFound("dotnet not found on PATH; set MODELGENERATOR_HOST to a published host binary")
        }

        let launcher = FileManager.default.temporaryDirectory
            .appendingPathComponent("modelgenerator-host-launcher.sh")
        let script = """
        #!/bin/sh
        exec "\(dotnet)" run --project "\(projectPath.path)" --no-launch-profile -- "$@"
        """
        try script.write(to: launcher, atomically: true, encoding: .utf8)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: launcher.path)
        return launcher
    }

    private static func findRepoRoot() throws -> URL {
        var candidates: [URL] = [
            URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        ]
        if let exe = Bundle.main.executableURL {
            candidates.append(exe.deletingLastPathComponent())
        }
        // When `swift run` is invoked from mac/ModelGeneratorMac
        candidates.append(
            URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
                .appendingPathComponent("../..")
                .standardized
        )

        for start in candidates {
            var dir = start.standardized
            for _ in 0..<14 {
                let hostProj = dir.appendingPathComponent("src/ModelGenerator.Host/ModelGenerator.Host.csproj")
                if FileManager.default.fileExists(atPath: hostProj.path) {
                    return dir
                }
                let parent = dir.deletingLastPathComponent()
                if parent.path == dir.path { break }
                dir = parent
            }
        }

        throw HostError.hostNotFound("Could not locate repository root containing src/ModelGenerator.Host")
    }

    private static func which(_ name: String) -> String? {
        let proc = Process()
        proc.executableURL = URL(fileURLWithPath: "/usr/bin/which")
        proc.arguments = [name]
        let out = Pipe()
        proc.standardOutput = out
        proc.standardError = Pipe()
        do {
            try proc.run()
            proc.waitUntilExit()
        } catch {
            return nil
        }
        let data = out.fileHandleForReading.readDataToEndOfFile()
        let path = String(data: data, encoding: .utf8)?.trimmingCharacters(in: .whitespacesAndNewlines)
        return (path?.isEmpty == false) ? path : nil
    }
}

enum HostError: LocalizedError {
    case hostNotFound(String)
    case hostNotReady
    case hostExitedEarly
    case rpc(String)
    case disconnected

    var errorDescription: String? {
        switch self {
        case .hostNotFound(let msg): return msg
        case .hostNotReady: return "ModelGenerator.Host did not become ready in time."
        case .hostExitedEarly: return "ModelGenerator.Host exited before accepting connections."
        case .rpc(let msg): return msg
        case .disconnected: return "Lost connection to ModelGenerator.Host."
        }
    }
}
