/*!
* jQueryTouch v0.0.6
* https://github.com/a-fung/jQueryTouch
*
* Copyright 2012 Man Kwan Liu
* Released under the Apache License Version 2.0
* http://www.apache.org/licenses/
*
* Date: Wed Oct 2012 23:14:09 GMT-0700 (Pacific Daylight Time)
*/
(function($) {
    $.fn.gestureInit = function(e) {
        if (!e || typeof(e) != "object") {
            e = {}
        }
        e = $.extend({
            prefix: "_gesture_",
            gesture_prefix: ""
        }, e);
        if (!e.prefix)
            e.prefix = "_gesture_";
        var f = {
            preventDefault: true,
            mouse: true,
            pen: true,
            maxtouch: 2,
            prefix: e.prefix
        };
        var g, original_angle, id1, sqr = function(n) {
            return n * n
        };
        var h = function(a) {
            var b = [], eventType = null;
            if (a.touches.length == 2) {
                b = a.touches;
                if (a.type == e.prefix + "touch_start") {
                    eventType = "start";
                    g = Math.sqrt(sqr(b[0].pageX - b[1].pageX) + sqr(b[0].pageY - b[1].pageY));
                    original_angle = Math.atan2(b[0].pageY - b[1].pageY, b[0].pageX - b[1].pageX);
                    id1 = b[0].id
                } else if (a.type == e.prefix + "touch_move") {
                    eventType = "move"
                }
            } else if (a.touches.length == 1 && a.type == e.prefix + "touch_end") {
                eventType = "end";
                var c = {
                    clientX: a.clientX,
                    clientY: a.clientY,
                    pageX: a.pageX,
                    pageY: a.pageY,
                    screenX: a.screenX,
                    screenY: a.screenY
                };
                b = (id1 == a.touches[0].id) ? [a.touches[0], c] : [c, a.touches[0]]
            }
            if (eventType) {
                var d = $.Event(e.gesture_prefix + "gesture_" + eventType);
                d = $.extend(d, {
                    scale: Math.sqrt(sqr(b[0].pageX - b[1].pageX) + sqr(b[0].pageY - b[1].pageY)) / g,
                    rotation: Math.atan2(b[0].pageY - b[1].pageY, b[0].pageX - b[1].pageX) - original_angle,
                    clientX: (b[0].clientX + b[1].clientX) / 2,
                    clientY: (b[0].clientY + b[1].clientY) / 2,
                    pageX: (b[0].pageX + b[1].pageX) / 2,
                    pageY: (b[0].pageY + b[1].pageY) / 2,
                    screenX: (b[0].screenX + b[1].screenX) / 2,
                    screenY: (b[0].screenY + b[1].screenY) / 2
                });
                try {
                    $(this).trigger(d)
                } catch (error) {
                    console.log(error)
                }
            }
        };
        this.touchInit(f);
        this.on(e.prefix + "touch_start", h);
        this.on(e.prefix + "touch_move", h);
        this.on(e.prefix + "touch_end", h);
        this.data(e.gesture_prefix + "_gesture_handler", h);
        return this
    };
    $.fn.gestureDispose = function(a, b) {
        if (!a || typeof(a) != "string") {
            a = "_gesture_"
        }
        if (!b || typeof(b) != "string") {
            a = ""
        }
        var c = this.data(b + "_gesture_handler");
        this.off(a + "touch_start", c);
        this.off(a + "touch_move", c);
        this.off(a + "touch_end", c);
        this.removeData(b + "_gesture_handler");
        this.touchDispose(a);
        return this
    }
})(jQuery);
