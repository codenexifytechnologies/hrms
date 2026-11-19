/* Sidebar menu: highlight the current page and keep the menu where the user scrolled it.
   Loaded right after the sidebar (before the page content) so it applies before the first paint: no jump, no delay. */
(function () {
  "use strict";
  var nav = document.querySelector(".app-sidebar .app-nav");
  if (!nav) return;
  // Restore a collapsed sidebar before the first paint (app.js, loaded last, would make it flash open).
  try { if (localStorage.getItem("cnx-sidebar") === "collapsed" && window.innerWidth >= 992) document.body.classList.add("sidebar-collapsed"); } catch (e) { /* ignore */ }
  var key = "cnx-nav-scroll:" + (document.body.classList.contains("portal-platform") ? "platform" : "org");
  function save() { try { sessionStorage.setItem(key, String(nav.scrollTop)); } catch (e) { /* storage unavailable */ } }

  // ---- Highlight: the menu link that best matches the current address (longest matching prefix wins).
  var path = location.pathname.toLowerCase().replace(/\/+$/, "") || "/";
  var best = null;
  var bestLength = -1;
  function clean(h) { return (h || "").toLowerCase().split(/[?#]/)[0].replace(/\/+$/, "") || "/"; }
  nav.querySelectorAll("a[href]").forEach(function (a) {
    var href = clean(a.getAttribute("href"));
    if (href.charAt(0) !== "/") return;
    // Portal home links (/org, /me, /superadmin) match only themselves; deeper links also match their sub-pages.
    var isRoot = href.split("/").length <= 2;
    // data-nav-match: another exact address for the same page (e.g. the dashboard is both /org and /org/dashboard).
    var alias = a.getAttribute("data-nav-match");
    var hit = path === href || (!isRoot && path.indexOf(href + "/") === 0) || (alias && path === clean(alias));
    if (hit && href.length > bestLength) { best = a; bestLength = href.length; }
  });
  if (best) { best.classList.add("active"); best.setAttribute("aria-current", "page"); }

  // ---- Keep the menu's scroll position between pages (per browser tab).
  try { var saved = sessionStorage.getItem(key); if (saved !== null) nav.scrollTop = parseInt(saved, 10) || 0; } catch (e) { /* ignore */ }
  // ...and make sure the highlighted item is visible (e.g. arriving from a link elsewhere on the page).
  if (best) {
    var n = nav.getBoundingClientRect(), r = best.getBoundingClientRect();
    if (r.top < n.top + 8 || r.bottom > n.bottom - 8) nav.scrollTop += (r.top - n.top) - (nav.clientHeight - r.height) / 2;
  }

  var pending = 0;
  nav.addEventListener("scroll", function () { if (!pending) pending = requestAnimationFrame(function () { pending = 0; save(); }); }, { passive: true });

  // ---- Clicking a menu item highlights it at once (the next page then keeps it highlighted).
  nav.addEventListener("click", function (e) {
    var a = e.target.closest("a[href]");
    if (!a || e.ctrlKey || e.metaKey || e.shiftKey || e.button > 0) return;
    nav.querySelectorAll("a.active").forEach(function (x) { x.classList.remove("active"); x.removeAttribute("aria-current"); });
    a.classList.add("active");
    a.setAttribute("aria-current", "page");
    save();
    if (window.innerWidth < 992) document.body.classList.remove("sidebar-open");
  });
})();
