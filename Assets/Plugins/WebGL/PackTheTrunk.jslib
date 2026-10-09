// The browser build's side of WebBridge.cs: the game's state as window.pttState, for the page and its test tools.
mergeInto(LibraryManager.library, {
  PttPublishState: function (json) {
    try { window.pttState = JSON.parse(UTF8ToString(json)); } catch (e) { }
  },
});
