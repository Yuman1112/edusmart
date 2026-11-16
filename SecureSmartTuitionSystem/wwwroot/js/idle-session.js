(() => {
    'use strict';
    const root = document.getElementById('idle-session');
    if (!root || !window.bootstrap) return;
    const modalElement = document.getElementById('idle-session-modal');
    const modal = new bootstrap.Modal(modalElement, { backdrop: 'static', keyboard: false });
    const button = document.getElementById('idle-session-continue');
    const error = document.getElementById('idle-session-error');
    const countdown = document.getElementById('idle-session-countdown');
    const token = root.querySelector('input[name="__RequestVerificationToken"]').value;
    let expires = 0, offset = 0, scope = '', warning = false, busy = false;
    let dirty = false, lastSent = 0, lastStatus = 0, checkingExpiry = false;
    const videoTimes = new WeakMap();
    const channel = typeof BroadcastChannel === 'function' ? new BroadcastChannel('edusmart-idle') : null;
    const now = () => Date.now() + offset;
    function publish(data) {
        if (channel) channel.postMessage(data);
        try { localStorage.setItem('edusmart-idle-sync', JSON.stringify({ ...data, nonce: Date.now() })); } catch (_) { /* Storage may be disabled. */ }
    }
    function accept(data, remote = false) {
        if (!Number.isFinite(data.serverNow) || !Number.isFinite(data.expiresAt) || typeof data.scope !== 'string') return;
        if (remote && (!scope || data.scope !== scope)) return;
        if (remote && data.expiresAt <= expires) return;
        scope = data.scope;
        if (!remote) offset = data.serverNow - Date.now();
        expires = data.expiresAt;
        if (expires - now() > 120000 && warning) { warning = false; modal.hide(); }
        if (!remote) publish(data);
    }
    if (channel) channel.onmessage = event => accept(event.data, true);
    window.addEventListener('storage', event => {
        if (event.key === 'edusmart-idle-sync' && event.newValue) {
            try { accept(JSON.parse(event.newValue), true); } catch (_) { /* Ignore malformed messages. */ }
        }
    });
    async function request(renew) {
        if (busy) return;
        busy = true;
        lastStatus = Date.now();
        if (renew) button.disabled = true;
        try {
            const response = await fetch(renew ? root.dataset.activityUrl : root.dataset.statusUrl, {
                method: renew ? 'POST' : 'GET', credentials: 'same-origin', cache: 'no-store',
                headers: { 'X-Requested-With': 'XMLHttpRequest', ...(renew ? { RequestVerificationToken: token } : {}) },
                signal: AbortSignal.timeout(10000), keepalive: renew
            });
            if (response.status === 401) { window.location.replace(root.dataset.loginUrl); return; }
            // A redirect can come from a business rule, not only authentication.
            // Never navigate away or resend credentials to a redirected endpoint.
            if (response.redirected || !response.ok) throw new Error('Session verification failed');
            accept(await response.json());
            error.hidden = true;
            lastStatus = Date.now();
            if (renew) { lastSent = Date.now(); dirty = false; }
        } catch (_) { if (warning) error.hidden = false; }
        finally { busy = false; button.disabled = false; }
    }
    function activity(event) {
        if (!event.isTrusted || warning || !expires || now() >= expires) return;
        dirty = true;
        if (Date.now() - lastSent >= 45000) void request(true);
    }
    // Moving the pointer, focus changes and automatic polling do not count.
    ['pointerdown', 'keydown', 'input'].forEach(name => document.addEventListener(name, activity, { passive: true }));
    button.addEventListener('click', () => { void request(true); });
    modalElement.addEventListener('shown.bs.modal', () => button.focus());
    function videoPlaying() {
        let playing = false;
        document.querySelectorAll('video[data-video-player]').forEach(video => {
            const previous = videoTimes.get(video);
            videoTimes.set(video, video.currentTime);
            if (!video.paused && !video.ended && !video.seeking && video.readyState >= 2 &&
                previous !== undefined && video.currentTime > previous && video.currentTime - previous < 5) playing = true;
        });
        return playing;
    }
    setInterval(() => {
        if (!expires) { if (!busy && Date.now() - lastStatus >= 15000) void request(false); return; }
        if (!warning && videoPlaying()) dirty = true;
        if (!warning && dirty && Date.now() - lastSent >= 45000 && now() < expires) void request(true);
        const remaining = expires - now();
        if (remaining <= 120000 && !warning) { warning = true; dirty = false; modal.show(); }
        if (warning) {
            const seconds = Math.max(0, Math.ceil(remaining / 1000));
            countdown.textContent = `${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`;
        }
        // Ask the server before redirecting: another tab may have renewed it.
        if (remaining <= 0 && !checkingExpiry && !busy && Date.now() - lastStatus >= 1000) {
            checkingExpiry = true;
            void request(false).finally(() => { checkingExpiry = false; });
        } else if (Date.now() - lastStatus >= 15000 && !busy) void request(false);
    }, 1000);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) void request(false); });
    void request(false);
})();
