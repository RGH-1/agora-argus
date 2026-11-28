// Live updates (AA-40): one SignalR connection per open Argus page. The layout loads SignalR only for logged-in users.
if (window.signalR) {
    const connection = new signalR.HubConnectionBuilder().withUrl("/hubs/status").withAutomaticReconnect().build();

    // The status board listens for this and reloads its cards; other pages ignore it.
    const boardChanged = () => document.dispatchEvent(new Event("argus:board-changed"));

    connection.on("BoardChanged", boardChanged);
    connection.on("StatusChanged", change => {
        boardChanged();
        showAlert(change);
    });
    connection.onreconnected(boardChanged); // changes may have been missed while disconnected
    connection.start().catch(error => console.warn("Live updates are off:", error));
}

/** A toast like "Agora is Down: No heartbeat for over 30 s · Incident #3". Down and Degraded stay until closed or replaced. */
function showAlert(change) {
    // The newest alert of a client replaces its older one, so a recovered client doesn't leave a red alert behind.
    for (const old of document.querySelectorAll(`#alerts [data-client="${CSS.escape(change.clientId)}"]`))
        bootstrap.Toast.getOrCreateInstance(old).hide();

    const colors = { Healthy: "success", Degraded: "warning", Down: "danger", Stopped: "secondary" };
    const toast = document.createElement("div");
    toast.dataset.client = change.clientId;
    toast.className = `toast text-bg-${colors[change.status] ?? "light"}`;
    toast.setAttribute("role", "alert");

    const body = document.createElement("div");
    body.className = "toast-body d-flex gap-2 align-items-start";
    const text = document.createElement("span");
    text.textContent = `${change.name} is ${change.status}: ${change.reason}`; // textContent: the reason comes from a client app
    body.append(text);

    if (change.incidentId) {
        const link = document.createElement("a");
        link.href = `/Incidents/Details/${change.incidentId}`;
        link.className = "text-reset fw-semibold text-nowrap ms-auto";
        link.textContent = change.incidentResolved ? `Incident #${change.incidentId} resolved` : `Incident #${change.incidentId}`;
        body.append(link);
    }

    const close = document.createElement("button");
    close.type = "button";
    close.className = "btn-close";
    close.dataset.bsDismiss = "toast";
    close.setAttribute("aria-label", "Close");
    body.append(close);

    toast.append(body);
    document.getElementById("alerts").append(toast);
    toast.addEventListener("hidden.bs.toast", () => toast.remove());
    new bootstrap.Toast(toast, { autohide: change.status !== "Down" && change.status !== "Degraded" }).show();
}