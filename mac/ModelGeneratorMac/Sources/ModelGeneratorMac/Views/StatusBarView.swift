import SwiftUI

struct StatusBarView: View {
    @Environment(AppModel.self) private var appModel

    var body: some View {
        HStack(spacing: 10) {
            Circle()
                .fill(appModel.isConnected ? Color.green : Color.red)
                .frame(width: 8, height: 8)
            Text(appModel.statusText)
                .font(.callout)
                .foregroundStyle(appModel.statusIsError ? .red : .primary)
                .lineLimit(2)
            Spacer()
            if appModel.isBusy {
                ProgressView()
                    .controlSize(.small)
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
        .background(.bar)
    }
}
