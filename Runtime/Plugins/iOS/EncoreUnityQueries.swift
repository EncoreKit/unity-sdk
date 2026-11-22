import Foundation
@_spi(EncoreBridge) import Encore

// The settings, the placement queries and the wrapper calls, called by IosPlatform.Queries.cs.

@_cdecl("EncoreUnity_SetClaimEnabled")
public func EncoreUnity_SetClaimEnabled(_ enabled: Bool) {
    onMain { Encore.shared.isClaimEnabled = enabled }
}

// The SDK's on/off switch. C# sends it before EncoreUnity_Configure too; both go through onMain, so it lands first.
@_cdecl("EncoreUnity_SetEnabled")
public func EncoreUnity_SetEnabled(_ enabled: Bool) {
    onMain { Encore.shared.isEnabled = enabled }
}

// The SDK clamps the limit to 1...10.
@_cdecl("EncoreUnity_SetPublisherRewardDailyLimit")
public func EncoreUnity_SetPublisherRewardDailyLimit(_ dailyLimit: Int32) {
    onMain { Encore.shared.publisherRewardPolicy = PublisherRewardPolicy(dailyLimit: Int(dailyLimit)) }
}

@_cdecl("EncoreUnity_Prefetch")
public func EncoreUnity_Prefetch(_ requestJson: UnsafePointer<CChar>?) {
    let request = UnitySerialization.placementRequest(requestJson.map { String(cString: $0) })
    // Fail closed, as show does: never warm a use case this bridge does not know.
    guard let useCase = request.useCase else { return }
    onMain { Encore.placement(request.placementId).useCase(useCase).prefetch() }
}

// One answer. A use case this bridge does not know answers false, as the SDK does for its own failures.
@_cdecl("EncoreUnity_HasOffers")
public func EncoreUnity_HasOffers(_ requestId: Int64, _ requestJson: UnsafePointer<CChar>?, _ onReply: EncoreUnityCallback?) {
    let request = UnitySerialization.placementRequest(requestJson.map { String(cString: $0) })
    guard let useCase = request.useCase else {
        return send(onReply, requestId, UnityPurchases.hasOffers(false), "EncoreUnity_HasOffers")
    }
    Task { @MainActor in
        let hasOffers = await Encore.placement(request.placementId).useCase(useCase).hasOffers()
        send(onReply, requestId, UnityPurchases.hasOffers(hasOffers), "EncoreUnity_HasOffers")
    }
}

// In place: C# calls it right before configure, and the SDK takes it from any thread.
@_cdecl("EncoreUnity_SetWrapper")
public func EncoreUnity_SetWrapper(_ name: UnsafePointer<CChar>?, _ version: UnsafePointer<CChar>?) {
    Encore.setWrapper(name: name.map { String(cString: $0) } ?? "", version: version.map { String(cString: $0) } ?? "")
}

// A failure C# contained. Both are fixed text from C#: a call name and an exception type name.
@_cdecl("EncoreUnity_ReportWrapperError")
public func EncoreUnity_ReportWrapperError(_ site: UnsafePointer<CChar>?, _ code: UnsafePointer<CChar>?) {
    Encore.reportWrapperError(where: site.map { String(cString: $0) } ?? "", message: "Contained by the Unity bridge", code: code.map { String(cString: $0) })
}
