// [Member E] Added: Tab switching function for card detail
function switchCardTab(tabName, itemName) {
    const nameId = itemName.replace(/\s/g, '-');

    // Switch tab buttons
    const tabButtons = document.querySelectorAll('.tab-switcher .tab-btn');
    tabButtons.forEach(btn => {
        if (btn.dataset.tab === tabName) {
            btn.classList.add('active');
            btn.style.background = 'rgba(99, 102, 241, 0.8)';
            btn.style.color = '#fff';
        } else {
            btn.classList.remove('active');
            btn.style.background = 'rgba(255,255,255,0.1)';
            btn.style.color = 'rgba(255,255,255,0.7)';
        }
    });

    // Switch tab content
    const infoTab = document.getElementById(`tab-info-${nameId}`);
    const interactTab = document.getElementById(`tab-interact-${nameId}`);

    if (tabName === 'info') {
        if (infoTab) {
            infoTab.style.display = 'block';
            infoTab.classList.add('active');
        }
        if (interactTab) {
            interactTab.style.display = 'none';
            interactTab.classList.remove('active');
        }
    } else if (tabName === 'interact') {
        if (infoTab) {
            infoTab.style.display = 'none';
            infoTab.classList.remove('active');
        }
        if (interactTab) {
            interactTab.style.display = 'block';
            interactTab.classList.add('active');
        }
    }
}
