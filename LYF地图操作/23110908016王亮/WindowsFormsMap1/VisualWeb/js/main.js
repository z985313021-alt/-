const STATE = {
    dataLoaded: false
};

let chart = null;
let categoryChart = null;
let mapPoints = [];
let allData = null;
let bridge = null;
let currentMode = 'point'; // 'point', 'choropleth', 'heatmap'
let roadsData = null;
let isRouteMode = false;
let routePoints = []; // [StartPoint, EndPoint]
let calculatedPath = null;
let pathICH = [];

const COLORS = ['#3b82f6', '#8b5cf6', '#ec4899', '#f59e0b', '#10b981'];

/**
 * [New] Categorized Image Folder Mapping
 * Maps ICH category to the actual folder names provided by the USER
 * 重要：左侧是数据库里的分类名，右侧是实际文件夹名
 */
const CATEGORY_FOLDER_MAP = {
    "传统戏剧": "传统戏剧非遗",
    "传统音乐": "传统音乐非遗",
    "传统技艺": "技艺非遗",
    "传统美术": "美术非遗",
    "民间文学": "民间文学非遗",
    "民俗": "民俗非遗",
    "曲艺": "曲艺非遗",
    "传统体育、游艺与杂技": "体育游艺杂技",
    "传统舞蹈": "舞蹈非遗",
    "传统医药": "医药非遗"
};

/**
 * [New] Image Path Generator
 * Generates an array of potential image paths for a given ICH item
 */
function getPossibleImagePaths(name, cat) {
    const folder = CATEGORY_FOLDER_MAP[cat] || "其他";
    // 修正：根据用户最新截图，分类文件夹直接位于 images/ 下
    // 重要：本地文件系统不需要 URL 编码，直接使用中文名
    const basePath = `images/${folder}/${name}`;

    const extensions = ['.jpg', '.png', '.jpeg', '.webp'];
    let paths = [];

    // 1. 尝试直接匹配各种后缀
    extensions.forEach(ext => paths.push(basePath + ext));

    // 2. 尝试带序号的匹配 (1-5) 且兼容所有后缀
    for (let i = 1; i <= 5; i++) {
        extensions.forEach(ext => paths.push(`${basePath}${i}${ext}`));
    }
    return paths;
}

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
            const prevActive = document.querySelector('.nav-item.active');
            if (prevActive) prevActive.classList.remove('active');
            item.classList.add('active');

            const view = item.dataset.view;
            handleViewChange(view);
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

    // [New] Route Mode Skeleton (Always show background roads if data exists)
    if (isRouteMode && roadsData) {
        // 1. Background Roads (Simplified high-level skeleton)
        option.series.push({
            name: '路网骨架',
            type: 'lines',
            coordinateSystem: 'geo',
            polyline: true,
            large: true, // [Critical] Enable large data mode for lines
            progressive: 2000,
            data: roadsData.features.map(f => ({
                coords: f.geometry.coordinates,
                lineStyle: { normal: { color: 'rgba(102, 126, 234, 0.4)', width: 1.0 } }
            })),
            silent: true
        });

        // 2. Active Selection Markers
        if (routePoints.length > 0) {
            option.series.push({
                type: 'scatter',
                coordinateSystem: 'geo',
                data: routePoints.map((p, i) => ({
                    name: i === 0 ? '起点' : '终点',
                    value: p
                })),
                symbolSize: 20,
                itemStyle: {
                    color: (p) => p.name === '起点' ? '#10b981' : '#ef4444',
                    shadowBlur: 10,
                    shadowColor: '#fff'
                },
                label: {
                    show: true,
                    formatter: '{b}',
                    position: 'top',
                    color: '#fff',
                    fontWeight: 'bold'
                }
            });
        }
    }

    // [New] Render Calculated Path (Persistent)
    if (calculatedPath) {
        option.series.push({
            name: '寻访路径',
            type: 'lines',
            coordinateSystem: 'geo',
            polyline: true,
            data: [{
                coords: calculatedPath,
                lineStyle: { normal: { color: '#00d2ff', width: 4, shadowBlur: 10, shadowColor: '#00d2ff' } }
            }],
            effect: {
                show: true,
                period: 4,
                trailLength: 0.7,
                color: '#fff',
                symbolSize: 4
            }
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

    // [New] Image Sniffing Logic (Multiplexing)
    const possiblePaths = getPossibleImagePaths(name, cat);

    // Create card HTML with side panel structure
    container.innerHTML = `
        <div class="card-detail-center">
            <div class="ich-card" onclick="handleCardClick(this)">
                <div class="card-image-full" id="card-media-gallery" style="background: ${gradientColors}">
                    <!-- Main image with failover logic -->
                    <img src="${possiblePaths[0]}" 
                         class="gallery-img active"
                         data-paths='${JSON.stringify(possiblePaths)}'
                         data-current="0"
                         style="width: 100%; height: 100%; object-fit: cover; object-position: center;"
                         onerror="handleImageError(this)">
                    
                    <div class="gallery-controls" id="gallery-nav" style="display:none">
                        <button class="gallery-btn prev" onclick="event.stopPropagation(); shiftGallery(this, -1)">‹</button>
                        <button class="gallery-btn next" onclick="event.stopPropagation(); shiftGallery(this, 1)">›</button>
                    </div>

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
        const imagePaths = getPossibleImagePaths(item.name, item.category);

        return `
                    <div class="hand-card" 
                         data-index="${index}"
                         data-name="${item.name}"
                         data-category="${item.category}"
                         data-city="${item.city}"
                         onclick="handleHandCardClick(this)">
                        <div class="hand-card-image" style="background: ${gradient}">
                            <img src="${imagePaths[0]}" 
                                 alt="${item.name}"
                                 data-paths='${JSON.stringify(imagePaths)}'
                                 data-current="0"
                                 style="width: 100%; height: 100%; object-fit: cover; object-position: center;"
                                 onerror="handleImageError(this)">
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
            // 点击地图区域
            if (isRouteMode) {
                handleRouteClick(params.event.event.zrX, params.event.event.zrY);
            } else {
                showCityCards(params.name);
            }
        }
    });
}

/**
 * [New] View Management
 */
function handleViewChange(view) {
    console.log("Switching to view:", view);

    // Toggle Itinerary Panel
    const itinerary = document.getElementById('route-itinerary');
    const chartBox = document.getElementById('category-chart');
    const placeholder = document.getElementById('detail-placeholder');

    if (view === 'route') {
        isRouteMode = true;
        if (itinerary) itinerary.style.display = 'block';
        if (chartBox) chartBox.style.display = 'none';
        if (placeholder) placeholder.style.display = 'none';

        // 加载并渲染路网骨架
        if (!roadsData) {
            loadRoadsData();
        } else {
            renderMap(getFilteredPoints());
        }
    } else {
        isRouteMode = false;
        if (itinerary) itinerary.style.display = 'none';
        if (chartBox) chartBox.style.display = 'block';
        if (placeholder) placeholder.style.display = 'block';

        renderMap(getFilteredPoints());
    }
}

/**
 * [Core Logic] Build Topo Graph from roads.json
 */
let roadGraph = new Map(); // "lng,lat" -> [{to: "lng,lat", dist: number}]

function buildRoadGraph() {
    if (!roadsData) return;
    roadGraph.clear();

    roadsData.features.forEach(feature => {
        const coords = feature.geometry.coordinates;
        for (let i = 0; i < coords.length - 1; i++) {
            const p1 = coords[i].join(',');
            const p2 = coords[i + 1].join(',');
            const dist = Math.sqrt(Math.pow(coords[i][0] - coords[i + 1][0], 2) + Math.pow(coords[i][1] - coords[i + 1][1], 2));

            if (!roadGraph.has(p1)) roadGraph.set(p1, []);
            if (!roadGraph.has(p2)) roadGraph.set(p2, []);

            roadGraph.get(p1).push({ to: p2, dist: dist, coords: coords[i + 1] });
            roadGraph.get(p2).push({ to: p1, dist: dist, coords: coords[i] });
        }
    });
    console.log("Road graph built with", roadGraph.size, "nodes.");
}

async function loadRoadsData() {
    console.log("Loading roads data via bridge...");
    try {
        // [New] Primary: Pull from C# bridge (Safe for large strings)
        if (window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.bridge) {
            const bridge = window.chrome.webview.hostObjects.bridge;
            const roadsJson = await bridge.GetRoadsData();
            if (roadsJson && !roadsJson.startsWith('{"error"')) {
                roadsData = JSON.parse(roadsJson);
                console.log("Roads data loaded via bridge. Features:", roadsData.features.length);
                buildRoadGraph();
                renderMap(getFilteredPoints());
                return;
            } else {
                console.warn("Bridge returned error for roads data:", roadsJson);
            }
        }

        // Fallback: Check global variable (legacy injection)
        if (window.ROADS_DATA) {
            console.log("Using legacy injected ROADS_DATA.");
            roadsData = window.ROADS_DATA;
            buildRoadGraph();
            renderMap(getFilteredPoints());
            return;
        }

        // Final Fallback: Fetch
        console.log("Attempting fetch fallback for roads.json...");
        const res = await fetch('data/roads.json');
        roadsData = await res.json();
        console.log("Roads data loaded via fetch.");
        buildRoadGraph();
        renderMap(getFilteredPoints());
    } catch (e) {
        console.warn("Failed to load roads data through all channels:", e);
    }
}

/**
 * [Core Logic] Start planning the route
 */
function startPlanning() {
    if (routePoints.length < 2) {
        alert("请先在地图上选定起点和终点！");
        return;
    }

    if (!roadsData) {
        alert("路网数据尚未加载完成，请稍后...");
        return;
    }

    // 1. 构建拓扑图和执行 Dijkstra (这里使用极简直线吸附模拟)
    const resultPath = calculateShortestPath(routePoints[0], routePoints[1]);

    if (!resultPath || resultPath.length === 0) {
        alert("未找到连通路径，请尝试重新选择点位。");
        return;
    }

    // 2. 识别沿途非遗 (缓冲区分析)
    const nearbyICH = findICHAlongPath(resultPath, 0.5); // 0.5度约50km

    // 3. 渲染结果
    calculatedPath = resultPath;
    pathICH = nearbyICH;
    renderMap(getFilteredPoints());
    updateItineraryUI();
}

function clearRoute() {
    routePoints = [];
    calculatedPath = null;
    pathICH = [];
    renderMap(getFilteredPoints());
    updateItineraryUI();
}

// [New] Binary Heap for Fast Dijkstra
class MinHeap {
    constructor() {
        this.heap = [];
    }
    push(node) {
        this.heap.push(node);
        this.bubbleUp();
    }
    pop() {
        if (this.size() === 0) return null;
        if (this.size() === 1) return this.heap.pop();
        const min = this.heap[0];
        this.heap[0] = this.heap.pop();
        this.bubbleDown();
        return min;
    }
    size() { return this.heap.length; }
    bubbleUp() {
        let index = this.heap.length - 1;
        while (index > 0) {
            let parentIndex = Math.floor((index - 1) / 2);
            if (this.heap[index].dist >= this.heap[parentIndex].dist) break;
            [this.heap[index], this.heap[parentIndex]] = [this.heap[parentIndex], this.heap[index]];
            index = parentIndex;
        }
    }
    bubbleDown() {
        let index = 0;
        while (true) {
            let leftChild = 2 * index + 1;
            let rightChild = 2 * index + 2;
            let smallest = index;
            if (leftChild < this.heap.length && this.heap[leftChild].dist < this.heap[smallest].dist) smallest = leftChild;
            if (rightChild < this.heap.length && this.heap[rightChild].dist < this.heap[smallest].dist) smallest = rightChild;
            if (smallest === index) break;
            [this.heap[index], this.heap[smallest]] = [this.heap[smallest], this.heap[index]];
            index = smallest;
        }
    }
}

function calculateShortestPath(startPoint, endPoint) {
    if (roadGraph.size === 0) return [startPoint, endPoint];

    // 1. Snapping
    let startNode = null, endNode = null;
    let minDistS = Infinity, minDistE = Infinity;
    for (let nodeKey of roadGraph.keys()) {
        const [lng, lat] = nodeKey.split(',').map(Number);
        const dS = Math.pow(lng - startPoint[0], 2) + Math.pow(lat - startPoint[1], 2);
        const dE = Math.pow(lng - endPoint[0], 2) + Math.pow(lat - endPoint[1], 2);
        if (dS < minDistS) { minDistS = dS; startNode = nodeKey; }
        if (dE < minDistE) { minDistE = dE; endNode = nodeKey; }
    }

    // 2. Fast Dijkstra with MinHeap
    let distances = new Map();
    let previous = new Map();
    let pq = new MinHeap();

    for (let node of roadGraph.keys()) {
        distances.set(node, Infinity);
    }
    distances.set(startNode, 0);
    pq.push({ id: startNode, dist: 0 });

    while (pq.size() > 0) {
        let { id: u, dist: d } = pq.pop();
        if (d > distances.get(u)) continue;
        if (u === endNode) break;

        const neighbors = roadGraph.get(u) || [];
        for (let edge of neighbors) {
            let alt = d + edge.dist;
            if (alt < distances.get(edge.to)) {
                distances.set(edge.to, alt);
                previous.set(edge.to, u);
                pq.push({ id: edge.to, dist: alt });
            }
        }
    }

    // 3. Reconstruct
    let path = [];
    let curr = endNode;
    if (previous.has(curr) || curr === startNode) {
        while (curr) {
            path.unshift(curr.split(',').map(Number));
            curr = previous.get(curr);
        }
    }
    return [startPoint, ...path, endPoint];
}

function findICHAlongPath(path, buffer) {
    const points = getFilteredPoints();
    // Use a step to speed up buffer check if path is long
    const step = Math.max(1, Math.floor(path.length / 50));
    return points.filter(p => {
        for (let i = 0; i < path.length; i += step) {
            const node = path[i];
            const dist = Math.sqrt(Math.pow(p.x - node[0], 2) + Math.pow(p.y - node[1], 2));
            if (dist < buffer) return true;
        }
        return false;
    }).slice(0, 5);
}

function renderRouteResult(path, ichList) {
    const option = chart.getOption();

    // 添加流光路径
    option.series.push({
        name: '寻访路径',
        type: 'lines',
        coordinateSystem: 'geo',
        polyline: true,
        data: [{
            coords: path,
            lineStyle: { normal: { color: '#00d2ff', width: 4, curveness: 0.2, shadowBlur: 10, shadowColor: '#00d2ff' } }
        }],
        effect: {
            show: true,
            period: 4,
            trailLength: 0.7,
            color: '#fff',
            symbolSize: 4
        }
    });

    chart.setOption(option);

    // 更新右侧面板显示非遗
    const steps = document.getElementById('route-steps');
    let ichHtml = ichList.length > 0 ? '<div style="margin-top:15px; border-top:1px solid #444; padding-top:10px;"><b>✨ 沿途非遗推荐：</b></div>' : '';

    ichList.forEach(p => {
        ichHtml += `
            <div class="itinerary-item" onclick="showDetail('${p.name}', '${p.category}', '${p.city}')">
                <div class="itinerary-num" style="background:#8b5cf6">★</div>
                <div>${p.name} <br/><small>${p.city} · ${p.category}</small></div>
            </div>
        `;
    });

    steps.innerHTML = `
        <div class="itinerary-item"><span class="itinerary-num">始</span> ${path[0][0].toFixed(2)}, ${path[0][1].toFixed(2)}</div>
        <div class="itinerary-item"><span class="itinerary-num">终</span> ${path[path.length - 1][0].toFixed(2)}, ${path[path.length - 1][1].toFixed(2)}</div>
        ${ichHtml}
        <button class="card-btn" style="margin-top:10px; width:100%" onclick="location.reload()">重新规划</button>
    `;
}

function handleRouteClick(x, y) {
    const pt = chart.convertFromPixel('geo', [x, y]);
    if (!pt) return;

    if (routePoints.length >= 2) routePoints = []; // Reset

    routePoints.push(pt);
    console.log("Point added for route:", pt);

    updateItineraryUI();
    renderMap(getFilteredPoints());
}

function updateItineraryUI() {
    const steps = document.getElementById('route-steps');
    if (!steps) return;

    if (calculatedPath) {
        let ichHtml = pathICH.length > 0 ? '<div style="margin-top:15px; border-top:1px solid #444; padding-top:10px;"><b>✨ 沿途非遗推荐：</b></div>' : '';
        pathICH.forEach(p => {
            ichHtml += `
                <div class="itinerary-item" onclick="showDetail('${p.name}', '${p.category}', '${p.city}')">
                    <div class="itinerary-num" style="background:#8b5cf6">★</div>
                    <div>${p.name} <br/><small>${p.city} · ${p.category}</small></div>
                </div>
            `;
        });

        steps.innerHTML = `
            <div class="itinerary-item"><span class="itinerary-num">始</span> ${calculatedPath[0][0].toFixed(2)}, ${calculatedPath[0][1].toFixed(2)}</div>
            <div class="itinerary-item"><span class="itinerary-num">终</span> ${calculatedPath[calculatedPath.length - 1][0].toFixed(2)}, ${calculatedPath[calculatedPath.length - 1][1].toFixed(2)}</div>
            ${ichHtml}
            <button class="card-btn" style="margin-top:10px; width:100%" onclick="clearRoute()">重新规划</button>
        `;
        return;
    }

    if (routePoints.length === 0) {
        steps.innerHTML = '请在地图上点击起点和终点...';
    } else if (routePoints.length === 1) {
        steps.innerHTML = '<div class="itinerary-item"><span class="itinerary-num">起</span> 已设置起点</div><div style="margin-top:5px; color:#aaa">请点击地图设置终点...</div>';
    } else {
        steps.innerHTML = `
            <div class="itinerary-item"><span class="itinerary-num">起</span> 起始坐标: ${routePoints[0][0].toFixed(2)}, ${routePoints[0][1].toFixed(2)}</div>
            <div class="itinerary-item"><span class="itinerary-num">终</span> 结束坐标: ${routePoints[1][0].toFixed(2)}, ${routePoints[1][1].toFixed(2)}</div>
            <div style="margin-top:10px; text-align:center">
                <button class="card-btn card-btn-primary" style="width:100%" onclick="startPlanning()">
                    ✨ 开始规划
                </button>
                <button class="card-btn" style="width:100%; margin-top:5px; background:rgba(255,255,255,0.1)" onclick="clearRoute()">
                    取消选择
                </button>
            </div>
        `;
    }
}

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
