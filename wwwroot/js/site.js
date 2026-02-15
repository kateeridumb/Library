(function () {
    const storageKey = 'siteTheme';
    const toggleBtn = document.getElementById('siteThemeToggle');
    const icon = document.getElementById('siteThemeIcon');
    const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;

    function applyTheme(theme) {
        document.body.classList.toggle('dark-theme', theme === 'dark');
        if (icon) {
            icon.className = theme === 'dark' ? 'fas fa-sun' : 'fas fa-moon';
        }
    }

    let theme = localStorage.getItem(storageKey);
    if (!theme) {
        theme = prefersDark ? 'dark' : 'light';
    }
    applyTheme(theme);

    toggleBtn?.addEventListener('click', () => {
        const nextTheme = document.body.classList.contains('dark-theme') ? 'light' : 'dark';
        localStorage.setItem(storageKey, nextTheme);
        applyTheme(nextTheme);
    });
})();
