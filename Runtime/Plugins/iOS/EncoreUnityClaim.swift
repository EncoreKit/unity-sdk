import Combine
import Foundation
@_spi(EncoreBridge) import Encore

// The native half of C#'s EncoreClaim: native claim handles keyed by the id C# picks. Calls and keys follow the Flutter
// claim bridge. Destroying a handle stops its state at once; a tap in flight keeps it until that tap is answered.

@MainActor final class ClaimEntry {
    let claim: EncoreClaim
    var subscription: AnyCancellable?
    // Taps C# is still waiting for.
    var taps = 0
    // Destroyed by C#; kept only until its taps are answered.
    var draining = false
    // Released: nothing more goes to C#.
    var finished = false

    init(claim: EncoreClaim) { self.claim = claim }
}

@MainActor var claims: [Int64: ClaimEntry] = [:]

@_cdecl("EncoreUnity_ClaimCreate")
public func EncoreUnity_ClaimCreate(_ id: Int64, _ optionsJson: UnsafePointer<CChar>?, _ callback: EncoreUnityCallback?) {
    guard callback != nil else { return BridgeLog.failure(#function, "no callback") }
    let create = UnitySerialization.claimCreate(jsonObject(optionsJson.map { String(cString: $0) }) ?? [:])
    onMain {
        destroyClaim(id)
        // Fail closed, as the Flutter bridge does: no claim, so C# stays hidden.
        guard let create else { return BridgeLog.failure("EncoreUnity_ClaimCreate", "no placement or known use case") }
        let claim = Encore.claim(
            placement: create.placement,
            useCase: create.useCase,
            autoOpen: create.autoOpenOff ? .off : nil,
            publisherReward: create.publisherReward,
            sheetHeadline: create.sheetHeadline,
            sheetSubheadline: create.sheetSubheadline
        )
        let entry = ClaimEntry(claim: claim)
        claim.onResult = { [weak entry] result in
            guard let entry, !entry.finished else { return }
            send(callback, id, UnitySerialization.claimResult(result), "EncoreUnity_ClaimCreate")
        }
        entry.subscription = claim.$state.sink { state in
            send(callback, id, UnitySerialization.claimState(state), "EncoreUnity_ClaimCreate")
        }
        claims[id] = entry
    }
}

@_cdecl("EncoreUnity_ClaimDestroy")
public func EncoreUnity_ClaimDestroy(_ id: Int64) {
    onMain { destroyClaim(id) }
}

@_cdecl("EncoreUnity_ClaimSetVisible")
public func EncoreUnity_ClaimSetVisible(_ id: Int64, _ fraction: Double) {
    onMain { claims[id]?.claim.setVisible(fraction) }
}

// Answers C# once: the sheet's result, or no offers for a claim it does not hold, as a hidden claim does.
@_cdecl("EncoreUnity_ClaimTap")
public func EncoreUnity_ClaimTap(_ id: Int64, _ target: UnsafePointer<CChar>?, _ requestId: Int64, _ callback: EncoreUnityCallback?) {
    guard callback != nil else { return BridgeLog.failure(#function, "no callback") }
    let target = UnitySerialization.claimTarget(target.map { String(cString: $0) })
    onMain {
        guard let entry = claims[id] else {
            return send(callback, requestId, UnitySerialization.presentationResult(.notPresented(.noOffers)), "EncoreUnity_ClaimTap")
        }
        entry.taps += 1
        Task { @MainActor in
            let result = await entry.claim.tap(target: target)
            send(callback, requestId, UnitySerialization.presentationResult(result), "EncoreUnity_ClaimTap")
            entry.taps -= 1
            if entry.draining, entry.taps == 0 { finishClaim(entry) }
        }
    }
}

@_cdecl("EncoreUnity_ClaimDismiss")
public func EncoreUnity_ClaimDismiss(_ id: Int64) {
    onMain { claims[id]?.claim.dismiss() }
}

// {} has no id, so it is no prize.
@_cdecl("EncoreUnity_ClaimSetPublisherReward")
public func EncoreUnity_ClaimSetPublisherReward(_ id: Int64, _ json: UnsafePointer<CChar>?) {
    let map = jsonObject(json.map { String(cString: $0) }) ?? [:]
    onMain { claims[id]?.claim.publisherReward = UnitySerialization.publisherReward(map["publisherReward"]) }
}

// A missing key goes back to the sheet's own text.
@_cdecl("EncoreUnity_ClaimSetSheetText")
public func EncoreUnity_ClaimSetSheetText(_ id: Int64, _ json: UnsafePointer<CChar>?) {
    let text = UnitySerialization.triggerSheetText(jsonObject(json.map { String(cString: $0) }) ?? [:])
    onMain {
        guard let claim = claims[id]?.claim else { return }
        claim.sheetHeadline = text.headline
        claim.sheetSubheadline = text.subheadline
    }
}

@MainActor func destroyClaim(_ id: Int64) {
    guard let entry = claims.removeValue(forKey: id) else { return }
    entry.subscription?.cancel()
    entry.subscription = nil
    entry.claim.setVisible(0)
    // The tap's Task holds a draining entry until it answers.
    if entry.taps == 0 { finishClaim(entry) } else { entry.draining = true }
}

@MainActor private func finishClaim(_ entry: ClaimEntry) {
    entry.finished = true
    entry.claim.onResult = nil
}

extension UnitySerialization {
    struct ClaimCreate: Equatable {
        let placement: String
        let useCase: UseCase
        let autoOpenOff: Bool
        let publisherReward: PublisherReward?
        let sheetHeadline: String?
        let sheetSubheadline: String?
    }

    // A blank placement or a use case this build cannot read is nil. An absent use case is the native default, reward.
    static func claimCreate(_ options: [String: Any]) -> ClaimCreate? {
        let placement = (options["placement"] as? String)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let useCase: UseCase?
        switch options["useCase"] {
        case nil: useCase = .rewardUsers
        case let raw as String: useCase = UseCase(rawValue: raw).flatMap { [.rewardUsers, .reduceChurn].contains($0) ? $0 : nil }
        default: useCase = nil
        }
        guard !placement.isEmpty, let useCase else { return nil }
        let text = triggerSheetText(options)
        return ClaimCreate(
            placement: placement, useCase: useCase, autoOpenOff: options["autoOpen"] as? String == AutoOpen.off.rawValue,
            publisherReward: publisherReward(options["publisherReward"]), sheetHeadline: text.headline, sheetSubheadline: text.subheadline
        )
    }

    // Absent or unknown is the native default, button (a reminder counts it as the card).
    static func claimTarget(_ raw: String?) -> EncoreClaim.Target { raw.flatMap(EncoreClaim.Target.init(rawValue:)) ?? .button }

    static func claimResult(_ result: PresentationResult) -> [String: Any] { ["event": "result", "result": presentationResult(result)] }

    static func claimState(_ state: EncoreClaim.State) -> [String: Any] {
        let payload: [String: Any]
        switch state {
        case .hidden: payload = ["kind": "hidden"]
        case .gifts(let gifts): payload = claimOffers("gifts", gifts.offerCount, gifts.logoURLs)
        case .thanks(let gifts): payload = claimOffers("thanks", gifts.offerCount, gifts.logoURLs)
        case .reminder(let r):
            payload = claimReminder(advertiserName: r.advertiserName, logoURL: r.logoURL, perk: r.perk, claimedAt: r.claimedAt,
                                    deadline: r.deadline, claimCount: r.claimCount, transactionId: r.transactionId)
        @unknown default: payload = ["kind": "hidden"]
        }
        return ["event": "state", "state": payload]
    }

    static func claimOffers(_ kind: String, _ offerCount: Int, _ logoURLs: [URL]) -> [String: Any] {
        ["kind": kind, "offerCount": offerCount, "logoUrls": logoURLs.map(\.absoluteString)]
    }

    // Dates in epoch milliseconds. One past what Int64 milliseconds hold is unreadable: hidden, never a trap.
    static func claimReminder(advertiserName: String, logoURL: URL?, perk: String?, claimedAt: Date, deadline: Date,
                              claimCount: Int, transactionId: String) -> [String: Any] {
        func millis(_ date: Date) -> Int64? { Int64(exactly: (date.timeIntervalSince1970 * 1000).rounded()) }
        guard let claimed = millis(claimedAt), let ends = millis(deadline) else { return ["kind": "hidden"] }
        var map: [String: Any] = [
            "kind": "reminder", "advertiserName": advertiserName, "claimedAt": claimed, "deadline": ends,
            "claimCount": claimCount, "transactionId": transactionId,
        ]
        if let logoURL { map["logoUrl"] = logoURL.absoluteString }
        if let perk { map["perk"] = perk }
        return map
    }
}
