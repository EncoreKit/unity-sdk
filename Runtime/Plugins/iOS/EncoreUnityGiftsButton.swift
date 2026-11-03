import UIKit
@_spi(EncoreBridge) import Encore

// Hosts a native trigger view (the gift button, the claim reminder, or any trigger id) over the Unity view, at a rect
// C# gives in Unity screen pixels (top-left origin). Unity has no embedded native views, so it floats above the game.

@MainActor var giftsButtons: [Int64: GiftsButtonHost] = [:]
// The view the hosts float over. Tests replace it, because a test process has no Unity window.
@MainActor var unityRootViewProvider: @MainActor () -> UIView? = unityRootView

@_cdecl("EncoreUnity_GiftsButtonCreate")
public func EncoreUnity_GiftsButtonCreate(_ id: Int64, _ optionsJson: UnsafePointer<CChar>?, _ callback: EncoreUnityCallback?) {
    guard callback != nil else { return BridgeLog.failure(#function, "no callback") }
    let options = jsonObject(optionsJson.map { String(cString: $0) }) ?? [:]
    guard let create = UnitySerialization.triggerCreate(options) else {
        BridgeLog.failure(#function, "no trigger id, placement or known use case")
        send(callback, id, UnitySerialization.giftsButtonHeight(0))
        return
    }
    let look = UnitySerialization.triggerLook(options)
    onMain {
        giftsButtons.removeValue(forKey: id)?.tearDown()
        // Fail closed before building the view: no window means nothing shows, and C# reads height 0.
        guard let root = unityRootViewProvider() else {
            BridgeLog.failure("EncoreUnity_GiftsButtonCreate", "no Unity root view")
            send(callback, id, UnitySerialization.giftsButtonHeight(0))
            return
        }
        let trigger = EncoreTriggerView(
            id: create.triggerId,
            placement: create.placement,
            useCase: .rewardUsers,
            autoOpen: create.autoOpenOff ? .off : nil,
            icon: look.icon,
            appearance: look.appearance,
            alignment: look.alignment
        )
        trigger.publisherReward = look.publisherReward
        // After the init: the init's own assignments run no didSet.
        applyText(trigger, options)
        trigger.values = UnitySerialization.values(options) ?? [:]
        trigger.onResult = { result in
            send(callback, id, UnitySerialization.giftsButtonResult(result), "EncoreUnity_GiftsButtonCreate")
        }
        let host = GiftsButtonHost(trigger: trigger) { height in
            send(callback, id, UnitySerialization.giftsButtonHeight(height), "EncoreUnity_GiftsButtonCreate")
        }
        root.addSubview(host)
        giftsButtons[id] = host
    }
}

@_cdecl("EncoreUnity_GiftsButtonSetFrame")
public func EncoreUnity_GiftsButtonSetFrame(_ id: Int64, _ x: Float, _ y: Float, _ width: Float, _ screenWidth: Float, _ screenHeight: Float) {
    onMain { giftsButtons[id]?.place(x: x, y: y, width: width, screenWidth: screenWidth, screenHeight: screenHeight) }
}

@_cdecl("EncoreUnity_GiftsButtonSetVisible")
public func EncoreUnity_GiftsButtonSetVisible(_ id: Int64, _ visible: Bool) {
    onMain { giftsButtons[id]?.setVisible(visible) }
}

// The native view redraws in place; recreating it would restart its mount and its impression.
@_cdecl("EncoreUnity_GiftsButtonUpdate")
public func EncoreUnity_GiftsButtonUpdate(_ id: Int64, _ optionsJson: UnsafePointer<CChar>?) {
    let options = jsonObject(optionsJson.map { String(cString: $0) }) ?? [:]
    onMain {
        guard let trigger = giftsButtons[id]?.trigger else { return }
        if let text = options["text"] as? [String: Any] { applyText(trigger, text) }
        if let values = UnitySerialization.values(options) { trigger.values = values }
        let update = UnitySerialization.triggerUpdate(options)
        if let alignment = update.alignment { trigger.alignment = alignment }
        if let appearance = update.appearance { trigger.appearance = appearance }
        if let reward = update.publisherReward { trigger.publisherReward = reward }
    }
}

@MainActor private func applyText(_ trigger: EncoreTriggerView, _ options: [String: Any]) {
    let text = UnitySerialization.triggerText(options)
    trigger.headline = text.headline
    trigger.subheadline = text.subheadline
    trigger.cta = text.cta
}

@_cdecl("EncoreUnity_GiftsButtonDestroy")
public func EncoreUnity_GiftsButtonDestroy(_ id: Int64) {
    onMain { giftsButtons.removeValue(forKey: id)?.tearDown() }
}

extension UnitySerialization {
    struct TriggerCreate: Equatable {
        let triggerId: String
        let placement: String
        let autoOpenOff: Bool
    }

    // Fail closed, as ShowPlacement does: no trigger id, no placement or an unknown use case shows nothing.
    // An id the native SDK does not know is the native view's call, as in the Flutter bridge.
    static func triggerCreate(_ options: [String: Any]) -> TriggerCreate? {
        let text: (String) -> String = { key in (options[key] as? String)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? "" }
        let triggerId = text("triggerId"), placement = text("placement")
        guard !triggerId.isEmpty, !placement.isEmpty, options["useCase"] as? String == UseCase.rewardUsers.rawValue else { return nil }
        return TriggerCreate(triggerId: triggerId, placement: placement, autoOpenOff: options["autoOpen"] as? String == AutoOpen.off.rawValue)
    }

    struct TriggerText: Equatable {
        let headline: String?
        let subheadline: String?
        let cta: String?
    }

    // An empty or absent slot is nil, so the look keeps its own text.
    static func triggerText(_ options: [String: Any]) -> TriggerText {
        let text: (String) -> String? = { key in (options[key] as? String).flatMap { $0.isEmpty ? nil : $0 } }
        return TriggerText(headline: text("headline"), subheadline: text("subheadline"), cta: text("cta"))
    }

    // Nil when the message carries no values.
    static func values(_ options: [String: Any]) -> [String: String]? { options["values"] as? [String: String] }
}

@MainActor private func unityRootView() -> UIView? {
    let windows = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.flatMap(\.windows)
    return (windows.first(where: \.isKeyWindow) ?? windows.first)?.rootViewController?.view
}

@MainActor final class GiftsButtonHost: UIView {
    let trigger: EncoreTriggerView
    private var onHeight: ((Double) -> Void)?
    // Points per Unity screen pixel. Unity can render below the display resolution.
    private var scaleY: CGFloat = 1
    private var reportedHeight: Double = -1

    init(trigger: EncoreTriggerView, onHeight: @escaping (Double) -> Void) {
        self.trigger = trigger
        self.onHeight = onHeight
        super.init(frame: .zero)
        // Hidden until C# places it, so it never flashes at the top-left corner.
        isHidden = true
        clipsToBounds = true
        trigger.translatesAutoresizingMaskIntoConstraints = false
        addSubview(trigger)
        NSLayoutConstraint.activate([
            trigger.leadingAnchor.constraint(equalTo: leadingAnchor),
            trigger.trailingAnchor.constraint(equalTo: trailingAnchor),
            trigger.topAnchor.constraint(equalTo: topAnchor),
        ])
    }

    // After this, nothing from this host or its trigger reaches C#.
    func tearDown() {
        trigger.onResult = nil
        onHeight = nil
        removeFromSuperview()
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) { fatalError("init(coder:) is not supported") }

    // A floating overlay that C# places, so it passes no safe area to the card. Otherwise an inset taken while it
    // first sat over the home indicator stays on the card after it moves, and the card draws too high and clips.
    override var safeAreaInsets: UIEdgeInsets { .zero }

    // A NaN or infinite frame makes CALayer raise, which crashes the game, so a bad rect is logged and dropped.
    func place(x: Float, y: Float, width: Float, screenWidth: Float, screenHeight: Float) {
        guard let root = superview else { return }
        // A root with no height makes scaleY 0, and layoutSubviews divides by it.
        guard [x, y, width, screenWidth, screenHeight].allSatisfy(\.isFinite), width >= 0, screenWidth > 0, screenHeight > 0,
              root.bounds.height > 0 else {
            return BridgeLog.failure("EncoreUnity_GiftsButtonSetFrame", "rect or root view size is not usable")
        }
        let scaleX = root.bounds.width / CGFloat(screenWidth)
        scaleY = root.bounds.height / CGFloat(screenHeight)
        frame = CGRect(x: CGFloat(x) * scaleX, y: CGFloat(y) * scaleY, width: CGFloat(width) * scaleX, height: frame.height)
        if isHidden { setVisible(true) }
        setNeedsLayout()
    }

    func setVisible(_ visible: Bool) {
        isHidden = !visible
        // Impressions count only while the game shows the button.
        trigger.hostVisibleFraction = visible ? 1 : 0
    }

    // The trigger sizes itself: zero when it shows nothing, its card height when it shows.
    override func layoutSubviews() {
        guard bounds.width > 0 else { return super.layoutSubviews() }
        let fitting = trigger.systemLayoutSizeFitting(
            CGSize(width: bounds.width, height: UIView.layoutFittingCompressedSize.height),
            withHorizontalFittingPriority: .required,
            verticalFittingPriority: .fittingSizeLevel
        )
        let height = fitting.height.isFinite && fitting.height > 0 ? ceil(fitting.height) : 0
        if frame.height != height { frame.size.height = height }
        super.layoutSubviews()
        let unityHeight = Double(height / scaleY)
        guard unityHeight != reportedHeight else { return }
        reportedHeight = unityHeight
        onHeight?(unityHeight)
    }
}
