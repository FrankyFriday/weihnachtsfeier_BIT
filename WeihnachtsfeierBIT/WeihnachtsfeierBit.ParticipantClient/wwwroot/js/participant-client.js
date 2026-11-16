export function wasNamePromptSkipped(key) {
    return sessionStorage.getItem(key) === "true";
}

export function markNamePromptSkipped(key) {
    sessionStorage.setItem(key, "true");
}
