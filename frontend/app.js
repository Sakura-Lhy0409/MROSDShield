(() => {
  const state = {
    page: "home",
    lang: "zh",
    data: {
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
      stableRem: 0,
      bootMin: true,
      minToTray: true,
      autoStart: false,
      killGpuProcesses: false,
      afterburnerProfile: 1,
      stableSeconds: 15,
      afterburnerPath: "",
      controlCenterPath: ""
    }
  };

  const $ = (id) => document.getElementById(id);

  const pageMap = {
    home: $("pageHome"),
    stats: $("pageStats"),
    settings: $("pageSettings")
  };

  const navItems = [...document.querySelectorAll(".nav-item")];
  const actionButtons = [...document.querySelectorAll("[data-action]")];
  const stepButtons = [...document.querySelectorAll("[data-step]")];
  const settingInputs = [...document.querySelectorAll("[data-setting]")];

  function post(type, payload = {}) {
    if (window.chrome && chrome.webview) {
      chrome.webview.postMessage({ type, ...payload });
    }
  }

  function setPage(page) {
    state.page = page;
    navItems.forEach((btn) => btn.classList.toggle("active", btn.dataset.page === page));
    Object.entries(pageMap).forEach(([key, el]) => {
      el.classList.toggle("active", key === page);
    });
  }

  function fmtBool(v, yes = "已启用", no = "已停用") {
    return v ? yes : no;
  }

  function setBadge(mode) {
    const badge = $("statusBadge");
    const hero = $("heroCard");
    badge.className = "status-badge";
    hero.className = "hero-card";
    if (mode === "active") {
      badge.textContent = "防护已启用";
      badge.classList.add("active");
      hero.classList.add("active");
    } else if (mode === "warn") {
      badge.textContent = "需要注意";
      badge.classList.add("warn");
      hero.classList.add("warn");
    } else {
      badge.textContent = "检测中";
      badge.classList.add("detecting");
      hero.classList.add("detecting");
    }
  }

  function render(data) {
    state.data = { ...state.data, ...data };

    const active = !!state.data.active;
    const warn = !!state.data.warn;
    setBadge(active ? (warn ? "warn" : "active") : "detecting");

    $("heroTitle").textContent = active
      ? (warn ? "需要注意" : "防护已启用")
      : (state.data.stableRem > 0 ? `正在检测控制中心 · ${state.data.stableRem}s` : "正在检测控制中心");

    $("heroSub").textContent = active
      ? (warn ? "GPU 超频配置已被修复并重应用小飞机配置" : "仅锁定 GPU 超频配置，控制中心功耗控制保持可用")
      : "等待 GCUBridge 服务稳定后开始防护";

    const svcText = state.data.svcFound ? (state.data.svcRunning ? "运行中" : "已停止") : "未找到";
    const gcuText = state.data.gcuService ? "运行中" : (state.data.killGpuProcesses ? "已屏蔽" : "已允许");
    const gcuuText = state.data.gcuUtil ? "运行中" : (state.data.killGpuProcesses ? "已屏蔽" : "已允许");
    const adminText = state.data.admin ? "已启用" : "未启用，部分功能可能失败";

    $("svcVal").textContent = svcText;
    $("gcuVal").textContent = gcuText;
    $("gcuuVal").textContent = gcuuText;
    $("adminVal").textContent = adminText;

    $("svcVal").className = state.data.svcRunning ? "good" : "bad";
    $("gcuVal").className = state.data.gcuService ? (state.data.killGpuProcesses ? "bad" : "good") : (state.data.killGpuProcesses ? "warn" : "");
    $("gcuuVal").className = state.data.gcuUtil ? (state.data.killGpuProcesses ? "bad" : "good") : (state.data.killGpuProcesses ? "warn" : "");
    $("adminVal").className = state.data.admin ? "good" : "warn";

    $("homeFixes").textContent = String(state.data.fileResets ?? 0);
    $("homeKills").textContent = String(state.data.totalKills ?? 0);
    $("homeUptime").textContent = state.data.uptime || "00:00:00";

    $("statFixes").textContent = String(state.data.fileResets ?? 0);
    $("statKills").textContent = String(state.data.totalKills ?? 0);
    $("statUptime").textContent = state.data.uptime || "00:00:00";
    const hrs = Number(state.data.uptimeHours || 0);
    $("statAvg").textContent = hrs > 0 ? ((state.data.fileResets || 0) / hrs).toFixed(1) : "0.0";

    $("abPath").textContent = state.data.afterburnerPath || "未找到";
    $("ccPath").textContent = state.data.controlCenterPath || "未找到";
    $("profileVal").textContent = String(state.data.afterburnerProfile || 1);
    $("stableSecondsVal").textContent = String(state.data.stableSeconds || 15);

    $("autoStartSwitch").checked = !!state.data.autoStart;
    $("bootMinSwitch").checked = !!state.data.bootMin;
    $("minToTraySwitch").checked = !!state.data.minToTray;
    $("killProcSwitch").checked = !!state.data.killGpuProcesses;
  }

  function requestStatus() {
    post("getStatus");
  }

  navItems.forEach((btn) => {
    btn.addEventListener("click", () => {
      setPage(btn.dataset.page);
      post("pageChanged", { page: btn.dataset.page });
    });
  });

  actionButtons.forEach((btn) => {
    btn.addEventListener("click", () => {
      post(btn.dataset.action);
    });
  });

  stepButtons.forEach((btn) => {
    btn.addEventListener("click", () => {
      post("stepSetting", {
        setting: btn.dataset.step,
        delta: Number(btn.dataset.delta || 0)
      });
    });
  });

  settingInputs.forEach((input) => {
    input.addEventListener("change", () => {
      post("toggleSetting", {
        setting: input.dataset.setting,
        value: input.checked
      });
    });
  });

  $("minBtn").addEventListener("click", () => post("minimizeToTray"));
  $("closeBtn").addEventListener("click", () => post("closeApp"));

  $("langBtn").addEventListener("click", () => {
    state.lang = state.lang === "zh" ? "en" : "zh";
    $("langBtn").textContent = state.lang === "zh" ? "中文" : "EN";
    post("toggleLanguage", { lang: state.lang });
  });

  if (window.chrome && chrome.webview) {
    chrome.webview.addEventListener("message", (event) => {
      const msg = event.data || {};
      if (msg.type === "state") {
        render(msg.data || {});
      } else if (msg.type === "notify") {
        console.log(msg.message || "");
      }
    });
  }

  window.addEventListener("DOMContentLoaded", () => {
    setPage("home");
    requestStatus();
    post("ready");
  });

  window.__mrosd = {
    setPage,
    render,
    requestStatus
  };
})();