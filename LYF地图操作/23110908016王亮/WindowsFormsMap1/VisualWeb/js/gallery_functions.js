/**
 * [New] Image Failover & Sniffing Logic
 * Tries next available path if current one fails.
 */
function handleImageError(img) {
    // Check if we have path data
    if (!img.dataset.paths) {
        console.warn('[Image] No path data for image, hiding');
        img.style.display = 'none';
        return;
    }

    const paths = JSON.parse(img.dataset.paths || "[]");
    let currentIdx = parseInt(img.dataset.current || "0");

    console.log(`[Image] Failed to load: ${paths[currentIdx]}`);

    // Try the next path in the queue
    if (currentIdx + 1 < paths.length) {
        currentIdx++;
        img.dataset.current = currentIdx;
        img.src = paths[currentIdx];
        console.log(`[Image] Trying next path: ${paths[currentIdx]}`);

        // If we found a working image and there are more potential ones, 
        // enable the gallery navigation
        const nav = document.getElementById('gallery-nav');
        if (nav && currentIdx >= 1) nav.style.display = 'flex';
    } else {
        // No more images to try, show placeholder color or hide
        console.warn(`[Image] All ${paths.length} paths failed. Showing gradient fallback.`);
        img.style.display = 'none';
    }
}

/**
 * [New] Gallery Navigation
 */
function shiftGallery(btn, direction) {
    const gallery = btn.closest('#card-media-gallery');
    const img = gallery.querySelector('.gallery-img');
    if (!img || !img.dataset.paths) return;

    const paths = JSON.parse(img.dataset.paths || "[]");
    let currentIdx = parseInt(img.dataset.current || "0");

    currentIdx = (currentIdx + direction + paths.length) % paths.length;

    // Switch image with a simple fade
    img.style.opacity = '0';
    setTimeout(() => {
        img.src = paths[currentIdx];
        img.dataset.current = currentIdx;
        img.onload = () => img.style.opacity = '1';
    }, 200);
}
