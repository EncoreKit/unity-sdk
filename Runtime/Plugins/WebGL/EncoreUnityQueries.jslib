// The settings and the placement queries, called by WebGLPlatform.Queries.cs. Uses $EncoreUnity from
// EncoreUnity.jslib. No web SDK has a placement prefetch yet, so Prefetch calls one only if a bundled build has it.
var EncoreUnityQueriesLib = {
  EncoreUnity_SetClaimEnabled__deps: ['$EncoreUnity'],
  EncoreUnity_SetClaimEnabled: function (enabled) {
    EncoreUnity.attempt('SetClaimEnabled', function () {
      var sdk = EncoreUnity.sdk();
      if (sdk) sdk.isClaimEnabled = !!enabled;
    });
  },

  // The web SDK's on/off switch, 2.7.0 and later. C# sends it before Configure too, so a game set off never starts the
  // SDK. 1 when the bundled SDK now has that value, 0 when off did not take (a bundle older than 2.7.0 has no switch, and
  // is always on), so C# fails closed instead of starting an SDK it cannot turn off. On is always taken.
  EncoreUnity_SetEnabled__deps: ['$EncoreUnity'],
  EncoreUnity_SetEnabled: function (enabled) {
    var on = !!enabled;
    var took = EncoreUnity.attempt('SetEnabled', function () {
      var sdk = EncoreUnity.sdk();
      if (!sdk || !('isEnabled' in sdk)) return on;
      sdk.isEnabled = on;
      return sdk.isEnabled === on;
    });
    return took === true ? 1 : 0;
  },

  EncoreUnity_Prefetch__deps: ['$EncoreUnity'],
  EncoreUnity_Prefetch: function (requestJson) {
    EncoreUnity.attempt('Prefetch', function () {
      var sdk = EncoreUnity.sdk();
      var request = EncoreUnity.parse(requestJson) || {};
      var useCase = EncoreUnity.useCase(request.useCase);
      if (!sdk || !useCase) return;
      var builder = sdk.placement(EncoreUnity.text(request.placementId)).useCase(useCase);
      // A newer SDK may answer with a promise; a rejection is contained like a throw.
      var pending = typeof builder.prefetch === 'function' ? builder.prefetch() : null;
      if (pending && typeof pending.then === 'function') {
        pending.then(null, function (e) { EncoreUnity.contain('Prefetch', e); });
      }
    });
  },

  // One answer. A use case this bridge does not know, a web SDK with no hasOffers, and any failure answer false.
  EncoreUnity_HasOffers__deps: ['$EncoreUnity'],
  EncoreUnity_HasOffers: function (requestId, requestJson, onReply) {
    var reply = EncoreUnity.replyOnce(onReply, requestId);
    var none = { hasOffers: false };
    try {
      var sdk = EncoreUnity.sdk();
      var request = EncoreUnity.parse(requestJson) || {};
      var useCase = EncoreUnity.useCase(request.useCase);
      if (!sdk || !useCase) return reply(none);
      var builder = sdk.placement(EncoreUnity.text(request.placementId)).useCase(useCase);
      if (typeof builder.hasOffers !== 'function') return reply(none);
      Promise.resolve(builder.hasOffers()).then(
        function (hasOffers) { reply({ hasOffers: hasOffers === true }); },
        function (e) { EncoreUnity.contain('HasOffers', e); reply(none); }
      );
    } catch (e) {
      EncoreUnity.contain('HasOffers', e);
      reply(none);
    }
  },

  // Before Configure. The bundled web SDK has setWrapper from 2.5.0; an older one has none, so this does nothing.
  EncoreUnity_SetWrapper__deps: ['$EncoreUnity'],
  EncoreUnity_SetWrapper: function (name, version) {
    EncoreUnity.attempt('SetWrapper', function () {
      var sdk = EncoreUnity.sdk();
      if (sdk && typeof sdk.setWrapper === 'function') sdk.setWrapper(UTF8ToString(name), UTF8ToString(version));
    });
  },

  // A failure C# contained. Both are fixed text from C#: a call name and an exception type name.
  EncoreUnity_ReportWrapperError__deps: ['$EncoreUnity'],
  EncoreUnity_ReportWrapperError: function (site, code) {
    EncoreUnity.report(UTF8ToString(site), UTF8ToString(code));
  },
};

mergeInto(LibraryManager.library, EncoreUnityQueriesLib);
