import Foundation
@_spi(EncoreBridge) import Encore
import os

// The C surface IosPlatform.cs calls through [DllImport("__Internal")]. Results go back as JSON over a C callback.
public typealias EncoreUnityCallback = @convention(c) (Int64, UnsafePointer<CChar>?) -> Void

private var outcomesTask: Task<Void, Never>?

// Every contained failure goes here: the local log, and the SDK's error reports. Reasons are fixed text: never a
// payload, which can carry user attributes.
enum BridgeLog {
    private static let logger = Logger(subsystem: "com.encorekit.unity", category: "bridge")
    // Off until Configure asks for WARN or more, the level the C# layer logs contained failures at.
    static var logsFailures = false
    static var sink: (_ call: String, _ reason: String) -> Void = { call, reason in
        guard logsFailures else { return }
        logger.error("\(call, privacy: .public): \(reason, privacy: .public)")
    }

    // The SDK call, which drops a report before configure. A test replaces it to read what is sent.
    static var sendReport: (_ site: String, _ message: String, _ code: String?) -> Void = { site, message, code in
        Encore.reportWrapperError(where: site, message: message, code: code)
    }

    // "EncoreUnity_ShowPlacement(_:_:_:)" from #function reports as "EncoreUnity_ShowPlacement".
    static func site(_ call: String) -> String { String(call.prefix { $0 != "(" }) }

    // The fixed reason doubles as the code, so reports group by it.
    static func failure(_ call: String, _ reason: String) {
        sink(call, reason)
        sendReport(site(call), reason, reason)
    }
}

// Unity calls on the main thread, so run in place to keep call order; Task only as a fallback.
func onMain(_ body: @escaping @MainActor () -> Void) {
    if Thread.isMainThread {
        MainActor.assumeIsolated(body)
    } else {
        Task { @MainActor in body() }
    }
}

private func string(_ pointer: UnsafePointer<CChar>?) -> String? {
    guard let pointer else { return nil }
    let value = String(cString: pointer)
    return value.isEmpty ? nil : value
}

// A JSON object, or nil. Unreadable JSON is logged, and the call then keeps its defaults.
func jsonObject(_ json: String?, _ call: String = #function) -> [String: Any]? {
    guard let json, !json.isEmpty else { return nil }
    guard let map = (try? JSONSerialization.jsonObject(with: Data(json.utf8))) as? [String: Any] else {
        BridgeLog.failure(call, "unreadable JSON")
        return nil
    }
    return map
}

// The one path to C#, always on the main thread. JSONSerialization raises, and does not throw, on NaN or infinity.
func send(_ callback: EncoreUnityCallback?, _ requestId: Int64, _ payload: [String: Any], _ call: String = #function) {
    guard let callback else { return BridgeLog.failure(call, "no callback") }
    var data = Data("{}".utf8)
    if JSONSerialization.isValidJSONObject(payload), let valid = try? JSONSerialization.data(withJSONObject: payload) {
        data = valid
    } else {
        BridgeLog.failure(call, "payload is not valid JSON")
    }
    let json = String(decoding: data, as: UTF8.self)
    if Thread.isMainThread {
        json.withCString { callback(requestId, $0) }
    } else {
        DispatchQueue.main.async { json.withCString { callback(requestId, $0) } }
    }
}

@_cdecl("EncoreUnity_Configure")
public func EncoreUnity_Configure(
    _ apiKey: UnsafePointer<CChar>?, _ optionsJson: UnsafePointer<CChar>?, _ onOutcome: EncoreUnityCallback?, _ onPurchase: EncoreUnityCallback?
) {
    guard let apiKey = string(apiKey) else { return BridgeLog.failure(#function, "no API key") }
    let options = UnityPurchases.configureOptions(string(optionsJson))
    BridgeLog.logsFailures = UnitySerialization.logsFailures(options.logLevel)
    onMain {
        Encore.shared.configure(
            apiKey: apiKey,
            purchaseController: unityPurchaseController(options, onPurchase),
            options: Encore.Options(logLevel: options.logLevel, unlock: options.unlock, transactionLinking: options.transactionLinking)
        )
        outcomesTask?.cancel()
        outcomesTask = Task { @MainActor in
            for await outcome in Encore.shared.outcomes {
                if Task.isCancelled { return }
                send(onOutcome, 0, UnitySerialization.placementOutcome(outcome), "EncoreUnity_Configure")
            }
        }
    }
}

@_cdecl("EncoreUnity_Identify")
public func EncoreUnity_Identify(_ userId: UnsafePointer<CChar>?, _ attributesJson: UnsafePointer<CChar>?) {
    guard let userId = string(userId) else { return BridgeLog.failure(#function, "no user id") }
    let attributes = UnitySerialization.userAttributes(string(attributesJson))
    onMain { Encore.shared.identify(userId: userId, attributes: attributes) }
}

@_cdecl("EncoreUnity_SetUserAttributes")
public func EncoreUnity_SetUserAttributes(_ attributesJson: UnsafePointer<CChar>?) {
    guard let attributes = UnitySerialization.userAttributes(string(attributesJson)) else { return }
    onMain { Encore.shared.setUserAttributes(attributes) }
}

@_cdecl("EncoreUnity_SetLanguage")
public func EncoreUnity_SetLanguage(_ tag: UnsafePointer<CChar>?) {
    guard let tag = string(tag) else { return BridgeLog.failure(#function, "no language tag") }
    onMain { Encore.shared.setLanguage(tag) }
}

@_cdecl("EncoreUnity_ClearLanguage")
public func EncoreUnity_ClearLanguage() {
    onMain { Encore.shared.clearLanguage() }
}

@_cdecl("EncoreUnity_Reset")
public func EncoreUnity_Reset() {
    onMain { Encore.shared.reset() }
}

@_cdecl("EncoreUnity_ShowPlacement")
public func EncoreUnity_ShowPlacement(_ requestId: Int64, _ requestJson: UnsafePointer<CChar>?, _ onResult: EncoreUnityCallback?) {
    let request = UnitySerialization.placementRequest(string(requestJson))
    // Fail closed: a use case this bridge does not know must never become a presentation.
    guard let useCase = request.useCase else {
        send(onResult, requestId, UnitySerialization.useCaseUnavailable)
        return
    }
    Task { @MainActor in
        var builder = Encore.placement(request.placementId).useCase(useCase)
        if let headline = request.headline { builder = builder.headline(headline) }
        if let subheadline = request.subheadline { builder = builder.subheadline(subheadline) }
        if let cta = request.cta { builder = builder.cta(cta) }
        if let decline = request.decline { builder = builder.decline(decline) }
        if let badge = request.badge { builder = builder.badge(badge) }
        if let appearance = request.appearance { builder = builder.appearance(appearance) }
        if let reward = request.publisherReward { builder = builder.publisherReward(reward) }
        let result = await builder.show()
        send(onResult, requestId, UnitySerialization.presentationResult(result), "EncoreUnity_ShowPlacement")
    }
}

// Native result to JSON. Keys and values match the Flutter bridge's EncoreSerialization verbatim.
enum UnitySerialization {
    static let useCaseUnavailable: [String: Any] = ["type": "notPresented", "reason": "use_case_unavailable"]

    static func logLevel(_ value: String?) -> Encore.LogLevel {
        switch value?.uppercased() {
        case "DEBUG": return .debug
        case "INFO": return .info
        case "WARN": return .warn
        case "ERROR": return .error
        default: return .none
        }
    }

    // The bridge logs its own failures from WARN up, as the C# layer does.
    static func logsFailures(_ level: Encore.LogLevel) -> Bool { level == .warn || level == .info || level == .debug }

    struct PlacementRequest {
        let placementId: String?
        let useCase: UseCase?
        let headline: String?
        let subheadline: String?
        let appearance: AppearanceConfig?
        let publisherReward: PublisherReward?
        var cta: String? = nil
        var decline: String? = nil
        var badge: String? = nil
    }

    // Empty strings are absent, as on Android. A use case this bridge does not list fails closed.
    static func placementRequest(_ json: String?, _ call: String = #function) -> PlacementRequest {
        let map = jsonObject(json, call) ?? [:]
        func text(_ key: String) -> String? { (map[key] as? String).flatMap { $0.isEmpty ? nil : $0 } }
        let useCase = text("useCase").flatMap(UseCase.init(rawValue:)).flatMap { [.rewardUsers, .reduceChurn].contains($0) ? $0 : nil }
        return PlacementRequest(
            placementId: text("placementId"), useCase: useCase, headline: text("headline"), subheadline: text("subheadline"),
            appearance: appearance(map["appearance"]), publisherReward: publisherReward(map["publisherReward"]),
            cta: text("cta"), decline: text("decline"), badge: text("badge")
        )
    }

    // Keys and values as the Flutter bridge's. An empty object is no appearance.
    static func appearance(_ raw: Any?) -> AppearanceConfig? {
        guard let map = raw as? [String: Any] else { return nil }
        let config = AppearanceConfig(
            accentColor: map["accentColor"] as? String,
            backgroundColor: map["backgroundColor"] as? String,
            backgroundEndColor: map["backgroundEndColor"] as? String,
            textColor: map["textColor"] as? String,
            secondaryTextColor: map["secondaryTextColor"] as? String,
            trackColor: map["trackColor"] as? String,
            onAccentColor: map["onAccentColor"] as? String,
            colorScheme: (map["colorScheme"] as? String).flatMap(AppearanceConfig.ColorScheme.init(rawValue:))
        )
        return config == AppearanceConfig() ? nil : config
    }

    // No id or no title is no prize. The SDK decides what blank text and the icon URL mean.
    static func publisherReward(_ raw: Any?) -> PublisherReward? {
        guard let map = raw as? [String: Any], let id = map["id"] as? String, let title = map["title"] as? String else { return nil }
        let icon = (map["iconUrl"] as? String)?.trimmingCharacters(in: .whitespacesAndNewlines)
        return PublisherReward(id: id, title: title, detail: map["detail"] as? String, iconUrl: icon.flatMap(URL.init(string:)))
    }

    struct TriggerLook: Equatable {
        let icon: TriggerIcon
        let alignment: EncoreAlignment
        let appearance: AppearanceConfig?
        let publisherReward: PublisherReward?
    }

    // The gift button's create options that SDUI looks read. Absent or unknown keeps the native default.
    static func triggerLook(_ options: [String: Any]) -> TriggerLook {
        TriggerLook(
            icon: (options["icon"] as? String).flatMap(TriggerIcon.init(rawValue:)) ?? .gift,
            alignment: alignment(options["alignment"]),
            appearance: appearance(options["appearance"]),
            publisherReward: publisherReward(options["publisherReward"])
        )
    }

    // Nil leaves that setting as it is. A present appearance or reward key always applies, and {} clears it.
    struct TriggerUpdate: Equatable {
        let alignment: EncoreAlignment?
        let appearance: AppearanceConfig??
        let publisherReward: PublisherReward??
    }

    static func triggerUpdate(_ options: [String: Any]) -> TriggerUpdate {
        TriggerUpdate(
            alignment: options["alignment"] == nil ? nil : alignment(options["alignment"]),
            appearance: options["appearance"] == nil ? nil : .some(appearance(options["appearance"])),
            publisherReward: options["publisherReward"] == nil ? nil : .some(publisherReward(options["publisherReward"]))
        )
    }

    static func alignment(_ raw: Any?) -> EncoreAlignment {
        (raw as? String).flatMap(EncoreAlignment.init(rawValue:)) ?? .center
    }

    static func giftsButtonHeight(_ height: Double) -> [String: Any] { ["event": "height", "height": height] }

    static func giftsButtonResult(_ result: PresentationResult) -> [String: Any] {
        ["event": "result", "result": presentationResult(result)]
    }

    static func userAttributes(_ json: String?, _ call: String = #function) -> UserAttributes? {
        guard let map = jsonObject(json, call), !map.isEmpty else { return nil }
        return UserAttributes(
            email: map["email"] as? String,
            firstName: map["firstName"] as? String,
            lastName: map["lastName"] as? String,
            phoneNumber: map["phoneNumber"] as? String,
            postalCode: map["postalCode"] as? String,
            city: map["city"] as? String,
            state: map["state"] as? String,
            countryCode: map["countryCode"] as? String,
            latitude: map["latitude"] as? String,
            longitude: map["longitude"] as? String,
            age: map["age"] as? Int,
            dateOfBirth: map["dateOfBirth"] as? String,
            gender: map["gender"] as? String,
            subscriptionTier: map["subscriptionTier"] as? String,
            monthsSubscribed: map["monthsSubscribed"] as? String,
            billingCycle: map["billingCycle"] as? String,
            lastPaymentAmount: map["lastPaymentAmount"] as? String,
            lastActiveDate: map["lastActiveDate"] as? String,
            totalSessions: map["totalSessions"] as? String,
            custom: map["custom"] as? [String: String] ?? [:]
        )
    }

    static func placementOutcome(_ outcome: PlacementOutcome) -> [String: Any] {
        switch outcome {
        case .presentation(let placementId, let result):
            return ["type": "presentation", "placementId": placementId, "result": presentationResult(result)]
        case .strictUnlockVerified(let transactionId):
            return ["type": "strictUnlockVerified", "transactionId": transactionId]
        @unknown default:
            return ["type": "unknown"]
        }
    }

    static func presentationResult(_ result: PresentationResult) -> [String: Any] {
        switch result {
        case .notPresented(let reason):
            if case .error(let error) = reason {
                return [
                    "type": "notPresented",
                    "reason": "error",
                    "errorType": errorType(error),
                    "errorMessage": error.errorDescription ?? "\(error)",
                ]
            }
            return ["type": "notPresented", "reason": notPresentedReason(reason)]
        case .presented(let outcome):
            return [
                "type": "presented",
                "advertiser": advertiserOutcome(outcome.advertiser),
                "publisher": publisherOutcome(outcome.publisher),
                "dismissal": outcome.dismissal.rawValue,
            ]
        @unknown default:
            return ["type": "notPresented", "reason": "unknown"]
        }
    }

    private static func notPresentedReason(_ reason: NotPresentedReason) -> String {
        switch reason {
        case .notConfigured: return "not_configured"
        case .alreadyPresenting: return "already_presenting"
        case .unsupportedOS: return "unsupported_ios"
        case .noOffers: return "no_offer_available"
        case .experimentControl: return "experiment_control"
        case .useCaseUnavailable: return "use_case_unavailable"
        case .error: return "error"
        @unknown default: return "unknown"
        }
    }

    private static func advertiserOutcome(_ outcome: AdvertiserOutcome) -> [String: Any] {
        switch outcome {
        case .notAttempted: return ["type": "notAttempted"]
        case .claimed(let offer): return ["type": "claimed", "offer": claimedOffer(offer)]
        case .verified(let offer): return ["type": "verified", "offer": claimedOffer(offer)]
        case .cooldown: return ["type": "cooldown"]
        case .failed(let error):
            return ["type": "failed", "errorType": errorType(error), "errorMessage": error.errorDescription ?? "\(error)"]
        @unknown default: return ["type": "notAttempted"]
        }
    }

    private static func claimedOffer(_ offer: ClaimedOffer) -> [String: Any] {
        var payload: [String: Any] = [
            "offerId": offer.offerId,
            "campaignId": offer.campaignId,
            "advertiserName": offer.advertiserName,
            "userConfirmedCompletion": offer.userConfirmedCompletion,
        ]
        if let transactionId = offer.transactionId { payload["transactionId"] = transactionId }
        return payload
    }

    private static func publisherOutcome(_ outcome: PublisherOutcome) -> String {
        switch outcome {
        case .notAttempted: return "not_attempted"
        case .purchased: return "purchased"
        case .cancelled: return "cancelled"
        case .pending: return "pending"
        case .failed: return "failed"
        @unknown default: return "unknown"
        }
    }

    // EncoreError.typeIdentifier is internal to the SDK, so its vocabulary is mirrored here.
    static func errorType(_ error: EncoreError) -> String {
        switch error {
        case .transport(.network): return "network_error"
        case .transport(.persistence): return "persistence_error"
        case .protocol(.http): return "http_error"
        case .protocol(.api): return "api_error"
        case .protocol(.decoding): return "decoding_error"
        case .integration(.notConfigured): return "not_configured"
        case .integration(.invalidApiKey): return "invalid_api_key"
        case .integration(.invalidURL): return "invalid_url"
        case .domain: return "domain_error"
        @unknown default: return "unknown_error"
        }
    }
}
