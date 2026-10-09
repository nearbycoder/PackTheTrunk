// The browser build's side of WebBridge.cs and Web.cs: the game's state as window.pttState, for the page and its test
// tools, and whether the page found a touch-first device (window.pttTouchFirst, set by index.html before the game loads).
mergeInto(LibraryManager.library, {
  PttPublishState: function (json) {
    try { window.pttState = JSON.parse(UTF8ToString(json)); } catch (e) { }
  },
  PttTouchFirst: function () {
    return window.pttTouchFirst ? 1 : 0;
  },
});
