mergeInto(LibraryManager.library, {
  TTWebAudioInit: function () {
    var tt = globalThis.tt;
    if (!tt || typeof tt.createInnerAudioContext !== "function") return;
    if (!globalThis.__TTWebAudioCtxs) {
      globalThis.__TTWebAudioCtxs = {};
      globalThis.__TTWebAudioEnabled = true;
      globalThis.__TTWebAudioUnlocked = false;
    }
    if (typeof tt.onTouchStart === "function") {
      tt.onTouchStart(function () {
        if (!globalThis.__TTWebAudioUnlocked && globalThis.tt) {
          globalThis.__TTWebAudioUnlocked = true;
          try {
            var c = globalThis.tt.createInnerAudioContext();
            c.volume = 0;
            c.src = "StreamingAssets/audio/move.mp3";
            c.play();
            setTimeout(function () { try { c.destroy(); } catch (e) {} }, 300);
          } catch (e) {}
        }
      });
    }
  },

  TTWebAudioUnlock: function () {
    if (!globalThis.tt || globalThis.__TTWebAudioUnlocked) return;
    globalThis.__TTWebAudioUnlocked = true;
    try {
      var c = globalThis.tt.createInnerAudioContext();
      c.volume = 0;
      c.src = "StreamingAssets/audio/move.mp3";
      c.play();
      setTimeout(function () { try { c.destroy(); } catch (e) {} }, 300);
    } catch (e) {}
  },

  TTWebAudioPlay: function (namePtr) {
    var tt = globalThis.tt;
    if (!tt || globalThis.__TTWebAudioEnabled === false) return;
    var name = typeof UTF8ToString !== "undefined" ? UTF8ToString(namePtr) : namePtr;
    try {
      var ctxs = globalThis.__TTWebAudioCtxs || (globalThis.__TTWebAudioCtxs = {});
      var ctx = ctxs[name];
      if (!ctx) {
        ctx = tt.createInnerAudioContext();
        ctxs[name] = ctx;
      }
      ctx.src = "StreamingAssets/audio/" + name + ".mp3";
      ctx.volume = globalThis.__TTWebAudioVolume === undefined ? 0.5 : globalThis.__TTWebAudioVolume;
      ctx.stop();
      ctx.play();
    } catch (e) {}
  },

  TTWebAudioSetVolume: function (volume) {
    globalThis.__TTWebAudioVolume = volume;
  },

  TTWebAudioSetEnabled: function (enabled) {
    globalThis.__TTWebAudioEnabled = enabled === 1;
  }
});
