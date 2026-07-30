// Colour-theme bootstrap. Runs during <head> parse so the correct theme is applied
// before first paint (no flash). Light is the default until the user chooses otherwise.
(function () {
    try {
        var t = localStorage.getItem('mpa-color-theme') || 'light';
        document.documentElement.setAttribute('data-theme', t);
    } catch (e) {
        document.documentElement.setAttribute('data-theme', 'light');
    }
})();

// Interop surface for the Blazor toggle.
window.mpaTheme = {
    get: function () {
        try { return localStorage.getItem('mpa-color-theme') || 'light'; }
        catch (e) { return 'light'; }
    },
    set: function (t) {
        try { localStorage.setItem('mpa-color-theme', t); } catch (e) { }
        document.documentElement.setAttribute('data-theme', t);
    }
};
