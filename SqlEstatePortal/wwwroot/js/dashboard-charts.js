(function () {
  var form = document.getElementById('dashboardFilters');
  if (form) {
    form.querySelectorAll('select').forEach(function (sel) {
      sel.addEventListener('change', function () { form.submit(); });
    });
  }

  var el = document.getElementById('dashboard-charts');
  if (!el || typeof Chart === 'undefined') return;

  var data;
  try {
    data = JSON.parse(el.textContent || '{}');
  } catch (e) {
    return;
  }

  Chart.defaults.font.family = '"Segoe UI", Calibri, sans-serif';
  Chart.defaults.color = '#5c6b7a';
  Chart.defaults.plugins.legend.labels.boxWidth = 12;
  Chart.defaults.plugins.legend.position = 'bottom';

  var palette = ['#1f4e79', '#2b6cb0', '#0e7490', '#2f855a', '#c05621', '#b42318', '#4a5568', '#2c5282', '#276749', '#9a4a00'];
  var severityColors = {
    Critical: '#b42318',
    High: '#c05621',
    Medium: '#b7791f',
    Low: '#2f855a',
    Info: '#2b6cb0',
    Unknown: '#718096'
  };

  function labels(items) {
    return (items || []).map(function (x) { return x.label; });
  }

  function values(items) {
    return (items || []).map(function (x) { return Number(x.value); });
  }

  function colorsFor(items) {
    return (items || []).map(function (x, i) {
      return severityColors[x.label] || palette[i % palette.length];
    });
  }

  function doughnut(id, items) {
    var canvas = document.getElementById(id);
    if (!canvas) return;
    new Chart(canvas, {
      type: 'doughnut',
      data: {
        labels: labels(items),
        datasets: [{
          data: values(items),
          backgroundColor: colorsFor(items),
          borderWidth: 0
        }]
      },
      options: {
        maintainAspectRatio: false,
        cutout: '58%',
        plugins: { legend: { position: 'bottom' } }
      }
    });
  }

  function bar(id, items, opts) {
    var canvas = document.getElementById(id);
    if (!canvas) return;
    var horizontal = !opts || opts.horizontal !== false;
    new Chart(canvas, {
      type: 'bar',
      data: {
        labels: labels(items),
        datasets: [{
          data: values(items),
          backgroundColor: opts && opts.color ? opts.color : '#2b6cb0',
          borderRadius: 4,
          maxBarThickness: 22
        }]
      },
      options: {
        indexAxis: horizontal ? 'y' : 'x',
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: {
          x: { grid: { display: !horizontal }, beginAtZero: true },
          y: { grid: { display: horizontal }, ticks: { autoSkip: false } }
        }
      }
    });
  }

  doughnut('chartSeverity', data.findingsBySeverity);
  doughnut('chartSupport', data.supportStatus);
  doughnut('chartRecovery', data.recoveryModels);
  doughnut('chartJobs', data.jobStatus);
  doughnut('chartServices', data.serviceStatus);
  doughnut('chartEditions', data.editions);
  bar('chartArea', data.findingsByArea);
  bar('chartServer', data.findingsByServer);
  bar('chartDatabases', data.topDatabasesMb);
  bar('chartVolumes', data.volumeFreePct, { color: '#0e7490' });
  bar('chartWaits', data.topWaits, { color: '#1f4e79' });

  var history = data.runHistory || [];
  var historyCanvas = document.getElementById('chartHistory');
  if (historyCanvas) {
    new Chart(historyCanvas, {
      type: 'bar',
      data: {
        labels: history.map(function (x) { return x.label; }),
        datasets: [
          { label: 'Critical', data: history.map(function (x) { return x.critical; }), backgroundColor: '#b42318', stack: 's' },
          { label: 'High', data: history.map(function (x) { return x.high; }), backgroundColor: '#c05621', stack: 's' },
          { label: 'Medium', data: history.map(function (x) { return x.medium; }), backgroundColor: '#b7791f', stack: 's' },
          { label: 'Low', data: history.map(function (x) { return x.low; }), backgroundColor: '#2f855a', stack: 's' }
        ]
      },
      options: {
        maintainAspectRatio: false,
        plugins: { legend: { position: 'bottom' } },
        scales: {
          x: { stacked: true, grid: { display: false } },
          y: { stacked: true, beginAtZero: true }
        }
      }
    });
  }
})();
