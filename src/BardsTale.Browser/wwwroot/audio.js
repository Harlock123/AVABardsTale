// Web Audio playback for the WebAssembly head. C# synthesizes each sound as WAV bytes
// (base64) and calls play(); we decode once per key, cache it, and play with a gain node.

let ctx = null;
const buffers = {};

function b64ToArrayBuffer(b64) {
    const bin = atob(b64);
    const bytes = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
    return bytes.buffer;
}

export async function play(key, b64, volume) {
    try {
        ctx ??= new (window.AudioContext || window.webkitAudioContext)();
        if (ctx.state === 'suspended') await ctx.resume();

        let buf = buffers[key];
        if (!buf) {
            buf = await ctx.decodeAudioData(b64ToArrayBuffer(b64));
            buffers[key] = buf;
        }

        const src = ctx.createBufferSource();
        src.buffer = buf;
        const gain = ctx.createGain();
        gain.gain.value = volume;
        src.connect(gain);
        gain.connect(ctx.destination);
        src.start();
    } catch {
        // Audio is non-essential; ignore failures (e.g. autoplay restrictions).
    }
}
