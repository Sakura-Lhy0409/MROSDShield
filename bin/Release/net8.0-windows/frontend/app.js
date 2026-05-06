(() => {
  const defaultData = {
    active: false,
    ready: false,
    warn: false,
    svcFound: false,
    svcRunning: false,
    gcuService: false,
    gcuUtil: false,
    admin: false,
    fileResets: 0,
    totalKills: 0,
    uptime: "00:00:00",
    uptimeHours: 0,
    stableRem: 0,
    bootMin: true,
    minToTray: true,
    autoStart: false,
    killGpuProcesses: false,
    afterburnerProfile: 1,
    stableSeconds: 15,
    afterburnerPath: "",
    controlCenterPath: "",
    processRows: [],
    cpuUsage: 0,
    memoryUsage: 0,
    diskUsage: -1,
    diskText: "未知",
    lastCheckTime: "",
    powerAutoSwitch: false,
    powerTargetProcess: "",
    powerPlanWhenFound: "",
    powerPlanWhenMissing: "",
    powerLastApplied: "",
    powerTargetRunning: false,
    powerProcessLassoDetected: false,
    powerSwitchSkipped: false,
    powerActivePlanGuid: "",
    powerActivePlanName: "",
    powerDesiredPlanGuid: "",
    powerDesiredPlanName: "",
    powerPlans: [],
    lockBestPerformanceMode: false,
    lockBestPerformanceActive: false,
    bestPerformancePlanGuid: ""
  };

  const state = {
    page: "home",
    logs: [],
    data: { ...defaultData },
    scanTimer: null,
    theme: "light"
  };

  const $ = (id) => document.getElementById(id);
  const $$ = (selector) => [...document.querySelectorAll(selector)];

  function post(type, payload = {}) {
    if (window.chrome && chrome.webview) {
      chrome.webview.postMessage({ type, ...payload });
      return;
    }
    console.log("[MR OSD Shield]", type, payload);
  }

  function safeSet(id, value) {
    const el = $(id);
    if (el) el.textContent = value;
  }

  function safeHtml(id, value) {
    const el = $(id);
    if (el) el.innerHTML = value;
  }

  function nowTime() {
    return new Date().toLocaleTimeString("zh-CN", { hour12: false });
  }

  function esc(value) {
    return String(value ?? "").replace(/[\u0026<>"']/g, (ch) => {
      if (ch === "\u0026") return "\u0026amp;";
      if (ch === "<") return "\u0026lt;";
      if (ch === ">") return "\u0026gt;";
      if (ch === '"') return "\u0026quot;";
      return "\u0026#39;";
    });
  }

  function setPage(page) {
    const target = $(`page${page.charAt(0).toUpperCase()}${page.slice(1)}`);
    if (!target) page = "home";
    state.page = page;
    $$(".nav-item").forEach((btn) => btn.classList.toggle("active", btn.dataset.page === page));
    $$(".page").forEach((el) => {
      const key = el.id.replace(/^page/, "").toLowerCase();
      el.classList.toggle("active", key === page);
    });
  }

  function applyTheme(theme) {
    state.theme = theme === "dark" ? "dark" : "light";
    document.body.classList.toggle("dark", state.theme === "dark");
    const btn = $("themeBtn");
    if (btn) {
      btn.textContent = state.theme === "dark" ? "☀" : "◐";
      btn.title = state.theme === "dark" ? "切换浅色主题" : "切换深色主题";
    }
    try {
      window.localStorage.setItem("mrosd-theme", state.theme);
    } catch { }
  }

  function initTheme() {
    let saved = "";
    try {
      saved = window.localStorage.getItem("mrosd-theme") || "";
    } catch { }

    if (saved === "dark" || saved === "light") {
      applyTheme(saved);
      return;
    }

    const prefersDark = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
    applyTheme(prefersDark ? "dark" : "light");
  }

  function getModel() {
    const d = state.data;
    const bridgeOk = !!d.svcFound && !!d.svcRunning;
    const adminOk = !!d.admin;
    const active = !!d.active && bridgeOk && adminOk;
    const warn = !!d.warn || !bridgeOk || !adminOk;
    const riskBlocked = Number(d.fileResets || 0) + Number(d.totalKills || 0);
    const cpu = Number(d.cpuUsage || 0);
    const mem = Number(d.memoryUsage || 0);
    const disk = Number(d.diskUsage || 0);
    const measuredLoad = Math.round(cpu * 0.5 + mem * 0.35 + Math.max(0, disk) * 0.15);
    const fallbackLoad = 22 + riskBlocked * 3 + (warn ? 18 : 0);
    const load = Math.max(0, Math.min(100, measuredLoad > 0 ? measuredLoad : fallbackLoad));

    return {
      active,
      warn,
      bridgeOk,
      adminOk,
      riskBlocked,
      load,
      level: warn ? "中风险" : active ? "低风险" : "检测中",
      title: warn ? "需要注意" : active ? "防护中" : "检测中",
      badge: warn ? "需要处理" : active ? "实时守护" : "检测中",
      subtitle: warn
        ? "发现服务、权限或配置存在异常，建议立即修复后重新检测。"
        : active
          ? "一切正常，GPU 控制中心和小飞机配置处于稳定防护状态。"
          : d.stableRem > 0
            ? `等待 GCUBridge 服务稳定，约 ${d.stableRem}s 后进入防护。`
            : "正在检测控制中心、管理员权限与 WebView2 通信状态。"
    };
  }

  function renderTop(model) {
    safeSet("sysStatusText", model.warn ? "需要注意" : model.active ? "运行正常" : "检测中");
    safeSet("bridgeStatusText", model.bridgeOk ? "正常" : state.data.svcFound ? "已停止" : "未找到");
    safeSet("shieldStatusText", model.active ? "已启动" : "检测中");
    safeSet("uptimeText", state.data.uptime || "00:00:00");
    safeSet("scanBtnText", model.active ? "重新检测" : "检测中");

    const dot = document.querySelector(".top-status .dot");
    if (dot) dot.className = `dot ${model.warn ? "warn" : model.active ? "ok" : "scan"}`;
  }

  function renderHero(model) {
    safeSet("heroTitle", model.title);
    safeSet("heroSub", model.subtitle);
    safeSet("guardBadge", model.badge);
    $("guardBadge")?.classList.toggle("good", !model.warn);
    $("guardBadge")?.classList.toggle("warn", model.warn);

    const statuses = {
      heroGpuLink: model.active ? "正常" : "检测中",
      heroSvcLink: model.bridgeOk ? "正常" : state.data.svcFound ? "已停止" : "未找到",
      heroAdminLink: model.adminOk ? "正常" : "需管理员",
      heroDriverLink: model.active ? "已加载" : "准备中"
    };

    Object.entries(statuses).forEach(([id, text]) => {
      const el = $(id);
      if (!el) return;
      el.textContent = text;
      el.className = text === "正常" || text === "已加载" ? "good" : text === "检测中" || text === "准备中" ? "warn" : "bad";
    });
  }

  function renderRisk(model) {
    safeSet("riskBlocked", String(model.riskBlocked));
    safeSet("riskLevel", model.level);
    safeSet("riskTrend", model.warn ? "波动" : "平稳");
    const trend = $("riskTrend");
    if (trend) {
      trend.classList.toggle("good", !model.warn);
      trend.classList.toggle("warn", model.warn);
    }
    safeHtml("sysLoad", `${model.load}<span>%</span>`);
    const bar = $("sysLoadBar");
    if (bar) bar.style.width = `${model.load}%`;
  }

  function statusClass(ok) {
    return ok ? "ok" : "bad";
  }

  function processStatusHtml(ok, textOk = "运行中", textBad = "未运行") {
    return `<span class="state-dot ${statusClass(ok)}"></span>${ok ? textOk : textBad}`;
  }

  function fallbackRows() {
    const d = state.data;
    return [
      { name: "GCUBridge 服务", running: !!d.svcRunning, pid: "-", resourceText: d.svcRunning ? "服务运行" : "未运行", detail: "等待后端同步" },
      { name: "GCUService.exe", running: !!d.gcuService, pid: "-", resourceText: "等待采样", detail: "等待后端同步" },
      { name: "GCUUtil.exe", running: !!d.gcuUtil, pid: "-", resourceText: "等待采样", detail: "等待后端同步" },
      { name: "管理员权限", running: !!d.admin, pid: "-", resourceText: d.admin ? "已授权" : "权限受限", detail: "等待后端同步" }
    ];
  }

  function renderProcessTables() {
    const d = state.data;
    const rows = Array.isArray(d.processRows) && d.processRows.length ? d.processRows : fallbackRows();

    safeHtml("coreProcessRows", rows.map((row) => {
      const isAdmin = String(row.name || "").includes("管理员");
      return `
        <tr title="${esc(row.detail || "")}">
          <td>${esc(row.name)}</td>
          <td>${processStatusHtml(!!row.running, isAdmin ? "已通过" : "运行中", isAdmin ? "受限" : "未运行")}</td>
          <td>${esc(row.pid || "-")}</td>
          <td>${esc(row.resourceText || "0% / 0 MB")}</td>
        </tr>
      `;
    }).join(""));

    const processRows = rows.concat([
      {
        name: "MSI Afterburner",
        running: !!d.afterburnerPath,
        pid: "-",
        resourceText: d.afterburnerPath ? "路径已配置" : "未找到",
        detail: d.afterburnerPath || "未配置 MSI Afterburner 路径",
        policy: "Profile 重应用",
        action: "重应用",
        handler: "applyAfterburner"
      },
      {
        name: d.powerTargetProcess ? `电源目标：${getPowerTargetSummary(d.powerTargetProcess)}` : "电源目标进程",
        running: !!d.powerTargetRunning,
        pid: "-",
        resourceText: d.powerAutoSwitch ? (d.powerSwitchSkipped ? "已暂停" : "自动切换") : "未启用",
        detail: d.powerProcessLassoDetected ? "检测到 Process Lasso，已暂停本软件电源计划切换" : "自动电源计划切换状态",
        policy: d.powerTargetRunning ? "命中计划" : "默认计划",
        action: "设置",
        handler: "profile"
      }
    ]);

    safeHtml("processPageRows", processRows.map((row) => {
      const policy = row.policy || (String(row.name || "").includes("GCU") && d.killGpuProcesses ? "旧版拦截" : "实时监控");
      const handler = row.handler || "getStatus";
      const action = row.action || "刷新";
      return `
        <tr title="${esc(row.detail || "")}">
          <td>${esc(row.name)}</td>
          <td>${processStatusHtml(!!row.running, row.running ? "正常" : "未检测")}</td>
          <td>${esc(policy)} / PID: ${esc(row.pid || "-")} / ${esc(row.resourceText || "")}</td>
          <td><button class="table-action" type="button" data-action="${esc(handler)}">${esc(action)}</button></td>
        </tr>
      `;
    }).join(""));
  }

  function renderStats() {
    const d = state.data;
    safeSet("homeFixes", String(d.fileResets ?? 0));
    safeSet("homeKills", String(d.totalKills ?? 0));
    safeSet("homeUptime", d.uptime || "00:00:00");
    safeSet("lastCheckTime", d.lastCheckTime || "等待检测");

    safeSet("cpuUsage", `${Number(d.cpuUsage || 0)}%`);
    safeSet("memUsage", `${Number(d.memoryUsage || 0)}%`);
    safeSet("diskIo", d.diskText && d.diskText !== "未知" ? `${d.diskText} 已用` : "未知");
  }

  function renderSubPages(model) {
    const d = state.data;
    safeSet("protectEngine", model.active ? "运行中" : "检测中");
    safeSet("protectBridge", model.bridgeOk ? "正常" : "异常");
    safeSet("protectStable", `${d.stableSeconds || 15}s`);
    safeSet("protectFixes", String(d.fileResets || 0));
    safeSet("protectGcuService", d.gcuService ? "运行中" : "未运行");
    safeSet("protectGcuUtil", d.gcuUtil ? "运行中" : "未运行");
    safeSet("protectKillMode", d.killGpuProcesses ? "已启用" : "未启用");
    safeSet("permAdmin", d.admin ? "已通过" : "需要管理员");
    safeSet("permAutoStart", d.autoStart ? "已启用" : "未启用");
    safeSet("permTray", d.minToTray ? "已启用" : "未启用");
    safeSet("permConfig", "可用");
  }

  function formatPowerPlanLabel(plan) {
    const guid = String(plan?.guid || "").trim();
    const name = String(plan?.name || "").trim() || guid || "未知计划";
    if (!guid) return name;
    return `${name} (${guid})`;
  }

  function parsePowerTargets(value) {
    return String(value || "")
      .split(/[,;，；|\r\n]+/)
      .map((item) => item.trim())
      .filter(Boolean);
  }

  function getPowerTargetSummary(value) {
    const targets = parsePowerTargets(value);
    if (!targets.length) return "未配置";
    if (targets.length <= 2) return targets.join("、");
    return `${targets.slice(0, 2).join("、")} 等 ${targets.length} 个进程`;
  }

  function uniquePowerPlans(plans, extras = []) {
    const map = new Map();
    [...extras, ...(Array.isArray(plans) ? plans : [])].forEach((plan) => {
      const guid = String(plan?.guid || "").trim().toUpperCase();
      if (!guid) return;
      if (!map.has(guid)) {
        map.set(guid, {
          guid,
          name: String(plan?.name || "").trim() || guid
        });
      }
    });
    return [...map.values()];
  }

  function buildPowerOptions(list, currentValue) {
    const plans = uniquePowerPlans(list, currentValue ? [{ guid: currentValue, name: "已保存计划" }] : []);
    if (!plans.length) {
      return '<option value="">未获取电源计划</option>';
    }
    return [
      '<option value="">请选择电源计划</option>',
      ...plans.map((plan) => `<option value="${esc(plan.guid)}">${esc(formatPowerPlanLabel(plan))}</option>`)
    ].join("");
  }

  function FindPowerPlanName(guid) {
    const d = state.data;
    const plans = Array.isArray(d.powerPlans) ? d.powerPlans : [];
    for (const plan of plans) {
      if (String(plan.guid || "").toUpperCase() === String(guid || "").toUpperCase()) {
        return plan.name || guid;
      }
    }
    return guid || "未知";
  }

  function getPowerStatusText(d) {
    if (d.lockBestPerformanceMode) {
      return d.lockBestPerformanceActive ? "已锁定最佳性能" : "锁定中";
    }
    if (!d.powerAutoSwitch) return "未启用";
    if (d.powerProcessLassoDetected) return "已暂停";
    if (d.powerTargetRunning) return "进程命中";
    return "监听中";
  }

  function renderPowerSettings() {
    const d = state.data;
    const powerPlans = Array.isArray(d.powerPlans) ? d.powerPlans : [];
    const targetInput = $("powerTargetProcess");
    const foundSelect = $("powerPlanFound");
    const missingSelect = $("powerPlanMissing");
    const lockSwitch = $("lockBestPerformanceMode");
    const autoSwitch = $("powerAutoSwitch");
    const saveBtn = document.querySelector('[data-action="savePowerConfig"]');

    const isLocked = !!d.lockBestPerformanceMode;

    if (targetInput && document.activeElement !== targetInput) {
      targetInput.value = d.powerTargetProcess || "";
    }

    if (foundSelect) {
      const currentValue = d.powerPlanWhenFound || "";
      foundSelect.innerHTML = buildPowerOptions(powerPlans, currentValue);
      foundSelect.value = currentValue;
      foundSelect.disabled = isLocked;
    }

    if (missingSelect) {
      const currentValue = d.powerPlanWhenMissing || "";
      missingSelect.innerHTML = buildPowerOptions(powerPlans, currentValue);
      missingSelect.value = currentValue;
      missingSelect.disabled = isLocked;
    }

    if (targetInput) {
      targetInput.disabled = isLocked;
    }

    if (saveBtn) {
      saveBtn.disabled = isLocked;
    }

    safeSet("powerModeBadge", getPowerStatusText(d));

    const badge = $("powerModeBadge");
    if (badge) {
      badge.classList.remove("good", "warn");
      if (isLocked && d.lockBestPerformanceActive) {
        badge.classList.add("good");
      } else if (isLocked && !d.lockBestPerformanceActive) {
        badge.classList.add("warn");
      } else if (d.powerAutoSwitch && !d.powerProcessLassoDetected) {
        badge.classList.add("good");
      } else if (d.powerProcessLassoDetected) {
        badge.classList.add("warn");
      }
    }

    safeSet("powerLassoState", d.powerProcessLassoDetected ? "已检测到" : "未检测");
    const targetSummary = getPowerTargetSummary(d.powerTargetProcess);
    safeSet("powerTargetState", d.powerTargetProcess ? `${targetSummary}${d.powerTargetRunning ? "（已命中）" : "（未命中）"}` : "未配置");
    
    if (isLocked && d.bestPerformancePlanGuid) {
      const planName = FindPowerPlanName(d.bestPerformancePlanGuid) || "最佳性能";
      safeSet("powerActivePlan", `${planName} (${d.bestPerformancePlanGuid})`);
    } else {
      safeSet("powerActivePlan", d.powerActivePlanName ? `${d.powerActivePlanName} (${d.powerActivePlanGuid || "未知"})` : "未知");
    }
    
    if (isLocked) {
      safeSet("powerSwitchState", d.lockBestPerformanceActive ? "已锁定到最佳性能模式" : "正在锁定最佳性能模式");
    } else {
      safeSet("powerSwitchState", d.powerSwitchSkipped
        ? "Process Lasso 已接管，已暂停切换"
        : d.powerAutoSwitch
          ? (d.powerTargetRunning ? "切换到进程命中方案" : "切换到未命中方案")
          : "待配置");
    }

    if (lockSwitch) lockSwitch.checked = isLocked;
    if (autoSwitch) {
      autoSwitch.checked = !!d.powerAutoSwitch;
      autoSwitch.disabled = isLocked;
    }
  }

  function renderSettings() {
    const d = state.data;
    safeSet("abPath", d.afterburnerPath || "未找到");
    safeSet("ccPath", d.controlCenterPath || "未找到");
    safeSet("profileVal", String(d.afterburnerProfile || 1));
    safeSet("stableSecondsVal", String(d.stableSeconds || 15));

    const switches = {
      autoStartSwitch: !!d.autoStart,
      bootMinSwitch: !!d.bootMin,
      minToTraySwitch: !!d.minToTray,
      killProcSwitch: !!d.killGpuProcesses
    };

    Object.entries(switches).forEach(([id, checked]) => {
      const el = $(id);
      if (el) el.checked = checked;
    });

    renderPowerSettings();
  }

  function addLog(message, level = "good") {
    state.logs.unshift({ time: nowTime(), message, level });
    state.logs = state.logs.slice(0, 18);
    renderLogs();
  }

  function renderLogs() {
    if (!state.logs.length) {
      state.logs = [
        { time: nowTime(), message: "防护界面已加载完成", level: "good" },
        { time: nowTime(), message: "权限校验与服务状态正在同步", level: "good" },
        { time: nowTime(), message: "WebView2 前后端通信已就绪", level: "good" }
      ];
    }

    safeHtml("logList", state.logs.map((item) => `
      <li class="${esc(item.level || "good")}">
        <span class="time">${esc(item.time)}</span>
        <span class="log-dot"></span>
        <span>${esc(item.message)}</span>
      </li>
    `).join(""));
  }

  function showToast(message) {
    const host = $("toastHost");
    if (!host) return;
    const toast = document.createElement("div");
    toast.className = "toast";
    toast.textContent = message;
    host.appendChild(toast);
    window.setTimeout(() => toast.remove(), 2400);
  }

  function render(data = {}) {
    state.data = { ...state.data, ...data };
    const model = getModel();
    renderTop(model);
    renderHero(model);
    renderRisk(model);
    renderProcessTables();
    renderStats(model);
    renderSubPages(model);
    renderSettings();
    renderLogs();
    bindActions();
  }

  function startScanPulse() {
    const btn = $("scanBtn");
    if (!btn) return;
    btn.classList.add("scanning");
    window.clearTimeout(state.scanTimer);
    state.scanTimer = window.setTimeout(() => btn.classList.remove("scanning"), 900);
  }

  function requestStatus() {
    startScanPulse();
    post("getStatus");
  }

  function savePowerConfig() {
    const targetProcess = $("powerTargetProcess")?.value?.trim() || "";
    const whenFoundGuid = $("powerPlanFound")?.value || "";
    const whenMissingGuid = $("powerPlanMissing")?.value || "";
    post("setPowerConfig", { targetProcess, whenFoundGuid, whenMissingGuid });
    state.data.powerTargetProcess = targetProcess;
    state.data.powerPlanWhenFound = whenFoundGuid;
    state.data.powerPlanWhenMissing = whenMissingGuid;
    addLog("已保存电源计划自动切换策略", "good");
    showToast("已保存电源计划策略");
    render();
  }

  function handleAction(action) {
    if (action === "savePowerConfig") {
      savePowerConfig();
      return;
    }

    if (action === "generateReport") {
      post("openLogDir");
      addLog("已生成本次安全状态摘要，并打开日志目录", "good");
      showToast("已打开日志目录");
      return;
    }

    const aliases = {
      openProcessView: "page:processes",
      openPerfDetail: "page:processes",
      openLogDetail: "openLogDir",
      customActions: "page:settings",
      profile: "page:settings",
      restartProtection: "repairNow"
    };

    const mapped = aliases[action] || action;
    if (mapped.startsWith("page:")) {
      const page = mapped.slice(5);
      setPage(page);
      post("pageChanged", { page });
      return;
    }

    if (mapped === "getStatus") {
      requestStatus();
      addLog("已手动刷新当前防护状态", "good");
      showToast("已刷新状态");
      return;
    }

    post(mapped);
    const messages = {
      repairNow: "已发送立即修复指令",
      applyAfterburner: "已发送小飞机 Profile 重应用指令",
      minimizeToTray: "正在最小化到系统托盘",
      chooseAfterburner: "正在选择 MSI Afterburner 路径",
      chooseControlCenter: "正在选择控制中心路径",
      openLogDir: "正在打开日志目录",
      openAppDir: "正在打开程序目录",
      refreshPowerPlans: "正在刷新电源计划列表"
    };
    if (messages[mapped]) {
      addLog(messages[mapped], "good");
      showToast(messages[mapped]);
    }
  }

  function bindActions() {
    $$("[data-action]").forEach((btn) => {
      if (btn.dataset.bound === "true") return;
      btn.dataset.bound = "true";
      btn.addEventListener("click", (event) => {
        event.preventDefault();
        handleAction(btn.dataset.action);
      });
    });
  }

  function bindStatic() {
    $$(".nav-item").forEach((btn) => {
      btn.addEventListener("click", () => {
        setPage(btn.dataset.page);
        post("pageChanged", { page: btn.dataset.page });
      });
    });

    $$("[data-step]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const setting = btn.dataset.step;
        const delta = Number(btn.dataset.delta || 0);
        if (setting === "profile") {
          state.data.afterburnerProfile = Math.max(1, Math.min(5, Number(state.data.afterburnerProfile || 1) + delta));
        }
        if (setting === "stableSeconds") {
          state.data.stableSeconds = Math.max(3, Math.min(60, Number(state.data.stableSeconds || 15) + delta));
        }
        post("stepSetting", { setting, delta });
        addLog(`已调整 ${setting} 设置`, "good");
        render();
      });
    });

    $$("[data-setting]").forEach((input) => {
      input.addEventListener("change", () => {
        state.data[input.dataset.setting] = input.checked;
        post("toggleSetting", { setting: input.dataset.setting, value: input.checked });
        addLog(`已${input.checked ? "启用" : "停用"} ${input.dataset.setting}`, "good");
        render();
      });
    });

    const powerInput = $("powerTargetProcess");
    if (powerInput) {
      powerInput.addEventListener("input", () => {
        state.data.powerTargetProcess = powerInput.value;
      });
      powerInput.addEventListener("blur", () => {
        state.data.powerTargetProcess = powerInput.value;
      });
    }

    $("powerPlanFound")?.addEventListener("change", () => {
      state.data.powerPlanWhenFound = $("powerPlanFound").value;
    });

    $("powerPlanMissing")?.addEventListener("change", () => {
      state.data.powerPlanWhenMissing = $("powerPlanMissing").value;
    });

    $("menuBtn")?.addEventListener("click", () => setPage("settings"));
    $("themeBtn")?.addEventListener("click", () => {
      applyTheme(state.theme === "dark" ? "light" : "dark");
      showToast(state.theme === "dark" ? "已切换深色主题" : "已切换浅色主题");
    });
  }

  if (window.chrome && chrome.webview) {
    chrome.webview.addEventListener("message", (event) => {
      const msg = event.data || {};
      if (msg.type === "state") render(msg.data || {});
      if (msg.type === "notify") {
        const data = msg.data || {};
        addLog(data.message || msg.message || "收到系统通知", data.level || msg.level || "good");
        showToast(data.message || msg.message || "收到系统通知");
      }
    });
  }

  window.addEventListener("DOMContentLoaded", () => {
    initTheme();
    bindStatic();
    bindActions();
    setPage("home");
    render();
    post("refreshPowerPlans");
    requestStatus();
    post("ready");
    window.setInterval(requestStatus, 3000);
  });

  window.__mrosd = { render, setPage, addLog, requestStatus, applyTheme, savePowerConfig };
})();