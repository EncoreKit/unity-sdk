import CoreGraphics
import Encore
import Foundation

/// Builds `SheetAppearance.components` from the bridge's JSON-like map. A value of the wrong type, or a string
/// `URL(string:)` rejects, is dropped here, never fatal; the SDK range-checks the rest and warns once per bad value.
enum EncoreSheetComponents {
    static func components(from raw: Any?) -> [SheetComponent: SheetComponentStyle] {
        guard let map = raw as? [String: Any] else { return [:] }
        var out: [SheetComponent: SheetComponentStyle] = [:]
        for (name, value) in map {
            let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !trimmed.isEmpty, let style = value as? [String: Any] else { continue }
            out[SheetComponent(trimmed)] = componentStyle(from: style)
        }
        return out
    }

    static func componentStyle(from m: [String: Any]) -> SheetComponentStyle {
        SheetComponentStyle(
            fill: color(m["fill"]),
            cornerRadius: number(m["cornerRadius"]).map { CGFloat($0) },
            borderWidth: number(m["borderWidth"]).map { CGFloat($0) },
            borderColor: color(m["borderColor"]),
            contentColor: color(m["contentColor"]),
            font: (m["font"] as? String).flatMap(SheetComponentStyle.FontRole.init(rawValue:)),
            image: url(m["image"]),
            trueImage: url(m["trueImage"]),
            falseImage: url(m["falseImage"]),
            backgroundImage: url(m["backgroundImage"]),
            animation: animation(m["animation"]),
            enter: enter(m["enter"]),
            loop: loop(m["loop"])
        )
    }

    static func color(_ v: Any?) -> SheetColor? {
        if let hex = v as? String { return SheetColor(hex) }
        guard let m = v as? [String: Any], let light = m["light"] as? String, let dark = m["dark"] as? String else { return nil }
        return SheetColor(light: light, dark: dark)
    }

    static func animation(_ v: Any?) -> SheetAnimation? {
        guard let m = v as? [String: Any], let file = url(m["url"]), let still = url(m["fallbackImage"]) else { return nil }
        var images: [String: SheetAnimationImage] = [:]
        for (id, value) in m["images"] as? [String: Any] ?? [:] {
            guard let slot = value as? [String: Any], let offer = whole(slot["offer"]),
                  let picture = (slot["picture"] as? String).flatMap(SheetOfferPicture.init(rawValue:)) else { continue }
            images[id] = SheetAnimationImage(offer: offer, picture: picture)
        }
        return SheetAnimation(url: file, fallbackImage: still, loop: bool(m["loop"]), autoplay: bool(m["autoplay"]),
                              images: images.isEmpty ? nil : images)
    }

    static func pose(_ v: Any?) -> SheetPose? {
        guard let m = v as? [String: Any] else { return nil }
        return SheetPose(scale: number(m["scale"]), opacity: number(m["opacity"]), rotation: number(m["rotation"]),
                         offsetX: number(m["offsetX"]), offsetY: number(m["offsetY"]))
    }

    static func enter(_ v: Any?) -> SheetEnter? {
        guard let m = v as? [String: Any], let from = pose(m["from"]), let duration = number(m["duration"]),
              let curve = curve(m["curve"]) else { return nil }
        return SheetEnter(from: from, duration: duration, delay: number(m["delay"]), curve: curve)
    }

    static func loop(_ v: Any?) -> SheetLoop? {
        guard let m = v as? [String: Any], let duration = number(m["duration"]), let raw = m["keyframes"] as? [Any],
              let curve = curve(m["curve"]) else { return nil }
        var keyframes: [SheetKeyframe] = []
        for frame in raw {
            guard let f = frame as? [String: Any], let at = number(f["at"]), let pose = pose(f["pose"]) else { return nil }
            keyframes.append(SheetKeyframe(at: at, pose: pose))
        }
        var repeatCount: Int?
        if isPresent(m["repeatCount"]) {
            // A count that is not a whole number drops the loop: unset would mean forever.
            guard let count = whole(m["repeatCount"]) else { return nil }
            repeatCount = count
        }
        return SheetLoop(duration: duration, keyframes: keyframes, curve: curve, phaseOffset: number(m["phaseOffset"]),
                         reducedMotion: pose(m["reducedMotion"]), delay: number(m["delay"]), repeatCount: repeatCount)
    }

    /// Absent is `.some(nil)` (the SDK's default curve); a name the SDK does not know is nil, so the motion is dropped.
    static func curve(_ v: Any?) -> SheetMotionCurve?? {
        guard isPresent(v) else { return .some(nil) }
        guard let curve = (v as? String).flatMap(SheetMotionCurve.init(rawValue:)) else { return nil }
        return .some(curve)
    }

    static func url(_ v: Any?) -> URL? {
        guard let text = (v as? String)?.trimmingCharacters(in: .whitespacesAndNewlines), !text.isEmpty else { return nil }
        return URL(string: text)
    }

    /// A finite number; a Boolean is not one, though NSNumber carries both.
    static func number(_ v: Any?) -> Double? {
        guard let n = v as? NSNumber, CFGetTypeID(n) != CFBooleanGetTypeID(), n.doubleValue.isFinite else { return nil }
        return n.doubleValue
    }

    static func whole(_ v: Any?) -> Int? {
        guard let d = number(v), d.rounded() == d, abs(d) <= 1_000_000_000 else { return nil }
        return Int(d)
    }

    static func bool(_ v: Any?) -> Bool? {
        guard let n = v as? NSNumber, CFGetTypeID(n) == CFBooleanGetTypeID() else { return nil }
        return n.boolValue
    }

    private static func isPresent(_ v: Any?) -> Bool { v != nil && !(v is NSNull) }
}
