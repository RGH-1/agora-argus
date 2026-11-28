(() => {
  const banner = document.getElementById('maintenance-banner');
  if (!banner || !window.signalR) return;
  const text = banner.querySelector('[data-maintenance-message]');
  let connection;
  const render = (status) => {
    if (!status || !status.enabled) { banner.hidden = true; return; }
    banner.hidden = false;
    text.textContent = status.displayMessage || status.message || 'Agora is currently under maintenance. Some actions are temporarily unavailable.';
  };
  const refresh = async () => { try { const response = await fetch('/Maintenance/Status', { credentials: 'same-origin', cache: 'no-store' }); if (response.ok) render(await response.json()); } catch { /* server-rendered banner remains authoritative */ } };
  const start = async () => {
    connection = new signalR.HubConnectionBuilder().withUrl('/hubs/maintenance').withAutomaticReconnect().build();
    connection.on('MaintenanceStatusChanged', render);
    connection.onreconnected(refresh);
    try { await connection.start(); await refresh(); } catch { window.setTimeout(start, 3000); }
  };
  start();
})();
