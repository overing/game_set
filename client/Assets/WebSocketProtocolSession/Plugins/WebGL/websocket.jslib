mergeInto(LibraryManager.library, {
    $instances: [{ id: 0 }],

    $stringToUTF8Buffer: function (str) {
        let length = lengthBytesUTF8(str) + 1;
        let buffer = _malloc(length);
        stringToUTF8(str, buffer, length);
        return buffer;
    },

    $addOpenEvent: function(instance, callback) {
        instance.socket.addEventListener('open', function (e) {
            dynCall_vi(callback, instance.id);
        });
    },

    $addMessageEvent__deps: [ '$stringToUTF8Buffer' ],
    $addMessageEvent: function(instance, callback) {
        instance.socket.addEventListener('message', function (e) {
            dynCall_vii(callback, instance.id, stringToUTF8Buffer(e.data));
        });
    },

    $addErrorEvent__deps: [ '$stringToUTF8Buffer' ],
    $addErrorEvent: function(instance, callback) {
        instance.socket.addEventListener('error', function (e) {
            dynCall_vii(callback, instance.id, stringToUTF8Buffer(JSON.stringify(e)));
        });
    },

    $addCloseEvent__deps: [ '$stringToUTF8Buffer' ],
    $addCloseEvent: function(instance, callback) {
        instance.socket.addEventListener('close', function (e) {
            dynCall_viiii(callback, instance.id, e.code, stringToUTF8Buffer(e.reason), e.wasClean ? 1 : 0);
        });
    },

    JsCreate__deps: [
        '$instances'
    ],
    JsCreate: function (url) {
        let instance = {
            url: UTF8ToString(url),
            openCallbacks: [],
            messageCallbacks: [],
            errorCallbacks: [],
            closeCallbacks: []
        };
        instance.id = instances.push(instance) - 1;
        return instance.id;
    },

    JsConnect__deps: [
        '$instances',
        '$addOpenEvent',
        '$addMessageEvent',
        '$addErrorEvent',
        '$addCloseEvent'
    ],
    JsConnect: function (id) {
        let instance = instances[id];
        instance.socket = new WebSocket(instance.url);
        instance.openCallbacks.forEach(callback => addOpenEvent(instance, callback));
        instance.messageCallbacks.forEach(callback => addMessageEvent(instance, callback));
        instance.errorCallbacks.forEach(callback => addErrorEvent(instance, callback));
        instance.closeCallbacks.forEach(callback => addCloseEvent(instance, callback));
    },

    JsSend__deps: [
        '$instances'
    ],
    JsSend: function (id, data) {
        let instance = instances[id];
        instance.socket.send(UTF8ToString(data));
    },

    JsClose__deps: [
        '$instances'
    ],
    JsClose: function (id) {
        let instance = instances[id];
        instance.socket.close();
        instance.openCallbacks = [];
        instance.messageCallbacks = [];
        instance.errorCallbacks = [];
        instance.closeCallbacks = [];
        instance.socket = null;
    },

    JsAddOpenEventListener__deps: [
        '$instances',
        '$addOpenEvent'
    ],
    JsAddOpenEventListener: function (id, callback) {
        let instance = instances[id];
        instance.openCallbacks.push(callback);
        if (instance.socket != null)
            addOpenEvent(instance, callback);
    },

    JsAddMessageEventListener__deps: [
        '$instances',
        '$addMessageEvent'
    ],
    JsAddMessageEventListener: function (id, callback) {
        let instance = instances[id];
        instance.messageCallbacks.push(callback);
        if (instance.socket != null)
            addMessageEvent(instance, callback);
    },

    JsAddErrorEventListener__deps: [
        '$instances',
        '$addErrorEvent'
    ],
    JsAddErrorEventListener: function (id, callback) {
        let instance = instances[id];
        instance.errorCallbacks.push(callback);
        if (instance.socket != null)
            addErrorEvent(instance, callback);
    },

    JsAddCloseEventListener__deps: [
        '$instances',
        '$addCloseEvent'
    ],
    JsAddCloseEventListener: function (id, callback) {
        let instance = instances[id];
        instance.closeCallbacks.push(callback);
        if (instance.socket != null)
            addCloseEvent(instance, callback);
    },
});