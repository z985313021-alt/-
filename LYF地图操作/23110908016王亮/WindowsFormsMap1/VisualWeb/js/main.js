const STATE = {
    dataLoaded: false
};

let chart = null;
let categoryChart = null;
let mapPoints = [];
let allData = null;
let bridge = null;
let currentMode = 'point'; // 'point', 'choropleth', 'heatmap'

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
        // Wait for bridge objects to be ready
        bridge = window.chrome.webview.hostObjects.bridge;

        // Listen for C# push messages (Keep as fallback/live updates)
        window.chrome.webview.addEventListener('message', (event) => {
            console.log("Received data from C# (Push)");
            handleIncomingData(event.data);
        });

        // [New] Active Pull: Try to get data immediately after registration
        try {
            console.log("Requesting initial data from bridge (Pull)...");
            const rawData = await bridge.GetAllData();
            if (rawData) {
                console.log("Initial data received via pull.");
                handleIncomingData(rawData);
            }
        } catch (err) {
            console.warn("Initial bridge pull failed (maybe not ready yet), waiting for push...", err);
        }
    } else {
        // Fallback to JSON file in standalone mode
        setTimeout(() => { if (!allData) loadFallbackData(); }, 1500);
    }
});

// [New] Centralized data handler to prevent duplication
function handleIncomingData(dataInput) {
    if (STATE.dataLoaded && allData) return; // Prevent double rendering on init

    try {
        const data = typeof dataInput === 'string' ? JSON.parse(dataInput) : dataInput;

        // Debug: log data summary
        console.log("Processing ICH data. Total points:", data.points ? data.points.length : 0);

        // [New] Virtual Batch Allocation for Demo Stability
        if (data.points) {
            data.points.forEach(p => {
                const dbBatch = parseInt(p.batch) || 0;
                // 如果数据库批次是 0 或 1 (代表数据过于集中)，则使用 Hash 分分摊到 1-5
                if (dbBatch <= 1) {
                    let hash = 0;
                    for (let i = 0; i < p.name.length; i++) {
                        hash = ((hash << 5) - hash) + p.name.charCodeAt(i);
                        hash |= 0;
                    }
                    p.vBatch = (Math.abs(hash) % 5) + 1; // 映射到 1, 2, 3, 4, 5
                } else {
                    p.vBatch = dbBatch; // 保留真实批次
                }
            });
        }

        renderDashboard(data);
        STATE.dataLoaded = true;
    } catch (err) {
        console.error("Data parse error:", err);
    }
}

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

    // [New] Initialize Map Click Listeners
    initMapEvents();
}

function initInteractions() {
    document.querySelectorAll('.nav-item').forEach(item => {
        item.addEventListener('click', () => {
            document.querySelector('.nav-item.active').classList.remove('active');
            item.classList.add('active');
        });
    });

    document.getElementById('btn-zoom-in').onclick = () => {
        handleMapAnimationStart();
        const opt = chart.getOption();
        chart.setOption({ geo: [{ zoom: (opt.geo[0].zoom || 1) * 1.5 }] });
    };

    document.getElementById('btn-zoom-out').onclick = () => {
        handleMapAnimationStart();
        const opt = chart.getOption();
        chart.setOption({ geo: [{ zoom: (opt.geo[0].zoom || 1) / 1.5 }] });
    };

    // [New] Analysis Mode Switching
    document.querySelectorAll('.analysis-btn').forEach(btn => {
        btn.onclick = () => {
            const mode = btn.dataset.mode;
            if (currentMode === mode) return;

            document.querySelector('.analysis-btn.active').classList.remove('active');
            btn.classList.add('active');
            currentMode = mode;

            console.log("Switching to analysis mode:", mode);
            renderMap(getFilteredPoints());
        };
    });

    // Reset Button
    const btnReset = document.getElementById('btn-reset');
    if (btnReset) {
        btnReset.onclick = () => {
            handleMapAnimationStart();
            chart.setOption({
                geo: [{ zoom: 1.1, center: [118.5, 36.4] }]
            });
        };
    }
    // Close cards overlay
    const btnClose = document.getElementById('btn-close-cards');
    if (btnClose) {
        btnClose.onclick = () => {
            document.getElementById('card-overlay').style.display = 'none';
        };
    }

    // [New] Time Slider initialization
    initTimeSlider();
}

let currentTimeBatch = 0; // 0 means all batches, 1-5 means cumulative

function initTimeSlider() {
    const slider = document.getElementById('time-slider');
    const display = document.getElementById('current-period');

    if (!slider) return;

    slider.oninput = () => {
        currentTimeBatch = parseInt(slider.value);
        const labels = ["全部批次", "第一批 (2006)", "第二批 (2008)", "第三批 (2011)", "第四批 (2014)", "第五批 (2021)"];
        display.innerText = labels[currentTimeBatch];

        console.log("Time filter changed to batch:", currentTimeBatch);

        // 执行过滤并重新渲染
        const filteredPoints = getFilteredPoints();
        renderMap(filteredPoints);

        // 同步更新侧边栏统计 (使看板也随时间变化)
        updateDashboardStats(filteredPoints);
    };
}

function getFilteredPoints() {
    // 滑块 0: 初始状态，故意隐藏所有点，模拟“从无到有”的震撼感
    if (currentTimeBatch === 0) return [];

    // 累积显示：显示虚拟批次 (vBatch) 在当前选择范围内的所有项目
    return mapPoints.filter(p => p.vBatch <= currentTimeBatch);
}

function updateDashboardStats(filteredPoints) {
    if (!allData) return;

    // 1. 更新总数显示
    const countEl = document.getElementById('total-count');
    if (countEl) countEl.innerText = filteredPoints.length.toLocaleString();

    // 2. 重新计算地市统计并实时更新侧边栏列表
    const cityList = document.getElementById('city-list');
    if (cityList) {
        const dynamicStats = {};
        filteredPoints.forEach(p => {
            dynamicStats[p.city] = (dynamicStats[p.city] || 0) + 1;
        });

        const sortedStats = Object.keys(dynamicStats)
            .map(name => ({ name, value: dynamicStats[name] }))
            .sort((a, b) => b.value - a.value)
            .slice(0, 8);

        cityList.innerHTML = '';
        sortedStats.forEach(city => {
            const div = document.createElement('div');
            div.className = 'stats-item';
            div.innerHTML = `<span>${city.name}</span><span style="color:var(--accent-blue)">${city.value}</span>`;
            cityList.appendChild(div);
        });
    }

    // 3. 更新类别图表 (Pie Chart) 联动时空变化
    if (categoryChart) {
        const catStats = {};
        filteredPoints.forEach(p => {
            catStats[p.category] = (catStats[p.category] || 0) + 1;
        });

        const catData = Object.keys(catStats).map(name => ({
            name: name,
            value: catStats[name]
        }));

        categoryChart.setOption({
            series: [{
                data: catData.map((c, i) => ({
                    name: c.name,
                    value: c.value,
                    itemStyle: { color: COLORS[i % COLORS.length] }
                }))
            }]
        });
    }
}

// [New] 处理地图动画开始，针对热力图进行特殊处理
function handleMapAnimationStart() {
    if (currentMode === 'heatmap') {
        console.log("Zooming... Hiding heatmap data for sync.");
        // 瞬间隐藏热力图层，避免拖影
        chart.setOption({
            series: [{ name: '非遗密度', data: [] }]
        });

        // 延迟刷新 (ECharts 缩放动画默认约 300-500ms)
        if (window.zoomRefreshTimer) clearTimeout(window.zoomRefreshTimer);
        window.zoomRefreshTimer = setTimeout(() => {
            console.log("Zoom finished. Refreshing heatmap.");
            renderMap(getFilteredPoints());
        }, 500);
    }
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
    // 获取当前视野状态，防止刷新时重置缩放和中心点
    const currentOpt = chart.getOption();
    const currentGeo = currentOpt && currentOpt.geo && currentOpt.geo[0];
    const targetCenter = currentGeo ? currentGeo.center : [118.5, 36.4];
    const targetZoom = currentGeo ? currentGeo.zoom : 1.1;

    // [New] 根据当前过滤后的点位实时计算地市统计数据，实现时空联动
    const dynamicCityStats = {};
    points.forEach(p => {
        const cityName = p.city ? p.city.replace('市', '') : '未知';
        dynamicCityStats[cityName] = (dynamicCityStats[cityName] || 0) + 1;
    });
    const cityData = Object.keys(dynamicCityStats).map(name => ({
        name: name,
        value: dynamicCityStats[name]
    }));

    // 基础配置模板
    const option = {
        backgroundColor: 'transparent',
        tooltip: {
            show: true,
            trigger: 'item',
            backgroundColor: 'rgba(255,255,255,0.95)',
            borderRadius: 12,
            borderWidth: 0,
            shadowBlur: 20,
            shadowColor: 'rgba(0,0,0,0.2)',
            textStyle: { color: '#333' }
        },
        geo: {
            map: 'shandong',
            roam: true,
            center: targetCenter,
            zoom: targetZoom,
            itemStyle: {
                areaColor: 'rgba(102, 126, 234, 0.05)',
                borderColor: 'rgba(102, 126, 234, 0.4)',
                borderWidth: 1.5
            },
            emphasis: {
                itemStyle: {
                    areaColor: 'rgba(102, 126, 234, 0.1)',
                    borderColor: '#667eea',
                    borderWidth: 2
                },
                label: { show: false }
            }
        },
        visualMap: null,
        series: [],
        animationDurationUpdate: 300, // 设定较短的动画时间，减少刷新延迟感
        animationEasingUpdate: 'cubicOut'
    };

    // 模式特定配置 (Mode-specific configurations)
    if (currentMode === 'point') {
        // 模式 📍: 点位分布 (Point Distribution)
        option.tooltip.formatter = (p) => {
            if (p.componentType === 'series' && p.data) {
                return `<div style="padding:10px"><b>${p.data.name}</b><br/>${p.data.value[2]}</div>`;
            }
            return '';
        };
        option.series.push({
            name: '项目分布',
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
                    x: 0.5, y: 0.5, r: 0.5,
                    colorStops: [{ offset: 0, color: '#00d2ff' }, { offset: 1, color: '#3b82f6' }]
                },
                shadowBlur: 15,
                shadowColor: 'rgba(59, 130, 246, 0.8)',
                borderColor: 'rgba(255,255,255,0.3)',
                borderWidth: 1
            },
            emphasis: { itemStyle: { scale: 1.8 } }
        });
    }
    else if (currentMode === 'choropleth') {
        // 模式 🗺️: 行政区划热力图 (Choropleth Map)
        // 使用动态计算出的地市数据 (cityData已在头部计算)
        console.log("Rendering Dynamic Choropleth Data:", cityData);

        option.tooltip.formatter = '{b}: {c} 项项目';
        option.visualMap = {
            min: 0,
            max: Math.max(...cityData.map(c => c.value), 10),
            left: 30,
            bottom: 30,
            text: ['高项目量', '低'],
            calculable: true,
            inRange: {
                color: ['#fff7ed', '#fdba74', '#f97316', '#ea580c', '#9a3412'] // 橙红暖色调
            },
            textStyle: { color: '#fff' }
        };

        option.series.push({
            name: '地市非遗分布',
            type: 'map',
            map: 'shandong',
            geoIndex: 0,
            data: cityData
        });
    }
    else if (currentMode === 'heatmap') {
        // 模式 🔥: 密度热力图 (Density Heatmap)
        option.visualMap = {
            min: 0,
            max: 3, // 降低阈值使分布更连贯
            left: 30,
            bottom: 30,
            show: true,
            text: ['高密度', '低'],
            calculable: true,
            inRange: {
                // 由中心向外：透明 -> 蓝 -> 绿 -> 黄 -> 红 (核心为红)
                color: ['rgba(0, 0, 255, 0)', 'rgba(0, 0, 255, 0.4)', 'cyan', 'lime', 'yellow', 'orange', 'red']
            },
            textStyle: { color: '#fff' }
        };

        option.series.push({
            name: '非遗密度',
            type: 'heatmap',
            coordinateSystem: 'geo',
            data: points.map(p => [p.x, p.y, 1]),
            pointSize: 20, // 适度减小点尺寸以增加层次感
            blurSize: 35   // 适度减小模糊半径以增强视觉锐度
        });
    }

    chart.setOption(option, {
        notMerge: true,
        lazyUpdate: false // 强制立即更新，解决缩放不同步问题
    });
}

function showDetail(name, cat, city) {
    // Hide old right sidebar detail if shown
    const detailPlaceholder = document.getElementById('detail-placeholder');
    const detailCard = document.getElementById('detail-card');
    if (detailPlaceholder) detailPlaceholder.style.display = 'block';
    if (detailCard) detailCard.style.display = 'none';

    // Show center card overlay
    const overlay = document.getElementById('card-overlay');
    const container = document.getElementById('card-container');

    // Generate a consistent hue based on name
    const hue = (name.length * 37) % 360;
    const gradientColors = `linear-gradient(135deg, hsl(${hue}, 60%, 60%) 0%, hsl(${hue + 40}, 60%, 40%) 100%)`;

    // Create card HTML with side panel structure
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

    if (overlay) overlay.style.display = 'flex';
    if (bridge) loadComments(name);
}

function showCityCards(cityName) {
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

    setTimeout(() => {
        const cards = document.querySelectorAll('.hand-card');
        const totalCards = cards.length;
        const cardWidth = 200;
        const overlapSpacing = 80;
        const totalWidth = (totalCards - 1) * overlapSpacing + cardWidth;
        const hand = document.getElementById('card-hand');
        if (!hand) return;

        const handWidth = hand.offsetWidth;
        const startX = (handWidth - totalWidth) / 2;

        cards.forEach((card, i) => {
            card.style.left = (startX + i * overlapSpacing) + 'px';
            card.style.zIndex = i + 1;
        });
    }, 50);
}

let selectedCard = null;
function handleHandCardClick(cardElement) {
    if (!cardElement.classList.contains('selected')) {
        document.querySelectorAll('.hand-card.selected').forEach(c => c.classList.remove('selected'));
        cardElement.classList.add('selected');
        selectedCard = cardElement;

        const hand = document.getElementById('card-hand');
        const centerX = (hand.offsetWidth - 200) / 2;
        cardElement.style.left = centerX + 'px';
    } else {
        showDetail(cardElement.dataset.name, cardElement.dataset.category, cardElement.dataset.city);
        selectedCard = null;
    }
}

function handleCardClick(card) {
    if (card.classList.contains('side-open')) {
        // Stay open or handle specific inner clicks
    } else if (card.classList.contains('expanded')) {
        card.classList.add('side-open');
    } else {
        card.classList.add('expanded');
    }
}

function toggleSideInfo(btn) {
    event.stopPropagation();
    btn.closest('.ich-card').classList.add('side-open');
}

function closeSideInfo(btn) {
    event.stopPropagation();
    btn.closest('.ich-card').classList.remove('side-open');
}

function likeItem(name) {
    console.log('Liked:', name);
}

async function loadComments(itemName) {
    if (!bridge) return;
    try {
        const json = await bridge.GetComments(itemName);
        const comments = JSON.parse(json);
        const list = document.getElementById('comment-list');
        if (!list) return;

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
    const btnLike = document.getElementById('btn-like');
    if (btnLike) {
        btnLike.onclick = async () => {
            const nameElem = document.getElementById('detail-title');
            if (bridge && nameElem) {
                const success = await bridge.AddLike(nameElem.innerText);
                if (success) alert('点赞成功！');
            }
        };
    }

    const btnComment = document.getElementById('btn-comment');
    if (btnComment) {
        btnComment.onclick = async () => {
            const nameElem = document.getElementById('detail-title');
            const input = document.getElementById('input-comment');
            if (bridge && nameElem && input) {
                const text = input.value.trim();
                if (text && await bridge.AddComment(nameElem.innerText, text)) {
                    input.value = '';
                    loadComments(nameElem.innerText);
                }
            }
        };
    }
}

async function loadFallbackData() {
    try {
        const res = await fetch('data/data.json');
        renderDashboard(await res.json());
    } catch (e) {
        console.error("Failed to load fallback data", e);
    }
}

// 事件监听中心：只注册一次
function initMapEvents() {
    if (!chart) return;

    chart.on('click', (params) => {
        if (params.componentType === 'series' && params.seriesType === 'scatter') {
            // 点击散点：显示详情
            showDetail(params.name, params.value[2], params.value[3]);
        } else if (params.componentType === 'geo' || (params.componentType === 'series' && params.seriesType === 'map')) {
            // 点击地图区域：显示该市卡片
            showCityCards(params.name);
        }
    });
}
