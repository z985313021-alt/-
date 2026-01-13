const STATE = {
    dataLoaded: false
};

let chart = null;
let categoryChart = null;
let mapPoints = [];
let allData = null;
let bridge = null;

const COLORS = ['#3b82f6', '#8b5cf6', '#ec4899', '#f59e0b', '#10b981'];

// Initialize layout and charts
document.addEventListener('DOMContentLoaded', async () => {
    // 1. Initialize Charts First
    await initCharts();

    // 2. Initialize Interactions
    initInteractions();
    initActionEvents();

    // 3. Initialize JS-C# Bridge and Data
    if (window.chrome && window.chrome.webview) {
        bridge = window.chrome.webview.hostObjects.bridge;

        // Listen for C# push messages
        window.chrome.webview.addEventListener('message', (event) => {
            console.log("Received data from C#");
            console.log("Raw event.data:", event.data);

            try {
                const data = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;

                // Debug: show first 200 chars of received data
                const preview = JSON.stringify(data).substring(0, 200);
                alert("接收到数据预览:\n" + preview + "\n\n总数据点: " + (data.points ? data.points.length : 0));

                renderDashboard(data);
            } catch (err) {
                console.error("Parse error:", err);
                console.error("Received data:", event.data);
                alert("数据解析错误: " + err.message + "\n\n收到的数据: " + (typeof event.data === 'string' ? event.data.substring(0, 200) : JSON.stringify(event.data).substring(0, 200)));
            }
        });
    } else {
        // Fallback to JSON file in standalone mode
        setTimeout(() => { if (!allData) loadFallbackData(); }, 1500);
    }
});

async function initCharts() {
    // Register Shandong Map from injected data
    try {
        // Wait for map data to be injected by C#
        let attempts = 0;
        while (!window.SHANDONG_MAP_DATA && attempts < 50) {
            await new Promise(resolve => setTimeout(resolve, 100));
            attempts++;
        }

        if (window.SHANDONG_MAP_DATA) {
            echarts.registerMap('shandong', window.SHANDONG_MAP_DATA);
            console.log("Shandong map registered successfully");
        } else {
            throw new Error("Map data not injected by C#");
        }
    } catch (e) {
        console.error("Failed to load map data", e);
        alert("地图数据加载失败: " + e.message);
        return; // Exit early if map can't load
    }

    const mapDom = document.getElementById('visual-map');
    chart = echarts.init(mapDom);

    const catDom = document.getElementById('category-chart');
    categoryChart = echarts.init(catDom);

    window.addEventListener('resize', () => {
        chart.resize();
        categoryChart.resize();
    });
}

function initInteractions() {
    document.querySelectorAll('.nav-item').forEach(item => {
        item.addEventListener('click', () => {
            document.querySelector('.nav-item.active').classList.remove('active');
            item.classList.add('active');
        });
    });

    document.getElementById('btn-zoom-in').onclick = () => {
        const opt = chart.getOption();
        chart.setOption({ geo: { zoom: (opt.geo[0].zoom || 1) * 1.5 } });
    };

    document.getElementById('btn-zoom-out').onclick = () => {
        const opt = chart.getOption();
        chart.setOption({ geo: { zoom: (opt.geo[0].zoom || 1) / 1.5 } });
    };

    document.getElementById('btn-reset').onclick = () => {
        chart.setOption({ geo: { zoom: 1, center: [118.5, 36.4] } });
    };
}

function renderDashboard(data) {
    console.log("Rendering dashboard with data:", data);

    if (!data || (!data.projectInfo && !data.points)) {
        alert("接收到的数据为空或格式错误");
        return;
    }

    allData = data;

    // Update total count
    if (data.projectInfo && data.projectInfo.totalItems) {
        document.getElementById('total-count').innerText = data.projectInfo.totalItems.toLocaleString();
    }

    // Update City List
    if (data.statsByCity && data.statsByCity.length > 0) {
        const cityList = document.getElementById('city-list');
        cityList.innerHTML = '';
        data.statsByCity.sort((a, b) => b.value - a.value).slice(0, 8).forEach(city => {
            const div = document.createElement('div');
            div.className = 'stats-item';
            div.innerHTML = `<span>${city.name}</span><span style="color:var(--accent-blue)">${city.value}</span>`;
            cityList.appendChild(div);
        });
    }

    // Render Charts
    if (data.categories && data.categories.length > 0) {
        renderCategoryChart(data.categories);
    }

    mapPoints = data.points || [];
    renderMap(mapPoints);
}

function renderCategoryChart(categories) {
    categoryChart.setOption({
        tooltip: { trigger: 'item', backgroundColor: 'rgba(255,255,255,0.9)', borderRadius: 12 },
        series: [{
            type: 'pie',
            radius: ['50%', '80%'],
            itemStyle: { borderRadius: 10, borderColor: '#fff', borderWidth: 4 },
            label: { show: false },
            data: categories.map((c, i) => ({
                name: c.name,
                value: c.count,
                itemStyle: { color: COLORS[i % COLORS.length] }
            }))
        }]
    });
}

function renderMap(points) {
    console.log("Rendering map with points:", points.length);

    chart.setOption({
        backgroundColor: 'transparent',
        tooltip: {
            show: true,
            trigger: 'item',
            formatter: (p) => p.data ? `<div style="padding:10px"><b>${p.data.name}</b><br/>${p.data.value[2]}</div>` : '',
            backgroundColor: 'rgba(255,255,255,0.95)',
            borderRadius: 12,
            borderWidth: 0,
            shadowBlur: 20,
            shadowColor: 'rgba(0,0,0,0.2)',
            textStyle: {
                color: '#333'
            }
        },
        geo: {
            map: 'shandong',
            roam: true,
            center: [118.5, 36.4],
            zoom: 1.1,
            itemStyle: {
                // 更亮的渐变色底图
                areaColor: {
                    type: 'linear',
                    x: 0,
                    y: 0,
                    x2: 1,
                    y2: 1,
                    colorStops: [
                        { offset: 0, color: 'rgba(102, 126, 234, 0.15)' },
                        { offset: 1, color: 'rgba(118, 75, 162, 0.15)' }
                    ]
                },
                borderColor: 'rgba(102, 126, 234, 0.6)',
                borderWidth: 2,
                shadowColor: 'rgba(102, 126, 234, 0.3)',
                shadowBlur: 10
            },
            emphasis: {
                itemStyle: {
                    areaColor: 'rgba(102, 126, 234, 0.3)',
                    borderColor: '#667eea',
                    borderWidth: 3,
                    shadowBlur: 20,
                    shadowColor: 'rgba(102, 126, 234, 0.6)'
                },
                label: { show: false }
            }
        },
        series: [{
            type: 'scatter',
            coordinateSystem: 'geo',
            data: points.map(p => ({
                name: p.name,
                value: [p.x, p.y, p.category, p.city]
            })),
            symbolSize: 12,
            itemStyle: {
                color: {
                    type: 'radial',
                    x: 0.5,
                    y: 0.5,
                    r: 0.5,
                    colorStops: [
                        { offset: 0, color: '#00d2ff' },
                        { offset: 1, color: '#3b82f6' }
                    ]
                },
                shadowBlur: 15,
                shadowColor: 'rgba(59, 130, 246, 0.8)',
                borderColor: 'rgba(255,255,255,0.3)',
                borderWidth: 1
            },
            emphasis: {
                itemStyle: {
                    scale: 1.8,
                    color: {
                        type: 'radial',
                        x: 0.5,
                        y: 0.5,
                        r: 0.5,
                        colorStops: [
                            { offset: 0, color: '#ff6b6b' },
                            { offset: 1, color: '#ee5a6f' }
                        ]
                    },
                    shadowBlur: 25,
                    shadowColor: 'rgba(255, 107, 107, 0.9)'
                }
            }
        }]
    });

    chart.on('click', 'series', (params) => {
        showDetail(params.name, params.value[2], params.value[3]);
    });

    // [New] Listen for map region clicks (e.g., clicking a city)
    chart.on('click', 'geo', (params) => {
        console.log('Clicked on region:', params.name);
        showCityCards(params.name);
    });
}


function showDetail(name, cat, city) {
    // Hide old right sidebar detail
    document.getElementById('detail-placeholder').style.display = 'block';
    document.getElementById('detail-card').style.display = 'none';

    // Show center card overlay
    const overlay = document.getElementById('card-overlay');
    const container = document.getElementById('card-container');

    // Generate a consistent hue
    const hue = (name.length * 37) % 360;
    const gradientColors = `linear-gradient(135deg, hsl(${hue}, 60%, 60%) 0%, hsl(${hue + 40}, 60%, 40%) 100%)`;

    // Create card HTML with side panel structure, wrapped in center container
    container.innerHTML = `
        <div class="card-detail-center">
            <div class="ich-card" onclick="handleCardClick(this)">
                <div class="card-image-full" style="background: ${gradientColors}">
                    <img src="images/${encodeURIComponent(name)}.jpg" 
                         alt="${name}"
                         style="width: 100%; height: 100%; object-fit: cover; transition: transform 0.6s ease;"
                         onerror="this.style.display='none'">
                    
                    <div class="card-gradient-overlay"></div>
                    <div class="card-title-overlay">
                        <h2>${name}</h2>
                    </div>
                </div>
                
                <!-- 第一次点击展开的内容 -->
                <div class="card-expanded-content" onclick="event.stopPropagation()">
                    <div class="card-meta">
                        <span class="card-tag category">${cat}</span>
                        <span class="card-tag city">${city}</span>
                    </div>
                    <p class="card-desc">
                        该非物质文化遗产项目属于<strong>${cat}</strong>类别，位于<strong>${city}</strong>。
                        点击“查看档案”或再次点击卡片可查看完整侧边详情。
                    </p>
                    <div class="card-actions">
                        <button class="card-btn card-btn-primary" onclick="toggleSideInfo(this)">
                            📂 查看档案
                        </button>
                        <button class="card-btn card-btn-secondary" onclick="event.stopPropagation(); likeItem('${name}')">
                            ❤️ 点赞
                        </button>
                        <button class="card-btn card-btn-secondary" onclick="event.stopPropagation(); showComments('${name}')">
                            💬 评论
                        </button>
                    </div>
                </div>

                <!-- 第二次点击/侧滑展开的详细内容 -->
                <div class="card-side-panel" onclick="event.stopPropagation()">
                    <div class="side-panel-header">
                        <div class="side-panel-title">${name}</div>
                        <div class="card-meta">
                            <span class="card-tag category">${cat}</span>
                            <span class="card-tag city">${city}</span>
                        </div>
                    </div>
                    
                    <div class="side-panel-info-row">
                        <i>📅</i> <strong>申报日期：</strong> 2006年
                    </div>
                    <div class="side-panel-info-row">
                        <i>🔢</i> <strong>项目编号：</strong> VIII-${Math.floor(Math.random() * 1000)}
                    </div>
                    <div class="side-panel-info-row">
                        <i>📍</i> <strong>保护单位：</strong> ${city}非遗保护中心
                    </div>

                    <div class="side-panel-desc">
                        <h3>项目简介</h3>
                        <p>这里将显示关于${name}的详细数据库记录。目前为模拟数据，后续将连接数据库展示完整的历史沿革、技艺特征、传承人信息等内容。</p>
                        <br>
                        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.</p>
                    </div>

                    <button class="btn-back-to-expand" onclick="closeSideInfo(this)">
                        ⬅️ 返回概览
                    </button>
                </div>
            </div>
        </div>
    `;

    overlay.style.display = 'flex';

    if (bridge) {
        loadComments(name);
    }
}

// [New] Show City Cards (Slay the Spire style hand)
function showCityCards(cityName) {
    // Filter all ICH items from this city
    const cityDisplayName = cityName.replace('市', '');
    const cityItems = mapPoints.filter(p =>
        p.city && (p.city.includes(cityDisplayName) || cityDisplayName.includes(p.city))
    );

    if (!cityItems || cityItems.length === 0) {
        console.log('No items found for city:', cityName);
        return;
    }

    const overlay = document.getElementById('card-overlay');
    const container = document.getElementById('card-container');

    // Create card hand
    const handHtml = `
        <div class="card-hand" id="card-hand">
            ${cityItems.map((item, index) => {
        const hue = (item.name.length * 37) % 360;
        const gradient = `linear-gradient(135deg, hsl(${hue}, 60%, 60%) 0%, hsl(${hue + 40}, 60%, 40%) 100%)`;

        return `
                    <div class="hand-card" 
                         data-index="${index}"
                         data-name="${item.name}"
                         data-category="${item.category}"
                         data-city="${item.city}"
                         onclick="handleHandCardClick(this)">
                        <div class="hand-card-image" style="background: ${gradient}">
                            <img src="images/${encodeURIComponent(item.name)}.jpg" 
                                 alt="${item.name}"
                                 onerror="this.style.display='none'">
                            <div class="hand-card-overlay"></div>
                            <div class="hand-card-title">${item.name}</div>
                        </div>
                    </div>
                `;
    }).join('')}
        </div>
    `;

    container.innerHTML = handHtml;
    overlay.style.display = 'flex';

    // Position cards with overlap effect
    setTimeout(() => {
        const cards = document.querySelectorAll('.hand-card');
        const totalCards = cards.length;
        const cardWidth = 200;
        const overlapSpacing = 80; // Cards overlap, only show 80px of each card
        const totalWidth = (totalCards - 1) * overlapSpacing + cardWidth;

        // Calculate center position relative to card-hand container
        const hand = document.getElementById('card-hand');
        const handWidth = hand.offsetWidth;
        const startX = (handWidth - totalWidth) / 2;

        cards.forEach((card, i) => {
            card.style.left = (startX + i * overlapSpacing) + 'px';
            card.style.zIndex = i + 1;
        });
    }, 50);
}

// [New] Handle hand card click
let selectedCard = null;

function handleHandCardClick(cardElement) {
    // First click: select card (move to center of hand, raise up)
    if (!cardElement.classList.contains('selected')) {
        // Deselect all other cards
        document.querySelectorAll('.hand-card.selected').forEach(c => {
            c.classList.remove('selected');
        });

        cardElement.classList.add('selected');
        selectedCard = cardElement;

        // Move to center position of the hand
        const hand = document.getElementById('card-hand');
        const handWidth = hand.offsetWidth;
        const centerX = (handWidth - 200) / 2; // 200 is card width
        cardElement.style.left = centerX + 'px';
    }
    // Second click: expand to full screen detail (center of screen)
    else {
        const name = cardElement.dataset.name;
        const category = cardElement.dataset.category;
        const city = cardElement.dataset.city;

        // Clear hand and show full detail in screen center
        showDetail(name, category, city);
        selectedCard = null;
    }
}


// Close card overlay
document.getElementById('btn-close-cards')?.addEventListener('click', () => {
    document.getElementById('card-overlay').style.display = 'none';
    document.getElementById('detail-placeholder').style.display = 'block';
});

// Card Click State Machine
function handleCardClick(card) {
    if (card.classList.contains('side-open')) {
        // Option: Click anywhere on main card to close side panel?
        // closeSideInfo(card);
    } else if (card.classList.contains('expanded')) {
        // Second click: Open side panel
        card.classList.add('side-open');
    } else {
        // First click: Expand bottom
        card.classList.add('expanded');
    }
}

// Helper: Open Side Info
function toggleSideInfo(btn) {
    event.stopPropagation();
    const card = btn.closest('.ich-card');
    card.classList.add('side-open');
}

// Helper: Close Side Info (Return to expanded)
function closeSideInfo(btn) {
    event.stopPropagation();
    const card = btn.tagName ? btn.closest('.ich-card') : btn;
    card.classList.remove('side-open');
}


// Like function
function likeItem(name) {
    console.log('Liked:', name);
    // TODO: Call bridge to record like
}

async function loadComments(itemName) {
    if (!bridge) return;
    try {
        const json = await bridge.GetComments(itemName);
        const comments = JSON.parse(json);
        const list = document.getElementById('comment-list');
        list.innerHTML = '';
        comments.forEach(c => {
            const d = document.createElement('div');
            d.style.marginBottom = '5px';
            d.innerHTML = `<b style="font-size:0.8em">${c.date}</b>: ${c.text}`;
            list.appendChild(d);
        });
    } catch (e) {
        console.error("Load Comments Error", e);
    }
}

function initActionEvents() {
    // Like Button
    document.getElementById('btn-like').onclick = async () => {
        const name = document.getElementById('detail-title').innerText;
        if (bridge) {
            const success = await bridge.AddLike(name);
            if (success) {
                alert('点赞成功！');
            }
        } else {
            alert('模拟环境无法点赞');
        }
    };

    // Comment Button
    document.getElementById('btn-comment').onclick = async () => {
        const name = document.getElementById('detail-title').innerText;
        const input = document.getElementById('input-comment');
        const text = input.value.trim();
        if (!text) return;

        if (bridge) {
            const success = await bridge.AddComment(name, text);
            if (success) {
                input.value = '';
                loadComments(name);
            }
        }
    };
}

async function loadFallbackData() {
    try {
        const res = await fetch('data/data.json');
        renderDashboard(await res.json());
    } catch (e) {
        console.error("Failed to load fallback data", e);
    }
}
