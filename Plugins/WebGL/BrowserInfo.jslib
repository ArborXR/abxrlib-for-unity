mergeInto(LibraryManager.library, {
    AbxrDetectDeviceModel: function () {
        var ua = (typeof navigator !== 'undefined' && navigator.userAgent) ? navigator.userAgent : "";
        function pick() {
            if (!ua) return "Unknown Browser";

            // Edge (order matters before Chrome)
            var m;
            if ((m = ua.match(/EdgA\/([\d.]+)/)) || (m = ua.match(/EdgiOS\/([\d.]+)/)) || (m = ua.match(/Edg\/([\d.]+)/)))
                return "Edge " + (m[1] || "");

            // Opera
            if ((m = ua.match(/OPR\/([\d.]+)/)) || (m = ua.match(/OPiOS\/([\d.]+)/)) || ua.indexOf("Opera") >= 0)
                return "Opera " + (m ? m[1] : "");

            // Samsung Internet
            if ((m = ua.match(/SamsungBrowser\/([\d.]+)/)))
                return "Samsung Internet " + (m[1] || "");

            // Firefox
            if ((m = ua.match(/FxiOS\/([\d.]+)/)) || (m = ua.match(/Firefox\/([\d.]+)/)))
                return "Firefox " + (m[1] || "");

            // Chrome (include iOS Chrome = CriOS)
            if ((m = ua.match(/CriOS\/([\d.]+)/)) || (m = ua.match(/Chrome\/([\d.]+)/))) {
                // Exclude Edge/Opera which also include "Chrome"
                if (ua.indexOf("Edg") < 0 && ua.indexOf("OPR") < 0 && ua.indexOf("OPiOS") < 0)
                    return "Chrome " + (m[1] || "");
            }

            // Safari (no Chrome/Chromium present)
            if (ua.indexOf("Safari") >= 0 && ua.indexOf("Chrome") < 0 && ua.indexOf("Chromium") < 0) {
                m = ua.match(/Version\/([\d.]+)/);
                return "Safari " + (m ? m[1] : "");
            }

            return "Unknown Browser";
        }

        var out = pick();
        var len = lengthBytesUTF8(out) + 1;
        var ptr = _malloc(len);
        stringToUTF8(out, ptr, len);
        return ptr; // Unity will marshal this to a C# string
    }
});