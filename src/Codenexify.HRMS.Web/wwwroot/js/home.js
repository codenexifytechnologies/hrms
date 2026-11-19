/* Public home page: rotating headline word, scroll reveals and number counters. */
(function () {
  "use strict";
  var still = window.matchMedia && matchMedia("(prefers-reduced-motion: reduce)").matches;

  // ---- Rotating word in the headline -------------------------------------------
  document.querySelectorAll("[data-rotate]").forEach(function (el) {
    var words = el.dataset.rotate.split("|"), i = 0, span = el.querySelector(".hx-word");
    if (still || !span || words.length < 2) return;
    setInterval(function () {
      span.classList.remove("hx-in");
      span.classList.add("hx-out");
      setTimeout(function () {
        i = (i + 1) % words.length;
        span.textContent = words[i];
        span.classList.remove("hx-out");
        span.classList.add("hx-in");
      }, 340);
    }, 2400);
  });

  // ---- Count a number up from zero when it scrolls into view ----------------------
  function countUp(el) {
    var target = parseInt(el.dataset.count, 10);
    if (!target || still) return;
    var start = null, dur = 1400, fmt = new Intl.NumberFormat("en-IN");
    function step(t) {
      if (start === null) start = t;
      var p = Math.min(1, (t - start) / dur), eased = 1 - Math.pow(1 - p, 3);
      el.textContent = fmt.format(Math.round(target * eased));
      if (p < 1) requestAnimationFrame(step);
    }
    el.textContent = "0";
    requestAnimationFrame(step);
  }

  // ---- Reveal sections as they scroll into view ------------------------------------
  // Content is visible by default (CSS only hides it once the html element has .hx-reveal), so nothing stays blank
  // while scripts download or if this file fails to load.
  var targets = document.querySelectorAll("[data-reveal]");
  function show(el) {
    el.classList.add("is-in");
    el.querySelectorAll("[data-count]").forEach(countUp);
  }
  if (still || !("IntersectionObserver" in window)) {
    targets.forEach(function (el) { el.classList.add("is-in"); });
    return;
  }
  // Whatever is already on screen (or above it, e.g. after following #compare) shows at once, without a flash.
  var viewport = window.innerHeight || document.documentElement.clientHeight;
  var pending = [];
  targets.forEach(function (el) {
    if (el.getBoundingClientRect().top < viewport) show(el); else pending.push(el);
  });
  document.documentElement.classList.add("hx-reveal");
  // Reveal as soon as the top edge enters the screen. A share-of-area threshold never triggers for tall blocks such
  // as the module comparison table (on a phone, 18% of it is taller than the screen).
  var io = new IntersectionObserver(function (entries) {
    entries.forEach(function (e) {
      if (!e.isIntersecting) return;
      show(e.target);
      io.unobserve(e.target);
    });
  }, { threshold: 0, rootMargin: "0px 0px -6% 0px" });
  pending.forEach(function (el) { io.observe(el); });
})();
