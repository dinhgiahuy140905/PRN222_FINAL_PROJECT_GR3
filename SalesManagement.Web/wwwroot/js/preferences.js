(() => {
    const root = document.documentElement;
    const body = document.body;
    const themeMedia = window.matchMedia("(prefers-color-scheme: dark)");
    const validThemes = new Set(["light", "dark", "system"]);

    const resolvedTheme = preference =>
        preference === "system" ? (themeMedia.matches ? "dark" : "light") : preference;

    const syncThemeControls = () => {
        const preference = root.dataset.themePreference || "system";
        const theme = root.dataset.theme || resolvedTheme(preference);
        document.querySelectorAll("[data-theme-choice]").forEach(input => {
            input.checked = input.value === preference;
        });
        const toggle = document.getElementById("themeToggle");
        if (toggle) {
            const targetLabel = theme === "dark" ? "Chế độ sáng" : "Chế độ tối";
            toggle.dataset.tooltip = targetLabel;
            toggle.title = targetLabel;
            toggle.setAttribute("aria-label", targetLabel);
        }
    };

    const setThemePreference = (preference, persist = true) => {
        if (!validThemes.has(preference)) preference = "system";
        const theme = resolvedTheme(preference);
        root.dataset.themePreference = preference;
        root.dataset.theme = theme;
        if (persist) localStorage.setItem("saleshub-theme", preference);
        syncThemeControls();
        window.dispatchEvent(new CustomEvent("saleshub:themechange", {
            detail: { preference, theme }
        }));
    };

    const syncSidebarControls = () => {
        const collapsed = root.dataset.sidebar === "collapsed";
        body.classList.toggle("sidebar-collapsed", collapsed);
        document.querySelectorAll("[data-sidebar-choice]").forEach(input => {
            input.checked = input.value === (collapsed ? "collapsed" : "expanded");
        });
        document.getElementById("sidebarCollapse")?.setAttribute("aria-expanded", (!collapsed).toString());
        const brand = document.getElementById("sidebarBrand");
        if (brand) {
            brand.title = collapsed ? "Mở menu" : "SalesHub";
            brand.setAttribute("aria-label", collapsed ? "Mở menu" : "SalesHub - Dashboard");
        }
    };

    const setSidebarCollapsed = (collapsed, persist = true) => {
        root.dataset.sidebar = collapsed ? "collapsed" : "expanded";
        if (persist) localStorage.setItem("saleshub-sidebar-collapsed", collapsed.toString());
        syncSidebarControls();
        window.dispatchEvent(new CustomEvent("saleshub:sidebarchange", {
            detail: { collapsed }
        }));
    };

    const setDrawer = open => {
        body.classList.toggle("sidebar-open", open);
        document.getElementById("menuToggle")?.setAttribute("aria-expanded", open.toString());
    };

    const sidebarTooltip = document.createElement("div");
    sidebarTooltip.className = "sidebar-tooltip";
    sidebarTooltip.setAttribute("role", "tooltip");
    document.body.appendChild(sidebarTooltip);
    const showTooltip = element => {
        if (window.innerWidth < 768 || root.dataset.sidebar !== "collapsed") return;
        const rect = element.getBoundingClientRect();
        sidebarTooltip.textContent = element.dataset.tooltip || "";
        sidebarTooltip.style.left = (rect.right + 10) + "px";
        sidebarTooltip.style.top = (rect.top + rect.height / 2) + "px";
        sidebarTooltip.classList.add("show");
    };
    const hideTooltip = () => sidebarTooltip.classList.remove("show");
    document.querySelectorAll(".sidebar [data-tooltip]").forEach(element => {
        element.addEventListener("mouseenter", () => showTooltip(element));
        element.addEventListener("mouseleave", hideTooltip);
        element.addEventListener("focus", () => showTooltip(element));
        element.addEventListener("blur", hideTooltip);
    });

    document.getElementById("themeToggle")?.addEventListener("click", () => {
        setThemePreference(root.dataset.theme === "dark" ? "light" : "dark");
    });
    document.querySelectorAll("[data-theme-choice]").forEach(input =>
        input.addEventListener("change", () => setThemePreference(input.value)));

    document.getElementById("sidebarCollapse")?.addEventListener("click", () =>
        setSidebarCollapsed(true));
    document.getElementById("sidebarBrand")?.addEventListener("click", event => {
        if (window.innerWidth >= 768 && root.dataset.sidebar === "collapsed") {
            event.preventDefault();
            setSidebarCollapsed(false);
        }
    });
    document.querySelectorAll("[data-sidebar-choice]").forEach(input =>
        input.addEventListener("change", () => setSidebarCollapsed(input.value === "collapsed")));

    document.getElementById("menuToggle")?.addEventListener("click", () =>
        setDrawer(!body.classList.contains("sidebar-open")));
    document.getElementById("sidebarBackdrop")?.addEventListener("click", () => setDrawer(false));
    window.addEventListener("resize", () => {
        if (window.innerWidth >= 768) setDrawer(false);
    });
    themeMedia.addEventListener("change", () => {
        if (root.dataset.themePreference === "system") setThemePreference("system", false);
    });

    setThemePreference(root.dataset.themePreference || "system", false);
    syncSidebarControls();

    window.SalesHubPreferences = {
        setTheme: setThemePreference,
        setSidebarCollapsed
    };
})();
