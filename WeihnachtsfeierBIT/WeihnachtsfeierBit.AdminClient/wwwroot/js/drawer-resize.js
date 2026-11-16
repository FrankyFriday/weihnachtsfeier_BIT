export function attachDrawerResize(handle) {
    let activePointerId = null;
    let startX = 0;
    let startWidth = 380;
    let previousCursor = "";
    let previousUserSelect = "";

    const root = document.documentElement;
    const readWidth = () => {
        const value = Number.parseFloat(getComputedStyle(root).getPropertyValue("--regie-drawer-width"));
        return Number.isFinite(value) ? value : 380;
    };

    const onPointerDown = event => {
        if (event.button !== 0 || activePointerId !== null) {
            return;
        }

        event.preventDefault();
        activePointerId = event.pointerId;
        startX = event.clientX;
        startWidth = readWidth();
        previousCursor = document.body.style.cursor;
        previousUserSelect = document.body.style.userSelect;
        document.body.style.cursor = "ew-resize";
        document.body.style.userSelect = "none";
        handle.setPointerCapture(event.pointerId);
    };

    const onPointerMove = event => {
        if (event.pointerId !== activePointerId) {
            return;
        }

        const maximum = Math.min(720, window.innerWidth - 80);
        const width = Math.max(280, Math.min(maximum, startWidth + startX - event.clientX));
        root.style.setProperty("--regie-drawer-width", `${width}px`);
    };

    const finishResize = event => {
        if (event.pointerId !== activePointerId) {
            return;
        }

        activePointerId = null;
        document.body.style.cursor = previousCursor;
        document.body.style.userSelect = previousUserSelect;
    };

    handle.addEventListener("pointerdown", onPointerDown);
    handle.addEventListener("pointermove", onPointerMove);
    handle.addEventListener("pointerup", finishResize);
    handle.addEventListener("pointercancel", finishResize);
    handle.addEventListener("lostpointercapture", finishResize);

    return {
        dispose() {
            handle.removeEventListener("pointerdown", onPointerDown);
            handle.removeEventListener("pointermove", onPointerMove);
            handle.removeEventListener("pointerup", finishResize);
            handle.removeEventListener("pointercancel", finishResize);
            handle.removeEventListener("lostpointercapture", finishResize);
            if (activePointerId !== null) {
                document.body.style.cursor = previousCursor;
                document.body.style.userSelect = previousUserSelect;
            }
        }
    };
}
