// IndexedDB-backed save storage for the WebAssembly head. Exposes a tiny
// promise-based API consumed from C# via [JSImport] (see IndexedDbSaveStore.cs).
// Save data persists across reloads/sessions, scoped to this browser + origin.

const DB_NAME = 'bardstale';
const STORE = 'saves';

function openDb() {
    return new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, 1);
        req.onupgradeneeded = () => req.result.createObjectStore(STORE);
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    });
}

export async function get(key) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const r = db.transaction(STORE, 'readonly').objectStore(STORE).get(key);
        r.onsuccess = () => resolve(r.result ?? null);
        r.onerror = () => reject(r.error);
    });
}

export async function set(key, value) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t = db.transaction(STORE, 'readwrite');
        t.objectStore(STORE).put(value, key);
        t.oncomplete = () => resolve();
        t.onerror = () => reject(t.error);
    });
}

export async function remove(key) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t = db.transaction(STORE, 'readwrite');
        t.objectStore(STORE).delete(key);
        t.oncomplete = () => resolve();
        t.onerror = () => reject(t.error);
    });
}

// Ask the browser to make this origin's storage durable (resists eviction,
// notably Safari's 7-day purge of script-writable storage). Best-effort.
export async function requestPersist() {
    if (navigator.storage && navigator.storage.persist) {
        try { return await navigator.storage.persist(); } catch { return false; }
    }
    return false;
}
