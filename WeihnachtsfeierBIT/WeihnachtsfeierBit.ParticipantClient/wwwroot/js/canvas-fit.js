let activeCanvasFitObservers = 0;

export function observeCanvasFit(container, content) {
    let disposed = false;
    let animationFrame = 0;

    const fit = () => {
        animationFrame = 0;
        if (disposed) {
            return;
        }

        const containerStyle = getComputedStyle(container);
        const availableWidth = container.clientWidth
            - parseFloat(containerStyle.paddingLeft)
            - parseFloat(containerStyle.paddingRight);
        const availableHeight = container.clientHeight
            - parseFloat(containerStyle.paddingTop)
            - parseFloat(containerStyle.paddingBottom);

        content.style.transform = "scale(1)";
        const contentBounds = content.getBoundingClientRect();
        const contentWidth = Math.max(contentBounds.width, content.scrollWidth);
        const contentHeight = Math.max(contentBounds.height, content.scrollHeight);
        if (availableWidth <= 0 || availableHeight <= 0 || contentWidth <= 0 || contentHeight <= 0) {
            return;
        }

        const scale = Math.min(1, availableWidth / contentWidth, availableHeight / contentHeight);
        content.style.transform = `scale(${scale})`;
    };

    const scheduleFit = () => {
        if (!disposed && animationFrame === 0) {
            animationFrame = requestAnimationFrame(fit);
        }
    };

    const resizeObserver = new ResizeObserver(scheduleFit);
    resizeObserver.observe(container);
    resizeObserver.observe(content);

    const mutationObserver = new MutationObserver(scheduleFit);
    mutationObserver.observe(content, {
        childList: true,
        characterData: true,
        subtree: true
    });

    window.addEventListener("resize", scheduleFit, { passive: true });

    if (activeCanvasFitObservers === 0) {
        document.documentElement.classList.add("canvas-document");
    }
    activeCanvasFitObservers++;
    scheduleFit();

    return {
        refresh: scheduleFit,
        dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            cancelAnimationFrame(animationFrame);
            resizeObserver.disconnect();
            mutationObserver.disconnect();
            window.removeEventListener("resize", scheduleFit);
            activeCanvasFitObservers--;
            if (activeCanvasFitObservers === 0) {
                document.documentElement.classList.remove("canvas-document");
            }
        }
    };
}
