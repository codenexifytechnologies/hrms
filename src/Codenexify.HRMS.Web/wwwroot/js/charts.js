/* Renders <canvas data-chart="bar|line|doughnut" data-source="jsonScriptId" data-label="Series"> */
(function () {
  "use strict";
  if (!window.Chart) return;
  const charts = [];
  // Brand blue first, then the navy family and supporting hues that stay distinguishable in both themes.
  const palette = ["#1F5EFF", "#14b8a6", "#f59e0b", "#ef4444", "#5B8CFF", "#8b5cf6", "#10b981", "#64748b"];

  function themeColors() {
    const dark = document.documentElement.getAttribute("data-bs-theme") === "dark";
    return { text: dark ? "#cbd5e1" : "#475569", grid: dark ? "rgba(148,163,184,.12)" : "rgba(100,116,139,.12)" };
  }

  function render() {
    charts.splice(0).forEach(c => c.destroy());
    const t = themeColors();
    Chart.defaults.color = t.text;
    Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
    document.querySelectorAll("canvas[data-chart]").forEach(canvas => {
      const src = document.getElementById(canvas.dataset.source);
      if (!src) return;
      let rows;
      try { rows = JSON.parse(src.textContent); } catch { return; }
      const type = canvas.dataset.chart;
      const accent = getComputedStyle(canvas).getPropertyValue("--cnx-accent").trim() || palette[0];
      const isPie = type === "doughnut" || type === "pie";
      // Multi-series: rows carry "values": [..] and data-label holds "Series A|Series B".
      const multi = rows.length > 0 && Array.isArray(rows[0].values);
      const names = (canvas.dataset.label || "").split("|");
      const seriesCount = multi ? rows[0].values.length : 1;
      const datasets = [];
      for (let i = 0; i < seriesCount; i++) {
        const color = i === 0 ? accent : palette[i % palette.length];
        datasets.push({
          label: names[i] || "",
          data: rows.map(r => multi ? r.values[i] : r.value),
          backgroundColor: isPie ? palette : (type === "line" ? color + "22" : color),
          borderColor: isPie ? "transparent" : color,
          borderWidth: isPie ? 0 : 2,
          borderRadius: type === "bar" ? 6 : 0,
          fill: type === "line" && !multi,
          tension: .35,
          pointRadius: 3,
          maxBarThickness: 36
        });
      }
      charts.push(new Chart(canvas, {
        type,
        data: { labels: rows.map(r => r.label), datasets },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          cutout: isPie ? "68%" : undefined,
          indexAxis: canvas.dataset.horizontal ? "y" : "x",
          plugins: { legend: { display: isPie || multi, position: "bottom", labels: { boxWidth: 10, usePointStyle: true } } },
          scales: isPie ? {} : {
            x: { grid: { display: false } },
            y: { beginAtZero: true, grid: { color: t.grid }, ticks: { precision: 0 } }
          }
        }
      }));
    });
  }

  render();
  document.addEventListener("cnx:theme", render);
})();
