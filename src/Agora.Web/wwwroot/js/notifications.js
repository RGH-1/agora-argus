(function () {
    if (!window.signalR || !document.body.dataset.authenticated) return;
    const container = document.getElementById('notification-toasts');
    const show = notification => {
        const toast = document.createElement('div');
        toast.className = 'toast align-items-center border-0 text-bg-' + (notification.type.includes('Approved') ? 'success' : notification.type.includes('Rejected') || notification.type.includes('Removed') || notification.type.includes('Banned') ? 'danger' : 'warning');
        toast.setAttribute('role', 'status');
        const body = document.createElement('div'); body.className = 'd-flex';
        const text = document.createElement(notification.url ? 'a' : 'div'); text.className = 'toast-body text-white'; text.textContent = notification.message;
        if (notification.url) { text.href = notification.url; text.classList.add('text-white'); }
        const close = document.createElement('button'); close.type = 'button'; close.className = 'btn-close btn-close-white me-2 m-auto'; close.dataset.bsDismiss = 'toast'; close.ariaLabel = 'Close';
        body.append(text, close); toast.append(body); container.append(toast); new bootstrap.Toast(toast, { delay: 7000 }).show();
        toast.addEventListener('hidden.bs.toast', () => toast.remove());
    };
    let refreshSequence = 0;
    let myForumsSequence = 0;
    const refreshQueue = queue => {
        const target = document.querySelector('[data-moderation-queue="' + queue + '"]');
        if (!target) return;
        const sequence = ++refreshSequence;
        fetch(window.location.href, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin' }).then(response => {
            if (!response.ok) throw new Error('Queue refresh failed: ' + response.status); return response.text();
        }).then(html => {
            if (sequence !== refreshSequence) return;
            const parsed = new DOMParser().parseFromString(html, 'text/html'); const next = parsed.querySelector('[data-moderation-queue="' + queue + '"]'); if (next && target.isConnected) target.replaceWith(next);
        }).catch(error => console.warn('Could not refresh moderation queue', error));
    };
    const refreshMyForums = () => {
        const target = document.getElementById('my-forums-list');
        if (!target) return;
        const sequence = ++myForumsSequence;
        fetch('/Forums/MineList', { credentials: 'same-origin' }).then(response => {
            if (!response.ok) throw new Error('My Forums refresh failed: ' + response.status); return response.text();
        }).then(html => { if (sequence === myForumsSequence && target.isConnected) target.innerHTML = html; })
            .catch(error => console.warn('Could not refresh My Forums', error));
    };
    const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/notifications').withAutomaticReconnect().build();
    connection.on('ReceiveNotification', notification => { if (notification.type === 'ModerationQueueChanged') refreshQueue(notification.message); else { show(notification); if (notification.type === 'PendingForumCreated') refreshQueue('forums'); if (notification.type === 'PendingReportCreated') refreshQueue('reports'); if (notification.type === 'ForumApproved' || notification.type === 'ForumRejected') refreshMyForums(); } });
    connection.onreconnected(() => { refreshQueue('forums'); refreshQueue('reports'); });
    let stopped = false; let starting = false; let retry = 0;
    const start = async () => {
        if (stopped || starting || connection.state !== signalR.HubConnectionState.Disconnected) return;
        starting = true;
        try { await connection.start(); retry = 0; }
        catch (error) {
            console.warn('Realtime notifications unavailable', error);
            if (!stopped && error?.statusCode !== 401 && error?.statusCode !== 403) {
                const delay = Math.min(30000, 1000 * Math.pow(2, retry++)); window.setTimeout(start, delay);
            }
        } finally { starting = false; }
    };
    window.addEventListener('pagehide', () => { stopped = true; connection.stop(); }, { once: true });
    start();
}());
