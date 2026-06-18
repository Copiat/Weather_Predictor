// ============================================================
//  WeatherWidget - native window interop
//  Forwards mouse-down drag events from the Blazor canvas to the
//  .NET side, which calls Win32 ReleaseCapture + SendMessage to
//  let the OS move the borderless, transparent Photino window.
// ============================================================
window.widgetInterop = {

    // Wire up the drag handler once the Blazor root is rendered.
    // dotNetRef: a DotNetObjectReference to the component exposing OnDragStart.
    initDrag: function (dotNetRef) {
        if (window.__widgetDragBound) return;
        window.__widgetDragBound = true;

        const isDraggable = (el) => {
            // Only start a native drag when the user presses on the "background
            // canvas" — never on buttons, inputs, links, scrollable lists, etc.
            if (!el) return false;
            if (el.closest && el.closest('.no-drag')) return false;
            if (el.closest && el.closest('button, input, a, select, textarea, .config-body, .cog-btn, .refresh-btn')) return false;
            return true;
        };

        const onDown = (e) => {
            if (e.button !== 0) return;           // left button only
            if (!isDraggable(e.target)) return;
            // Ask .NET to release capture + send WM_NCLBUTTONDOWN(HTCAPTION).
            dotNetRef.invokeMethodAsync('OnDragStart');
        };

        document.addEventListener('mousedown', onDown);
        // Touch support for touch-capable Windows devices
        document.addEventListener('touchstart', (e) => {
            if (!isDraggable(e.target)) return;
            dotNetRef.invokeMethodAsync('OnDragStart');
        }, { passive: true });
    },

    // Focal blur for inputs already handled in CSS; helper kept for future use.
    focusElement: function (selector) {
        const el = document.querySelector(selector);
        if (el) el.focus();
    }
};
