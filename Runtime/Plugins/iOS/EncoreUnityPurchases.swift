import Combine
import Foundation
import Encore

// The purchase controller C# implements, the configure options, and the entitlement calls.

private var purchaseController: UnityPurchaseController?
private var isActiveSubscriptions: [Int64: AnyCancellable] = [:]

// A failure in or on the way to the C# controller. The SDK records it as PublisherOutcome.failed.
struct UnityPurchaseError: LocalizedError {
    let message: String
    var errorDescription: String? { message }
}

// Hands each purchase to C# and suspends until C# answers through EncoreUnity_AnswerPurchase.
@MainActor
final class UnityPurchaseController: EncorePurchaseController {
    private let sendRequest: (Int64, [String: Any]) -> Void
    private var pending: [Int64: CheckedContinuation<EncorePurchaseResult, Error>] = [:]
    private var nextRequestId: Int64 = 0

    init(sendRequest: @escaping (Int64, [String: Any]) -> Void) {
        self.sendRequest = sendRequest
    }

    // No timeout here: the SDK fails a purchase C# never answers after 300 s and cancels this task. The cancel
    // ends the request, so its continuation never waits forever.
    func purchase(_ request: PurchaseRequest) async throws -> EncorePurchaseResult {
        nextRequestId += 1
        let requestId = nextRequestId
        return try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { continuation in
                // Already cancelled: onCancel ran first, so never start a store purchase C# would then lose.
                guard !Task.isCancelled else { return continuation.resume(throwing: CancellationError()) }
                pending[requestId] = continuation
                sendRequest(requestId, UnityPurchases.purchaseRequest(requestId, request))
            }
        } onCancel: {
            Task { @MainActor in self.end(requestId, with: .failure(CancellationError())) }
        }
    }

    var pendingCount: Int { pending.count }

    // A late answer for a request that already ended is dropped.
    func answer(_ requestId: Int64, _ json: String?) {
        guard pending[requestId] != nil else { return BridgeLog.failure("EncoreUnity_AnswerPurchase", "no open request with this id") }
        end(requestId, with: UnityPurchases.purchaseReply(json, "EncoreUnity_AnswerPurchase"))
    }

    private func end(_ requestId: Int64, with result: Result<EncorePurchaseResult, Error>) {
        pending.removeValue(forKey: requestId)?.resume(with: result)
    }
}

enum UnityPurchases {
    struct ConfigureOptions: Equatable {
        let logLevel: Encore.LogLevel
        let unlock: UnlockMode
        let transactionLinking: Bool
        let hasPurchaseController: Bool
    }

    // Unknown or missing values keep the SDK defaults: optimistic, linking on, no controller.
    static func configureOptions(_ json: String?, _ call: String = #function) -> ConfigureOptions {
        let map = jsonObject(json, call) ?? [:]
        return ConfigureOptions(
            logLevel: UnitySerialization.logLevel(map["logLevel"] as? String),
            unlock: map["unlock"] as? String == "strict" ? .strict : .optimistic,
            transactionLinking: strictBool(map["transactionLinking"]) ?? true,
            hasPurchaseController: strictBool(map["hasPurchaseController"]) == true
        )
    }

    // JSONSerialization reads 1 and true alike as NSNumber; only a JSON boolean counts.
    private static func strictBool(_ raw: Any?) -> Bool? {
        guard let number = raw as? NSNumber, CFGetTypeID(number) == CFBooleanGetTypeID() else { return nil }
        return number.boolValue
    }

    // Keys as the React Native bridge's: absent optionals are left out. iOS has no base plan.
    static func purchaseRequest(_ requestId: Int64, _ request: PurchaseRequest) -> [String: Any] {
        var payload: [String: Any] = ["requestId": requestId, "productId": request.productId]
        if let placementId = request.placementId { payload["placementId"] = placementId }
        if let promoOfferId = request.promoOfferId { payload["promoOfferId"] = promoOfferId }
        return payload
    }

    // An error, or a result this bridge does not know, fails the purchase: never a phantom .purchased.
    static func purchaseReply(_ json: String?, _ call: String = #function) -> Result<EncorePurchaseResult, Error> {
        guard let json, let map = jsonObject(json, call) else {
            return .failure(UnityPurchaseError(message: "Unreadable purchase reply: \(json ?? "nil")"))
        }
        if let error = map["error"] as? String { return .failure(UnityPurchaseError(message: error)) }
        switch map["result"] as? String {
        case "purchased": return .success(.purchased)
        case "cancelled": return .success(.cancelled)
        case "pending": return .success(.pending)
        default: return .failure(UnityPurchaseError(message: "Unknown purchase result: \(json)"))
        }
    }

    // Nil for a type this bridge does not know, which reads as not active.
    static func entitlementQuery(_ json: String?, _ call: String = #function) -> (Entitlement, EntitlementScope)? {
        guard let map = jsonObject(json, call) else { return nil }
        let value = map["value"] as? Double
        let units: [String: EntitlementUnit] = ["months": .months, "days": .days, "percent": .percent, "dollars": .dollars]
        let unit = (map["unit"] as? String).flatMap { units[$0] }
        let scope: EntitlementScope = map["scope"] as? String == "verified" ? .verified : .all
        switch map["type"] as? String {
        case "freeTrial": return (.freeTrial(value: value, unit: unit), scope)
        case "discount": return (.discount(value: value, unit: unit), scope)
        case "credit": return (.credit(value: value, unit: unit), scope)
        default: return nil
        }
    }

    static func active(_ active: Bool) -> [String: Any] { ["active": active] }
    static func hasOffers(_ hasOffers: Bool) -> [String: Any] { ["hasOffers": hasOffers] }

    static func revokeResult(_ result: Result<Void, EncoreError>) -> [String: Any] {
        switch result {
        case .success: return ["success": true]
        case .failure(let error):
            return ["success": false, "errorType": UnitySerialization.errorType(error), "errorMessage": error.errorDescription ?? "\(error)"]
        }
    }
}

// Called from EncoreUnity_Configure. With no controller the SDK never attempts a purchase.
@MainActor
func unityPurchaseController(_ options: UnityPurchases.ConfigureOptions, _ onPurchase: EncoreUnityCallback?) -> UnityPurchaseController? {
    guard options.hasPurchaseController else { return nil }
    let controller = UnityPurchaseController { requestId, payload in send(onPurchase, requestId, payload, "EncoreUnity_Configure") }
    purchaseController = controller
    return controller
}

@_cdecl("EncoreUnity_AnswerPurchase")
public func EncoreUnity_AnswerPurchase(_ requestId: Int64, _ replyJson: UnsafePointer<CChar>?) {
    let json = replyJson.map { String(cString: $0) }
    onMain {
        guard let purchaseController else { return BridgeLog.failure("EncoreUnity_AnswerPurchase", "no purchase controller") }
        purchaseController.answer(requestId, json)
    }
}

@_cdecl("EncoreUnity_IsActive")
public func EncoreUnity_IsActive(_ requestId: Int64, _ queryJson: UnsafePointer<CChar>?, _ onReply: EncoreUnityCallback?) {
    guard let query = UnityPurchases.entitlementQuery(queryJson.map { String(cString: $0) }) else {
        send(onReply, requestId, UnityPurchases.active(false))
        return
    }
    Task { @MainActor in
        let active = await Encore.shared.isActive(query.0, in: query.1)
        send(onReply, requestId, UnityPurchases.active(active), "EncoreUnity_IsActive")
    }
}

@_cdecl("EncoreUnity_IsActiveSubscribe")
public func EncoreUnity_IsActiveSubscribe(_ id: Int64, _ queryJson: UnsafePointer<CChar>?, _ onReply: EncoreUnityCallback?) {
    guard let query = UnityPurchases.entitlementQuery(queryJson.map { String(cString: $0) }) else {
        send(onReply, id, UnityPurchases.active(false))
        return
    }
    onMain {
        isActiveSubscriptions[id] = Encore.shared.isActivePublisher(for: query.0, in: query.1)
            .sink { send(onReply, id, UnityPurchases.active($0), "EncoreUnity_IsActiveSubscribe") }
    }
}

@_cdecl("EncoreUnity_IsActiveUnsubscribe")
public func EncoreUnity_IsActiveUnsubscribe(_ id: Int64) {
    onMain { isActiveSubscriptions.removeValue(forKey: id)?.cancel() }
}

@_cdecl("EncoreUnity_RevokeEntitlements")
public func EncoreUnity_RevokeEntitlements(_ requestId: Int64, _ onReply: EncoreUnityCallback?) {
    onMain {
        Encore.shared.revokeEntitlements { result in send(onReply, requestId, UnityPurchases.revokeResult(result), "EncoreUnity_RevokeEntitlements") }
    }
}
