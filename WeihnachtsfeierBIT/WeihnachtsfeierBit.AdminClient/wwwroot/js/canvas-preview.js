export function observeCanvasPreview(stage, frame, width, height) {
    let disposed = false;
    let animationFrame = 0;

    const refresh = () => {
        animationFrame = 0;
        if (disposed) {
            return;
        }

        const scale = Math.min(stage.clientWidth / width, stage.clientHeight / height);
        if (scale > 0) {
            frame.style.width = `${width}px`;
            frame.style.height = `${height}px`;
            frame.style.left = `${(stage.clientWidth - width * scale) / 2}px`;
            frame.style.top = `${(stage.clientHeight - height * scale) / 2}px`;
            frame.style.transform = `scale(${scale})`;
        }
    };

    const scheduleRefresh = () => {
        if (!disposed && animationFrame === 0) {
            animationFrame = requestAnimationFrame(refresh);
        }
    };

    const resizeObserver = new ResizeObserver(scheduleRefresh);
    resizeObserver.observe(stage);
    scheduleRefresh();

    return {
        refresh: scheduleRefresh,
        dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            cancelAnimationFrame(animationFrame);
            resizeObserver.disconnect();
        }
    };
}
