(() => {
  'use strict';
  const root = document.documentElement;
  const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
  const finePointer = matchMedia('(hover: hover) and (pointer: fine)');
  const toggle = document.querySelector('[data-motion-toggle]');
  let enabled = true;
  try { enabled = localStorage.getItem('restart-site-motion') !== 'off'; } catch { /* Optional preference. */ }

  // Reveal real content once. Without this script, everything remains visible.
  const revealGroups = [
    ['.hero-copy > *, .developer-hero > *', 65],
    ['.hero-product', 0],
    ['.facts > span', 55],
    ['.section-heading > *, .live-copy > *, .demo-heading > *', 55],
    ['.feature-tabs, .live-art, .video-frame, .download-grid > div, .faq-grid > div:first-child', 0],
    ['.install-steps > li, .faq-list > details, .support-section > *, .developer-support > *', 65],
    ['.developer-section > h2, .developer-section > p, .developer-pair, .developer-section > .code-panel, .table-scroll, .callout, .resource-list', 0]
  ];
  for (const [selector, stagger] of revealGroups) {
    document.querySelectorAll(selector).forEach((element, index) => {
      element.dataset.reveal = '';
      element.style.setProperty('--reveal-delay', `${Math.min(index % 7, 5) * stagger}ms`);
    });
  }
  const reveals = [...document.querySelectorAll('[data-reveal]')];
  const observer = 'IntersectionObserver' in window ? new IntersectionObserver(entries => {
    for (const entry of entries) {
      if (!entry.isIntersecting) continue;
      entry.target.classList.add('is-visible');
      observer.unobserve(entry.target);
    }
  }, { threshold: 0.08, rootMargin: '0px 0px -24px 0px' }) : null;

  const applyMotion = () => {
    const active = enabled && !reducedMotion.matches;
    root.dataset.motion = active ? 'on' : 'off';
    root.classList.toggle('motion-ready', Boolean(active && observer));
    if (active) reveals.forEach(element => { if (!element.classList.contains('is-visible')) observer?.observe(element); });
    else { observer?.disconnect(); resetTilt(); }
    if (toggle) {
      toggle.hidden = false;
      toggle.setAttribute('aria-pressed', String(active));
      toggle.querySelector('span').textContent = reducedMotion.matches ? 'Reduced motion' : `Motion ${active ? 'on' : 'off'}`;
      toggle.disabled = reducedMotion.matches;
      toggle.title = reducedMotion.matches ? 'Following your system’s reduced-motion setting' : 'Turn decorative page motion on or off';
    }
  };
  toggle?.addEventListener('click', () => {
    enabled = !enabled;
    try { localStorage.setItem('restart-site-motion', enabled ? 'on' : 'off'); } catch { /* Optional preference. */ }
    applyMotion();
  });
  reducedMotion.addEventListener('change', applyMotion);
  document.addEventListener('focusin', event => {
    // Keyboard users never have to wait for a scroll entrance.
    for (let element = event.target; element instanceof HTMLElement; element = element.parentElement) {
      if (element.hasAttribute('data-reveal')) element.classList.add('is-visible');
    }
  });

  const product = document.querySelector('.hero-product');
  let tiltFrame = 0;
  let pointerX = 0;
  let pointerY = 0;
  function resetTilt() {
    cancelAnimationFrame(tiltFrame);
    tiltFrame = 0;
    product?.style.removeProperty('--tilt-x');
    product?.style.removeProperty('--tilt-y');
    product?.style.removeProperty('--light-x');
    product?.style.removeProperty('--light-y');
  }
  product?.addEventListener('pointermove', event => {
    if (root.dataset.motion !== 'on' || !finePointer.matches || event.pointerType === 'touch') return;
    const bounds = product.getBoundingClientRect();
    pointerX = (event.clientX - bounds.left) / bounds.width;
    pointerY = (event.clientY - bounds.top) / bounds.height;
    if (tiltFrame) return;
    tiltFrame = requestAnimationFrame(() => {
      tiltFrame = 0;
      product.style.setProperty('--tilt-x', `${(0.5 - pointerY) * 4}deg`);
      product.style.setProperty('--tilt-y', `${(pointerX - 0.5) * 5}deg`);
      product.style.setProperty('--light-x', `${pointerX * 100}%`);
      product.style.setProperty('--light-y', `${pointerY * 100}%`);
    });
  });
  product?.addEventListener('pointerleave', resetTilt);
  finePointer.addEventListener('change', resetTilt);

  const header = document.querySelector('.site-header');
  let scrollFrame = 0;
  let scrollRange = 1;
  const updateScroll = () => {
    scrollFrame = 0;
    const progress = Math.max(0, Math.min(1, window.scrollY / scrollRange));
    root.style.setProperty('--reading-progress', progress);
    header?.classList.toggle('is-scrolled', window.scrollY > 20);
  };
  const measure = () => {
    scrollRange = Math.max(1, root.scrollHeight - innerHeight);
    updateScroll();
  };
  addEventListener('scroll', () => { if (!scrollFrame) scrollFrame = requestAnimationFrame(updateScroll); }, { passive: true });
  addEventListener('resize', measure, { passive: true });
  if ('ResizeObserver' in window) new ResizeObserver(measure).observe(document.body);
  addEventListener('pageshow', measure);
  measure();
  applyMotion();
})();
