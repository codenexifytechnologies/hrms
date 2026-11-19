/* Sign-in and registration pages: highlight carousel, show/hide password, registration wizard, password strength. */
(function () {
  "use strict";
  var still = window.matchMedia && matchMedia("(prefers-reduced-motion: reduce)").matches;

  // ---- Rotating highlights in the brand panel -------------------------------------------
  document.querySelectorAll("[data-ax-slides]").forEach(function (box) {
    var slides = box.querySelectorAll(".ax-slide"), dots = box.querySelectorAll(".ax-dot"), i = 0, timer;
    function show(n) {
      i = n;
      slides.forEach(function (s, k) { s.classList.toggle("is-active", k === n); s.setAttribute("aria-hidden", k === n ? "false" : "true"); });
      dots.forEach(function (d, k) { d.classList.toggle("is-active", k === n); });
    }
    function start() { if (!still) timer = setInterval(function () { show((i + 1) % slides.length); }, 4500); }
    dots.forEach(function (d) { d.addEventListener("click", function () { clearInterval(timer); show(parseInt(d.dataset.axDot, 10)); start(); }); });
    start();
  });

  // ---- Show / hide password ---------------------------------------------------------------
  document.querySelectorAll("[data-ax-eye]").forEach(function (btn) {
    var input = btn.parentElement.querySelector("input");
    btn.addEventListener("click", function () {
      var show = input.type === "password";
      input.type = show ? "text" : "password";
      btn.setAttribute("aria-label", show ? "Hide password" : "Show password");
      btn.setAttribute("aria-pressed", show ? "true" : "false");
      btn.querySelector("i").className = "bi " + (show ? "bi-eye-slash" : "bi-eye");
    });
  });

  // ---- Password strength (length, cases, digits, symbols) ------------------------------------
  document.querySelectorAll("[data-ax-meter]").forEach(function (meter) {
    var input = document.getElementById(meter.dataset.axMeter), label = meter.nextElementSibling;
    var min = parseInt(meter.dataset.axMin || "8", 10);
    var words = ["", "Weak", "Fair", "Good", "Strong"];
    function score(v) {
      if (!v) return 0;
      var s = 0;
      if (v.length >= 8) s++;
      if (v.length >= 12) s++;
      if (/[a-z]/.test(v) && /[A-Z]/.test(v)) s++;
      if (/\d/.test(v) && /[^A-Za-z0-9]/.test(v)) s++;
      return Math.max(1, Math.min(4, s));
    }
    input.addEventListener("input", function () {
      var s = score(input.value);
      meter.dataset.score = s;
      if (label) label.textContent = input.value ? words[s] + (input.value.length < min ? " · at least " + min + " characters" : "") : "";
    });
  });

  // ---- "Must be ticked" rule for consent checkboxes (data-val-mustbetrue) --------------------------
  // Registered before jQuery's ready handler, when unobtrusive validation parses the forms.
  if (window.jQuery && jQuery.validator && jQuery.validator.unobtrusive) {
    jQuery.validator.addMethod("mustbetrue", function (value, element) { return element.checked; });
    jQuery.validator.unobtrusive.adapters.addBool("mustbetrue");
  }

  // ---- Registration wizard ---------------------------------------------------------------------
  var wizard = document.querySelector("[data-ax-wizard]");
  if (!wizard) return;
  wizard.classList.add("ax-wizard-on");
  var panes = Array.prototype.slice.call(wizard.querySelectorAll(".ax-pane")),
      pills = document.querySelectorAll(".ax-step-pill"),
      back = wizard.querySelector("[data-ax-back]"), next = wizard.querySelector("[data-ax-next]"), submit = wizard.querySelector("[data-ax-submit]"),
      current = 0;

  function go(n, backwards) {
    panes.forEach(function (p, k) { p.classList.toggle("is-current", k === n); p.classList.toggle("is-back", k === n && !!backwards); });
    pills.forEach(function (p, k) {
      p.classList.toggle("is-current", k === n);
      p.classList.toggle("is-done", k < n);
      var icon = p.querySelector("i");
      if (icon) icon.className = "bi " + (k < n ? "bi-check-circle-fill" : "bi-circle");
    });
    back.hidden = n === 0;
    next.hidden = n === panes.length - 1;
    submit.hidden = n !== panes.length - 1;
    current = n;
    var first = panes[n].querySelector("input:not([type=hidden]):not([type=radio]), select");
    if (first && document.activeElement && document.activeElement.tagName !== "BODY") first.focus({ preventScroll: true });
  }

  // A step is valid when jQuery unobtrusive validation (already on the page) passes for its fields.
  function stepValid(n) {
    var fields = panes[n].querySelectorAll("input, select, textarea");
    if (window.jQuery && jQuery(wizard).validate) {
      var ok = true;
      fields.forEach(function (f) { if (f.name && !jQuery(f).valid()) ok = false; });
      return ok;
    }
    return Array.prototype.every.call(fields, function (f) { return f.checkValidity(); });
  }

  next.addEventListener("click", function () { if (stepValid(current)) go(current + 1); });
  back.addEventListener("click", function () { go(current - 1, true); });
  // Enter in a field moves to the next step instead of submitting early.
  wizard.addEventListener("keydown", function (e) {
    if (e.key === "Enter" && current < panes.length - 1 && e.target.tagName === "INPUT") { e.preventDefault(); next.click(); }
  });

  // After a failed submit, open the first step that holds an error.
  var withError = panes.findIndex(function (p) { return p.querySelector(".input-validation-error, .field-validation-error"); });
  go(withError >= 0 ? withError : 0);
})();
