mergeInto(LibraryManager.library, {
    AbxrNavigateTo: function(urlPtr) {
        var url = UTF8ToString(urlPtr);
        window.location.href = url;
    },

    // Removes the auth_handoff value from the address bar without reloading the page.
    // Called by the receiving SDK once it has read the handoff, so it does not
    // linger in the URL, browser history, or referer headers on outbound links.
    // Handles both fragment (#auth_handoff=...) and query (?auth_handoff=...) forms;
    AbxrStripHandoffFromUrl: function() {
        try {
            var loc = window.location;
            var cleanedHash = loc.hash;
            var cleanedSearch = loc.search;

            // Strip from fragment
            if (cleanedHash && cleanedHash.length > 1) {
                var hashBody = cleanedHash.charAt(0) === '#' ? cleanedHash.substring(1) : cleanedHash;
                var hashParts = hashBody.split('&').filter(function(p) {
                    if (!p) return false;
                    var eq = p.indexOf('=');
                    var key = eq >= 0 ? p.substring(0, eq) : p;
                    return decodeURIComponent(key) !== 'auth_handoff';
                });
                cleanedHash = hashParts.length > 0 ? '#' + hashParts.join('&') : '';
            }

            // Strip from query
            if (cleanedSearch && cleanedSearch.length > 1) {
                var searchBody = cleanedSearch.charAt(0) === '?' ? cleanedSearch.substring(1) : cleanedSearch;
                var searchParts = searchBody.split('&').filter(function(p) {
                    if (!p) return false;
                    var eq = p.indexOf('=');
                    var key = eq >= 0 ? p.substring(0, eq) : p;
                    return decodeURIComponent(key) !== 'auth_handoff';
                });
                cleanedSearch = searchParts.length > 0 ? '?' + searchParts.join('&') : '';
            }

            var newUrl = loc.pathname + cleanedSearch + cleanedHash;
            if (window.history && window.history.replaceState) {
                window.history.replaceState(null, '', newUrl);
            }
        } catch (e) {}
    }
});
