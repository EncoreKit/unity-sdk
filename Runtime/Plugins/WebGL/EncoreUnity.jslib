// The WebGL bridge. C# calls these through DllImport("__Internal"); results go back through function pointers, as
// JSON in the same shape the iOS and Android bridges send. The SDK is the bundled web-sdk UMD (encore-web-sdk.jspre).
var EncoreUnityLib = {
  $EncoreUnity: {
    outcome: 0,
    stopStrictUnlock: null,
    strictToken: null,
    buttons: {},
    // Buttons that failed to remount. Their old handle may still have an open sheet, so its result still goes to C#.
    orphans: {},
    overlay: null,

    sdk: function () {
      var umd = typeof Encore !== 'undefined' ? Encore : window.Encore;
      return umd && (umd.Encore || umd.default);
    },

    // Off until Configure asks for WARN or more, the level the C# layer logs contained failures at.
    logsFailures: false,

    // A failure the bridge contained: the console from WARN up, and the web SDK's error reports with the call name and
    // the error's name only, never its message.
    contain: function (where, error) {
      if (EncoreUnity.logsFailures) console.error('[Encore] ' + where + ' failed. The game continues.', error);
      EncoreUnity.report(where, error && typeof error.name === 'string' ? error.name : 'Error');
    },

    // The bundled web SDK has reportWrapperError from 2.5.0; an older one has none, so this sends nothing. The SDK
    // drops a report before configure.
    report: function (where, code) {
      try {
        var sdk = EncoreUnity.sdk();
        if (sdk && typeof sdk.reportWrapperError === 'function') sdk.reportWrapperError(String(where), 'Contained by the Unity bridge', code);
      } catch (e) {
        // A report that fails is dropped, never reported again.
      }
    },

    // Runs fn, and contains a throw from it, so no exported entry point can break the game loop.
    attempt: function (where, fn) {
      try {
        return fn();
      } catch (e) {
        EncoreUnity.contain(where, e);
      }
    },

    send: function (callback, id, payload) {
      var ptr = 0;
      try {
        var json = JSON.stringify(payload);
        var size = lengthBytesUTF8(json) + 1;
        ptr = _malloc(size);
        stringToUTF8(json, ptr, size);
        {{{ makeDynCall('vii', 'callback') }}}(id, ptr);
      } catch (e) {
        EncoreUnity.contain('The Unity callback', e);
      } finally {
        if (ptr) _free(ptr);
      }
    },

    // Exactly one reply per request, so a sync throw after the SDK already answered cannot send a second one.
    replyOnce: function (callback, id) {
      var replied = false;
      return function (payload) {
        if (replied) return false;
        replied = true;
        EncoreUnity.send(callback, id, payload);
        return true;
      };
    },

    parse: function (ptr) {
      if (!ptr) return null;
      try {
        return JSON.parse(UTF8ToString(ptr));
      } catch (e) {
        return null;
      }
    },

    text: function (value) {
      return typeof value === 'string' && value.length > 0 ? value : undefined;
    },

    // Fail closed, as the native bridges do: an unknown use case never presents.
    useCase: function (wire) {
      return { 'post-action-reward': 'rewardUsers', 'churn-intervention': 'reduceChurn' }[wire] || null;
    },

    errorResult: function (error) {
      var errorType = 'domain_error';
      var errorMessage = 'Unknown error';
      try {
        errorType = (error && error.type) || errorType;
        errorMessage = (error && error.message) || String(error);
      } catch (e) {}
      return { type: 'notPresented', reason: 'error', errorType: errorType, errorMessage: errorMessage };
    },

    // web-sdk ShowResult to the bridge result. Web has no publisher purchase and no verified or cooldown outcome.
    result: function (r) {
      if (!r) return EncoreUnity.errorResult({ message: 'No result from the web SDK' });
      if (r.status === 'unavailable') return { type: 'notPresented', reason: 'no_offer_available' };
      if (r.status === 'claimed') {
        var offer = { offerId: r.offerId, campaignId: r.campaignId, advertiserName: r.advertiserName };
        if (r.transactionId) offer.transactionId = r.transactionId;
        if (typeof r.claimUrl === 'string' && r.claimUrl) offer.claimUrl = r.claimUrl;
        return {
          type: 'presented',
          advertiser: { type: 'claimed', offer: offer },
          publisher: 'not_attempted',
          dismissal: 'flow_completed',
        };
      }
      var reason = r.reason;
      if (reason && typeof reason === 'object') return EncoreUnity.errorResult(reason.error);
      if (reason === 'noOffersAvailable') return { type: 'notPresented', reason: 'no_offer_available' };
      if (reason === 'alreadyPresenting') return { type: 'notPresented', reason: 'already_presenting' };
      if (reason === 'notConfigured') return { type: 'notPresented', reason: 'not_configured' };
      // The game turned the SDK off: no sheet was shown, as on Android and iOS.
      if (reason === 'disabled') return { type: 'notPresented', reason: 'disabled' };
      var dismissal = { userClosedModal: 'close_button', userDeclinedLastOffer: 'last_offer_declined' }[reason] || 'dismissed';
      return { type: 'presented', advertiser: { type: 'notAttempted' }, publisher: 'not_attempted', dismissal: dismissal };
    },

    emitOutcome: function (placementId, result) {
      if (!EncoreUnity.outcome) return;
      var payload = { type: 'presentation', result: result };
      if (placementId) payload.placementId = placementId;
      EncoreUnity.send(EncoreUnity.outcome, 0, payload);
    },

    // The gift buttons' layer, fixed over the canvas. It moves into the fullscreen element, or the browser hides it.
    layer: function () {
      var overlay = EncoreUnity.overlay;
      if (!overlay) {
        overlay = EncoreUnity.overlay = document.createElement('div');
        overlay.setAttribute('data-encore-unity', 'overlay');
        overlay.style.cssText = 'position:fixed;left:0;top:0;width:0;height:0;overflow:visible;pointer-events:none;z-index:9000;';
        var relayout = function () { EncoreUnity.attempt('Gift button layout', EncoreUnity.relayout); };
        window.addEventListener('resize', relayout);
        document.addEventListener('fullscreenchange', relayout);
        document.addEventListener('webkitfullscreenchange', relayout);
      }
      var parent = document.fullscreenElement || document.webkitFullscreenElement || document.body;
      if (parent.tagName === 'CANVAS') parent = document.body;
      if (overlay.parentNode !== parent) parent.appendChild(overlay);
      return overlay;
    },

    // The web SDK mounts its sheet on document.body, so a fullscreen element without body in it hides the sheet. While
    // a sheet is open (the SDK's body class, for a placement and a gift button's sheet alike), leave that fullscreen.
    leaveFullscreenForSheet: function () {
      if (!document.body.classList.contains('encore-no-scroll')) return;
      var element = document.fullscreenElement || document.webkitFullscreenElement;
      if (!element || element.contains(document.body)) return;
      var exit = document.fullscreenElement ? document.exitFullscreen : document.webkitExitFullscreen;
      if (typeof exit !== 'function') return;
      var done = exit.call(document);
      if (done && typeof done.catch === 'function') done.catch(function () {});
    },

    // Checks when a sheet opens and when the game enters fullscreen again while one is open.
    watchFullscreen: function () {
      if (EncoreUnity.fullscreenWatch || typeof MutationObserver === 'undefined') return;
      var check = function () { EncoreUnity.attempt('Leaving fullscreen for a sheet', EncoreUnity.leaveFullscreenForSheet); };
      var watch = new MutationObserver(check);
      watch.observe(document.body, { attributes: true, attributeFilter: ['class'] });
      document.addEventListener('fullscreenchange', check);
      document.addEventListener('webkitfullscreenchange', check);
      EncoreUnity.fullscreenWatch = watch;
    },

    canvasRect: function () {
      return Module.canvas ? Module.canvas.getBoundingClientRect() : { left: 0, top: 0, width: 0, height: 0 };
    },

    relayout: function () {
      EncoreUnity.layer();
      for (var id in EncoreUnity.buttons) EncoreUnity.place(EncoreUnity.buttons[id]);
    },

    // Unity screen pixels to CSS pixels: the canvas backing store is its CSS size times devicePixelRatio.
    place: function (button) {
      var frame = button.frame;
      if (!frame || frame.screenWidth <= 0 || frame.screenHeight <= 0) return;
      var rect = EncoreUnity.canvasRect();
      var sx = rect.width / frame.screenWidth;
      button.scaleY = rect.height / frame.screenHeight;
      var style = button.host.style;
      style.left = rect.left + frame.x * sx + 'px';
      style.top = rect.top + frame.y * button.scaleY + 'px';
      style.width = frame.width * sx + 'px';
    },

    // A hidden host measures 0, but the button keeps its height while hidden, as on iOS. A destroyed or replaced
    // button reports nothing, because its id may already belong to a new one.
    reportHeight: function (button) {
      if (EncoreUnity.buttons[button.id] !== button || button.host.style.display === 'none') return;
      var css = button.host.getBoundingClientRect().height;
      var height = button.scaleY > 0 ? css / button.scaleY : 0;
      if (Math.abs(height - button.reported) < 0.5) return;
      button.reported = height;
      EncoreUnity.send(button.callback, button.id, { event: 'height', height: height });
    },

    // The keys are the web SDK's own. Strings pass through as the native bridges pass them; {} means none. Only a
    // placement passes withSheet: a trigger's own appearance takes no sheet, as natively. The sheet fonts pass as CSS font
    // families; the trigger look fields (fontFamily to iconImage) stay native, because web SDK 2.6.0 has none of them.
    appearance: function (a, withSheet) {
      if (!a || typeof a !== 'object') return undefined;
      var out = {};
      ['accentColor', 'backgroundColor', 'textColor', 'secondaryTextColor', 'trackColor', 'onAccentColor', 'backgroundEndColor']
        .forEach(function (key) { if (typeof a[key] === 'string') out[key] = a[key]; });
      if (['system', 'light', 'dark'].indexOf(a.colorScheme) >= 0) out.colorScheme = a.colorScheme;
      if (withSheet && EncoreUnity.isRecord(a.sheet)) {
        var sheet = {};
        ['headingFontFamily', 'bodyFontFamily'].forEach(function (key) {
          if (typeof a.sheet[key] === 'string' && a.sheet[key].trim()) sheet[key] = a.sheet[key].trim();
        });
        var components = EncoreUnity.sheetComponents(a.sheet.components);
        if (components) sheet.components = components;
        if (Object.keys(sheet).length) out.sheet = sheet;
      }
      return Object.keys(out).length ? out : undefined;
    },

    isRecord: function (v) { return v !== null && typeof v === 'object' && !Array.isArray(v); },
    // A finite number; a boolean is not one.
    isNumber: function (v) { return typeof v === 'number' && isFinite(v); },
    isCurve: function (v) { return v === undefined || v === null || ['linear', 'easeIn', 'easeOut', 'easeInOut'].indexOf(v) >= 0; },

    // The bridge's component styles as the web SDK's skin shape, which Swift's SheetComponentStyle.json also writes.
    // Type checks only, with the native drop rules; the web SDK range-checks the rest and warns.
    sheetComponents: function (raw) {
      if (!EncoreUnity.isRecord(raw)) return undefined;
      var out = {};
      Object.keys(raw).forEach(function (name) {
        var trimmed = name.trim();
        if (trimmed && EncoreUnity.isRecord(raw[name])) out[trimmed] = EncoreUnity.skinStyle(raw[name]);
      });
      return Object.keys(out).length ? out : undefined;
    },

    skinStyle: function (m) {
      var U = EncoreUnity;
      var style = {};
      ['fill', 'borderColor', 'contentColor'].forEach(function (key) {
        var c = m[key];
        if (typeof c === 'string') style[key] = c;
        else if (U.isRecord(c) && typeof c.light === 'string' && typeof c.dark === 'string') style[key] = { light: c.light, dark: c.dark };
      });
      ['cornerRadius', 'borderWidth'].forEach(function (key) { if (U.isNumber(m[key])) style[key] = m[key]; });
      if (m.font === 'heading' || m.font === 'body') style.font = m.font;
      ['image', 'trueImage', 'falseImage', 'backgroundImage'].forEach(function (key) {
        if (typeof m[key] === 'string' && m[key].trim()) style[key] = m[key].trim();
      });
      var animation = U.skinAnimation(m.animation);
      if (animation) style.animation = animation;
      var enter = U.skinEnter(m.enter);
      if (enter) style.enter = enter;
      var loop = U.skinLoop(m.loop);
      if (loop) style.loop = loop;
      return style;
    },

    // offsetX and offsetY become offset { x, y }: one set writes both, the other 0.
    skinPose: function (p) {
      if (!EncoreUnity.isRecord(p)) return undefined;
      var out = {};
      ['scale', 'opacity', 'rotation'].forEach(function (key) { if (EncoreUnity.isNumber(p[key])) out[key] = p[key]; });
      var x = EncoreUnity.isNumber(p.offsetX) ? p.offsetX : undefined;
      var y = EncoreUnity.isNumber(p.offsetY) ? p.offsetY : undefined;
      if (x !== undefined || y !== undefined) out.offset = { x: x || 0, y: y || 0 };
      return out;
    },

    // An images slot's picture becomes the urlBinding an asyncImage uses for it.
    skinAnimation: function (a) {
      var U = EncoreUnity;
      if (!U.isRecord(a) || typeof a.url !== 'string' || !a.url.trim() || typeof a.fallbackImage !== 'string' || !a.fallbackImage.trim()) return undefined;
      var out = { url: a.url.trim(), fallbackImage: a.fallbackImage.trim() };
      if (typeof a.loop === 'boolean') out.loop = a.loop;
      if (typeof a.autoplay === 'boolean') out.autoplay = a.autoplay;
      var bindings = { logo: 'offerLogoImage', hero: 'offerHeroImage', primaryCreative: 'offerPrimaryCreative' };
      var images = {};
      if (U.isRecord(a.images)) {
        Object.keys(a.images).forEach(function (id) {
          var slot = a.images[id];
          if (U.isRecord(slot) && U.isNumber(slot.offer) && Math.floor(slot.offer) === slot.offer && bindings.hasOwnProperty(slot.picture)) {
            images[id] = { offer: slot.offer, urlBinding: bindings[slot.picture] };
          }
        });
      }
      if (Object.keys(images).length) out.images = images;
      return out;
    },

    skinEnter: function (e) {
      var U = EncoreUnity;
      var from = U.isRecord(e) ? U.skinPose(e.from) : undefined;
      if (!from || !U.isNumber(e.duration) || !U.isCurve(e.curve)) return undefined;
      var out = { from: from, duration: e.duration };
      if (U.isNumber(e.delay)) out.delay = e.delay;
      if (e.curve != null) out.curve = e.curve;
      return out;
    },

    // A keyframe { at, pose } becomes { at, ...pose }. One malformed keyframe drops the loop.
    skinLoop: function (l) {
      var U = EncoreUnity;
      if (!U.isRecord(l) || !U.isNumber(l.duration) || !Array.isArray(l.keyframes) || !U.isCurve(l.curve)) return undefined;
      var keyframes = [];
      for (var i = 0; i < l.keyframes.length; i++) {
        var f = l.keyframes[i];
        var pose = U.isRecord(f) && U.isNumber(f.at) ? U.skinPose(f.pose) : undefined;
        if (!pose) return undefined;
        keyframes.push(Object.assign({ at: f.at }, pose));
      }
      // A count that is not a whole number drops the loop: unset would mean forever.
      if (l.repeatCount != null && !(U.isNumber(l.repeatCount) && Math.floor(l.repeatCount) === l.repeatCount)) return undefined;
      var out = { duration: l.duration, keyframes: keyframes };
      if (l.curve != null) out.curve = l.curve;
      if (U.isNumber(l.phaseOffset)) out.phaseOffset = l.phaseOffset;
      var reduced = U.skinPose(l.reducedMotion);
      if (reduced) out.reducedMotion = reduced;
      if (U.isNumber(l.delay)) out.delay = l.delay;
      if (l.repeatCount != null) out.repeatCount = l.repeatCount;
      return out;
    },

    // No id or no title is no prize, as in the native bridges. The SDK decides what blank text means.
    reward: function (r) {
      if (!r || typeof r.id !== 'string' || typeof r.title !== 'string') return undefined;
      var out = { id: r.id, title: r.title };
      if (typeof r.detail === 'string') out.detail = r.detail;
      if (typeof r.iconUrl === 'string') out.iconUrl = r.iconUrl;
      return out;
    },

    // An unknown value falls back to center, as on Android and iOS.
    alignment: function (value) {
      return ['leading', 'center', 'trailing'].indexOf(value) >= 0 ? value : 'center';
    },

    // A web trigger handle has no setter for these, so one remount covers every change made in the same frame.
    remountSoon: function (button) {
      if (button.remountPending) return;
      button.remountPending = true;
      Promise.resolve().then(function () {
        button.remountPending = false;
        if (EncoreUnity.buttons[button.id] !== button) return;
        try {
          var old = button.handle;
          button.handle = null;
          if (old) old.unmount();
          EncoreUnity.mount(button);
        } catch (e) {
          EncoreUnity.contain('Gift button remount', e);
          EncoreUnity.fail(button);
        }
      });
    },

    // Removes a button that could not mount, and tells C# it has no height.
    fail: function (button) {
      if (EncoreUnity.buttons[button.id] !== button) return;
      EncoreUnity.destroy(button.id);
      EncoreUnity.orphans[button.id] = button;
      EncoreUnity.send(button.callback, button.id, { event: 'height', height: 0 });
    },

    mount: function (button) {
      var sdk = EncoreUnity.sdk();
      if (!sdk) throw new Error('The web SDK is missing from this build.');
      var o = button.options;
      var options = {
        placement: o.placement,
        useCase: 'rewardUsers',
        values: o.values || {},
        // Dropped after destroy, as on iOS. A remount, or a failed one, keeps it, so a claim is not lost.
        onResult: function (r) {
          if (EncoreUnity.buttons[button.id] !== button && EncoreUnity.orphans[button.id] !== button) return;
          var result = EncoreUnity.attempt('Gift button result', function () { return EncoreUnity.result(r); }) ||
            EncoreUnity.errorResult({ message: 'The web SDK sent a result the bridge could not read.' });
          EncoreUnity.send(button.callback, button.id, { event: 'result', result: result });
          EncoreUnity.emitOutcome(o.placement, result);
        },
      };
      if (o.autoOpen === 'off') options.autoOpen = 'off';
      if (EncoreUnity.text(o.headline)) options.headline = o.headline;
      if (EncoreUnity.text(o.subheadline)) options.subheadline = o.subheadline;
      if (EncoreUnity.text(o.cta)) options.cta = o.cta;
      if (EncoreUnity.text(o.sheetHeadline)) options.sheetHeadline = o.sheetHeadline;
      if (EncoreUnity.text(o.sheetSubheadline)) options.sheetSubheadline = o.sheetSubheadline;
      if (['gift', 'appIcon', 'none'].indexOf(o.icon) >= 0) options.icon = o.icon;
      if (o.alignment !== undefined) options.alignment = EncoreUnity.alignment(o.alignment);
      var appearance = EncoreUnity.appearance(o.appearance);
      if (appearance) options.appearance = appearance;
      var reward = EncoreUnity.reward(o.publisherReward);
      if (reward) options.publisherReward = reward;
      // The SDK's own mount for an id it names (claimReminder forces autoOpen off), else the generic one.
      var named = sdk.triggers[o.triggerId];
      button.handle = named && typeof named.mount === 'function'
        ? named.mount(button.host, options)
        : sdk.triggers.mount(o.triggerId, button.host, options);
    },

    // Custom claims (Encore.Claim) by the id C# picks. A destroyed claim with a tap in flight stays until the tap answers.
    claims: {},

    // web-sdk EncoreClaimState to the Flutter claim channel's state. Anything it cannot read is hidden.
    claimState: function (s) {
      var U = EncoreUnity;
      if (!s || typeof s !== 'object') return { kind: 'hidden' };
      if ((s.kind === 'gifts' || s.kind === 'thanks') && U.isNumber(s.offerCount) && Array.isArray(s.logos)) {
        var logoUrls = s.logos
          .map(function (l) { return l && typeof l.logoUrl === 'string' ? l.logoUrl : null; })
          .filter(function (u) { return u !== null; });
        return { kind: s.kind, offerCount: s.offerCount, logoUrls: logoUrls };
      }
      var time = function (d) { return d instanceof Date && isFinite(d.getTime()) ? d.getTime() : null; };
      if (s.kind === 'reminder' && typeof s.advertiserName === 'string' && typeof s.transactionId === 'string' &&
          U.isNumber(s.claimCount) && time(s.claimedAt) !== null && time(s.deadline) !== null) {
        var out = {
          kind: 'reminder', advertiserName: s.advertiserName, claimedAt: time(s.claimedAt), deadline: time(s.deadline),
          claimCount: s.claimCount, transactionId: s.transactionId,
        };
        if (typeof s.logoUrl === 'string') out.logoUrl = s.logoUrl;
        if (typeof s.perk === 'string') out.perk = s.perk;
        return out;
      }
      return { kind: 'hidden' };
    },

    // Stops the claim's state at once; releases it now, or once its taps answer. Each step on its own.
    destroyClaim: function (id) {
      var entry = EncoreUnity.claims[id];
      if (!entry) return;
      delete EncoreUnity.claims[id];
      EncoreUnity.attempt('Claim unsubscribe', function () { if (entry.unsubscribe) entry.unsubscribe(); });
      EncoreUnity.attempt('Claim setVisible', function () { if (entry.handle) entry.handle.setVisible(0); });
      if (entry.taps === 0) EncoreUnity.finishClaim(entry);
      else entry.draining = true;
    },

    finishClaim: function (entry) {
      entry.finished = true;
      EncoreUnity.attempt('Claim destroy', function () { if (entry.handle) entry.handle.destroy(); });
    },

    destroy: function (id) {
      delete EncoreUnity.orphans[id];
      var button = EncoreUnity.buttons[id];
      if (!button) return;
      delete EncoreUnity.buttons[id];
      // Each step on its own, so a throw from one cannot leave the host on the page.
      EncoreUnity.attempt('Gift button observer', function () { if (button.observer) button.observer.disconnect(); });
      EncoreUnity.attempt('Gift button unmount', function () { if (button.handle) button.handle.unmount(); });
      button.handle = null;
      EncoreUnity.attempt('Gift button host', function () {
        if (button.host.parentNode) button.host.parentNode.removeChild(button.host);
      });
    },
  },

  // The web SDK has no store, so transactionLinking and the purchase controller do not apply.
  EncoreUnity_Configure: function (apiKey, optionsJson, onOutcome) {
    EncoreUnity.outcome = onOutcome;
    EncoreUnity.attempt('Configure', function () {
      var options = EncoreUnity.parse(optionsJson) || {};
      EncoreUnity.logsFailures = ['warn', 'info', 'debug'].indexOf(String(options.logLevel || '').toLowerCase()) >= 0;
      var sdk = EncoreUnity.sdk();
      if (!sdk) throw new Error('The web SDK is missing from this build.');
      var config = {
        apiKey: UTF8ToString(apiKey),
        logLevel: String(options.logLevel || 'none').toLowerCase(),
        unlockMode: options.unlock === 'strict' ? 'strict' : 'optimistic',
      };
      // Test builds only: a page can point the SDK at another backend, as the native harness builds do.
      if (typeof window.EncoreUnityEnvironment === 'string') config.environment = window.EncoreUnityEnvironment;
      sdk.configure(config);
      EncoreUnity.attempt('Fullscreen watch', EncoreUnity.watchFullscreen);
      var stop = EncoreUnity.stopStrictUnlock;
      EncoreUnity.stopStrictUnlock = null;
      if (stop) EncoreUnity.attempt('Strict unlock unsubscribe', stop);
      // The token silences a listener from an earlier Configure, even when its unsubscribe failed.
      var token = EncoreUnity.strictToken = {};
      EncoreUnity.stopStrictUnlock = sdk.onStrictUnlockVerified(function (transactionId) {
        if (EncoreUnity.strictToken !== token || !EncoreUnity.outcome) return;
        EncoreUnity.send(EncoreUnity.outcome, 0, { type: 'strictUnlockVerified', transactionId: transactionId });
      });
    });
  },

  // The web SDK answers only true or false, with no error detail.
  EncoreUnity_RevokeEntitlements: function (requestId, onReply) {
    var reply = EncoreUnity.replyOnce(onReply, requestId);
    var failure = { success: false, errorType: 'domain_error', errorMessage: 'The web SDK could not revoke entitlements.' };
    try {
      var sdk = EncoreUnity.sdk();
      if (!sdk) {
        reply(failure);
        return;
      }
      sdk.revokeEntitlements().then(
        function (ok) { reply(ok ? { success: true } : failure); },
        function () { reply(failure); }
      );
    } catch (e) {
      EncoreUnity.contain('RevokeEntitlements', e);
      reply(failure);
    }
  },

  EncoreUnity_Identify: function (userId, attributesJson) {
    EncoreUnity.attempt('Identify', function () {
      var sdk = EncoreUnity.sdk();
      if (sdk) sdk.identify(UTF8ToString(userId), EncoreUnity.parse(attributesJson) || undefined);
    });
  },

  EncoreUnity_SetUserAttributes: function (attributesJson) {
    EncoreUnity.attempt('SetUserAttributes', function () {
      var sdk = EncoreUnity.sdk();
      var attributes = EncoreUnity.parse(attributesJson);
      if (sdk && attributes) sdk.setUserAttributes(attributes);
    });
  },

  EncoreUnity_SetLanguage: function (tag) {
    EncoreUnity.attempt('SetLanguage', function () {
      var sdk = EncoreUnity.sdk();
      if (sdk) sdk.setLanguage(UTF8ToString(tag));
    });
  },

  EncoreUnity_ClearLanguage: function () {
    EncoreUnity.attempt('ClearLanguage', function () {
      var sdk = EncoreUnity.sdk();
      if (sdk) sdk.clearLanguage();
    });
  },

  EncoreUnity_Reset: function () {
    EncoreUnity.attempt('Reset', function () {
      var sdk = EncoreUnity.sdk();
      if (sdk) sdk.reset();
    });
  },

  EncoreUnity_ShowPlacement: function (requestId, requestJson, onResult) {
    var reply = EncoreUnity.replyOnce(onResult, requestId);
    try {
      var sdk = EncoreUnity.sdk();
      var request = EncoreUnity.parse(requestJson) || {};
      var useCase = EncoreUnity.useCase(request.useCase);
      if (!sdk || !useCase) {
        reply(sdk
          ? { type: 'notPresented', reason: 'use_case_unavailable' }
          : EncoreUnity.errorResult({ message: 'The web SDK is missing from this build.' }));
        return;
      }
      var placementId = EncoreUnity.text(request.placementId);
      var options = {};
      var appearance = EncoreUnity.appearance(request.appearance, true);
      if (appearance) options.appearance = appearance;
      var builder = (placementId ? sdk.placement(placementId, options) : sdk.placement(options)).useCase(useCase);
      var reward = EncoreUnity.reward(request.publisherReward);
      if (reward) builder = builder.publisherReward(reward);
      if (EncoreUnity.text(request.headline)) builder = builder.headline(request.headline);
      if (EncoreUnity.text(request.subheadline)) builder = builder.subheadline(request.subheadline);
      builder.show().then(
        function (r) {
          var result = EncoreUnity.attempt('ShowPlacement result', function () { return EncoreUnity.result(r); }) ||
            EncoreUnity.errorResult({ message: 'The web SDK sent a result the bridge could not read.' });
          if (reply(result)) EncoreUnity.emitOutcome(placementId, result);
        },
        function (error) { reply(EncoreUnity.errorResult(error)); }
      );
    } catch (e) {
      EncoreUnity.contain('ShowPlacement', e);
      reply(EncoreUnity.errorResult(e));
    }
  },

  EncoreUnity_GiftsButtonCreate: function (id, optionsJson, onMessage) {
    var button = null;
    try {
      var options = EncoreUnity.parse(optionsJson) || {};
      var placement = typeof options.placement === 'string' ? options.placement.trim() : '';
      var triggerId = typeof options.triggerId === 'string' ? options.triggerId.trim() : '';
      // A trigger is a reward surface only, as on Android and iOS.
      if (!triggerId || !placement || EncoreUnity.useCase(options.useCase) !== 'rewardUsers' || !EncoreUnity.sdk()) {
        EncoreUnity.send(onMessage, id, { event: 'height', height: 0 });
        return;
      }
      options.placement = placement;
      options.triggerId = triggerId;
      EncoreUnity.destroy(id);
      var host = document.createElement('div');
      // Hidden until C# places it, so it never flashes at the top-left corner.
      host.style.cssText = 'position:fixed;display:none;pointer-events:auto;';
      button = { id: id, callback: onMessage, options: options, host: host, frame: null, scaleY: 0, reported: -1, visible: true };
      // Registered before the first step that can throw, so fail() can take every step back.
      EncoreUnity.buttons[id] = button;
      EncoreUnity.layer().appendChild(host);
      button.observer = new ResizeObserver(function () {
        EncoreUnity.attempt('Gift button height', function () { EncoreUnity.reportHeight(button); });
      });
      button.observer.observe(host);
      EncoreUnity.mount(button);
    } catch (e) {
      EncoreUnity.contain('GiftsButtonCreate', e);
      if (button) EncoreUnity.fail(button);
      else EncoreUnity.send(onMessage, id, { event: 'height', height: 0 });
    }
  },

  EncoreUnity_GiftsButtonSetFrame: function (id, x, y, width, screenWidth, screenHeight) {
    EncoreUnity.attempt('GiftsButtonSetFrame', function () {
      var button = EncoreUnity.buttons[id];
      if (!button) return;
      var first = !button.frame;
      button.frame = { x: x, y: y, width: width, screenWidth: screenWidth, screenHeight: screenHeight };
      if (first && button.visible) button.host.style.display = 'block';
      EncoreUnity.place(button);
      EncoreUnity.reportHeight(button);
    });
  },

  // display:none also stops the web SDK counting an impression while the game hides the button.
  EncoreUnity_GiftsButtonSetVisible: function (id, visible) {
    EncoreUnity.attempt('GiftsButtonSetVisible', function () {
      var button = EncoreUnity.buttons[id];
      if (!button) return;
      button.visible = !!visible;
      button.host.style.display = button.visible && button.frame ? 'block' : 'none';
      EncoreUnity.reportHeight(button);
    });
  },

  EncoreUnity_GiftsButtonUpdate: function (id, updateJson) {
    EncoreUnity.attempt('GiftsButtonUpdate', function () {
      var button = EncoreUnity.buttons[id];
      var update = EncoreUnity.parse(updateJson) || {};
      if (!button) return;
      if (update.text) {
        var t = update.text;
        button.options.headline = t.headline;
        button.options.subheadline = t.subheadline;
        button.options.cta = t.cta;
        if (button.handle) {
          button.handle.setText({
            headline: EncoreUnity.text(t.headline),
            subheadline: EncoreUnity.text(t.subheadline),
            cta: EncoreUnity.text(t.cta),
          });
        }
      }
      if (update.sheetText) {
        var st = update.sheetText;
        button.options.sheetHeadline = st.sheetHeadline;
        button.options.sheetSubheadline = st.sheetSubheadline;
        // Both keys always: undefined goes back to the sheet's own text.
        if (button.handle && typeof button.handle.setSheetText === 'function') {
          button.handle.setSheetText({
            sheetHeadline: EncoreUnity.text(st.sheetHeadline),
            sheetSubheadline: EncoreUnity.text(st.sheetSubheadline),
          });
        }
      }
      if (update.alignment) {
        button.options.alignment = EncoreUnity.alignment(update.alignment);
        if (button.handle) button.handle.setAlignment(button.options.alignment);
      }
      ['values', 'appearance', 'publisherReward'].forEach(function (key) {
        if (update[key]) {
          button.options[key] = update[key];
          EncoreUnity.remountSoon(button);
        }
      });
    });
  },

  // destroy() guards each of its steps, so it cannot throw.
  EncoreUnity_GiftsButtonDestroy: function (id) {
    EncoreUnity.destroy(id);
  },

  // Fail closed, as the native bridges do: a blank placement, an unknown use case or no web SDK makes no claim, so C#
  // stays hidden. An absent use case is the reward default.
  EncoreUnity_ClaimCreate: function (id, optionsJson, onMessage) {
    EncoreUnity.attempt('ClaimCreate', function () {
      EncoreUnity.destroyClaim(id);
      var o = EncoreUnity.parse(optionsJson) || {};
      var placement = typeof o.placement === 'string' ? o.placement.trim() : '';
      var useCase = o.useCase === undefined ? 'rewardUsers' : EncoreUnity.useCase(o.useCase);
      var sdk = EncoreUnity.sdk();
      if (!placement || !useCase || !sdk || typeof sdk.claim !== 'function') return;
      var entry = { id: id, callback: onMessage, handle: null, unsubscribe: null, taps: 0, draining: false, finished: false };
      // Registered first, so a throw below is taken back by destroyClaim.
      EncoreUnity.claims[id] = entry;
      try {
        var options = {
          placement: placement,
          useCase: useCase,
          onResult: function (r) {
            if (entry.finished) return;
            var result = EncoreUnity.attempt('Claim result', function () { return EncoreUnity.result(r); }) ||
              EncoreUnity.errorResult({ message: 'The web SDK sent a result the bridge could not read.' });
            EncoreUnity.send(entry.callback, id, { event: 'result', result: result });
            EncoreUnity.emitOutcome(placement, result);
          },
        };
        if (o.autoOpen === 'off') options.autoOpen = 'off';
        var reward = EncoreUnity.reward(o.publisherReward);
        if (reward) options.publisherReward = reward;
        if (EncoreUnity.text(o.sheetHeadline)) options.sheetHeadline = o.sheetHeadline;
        if (EncoreUnity.text(o.sheetSubheadline)) options.sheetSubheadline = o.sheetSubheadline;
        entry.handle = sdk.claim(options);
        entry.unsubscribe = entry.handle.subscribe(function (state) {
          if (EncoreUnity.claims[id] !== entry) return;
          var payload = EncoreUnity.attempt('Claim state', function () { return EncoreUnity.claimState(state); }) || { kind: 'hidden' };
          EncoreUnity.send(entry.callback, id, { event: 'state', state: payload });
        });
      } catch (e) {
        EncoreUnity.destroyClaim(id);
        throw e;
      }
    });
  },

  EncoreUnity_ClaimDestroy: function (id) {
    EncoreUnity.destroyClaim(id);
  },

  EncoreUnity_ClaimSetVisible: function (id, fraction) {
    EncoreUnity.attempt('ClaimSetVisible', function () {
      var entry = EncoreUnity.claims[id];
      if (entry && entry.handle) entry.handle.setVisible(fraction);
    });
  },

  // Answers C# once: the sheet's result, no offers for a claim it does not hold, or an error.
  EncoreUnity_ClaimTap: function (id, target, requestId, onResult) {
    var reply = EncoreUnity.replyOnce(onResult, requestId);
    var entry = null;
    var settled = false;
    var settle = function () {
      if (settled || !entry) return;
      settled = true;
      entry.taps -= 1;
      if (entry.draining && entry.taps === 0 && !entry.finished) EncoreUnity.finishClaim(entry);
    };
    try {
      entry = EncoreUnity.claims[id] || null;
      if (!entry || !entry.handle) {
        entry = null;
        reply({ type: 'notPresented', reason: 'no_offer_available' });
        return;
      }
      var wire = UTF8ToString(target);
      entry.taps += 1;
      entry.handle.tap(['button', 'card', 'arrow'].indexOf(wire) >= 0 ? wire : undefined).then(
        function (r) {
          var result = EncoreUnity.attempt('ClaimTap result', function () { return EncoreUnity.result(r); }) ||
            EncoreUnity.errorResult({ message: 'The web SDK sent a result the bridge could not read.' });
          reply(result);
          settle();
        },
        function (error) {
          reply(EncoreUnity.errorResult(error));
          settle();
        }
      );
    } catch (e) {
      EncoreUnity.contain('ClaimTap', e);
      reply(EncoreUnity.errorResult(e));
      settle();
    }
  },

  EncoreUnity_ClaimDismiss: function (id) {
    EncoreUnity.attempt('ClaimDismiss', function () {
      var entry = EncoreUnity.claims[id];
      if (entry && entry.handle) entry.handle.dismiss();
    });
  },

  // Both keys always: undefined goes back to the sheet's own text.
  EncoreUnity_ClaimSetSheetText: function (id, json) {
    EncoreUnity.attempt('ClaimSetSheetText', function () {
      var entry = EncoreUnity.claims[id];
      var text = EncoreUnity.parse(json) || {};
      if (!entry || !entry.handle) return;
      entry.handle.setSheetText({
        sheetHeadline: EncoreUnity.text(text.sheetHeadline),
        sheetSubheadline: EncoreUnity.text(text.sheetSubheadline),
      });
    });
  },
};

autoAddDeps(EncoreUnityLib, '$EncoreUnity');
mergeInto(LibraryManager.library, EncoreUnityLib);
