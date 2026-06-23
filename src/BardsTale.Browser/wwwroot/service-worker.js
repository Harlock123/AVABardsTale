// Service worker for offline play. Uses a cache-first strategy that populates the
// cache as files are fetched, so after the first online load the whole app
// (including the hashed _framework/*.wasm runtime files) is available offline.
// Bump CACHE_VERSION to force clients to refetch after a deploy.

const CACHE_VERSION = 'bardstale-v2';
const APP_SHELL = [
    './',
    './index.html',
    './main.js',
    './saveStore.js',
    './manifest.webmanifest',
    './icon.svg',
    './icon-192.png',
    './icon-512.png',
    './icon-maskable-512.png'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_VERSION)
            .then(cache => cache.addAll(APP_SHELL))
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(k => k !== CACHE_VERSION).map(k => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') return;
    event.respondWith(
        caches.match(event.request).then(cached => {
            if (cached) return cached;
            return fetch(event.request).then(response => {
                if (response && response.ok && response.type === 'basic') {
                    const copy = response.clone();
                    caches.open(CACHE_VERSION).then(cache => cache.put(event.request, copy));
                }
                return response;
            }).catch(() => cached);
        })
    );
});
