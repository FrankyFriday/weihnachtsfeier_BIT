let player;
let deviceId;
let accessToken;
let pendingPopup;
let sdkPromise;
const preparedButtons = new WeakSet();

export function prepareAuthButton(button) {
  if (!button || preparedButtons.has(button)) return;
  preparedButtons.add(button);
  button.addEventListener("click", () => {
    pendingPopup?.close();
    pendingPopup = window.open("about:blank", "spotify-hitster-login", "width=540,height=720,popup");
    if (pendingPopup) {
      pendingPopup.document.title = "Spotify verbinden";
      pendingPopup.document.body.textContent = "Spotify-Anmeldung wird vorbereitet …";
    }
  }, true);
}

export function cancelAuthorization() {
  pendingPopup?.close();
  pendingPopup = undefined;
}

export async function authorize(url, dotNetReference) {
  const popup = pendingPopup;
  pendingPopup = null;
  if (!popup) throw new Error("Der Spotify-Anmeldepopup wurde blockiert. Bitte Popups für diese Seite erlauben.");

  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => finish(null, new Error("Spotify-Anmeldung abgebrochen oder Zeitüberschreitung.")), 180_000);
    const finish = (result, error) => {
      clearTimeout(timer);
      window.removeEventListener("message", onMessage);
      popup.close();
      if (error) reject(error);
      else resolve(result);
    };
    const onMessage = event => {
      if (event.origin !== window.location.origin || event.source !== popup ||
          event.data?.type !== "hitster-spotify-oauth") return;
      finish({ code: event.data.code, state: event.data.state, error: event.data.error }, null);
    };

    window.addEventListener("message", onMessage);
    try {
      popup.location.href = url;
    } catch (error) {
      finish(null, error);
    }
  });
}

function loadSdk() {
  if (window.Spotify) return Promise.resolve();
  if (!sdkPromise) {
    sdkPromise = new Promise((resolve, reject) => {
      const timeout = setTimeout(() => reject(new Error("Spotify Web Playback SDK konnte nicht geladen werden.")), 20_000);
      window.onSpotifyWebPlaybackSDKReady = () => {
        clearTimeout(timeout);
        resolve();
      };
      const script = document.createElement("script");
      script.src = "https://sdk.scdn.co/spotify-player.js";
      script.onerror = () => {
        clearTimeout(timeout);
        reject(new Error("Spotify Web Playback SDK konnte nicht geladen werden."));
      };
      document.head.appendChild(script);
    });
  }
  return sdkPromise;
}

export async function connect(token) {
  accessToken = token;
  await loadSdk();
  if (player) await player.disconnect();

  player = new window.Spotify.Player({
    name: "Weihnachtsfeier Hitster",
    getOAuthToken: callback => callback(accessToken),
    volume: 0.65
  });

  const ready = new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error("Spotify-Wiedergabegerät wurde nicht bereit.")), 20_000);
    player.addListener("ready", event => {
      clearTimeout(timeout);
      deviceId = event.device_id;
      resolve(deviceId);
    });
    for (const eventName of ["initialization_error", "authentication_error", "account_error", "playback_error"]) {
      player.addListener(eventName, event => {
        clearTimeout(timeout);
        reject(new Error(`${event.message}${eventName === "account_error" ? " (Spotify Premium erforderlich)" : ""}`));
      });
    }
  });

  const connected = await player.connect();
  if (!connected) throw new Error("Spotify-Wiedergabegerät konnte nicht verbunden werden.");
  return await ready;
}

export function setToken(token) {
  accessToken = token;
}

export async function play(token, trackId) {
  accessToken = token;
  if (!player || !deviceId) throw new Error("Bitte zuerst Spotify verbinden.");
  await player.activateElement();

  const headers = {
    Authorization: `Bearer ${accessToken}`,
    "Content-Type": "application/json"
  };
  const transfer = await fetch("https://api.spotify.com/v1/me/player", {
    method: "PUT",
    headers,
    body: JSON.stringify({ device_ids: [deviceId], play: true })
  });
  if (!transfer.ok && transfer.status !== 204) {
    throw new Error(`Spotify-Gerät konnte nicht aktiviert werden (${transfer.status}).`);
  }

  const response = await fetch(`https://api.spotify.com/v1/me/player/play?device_id=${encodeURIComponent(deviceId)}`, {
    method: "PUT",
    headers,
    body: JSON.stringify({ uris: [`spotify:track:${trackId}`] })
  });
  if (!response.ok && response.status !== 204) {
    throw new Error(`Song konnte nicht gestartet werden (${response.status}). Spotify Premium ist erforderlich.`);
  }
}

export async function pause() {
  if (player) await player.pause();
}

export async function disconnect() {
  if (player) await player.disconnect();
  player = undefined;
  deviceId = undefined;
  accessToken = undefined;
  pendingPopup?.close();
  pendingPopup = undefined;
}
