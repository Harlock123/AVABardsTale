// Looping background music for the WebAssembly head. C# synthesizes each track as WAV
// bytes (base64); we decode once per track, cache it, and play it on a looping buffer
// source through a gain node. Only one track plays at a time.

let ctx = null;
let current = null; // { source, gain, key }
const buffers = {};

function b64ToArrayBuffer(b64) {
    const bin = atob(b64);
    const bytes = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
    return bytes.buffer;
}

export async function playMusic(key, b64, volume) {
    try {
        ctx ??= new (window.AudioContext || window.webkitAudioContext)();
        if (ctx.state === 'suspended') await ctx.resume();

        // Already playing this track — just adjust the volume.
        if (current && current.key === key) {
            current.gain.gain.value = volume;
            return;
        }
        stopMusic();

        let buf = buffers[key];
        if (!buf) {
            buf = await ctx.decodeAudioData(b64ToArrayBuffer(b64));
            buffers[key] = buf;
        }

        const source = ctx.createBufferSource();
        source.buffer = buf;
        source.loop = true; // seamless native loop
        const gain = ctx.createGain();
        gain.gain.value = volume;
        source.connect(gain);
        gain.connect(ctx.destination);
        source.start();
        current = { source, gain, key };
    } catch {
        // Autoplay restrictions or decode failure — music is non-essential.
    }
}

export function stopMusic() {
    try {
        if (current) {
            current.source.stop();
            current.source.disconnect();
            current.gain.disconnect();
        }
    } catch {
        // already stopped
    }
    current = null;
}

export function setMusicVolume(volume) {
    try {
        if (current) current.gain.gain.value = volume;
    } catch {
        // ignore
    }
}
