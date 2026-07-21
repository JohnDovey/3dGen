import Foundation

/// NDJSON JSON-RPC client over a Unix domain socket (Host protocol 1.0).
actor HostClient {
    private let socketPath: String
    private var requestCounter: UInt64 = 0

    init(socketPath: String) {
        self.socketPath = socketPath
    }

    func ping() async throws -> PingResult {
        try await call(method: "ping", params: [:] as [String: String], as: PingResult.self)
    }

    func generateParts(model: WireModel) async throws -> GeneratePartsResult {
        try await call(method: "generateParts", params: ["model": model], as: GeneratePartsResult.self)
    }

    func exportStl(model: WireModel, path: String) async throws -> ExportStlResult {
        struct Params: Encodable {
            let model: WireModel
            let path: String
        }
        return try await call(method: "exportStl", params: Params(model: model, path: path), as: ExportStlResult.self)
    }

    func exportProject(model: WireModel, path: String, appVersion: String? = nil) async throws -> ExportProjectResult {
        struct Params: Encodable {
            let model: WireModel
            let path: String
            let appVersion: String?
        }
        return try await call(
            method: "exportProject",
            params: Params(model: model, path: path, appVersion: appVersion),
            as: ExportProjectResult.self
        )
    }

    func importProject(path: String) async throws -> WireModel {
        struct Params: Encodable { let path: String }
        let result: ImportProjectResult = try await call(
            method: "importProject",
            params: Params(path: path),
            as: ImportProjectResult.self
        )
        return result.model
    }

    func listModels() async throws -> ListModelsResult {
        try await call(method: "listModels", params: [:] as [String: String], as: ListModelsResult.self)
    }

    func getModel(id: Int) async throws -> WireModel {
        struct Params: Encodable { let id: Int }
        let result: GetModelResult = try await call(method: "getModel", params: Params(id: id), as: GetModelResult.self)
        return result.model
    }

    func saveModel(_ model: WireModel, saveMesh: Bool = true) async throws -> SaveModelResult {
        struct Params: Encodable {
            let model: WireModel
            let saveMesh: Bool
        }
        return try await call(method: "saveModel", params: Params(model: model, saveMesh: saveMesh), as: SaveModelResult.self)
    }

    func deleteModel(id: Int) async throws -> DeleteModelResult {
        struct Params: Encodable { let id: Int }
        return try await call(method: "deleteModel", params: Params(id: id), as: DeleteModelResult.self)
    }

    // MARK: SVG library

    func listSvgFiles(query: String = "") async throws -> SvgLibraryListResult {
        struct Params: Encodable { let query: String }
        return try await call(method: "listSvgFiles", params: Params(query: query), as: SvgLibraryListResult.self)
    }

    func readSvgContent(fileName: String) async throws -> SvgContentResult {
        struct Params: Encodable { let fileName: String }
        return try await call(method: "readSvgContent", params: Params(fileName: fileName), as: SvgContentResult.self)
    }

    func importSvgFile(path: String) async throws -> SvgImportResult {
        struct Params: Encodable { let path: String }
        return try await call(method: "importSvgFile", params: Params(path: path), as: SvgImportResult.self)
    }

    func deleteSvgFile(fileName: String) async throws -> SvgImportResult {
        struct Params: Encodable { let fileName: String }
        return try await call(method: "deleteSvgFile", params: Params(fileName: fileName), as: SvgImportResult.self)
    }

    func setSvgKeywords(fileName: String, keywords: [String]) async throws -> SvgKeywordsResult {
        struct Params: Encodable {
            let fileName: String
            let keywords: [String]
        }
        return try await call(
            method: "setSvgKeywords",
            params: Params(fileName: fileName, keywords: keywords),
            as: SvgKeywordsResult.self
        )
    }

    func renderSvgThumbnail(fileName: String? = nil, svgContent: String? = nil, width: Int = 64, height: Int = 64) async throws -> SvgThumbnailResult {
        struct Params: Encodable {
            let fileName: String?
            let svgContent: String?
            let width: Int
            let height: Int
        }
        return try await call(
            method: "renderSvgThumbnail",
            params: Params(fileName: fileName, svgContent: svgContent, width: width, height: height),
            as: SvgThumbnailResult.self
        )
    }

    // MARK: Image library

    func listImageFiles(query: String = "") async throws -> ImageLibraryListResult {
        struct Params: Encodable { let query: String }
        return try await call(method: "listImageFiles", params: Params(query: query), as: ImageLibraryListResult.self)
    }

    func readImageBytes(fileName: String) async throws -> ImageBytesResult {
        struct Params: Encodable { let fileName: String }
        return try await call(method: "readImageBytes", params: Params(fileName: fileName), as: ImageBytesResult.self)
    }

    func importImageFile(path: String) async throws -> ImageImportResult {
        struct Params: Encodable { let path: String }
        return try await call(method: "importImageFile", params: Params(path: path), as: ImageImportResult.self)
    }

    func deleteImageFile(fileName: String) async throws -> ImageImportResult {
        struct Params: Encodable { let fileName: String }
        return try await call(method: "deleteImageFile", params: Params(fileName: fileName), as: ImageImportResult.self)
    }

    func setImageKeywords(fileName: String, keywords: [String]) async throws -> ImageKeywordsResult {
        struct Params: Encodable {
            let fileName: String
            let keywords: [String]
        }
        return try await call(
            method: "setImageKeywords",
            params: Params(fileName: fileName, keywords: keywords),
            as: ImageKeywordsResult.self
        )
    }

    func renderImageThumbnail(fileName: String? = nil, imageData: Data? = nil, width: Int = 64, height: Int = 64) async throws -> ImageThumbnailResult {
        struct Params: Encodable {
            let fileName: String?
            let imageData: Data?
            let width: Int
            let height: Int
        }
        return try await call(
            method: "renderImageThumbnail",
            params: Params(fileName: fileName, imageData: imageData, width: width, height: height),
            as: ImageThumbnailResult.self
        )
    }

    private func call<P: Encodable, R: Decodable>(method: String, params: P, as type: R.Type) async throws -> R {
        requestCounter += 1
        let id = String(requestCounter)

        let request = RpcRequest(id: id, method: method, params: params)
        let encoder = JSONEncoder()
        encoder.outputFormatting = []
        // camelCase is default for CodingKeys on our types; Core expects camelCase property names.
        // WireModel uses camelCase property names already matching HostProtocol.

        var requestData = try encoder.encode(request)
        requestData.append(contentsOf: "\n".utf8)

        let responseData = try await send(requestData)
        // Slice result/error out with JSONSerialization — avoids re-encoding the whole
        // generateParts mesh through a Double-only tree (multi‑MB, multi‑second).
        let (errorMessage, resultData) = try Self.splitRpcResponse(responseData)
        if let errorMessage {
            throw HostError.rpc(errorMessage)
        }
        guard let resultData else {
            throw HostError.rpc("Missing result for \(method)")
        }
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        do {
            return try decoder.decode(R.self, from: resultData)
        } catch let error as DecodingError {
            throw HostError.rpc("\(method) decode failed: \(Self.describeDecodingError(error))")
        } catch {
            throw error
        }
    }

    private static func describeDecodingError(_ error: DecodingError) -> String {
        switch error {
        case .typeMismatch(let type, let ctx):
            return "type mismatch (\(type)) at \(ctx.codingPath.map(\.stringValue).joined(separator: ".")): \(ctx.debugDescription)"
        case .valueNotFound(let type, let ctx):
            return "missing \(type) at \(ctx.codingPath.map(\.stringValue).joined(separator: "."))"
        case .keyNotFound(let key, let ctx):
            return "key '\(key.stringValue)' not found at \(ctx.codingPath.map(\.stringValue).joined(separator: "."))"
        case .dataCorrupted(let ctx):
            return "data corrupted at \(ctx.codingPath.map(\.stringValue).joined(separator: ".")): \(ctx.debugDescription)"
        @unknown default:
            return error.localizedDescription
        }
    }

    /// Extract `error.message` or raw `result` JSON bytes from an RPC response line.
    private static func splitRpcResponse(_ responseData: Data) throws -> (error: String?, result: Data?) {
        let obj = try JSONSerialization.jsonObject(with: responseData, options: [.fragmentsAllowed])
        guard let root = obj as? [String: Any] else {
            throw HostError.rpc("Invalid RPC response")
        }
        if let err = root["error"] as? [String: Any] {
            let message = (err["message"] as? String) ?? "RPC error"
            return (message, nil)
        }
        guard let result = root["result"] else {
            return (nil, nil)
        }
        if result is NSNull {
            return (nil, nil)
        }
        let data = try JSONSerialization.data(withJSONObject: result, options: [])
        return (nil, data)
    }

    private func send(_ requestLine: Data) async throws -> Data {
        try await withCheckedThrowingContinuation { continuation in
            DispatchQueue.global(qos: .userInitiated).async {
                do {
                    let data = try Self.unixRoundTrip(socketPath: self.socketPath, request: requestLine)
                    continuation.resume(returning: data)
                } catch {
                    continuation.resume(throwing: error)
                }
            }
        }
    }

    /// Connect, write one request line, read one response line, disconnect.
    /// Stateless connections keep the host simple and avoid concurrent write races.
    private static func unixRoundTrip(socketPath: String, request: Data) throws -> Data {
        let fd = socket(AF_UNIX, SOCK_STREAM, 0)
        guard fd >= 0 else {
            throw HostError.disconnected
        }
        defer { close(fd) }

        var addr = sockaddr_un()
        addr.sun_family = sa_family_t(AF_UNIX)
        let pathBytes = socketPath.utf8CString
        guard pathBytes.count <= MemoryLayout.size(ofValue: addr.sun_path) else {
            throw HostError.rpc("Socket path too long")
        }
        withUnsafeMutablePointer(to: &addr.sun_path) { ptr in
            ptr.withMemoryRebound(to: CChar.self, capacity: pathBytes.count) { cptr in
                for (i, b) in pathBytes.enumerated() {
                    cptr[i] = b
                }
            }
        }

        let addrLen = socklen_t(MemoryLayout<sockaddr_un>.size)
        let connectResult = withUnsafePointer(to: &addr) { ptr in
            ptr.withMemoryRebound(to: sockaddr.self, capacity: 1) { sockPtr in
                connect(fd, sockPtr, addrLen)
            }
        }
        guard connectResult == 0 else {
            throw HostError.disconnected
        }

        var written = 0
        while written < request.count {
            let n = request.withUnsafeBytes { buf -> Int in
                guard let base = buf.baseAddress else { return -1 }
                return Darwin.write(fd, base.advanced(by: written), request.count - written)
            }
            if n <= 0 { throw HostError.disconnected }
            written += n
        }

        var response = Data()
        var buffer = [UInt8](repeating: 0, count: 64 * 1024)
        while true {
            let n = Darwin.read(fd, &buffer, buffer.count)
            if n < 0 { throw HostError.disconnected }
            if n == 0 { break }
            response.append(buffer, count: n)
            if response.contains(UInt8(ascii: "\n")) { break }
        }

        // Trim to first line
        if let nl = response.firstIndex(of: UInt8(ascii: "\n")) {
            response = response.subdata(in: response.startIndex..<nl)
        }
        guard !response.isEmpty else { throw HostError.disconnected }
        return response
    }
}

// MARK: - RPC envelopes

private struct RpcRequest<P: Encodable>: Encodable {
    let id: String
    let method: String
    let params: P
}
