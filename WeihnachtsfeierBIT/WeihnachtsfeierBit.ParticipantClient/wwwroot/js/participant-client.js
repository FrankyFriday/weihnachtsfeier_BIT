const deviceIdStorageKey = "weihnachtsfeierbit:device-id";

export function wasNamePromptSkipped(key) {
    return sessionStorage.getItem(key) === "true";
}

export function markNamePromptSkipped(key) {
    sessionStorage.setItem(key, "true");
}

export function getOrCreateDeviceId() {
    const storedId = localStorage.getItem(deviceIdStorageKey);
    if (storedId && /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(storedId)) {
        return storedId;
    }

    const deviceId = typeof crypto.randomUUID === "function"
        ? crypto.randomUUID()
        : createRandomDeviceId();
    localStorage.setItem(deviceIdStorageKey, deviceId);
    return deviceId;
}

export function consumeEmulatorTicketFromFragment() {
    const fragment = new URLSearchParams(window.location.hash.slice(1));
    const ticket = fragment.get("emulatorTicket");
    if (ticket !== null) {
        fragment.delete("emulatorTicket");
        const remainingFragment = fragment.toString();
        const cleanUrl = `${window.location.pathname}${window.location.search}${remainingFragment ? `#${remainingFragment}` : ""}`;
        window.history.replaceState(window.history.state, "", cleanUrl);
    }

    return ticket;
}

function createRandomDeviceId() {
    const bytes = crypto.getRandomValues(new Uint8Array(16));
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    const hex = Array.from(bytes, value => value.toString(16).padStart(2, "0")).join("");
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
