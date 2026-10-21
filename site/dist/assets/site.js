(() => {
  'use strict';
  const root = document.documentElement;
  const system = matchMedia('(prefers-color-scheme: dark)');
  let preference = root.dataset.preference || 'system';
  const applyTheme = value => {
    preference = ['system', 'light', 'dark'].includes(value) ? value : 'system';
    const theme = preference === 'system' ? (system.matches ? 'dark' : 'light') : preference;
    root.dataset.theme = theme;
    root.dataset.preference = preference;
    document.querySelectorAll('[data-theme-picker]').forEach(picker => { picker.value = preference; });
    document.querySelectorAll('[data-theme-image]').forEach(image => { image.src = image.dataset[theme]; });
    try { localStorage.setItem('restart-site-theme', preference); } catch { /* Theme changes work without browser storage. */ }
  };
  document.querySelectorAll('[data-theme-picker]').forEach(picker => picker.addEventListener('change', () => applyTheme(picker.value)));
  system.addEventListener('change', () => { if (preference === 'system') applyTheme('system'); });
  applyTheme(preference);

  const toggle = document.querySelector('.menu-toggle');
  const navigation = document.querySelector('#primary-nav');
  toggle?.addEventListener('click', () => {
    const expanded = toggle.getAttribute('aria-expanded') !== 'true';
    toggle.setAttribute('aria-expanded', String(expanded)); navigation.classList.toggle('is-open', expanded);
  });
  navigation?.addEventListener('click', event => {
    if (event.target.closest('a')) { toggle?.setAttribute('aria-expanded', 'false'); navigation.classList.remove('is-open'); }
  });
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && navigation?.classList.contains('is-open')) {
      toggle.setAttribute('aria-expanded', 'false'); navigation.classList.remove('is-open'); toggle.focus();
    }
  });

  document.querySelectorAll('[data-tabs]').forEach(group => {
    const tabs = [...group.querySelectorAll('[role="tab"]')];
    const activate = (tab, focus = false) => {
      tabs.forEach(item => {
        const selected = item === tab;
        item.setAttribute('aria-selected', String(selected)); item.tabIndex = selected ? 0 : -1;
        document.getElementById(item.getAttribute('aria-controls')).hidden = !selected;
      });
      if (focus) tab.focus();
    };
    tabs.forEach((tab, index) => {
      tab.addEventListener('click', () => activate(tab));
      tab.addEventListener('keydown', event => {
        let next;
        if (event.key === 'ArrowRight' || event.key === 'ArrowDown') next = (index + 1) % tabs.length;
        else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') next = (index - 1 + tabs.length) % tabs.length;
        else if (event.key === 'Home') next = 0;
        else if (event.key === 'End') next = tabs.length - 1;
        if (next !== undefined) { event.preventDefault(); activate(tabs[next], true); }
      });
    });
    activate(tabs.find(tab => tab.getAttribute('aria-selected') === 'true') || tabs[0]);
  });

  const dialog = document.querySelector('.screenshot-dialog');
  const open = document.querySelector('[data-open-screenshot]');
  const close = document.querySelector('[data-close-screenshot]');
  open?.addEventListener('click', () => {
    if (dialog.showModal) dialog.showModal(); else location.href = dialog.querySelector('img').src;
  });
  close?.addEventListener('click', () => dialog.close());
  dialog?.addEventListener('click', event => { if (event.target === dialog) dialog.close(); });
  dialog?.addEventListener('close', () => open?.focus());
})();
