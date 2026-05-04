// LOTV service worker — minimal: passes through, handles push.

self.addEventListener('install', (e) => { self.skipWaiting(); });
self.addEventListener('activate', (e) => { e.waitUntil(self.clients.claim()); });

// Receive a push from the server (server uses Web Push protocol; payload is JSON).
self.addEventListener('push', (event) => {
  let data = {};
  try { data = event.data ? event.data.json() : {}; } catch { data = { title: 'LOTV', body: event.data && event.data.text() }; }
  const title = data.title || 'LOTV Ministry';
  const options = {
    body:  data.body  || '',
    icon:  '/favicon.png',
    badge: '/favicon.png',
    data:  { url: data.url || '/' }
  };
  event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const url = (event.notification.data && event.notification.data.url) || '/';
  event.waitUntil(clients.openWindow(url));
});
