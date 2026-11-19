// Loaded synchronously in <head> to apply the saved theme before first paint.
(function () {
  var t = null;
  try { t = localStorage.getItem("cnx-theme"); } catch (e) { /* storage unavailable */ }
  if (!t) t = window.matchMedia && matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
  document.documentElement.setAttribute("data-bs-theme", t);
})();
