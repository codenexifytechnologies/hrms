/* Codenexify HRMS — shared client behaviour (no inline scripts: CSP friendly) */
(function () {
  "use strict";

  const storage = {
    get(k) { try { return localStorage.getItem(k); } catch { return null; } },
    set(k, v) { try { localStorage.setItem(k, v); } catch { /* storage unavailable */ } }
  };

  // ---- Theme (light / dark) -------------------------------------------------
  const root = document.documentElement;
  function applyTheme(theme) {
    root.setAttribute("data-bs-theme", theme);
    document.querySelectorAll("[data-theme-icon]").forEach(i => {
      i.className = theme === "dark" ? "bi bi-sun" : "bi bi-moon-stars";
    });
    document.dispatchEvent(new CustomEvent("cnx:theme", { detail: theme }));
  }
  const saved = storage.get("cnx-theme");
  applyTheme(saved || (window.matchMedia && matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light"));
  document.addEventListener("click", e => {
    if (e.target.closest("[data-toggle-theme]")) {
      const next = root.getAttribute("data-bs-theme") === "dark" ? "light" : "dark";
      storage.set("cnx-theme", next);
      applyTheme(next);
    }
  });

  // ---- Sidebar ---------------------------------------------------------------
  if (storage.get("cnx-sidebar") === "collapsed" && window.innerWidth >= 992) document.body.classList.add("sidebar-collapsed");
  document.addEventListener("click", e => {
    if (e.target.closest("[data-toggle-sidebar]")) {
      if (window.innerWidth < 992) {
        document.body.classList.toggle("sidebar-open");
      } else {
        document.body.classList.toggle("sidebar-collapsed");
        storage.set("cnx-sidebar", document.body.classList.contains("sidebar-collapsed") ? "collapsed" : "expanded");
      }
    }
    if (e.target.closest(".app-backdrop")) document.body.classList.remove("sidebar-open");
  });

  // (The active menu item and the menu's scroll position are handled early by nav.js.)

  // ---- Page loading bar: instant feedback when a link or form starts loading the next page ------------------
  const bar = document.createElement("div");
  bar.className = "page-progress";
  bar.setAttribute("aria-hidden", "true");
  document.body.appendChild(bar);
  let barTimer = 0;
  function startBar() {
    clearTimeout(barTimer);
    bar.classList.remove("is-done");
    bar.classList.add("is-loading");
    // Downloads (exports, payslips) never unload the page: hide the bar again after a while.
    barTimer = setTimeout(stopBar, 8000);
  }
  function stopBar() { clearTimeout(barTimer); bar.classList.remove("is-loading"); bar.classList.add("is-done"); }
  document.addEventListener("click", e => {
    const a = e.target.closest("a[href]");
    if (!a || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey || e.button > 0) return;
    const href = a.getAttribute("href") || "";
    if (a.target === "_blank" || a.hasAttribute("download") || href.startsWith("#") || href.startsWith("mailto:") || href.startsWith("tel:") || href.startsWith("javascript:")) return;
    if (a.origin !== location.origin || /\/(export|download|document|payslip|print)/i.test(a.pathname)) return;
    if (a.pathname === location.pathname && a.search === location.search && a.hash) return;
    // Wait until every other click handler has run (confirm dialogs, menus) and only start if the page really navigates.
    setTimeout(() => { if (!e.defaultPrevented) startBar(); }, 0);
  });
  document.addEventListener("submit", e => {
    const f = e.target;
    if (f.target === "_blank" || /\/(export|download)/i.test(f.getAttribute("action") || "")) return;
    setTimeout(() => { if (!e.defaultPrevented) startBar(); }, 0);
  });
  // Coming back with the browser's Back button can restore the old page from memory: reset the bar.
  window.addEventListener("pageshow", stopBar);

  // ---- Toasts ---------------------------------------------------------------
  window.cnxToast = function (message, type) {
    const container = document.getElementById("toastContainer");
    if (!container) return;
    const tone = { success: "text-bg-success", error: "text-bg-danger", warning: "text-bg-warning", info: "text-bg-primary" }[type] || "text-bg-dark";
    const el = document.createElement("div");
    el.className = `toast align-items-center border-0 ${tone}`;
    el.setAttribute("role", "status");
    el.innerHTML = '<div class="d-flex"><div class="toast-body"></div><button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button></div>';
    el.querySelector(".toast-body").textContent = message;
    container.appendChild(el);
    new bootstrap.Toast(el, { delay: 5000 }).show();
    el.addEventListener("hidden.bs.toast", () => el.remove());
  };
  document.querySelectorAll("[data-flash]").forEach(f => window.cnxToast(f.getAttribute("data-message"), f.getAttribute("data-flash")));

  // ---- Confirmation dialogs (forms / buttons with data-confirm) ---------------
  const modalEl = document.getElementById("confirmModal");
  let pendingForm = null, pendingSubmitter = null;
  document.addEventListener("submit", e => {
    const form = e.target;
    // The message can sit on the form or on the button that was clicked.
    const source = e.submitter && e.submitter.hasAttribute("data-confirm") ? e.submitter : form;
    const msg = source.getAttribute("data-confirm");
    if (!msg || form.dataset.confirmed === "1" || !modalEl) return;
    e.preventDefault();
    pendingForm = form;
    pendingSubmitter = e.submitter || null;
    modalEl.querySelector("[data-confirm-text]").textContent = msg;
    const danger = source.hasAttribute("data-confirm-danger");
    const ok = modalEl.querySelector("[data-confirm-ok]");
    ok.className = "btn " + (danger ? "btn-danger" : "btn-accent");
    bootstrap.Modal.getOrCreateInstance(modalEl).show();
  });
  if (modalEl) {
    modalEl.querySelector("[data-confirm-ok]").addEventListener("click", () => {
      if (!pendingForm) return;
      pendingForm.dataset.confirmed = "1";
      bootstrap.Modal.getOrCreateInstance(modalEl).hide();
      // Re-send with the same button so its name/value (e.g. approve=true) is kept.
      if (pendingForm.requestSubmit) pendingForm.requestSubmit(pendingSubmitter && pendingSubmitter.form === pendingForm ? pendingSubmitter : undefined);
      else pendingForm.submit();
    });
  }

  // ---- Prevent double submit + button spinners -------------------------------
  document.addEventListener("submit", e => {
    const form = e.target;
    if (e.defaultPrevented) return;
    if (window.jQuery && jQuery(form).valid && jQuery(form).data("validator") && !jQuery(form).valid()) return;
    // Disabled buttons aren't sent, so keep the clicked button's name/value (e.g. "Save draft" vs "Submit") in a hidden field.
    const sub = e.submitter;
    if (sub && sub.name && !form.querySelector(`input[type=hidden][data-submitter][name="${CSS.escape(sub.name)}"]`)) {
      const keep = document.createElement("input");
      keep.type = "hidden"; keep.name = sub.name; keep.value = sub.value; keep.dataset.submitter = "";
      form.appendChild(keep);
    } else if (sub && sub.name) {
      form.querySelector(`input[type=hidden][data-submitter][name="${CSS.escape(sub.name)}"]`).value = sub.value;
    }
    form.querySelectorAll("button[type=submit]").forEach(b => {
      b.disabled = true;
      if (!b.querySelector(".spinner-border")) b.insertAdjacentHTML("afterbegin", '<span class="spinner-border spinner-border-sm me-1"></span>');
    });
  });

  // ---- Auto-submit filters ----------------------------------------------------
  document.querySelectorAll("[data-autosubmit]").forEach(el => el.addEventListener("change", () => el.form && el.form.submit()));

  // ---- GPS attendance: attach the device location to the punch (only on forms the server marked) ----
  document.querySelectorAll("form[data-geo-punch]").forEach(form => {
    const status = form.querySelector("[data-geo-status]");
    let located = false;
    form.addEventListener("submit", e => {
      if (located || !navigator.geolocation) return;
      e.preventDefault();
      const button = form.querySelector("button[type=submit]");
      button.disabled = true;
      if (status) status.textContent = "Getting your location…";
      const send = () => { located = true; button.disabled = false; form.requestSubmit(button); };
      navigator.geolocation.getCurrentPosition(pos => {
        form.elements.latitude.value = pos.coords.latitude.toFixed(6);
        form.elements.longitude.value = pos.coords.longitude.toFixed(6);
        form.elements.accuracy.value = Math.round(pos.coords.accuracy);
        send();
      }, () => {
        // Without a location the server decides: recorded mode still punches; enforced mode explains why it can't.
        if (status) status.textContent = "Location not available.";
        send();
      }, { enableHighAccuracy: true, timeout: 10000, maximumAge: 30000 });
    });
  });

  // ---- Selfie clock-in: take a camera photo before the punch is sent -------------
  document.querySelectorAll("form[data-selfie-punch]").forEach(form => {
    const modalEl = document.getElementById("selfieModal");
    if (!modalEl || !window.bootstrap) return;
    const video = modalEl.querySelector("[data-selfie-video]");
    const status = modalEl.querySelector("[data-selfie-status]");
    const capture = modalEl.querySelector("[data-selfie-capture]");
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    let stream = null;
    const stop = () => { if (stream) stream.getTracks().forEach(t => t.stop()); stream = null; video.srcObject = null; };
    modalEl.addEventListener("hidden.bs.modal", stop);

    form.addEventListener("submit", e => {
      if (form.elements.selfie.value) return; // photo taken: let the punch go
      e.preventDefault();
      if (modalEl.classList.contains("show")) return;
      capture.disabled = true;
      status.textContent = "Starting the camera…";
      modal.show();
      if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) { status.textContent = "This browser can't use the camera. Use the mobile app or another browser."; return; }
      navigator.mediaDevices.getUserMedia({ video: { facingMode: "user", width: { ideal: 640 }, height: { ideal: 480 } }, audio: false })
        .then(s => { stream = s; video.srcObject = s; status.textContent = "Look at the camera, then take the photo."; capture.disabled = false; })
        .catch(() => { status.textContent = "Camera access was blocked. Allow the camera for this site and try again."; });
    });

    capture.addEventListener("click", () => {
      if (!video.videoWidth) return;
      // Downscale to at most 480 px wide and compress, keeping the upload small.
      const scale = Math.min(1, 480 / video.videoWidth);
      const canvas = document.createElement("canvas");
      canvas.width = Math.round(video.videoWidth * scale);
      canvas.height = Math.round(video.videoHeight * scale);
      canvas.getContext("2d").drawImage(video, 0, 0, canvas.width, canvas.height);
      form.elements.selfie.value = canvas.toDataURL("image/jpeg", 0.75);
      modal.hide();
      form.requestSubmit();
    });
  });

  // ---- HR: fill an office location from the current device position --------------
  document.querySelectorAll("form[data-geo-fill] [data-geo-here]").forEach(btn => btn.addEventListener("click", () => {
    const form = btn.closest("form");
    if (!navigator.geolocation) { btn.textContent = "Location not available"; return; }
    btn.disabled = true;
    navigator.geolocation.getCurrentPosition(pos => {
      form.querySelector("[data-geo-lat]").value = pos.coords.latitude.toFixed(6);
      form.querySelector("[data-geo-lng]").value = pos.coords.longitude.toFixed(6);
      btn.disabled = false;
    }, () => { btn.disabled = false; btn.textContent = "Location blocked"; }, { enableHighAccuracy: true, timeout: 10000 });
  }));

  // ---- <input type="month"> feeding separate Year/Month fields -----------------
  document.querySelectorAll("[data-month-split]").forEach(input => {
    const form = input.form;
    const sync = () => {
      const [y, m] = (input.value || "").split("-");
      if (!y || !m) return;
      form.querySelector("[data-month-year]").value = y;
      form.querySelector("[data-month-month]").value = String(parseInt(m, 10));
    };
    input.addEventListener("change", sync);
    sync();
  });

  // ---- Platform announcements: dismissal remembered per browser ----------------
  const dismissedKey = id => "cnx.announcement." + id;
  document.querySelectorAll("[data-announcement]").forEach(el => {
    try { if (localStorage.getItem(dismissedKey(el.dataset.announcement)) && el.querySelector("[data-dismiss-announcement]")) el.remove(); } catch { /* storage unavailable */ }
  });
  document.querySelectorAll("[data-dismiss-announcement]").forEach(btn => btn.addEventListener("click", () => {
    try { localStorage.setItem(dismissedKey(btn.dataset.dismissAnnouncement), "1"); } catch { /* storage unavailable */ }
    btn.closest("[data-announcement]")?.remove();
  }));

  // ---- Modals live under <body>: the animated content area forms its own stacking
  // context, which would otherwise put a modal underneath its backdrop. ---------
  document.querySelectorAll(".app-content .modal").forEach(m => document.body.appendChild(m));

  // ---- Bootstrap modals that carry an id into a form ---------------------------
  document.addEventListener("show.bs.modal", e => {
    const trigger = e.relatedTarget;
    if (!trigger) return;
    Object.entries(trigger.dataset).forEach(([k, v]) => {
      if (!k.startsWith("set")) return;
      const name = k.substring(3).replace(/^./, c => c.toLowerCase());
      if (name === "action") {
        const form = e.target.querySelector("form[data-bind-action]") || (e.target.matches("form") ? e.target : null);
        if (form) form.setAttribute("action", v);
        return;
      }
      e.target.querySelectorAll(`[data-bind="${name}"]`).forEach(t => {
        if ("value" in t && t.tagName !== "SPAN" && t.tagName !== "STRONG") t.value = v; else t.textContent = v;
      });
    });
  });

  // ---- Open the tab named in the URL hash (e.g. #subscription) -----------------
  if (location.hash) {
    const tabBtn = document.querySelector(`[data-bs-toggle="tab"][data-bs-target="${CSS.escape(location.hash)}"]`);
    if (tabBtn) bootstrap.Tab.getOrCreateInstance(tabBtn).show();
  }
  document.querySelectorAll('[data-bs-toggle="tab"]').forEach(btn => btn.addEventListener("shown.bs.tab", () => {
    history.replaceState(null, "", btn.getAttribute("data-bs-target"));
  }));

  // ---- Leave form: half-day is a single date and needs a session -------------------
  document.querySelectorAll("[data-leave-half]").forEach(half => {
    const scope = half.closest("form");
    const from = scope.querySelector("[data-leave-from]");
    const to = scope.querySelector("[data-leave-to]");
    const session = scope.querySelector("[data-leave-session]");
    const type = scope.querySelector("[data-leave-type]");
    const sync = () => {
      const opt = type && type.selectedOptions[0];
      const halfAllowed = !opt || !opt.value || opt.dataset.half === "1";
      half.disabled = !halfAllowed;
      if (!halfAllowed) half.checked = false;
      to.readOnly = half.checked;
      if (half.checked) to.value = from.value;
      session.disabled = !half.checked;
      if (half.checked && !session.value) session.value = session.options[1] ? session.options[1].value : "";
      if (!half.checked) session.value = "";
    };
    [half, from, type].forEach(el => el && el.addEventListener("change", sync));
    from.addEventListener("change", () => { if (to.value < from.value) to.value = from.value; });
    sync();

    // Live "uses N working days" preview (weekly offs and holidays excluded server-side).
    const previewUrl = scope.getAttribute("data-leave-preview");
    const out = scope.querySelector("[data-leave-days]");
    if (previewUrl && out) {
      const refresh = async () => {
        if (!type.value || !from.value || !to.value) { out.textContent = ""; return; }
        const p = new URLSearchParams();
        scope.querySelectorAll("[name]").forEach(el => {
          if (el.name.startsWith("application.") && !el.disabled && (el.type !== "checkbox" || el.checked)) p.set(el.name, el.type === "checkbox" ? "true" : el.value);
        });
        try {
          const r = await fetch(previewUrl + "?" + p.toString(), { headers: { Accept: "application/json" } });
          const j = await r.json();
          out.textContent = j.days > 0 ? `Uses ${j.days} working day(s).` : "These dates don't need leave or aren't valid for this leave type.";
          out.className = "col-12 small " + (j.days > 0 ? "text-muted" : "text-warning");
        } catch { out.textContent = ""; }
      };
      [type, from, to, half, session].forEach(el => el && el.addEventListener("change", refresh));
    }
  });

  // ---- Print buttons (payslips, settlements) -----------------------------------
  document.addEventListener("click", e => { if (e.target.closest("[data-print]")) window.print(); });

  // ---- Salary breakup preview (structure + annual gross → components & CTC) ----
  document.querySelectorAll("[data-salary-preview]").forEach(form => {
    const out = form.querySelector("[data-preview-out]");
    const structure = form.querySelector("[name$='SalaryStructureId']");
    const gross = form.querySelector("[name$='AnnualGross']");
    const pf = form.querySelector("[name$='PfApplicable'][type=checkbox]");
    const fmt = v => Number(v).toLocaleString("en-IN", { maximumFractionDigits: 0 });
    const refresh = async () => {
      if (!structure.value || !(Number(gross.value) > 0)) { out.innerHTML = ""; return; }
      const url = form.getAttribute("data-salary-preview") + `?structureId=${encodeURIComponent(structure.value)}&annualGross=${encodeURIComponent(gross.value)}&pfApplicable=${pf ? pf.checked : true}`;
      try {
        const j = await (await fetch(url)).json();
        if (j.error) { out.textContent = j.error; out.className = "small text-danger"; return; }
        out.className = "small";
        out.replaceChildren();
        const table = document.createElement("table");
        table.className = "table table-sm mb-0";
        const add = (label, value, bold) => {
          const tr = table.insertRow();
          const a = tr.insertCell(); a.textContent = label;
          const b = tr.insertCell(); b.textContent = "₹" + fmt(value); b.className = "text-end";
          if (bold) tr.className = "fw-semibold";
        };
        j.components.forEach(c => add(c.name + (c.type === "Deduction" ? " (deduction)" : ""), c.monthly));
        add("Monthly gross", j.monthlyGross, true);
        if (j.employerPf) add("Employer PF + EDLI + admin", j.employerPf);
        if (j.employerEsi) add("Employer ESI", j.employerEsi);
        add("Monthly CTC", j.monthlyCtc, true);
        add("Annual CTC", j.annualCtc, true);
        out.appendChild(table);
      } catch { out.textContent = ""; }
    };
    [structure, gross, pf].forEach(el => el && el.addEventListener("change", refresh));
    gross.addEventListener("input", () => { clearTimeout(gross._t); gross._t = setTimeout(refresh, 400); });
    document.addEventListener("show.bs.modal", () => setTimeout(refresh, 50));
  });

  // ---- Tooltips ---------------------------------------------------------------
  document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(el => new bootstrap.Tooltip(el));

  // ---- Company code availability (registration) -------------------------------
  const codeInput = document.querySelector("[data-code-check]");
  if (codeInput) {
    const out = document.getElementById("codeStatus");
    let timer;
    const nameInput = document.querySelector("[data-code-source]");
    if (nameInput) nameInput.addEventListener("input", () => {
      if (codeInput.dataset.touched) return;
      codeInput.value = nameInput.value.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "").substring(0, 30);
      codeInput.dispatchEvent(new Event("input"));
    });
    codeInput.addEventListener("keydown", () => { codeInput.dataset.touched = "1"; });
    codeInput.addEventListener("input", () => {
      clearTimeout(timer);
      const v = codeInput.value.trim();
      if (v.length < 3) { out.textContent = ""; return; }
      timer = setTimeout(async () => {
        try {
          const r = await fetch(codeInput.getAttribute("data-code-check") + "?code=" + encodeURIComponent(v));
          const j = await r.json();
          out.textContent = j.available ? "✓ Available" : "✗ Not available";
          out.className = "small " + (j.available ? "text-success" : "text-danger");
        } catch { out.textContent = ""; }
      }, 350);
    });
  }
})();
