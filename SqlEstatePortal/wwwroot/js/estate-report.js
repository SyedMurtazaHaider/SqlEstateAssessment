(function () {
  function applyFilters(root) {
    root.querySelectorAll('table').forEach(function (table) {
      if (typeof window.refreshTable === 'function') {
        window.refreshTable(table, { resetPage: true });
      }
    });
  }

  window.applyEstateFilters = applyFilters;

  document.querySelectorAll('[data-estate-report]').forEach(function (root) {
    var select = root.querySelector('.server-filter');
    if (select) {
      select.addEventListener('change', function () { applyFilters(root); });
    }

    var runSelect = root.querySelector('.assessment-filter');
    if (runSelect) {
      runSelect.addEventListener('change', function () {
        if (runSelect.value) window.location.href = runSelect.value;
      });
    }

    root.querySelectorAll('.sev-chip').forEach(function (chip) {
      chip.addEventListener('click', function () {
        root.querySelectorAll('.sev-chip').forEach(function (c) { c.classList.remove('active'); });
        chip.classList.add('active');
        applyFilters(root);
      });
    });

    root.querySelectorAll('.sev-jump').forEach(function (card) {
      card.addEventListener('click', function () {
        var sev = card.getAttribute('data-severity');
        var findingsTab = root.querySelector('[data-bs-target="#tab-findings"]');
        if (findingsTab) findingsTab.click();
        var chip = root.querySelector('.sev-chip[data-severity="' + sev + '"]');
        if (chip) chip.click();
      });
    });

    root.querySelectorAll('[data-bs-toggle="tab"]').forEach(function (tab) {
      tab.addEventListener('shown.bs.tab', function () { applyFilters(root); });
    });
  });
})();
