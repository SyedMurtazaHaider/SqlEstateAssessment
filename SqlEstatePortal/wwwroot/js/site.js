(function () {
  var toggle = document.getElementById('sidebarToggle');
  if (toggle) {
    toggle.addEventListener('click', function () {
      document.body.classList.toggle('sidebar-open');
    });
  }

  var PAGE_SIZES = [10, 25, 50, 100];

  function rowText(row) {
    return (row.textContent || '').replace(/\s+/g, ' ').toLowerCase();
  }

  function getState(table) {
    if (!table._tableUi) table._tableUi = { page: 1, pageSize: 10 };
    return table._tableUi;
  }

  function estateContext(table) {
    var root = table.closest('[data-estate-report]');
    if (!root) return { server: '', severity: '' };
    var sevBtn = root.querySelector('.sev-chip.active');
    return {
      server: (root.querySelector('.server-filter') || {}).value || '',
      severity: sevBtn ? (sevBtn.getAttribute('data-severity') || '') : ''
    };
  }

  function rowMatches(row, query, colQueries, ctx) {
    if (query && rowText(row).indexOf(query) === -1) return false;
    if (ctx.server) {
      var rowServer = row.getAttribute('data-server');
      if (rowServer && rowServer !== ctx.server) return false;
    }
    if (ctx.severity) {
      var rowSev = row.getAttribute('data-severity');
      if (rowSev && rowSev !== ctx.severity) return false;
    }
    if (colQueries && colQueries.length) {
      for (var i = 0; i < colQueries.length; i++) {
        var colQuery = colQueries[i];
        if (!colQuery) continue;
        var cell = row.cells[i];
        var text = cell ? (cell.textContent || '').replace(/\s+/g, ' ').toLowerCase() : '';
        if (text.indexOf(colQuery) === -1) return false;
      }
    }
    return true;
  }

  function columnQueries(table) {
    var queries = [];
    (table._columnInputs || []).forEach(function (input, i) {
      queries[i] = input ? input.value.trim().toLowerCase() : '';
    });
    return queries;
  }

  function pageWindow(current, total) {
    if (total <= 7) {
      var all = [];
      for (var i = 1; i <= total; i++) all.push(i);
      return all;
    }
    var pages = [1];
    var start = Math.max(2, current - 1);
    var end = Math.min(total - 1, current + 1);
    if (current <= 3) {
      start = 2;
      end = 4;
    }
    if (current >= total - 2) {
      start = total - 3;
      end = total - 1;
    }
    if (start > 2) pages.push('…');
    for (var p = start; p <= end; p++) pages.push(p);
    if (end < total - 1) pages.push('…');
    pages.push(total);
    return pages;
  }

  function renderPager(pager, state, matched, pages) {
    pager.innerHTML = '';
    var info = document.createElement('span');
    info.className = 'pager-info';
    if (!matched) {
      info.textContent = '0 rows';
    } else {
      var start = (state.page - 1) * state.pageSize + 1;
      var end = Math.min(state.page * state.pageSize, matched);
      info.textContent = start + '–' + end + ' of ' + matched;
    }
    pager.appendChild(info);

    var buttons = document.createElement('div');
    buttons.className = 'pager-buttons';

    function addBtn(label, page, disabled, active) {
      var btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'btn btn-sm btn-outline-secondary' + (active ? ' active' : '');
      btn.textContent = label;
      btn.disabled = !!disabled;
      if (!disabled && page) btn.setAttribute('data-page', String(page));
      buttons.appendChild(btn);
    }

    addBtn('Prev', state.page - 1, state.page <= 1);
    pageWindow(state.page, pages).forEach(function (item) {
      if (item === '…') {
        var ellipsis = document.createElement('span');
        ellipsis.className = 'pager-ellipsis';
        ellipsis.textContent = '…';
        buttons.appendChild(ellipsis);
        return;
      }
      addBtn(String(item), item, false, item === state.page);
    });
    addBtn('Next', state.page + 1, state.page >= pages);
    pager.appendChild(buttons);
    pager.hidden = matched === 0;
  }

  function refreshTable(table, options) {
    if (!table || !table.tBodies.length) return;
    var state = getState(table);
    if (options && options.resetPage) state.page = 1;
    if (table._pageSizeSelect) {
      var chosen = parseInt(table._pageSizeSelect.value, 10);
      if (chosen) state.pageSize = chosen;
    }

    var query = table._searchInput ? table._searchInput.value.trim().toLowerCase() : '';
    var colQueries = columnQueries(table);
    var ctx = estateContext(table);
    var rows = Array.prototype.slice.call(table.querySelectorAll('tbody tr'));
    var matched = rows.filter(function (row) { return rowMatches(row, query, colQueries, ctx); });
    var pages = Math.max(1, Math.ceil(matched.length / state.pageSize) || 1);
    if (state.page > pages) state.page = pages;

    var start = (state.page - 1) * state.pageSize;
    var end = start + state.pageSize;
    rows.forEach(function (row) { row.hidden = true; });
    matched.forEach(function (row, index) {
      row.hidden = index < start || index >= end;
    });

    if (table._pager) renderPager(table._pager, state, matched.length, pages);
    if (table._hint) table._hint.hidden = matched.length > 0 || rows.length === 0;
  }

  window.refreshTable = refreshTable;

  function bindColumnFilters(table) {
    table._columnInputs = [];
    var filterRow = table.querySelector('thead tr.column-filters');
    if (!filterRow) return;
    Array.prototype.forEach.call(filterRow.cells, function (cell, i) {
      var input = cell.querySelector('input.column-search');
      table._columnInputs[i] = input || null;
      if (!input || input.dataset.bound === '1') return;
      input.dataset.bound = '1';
      input.addEventListener('input', function () { refreshTable(table, { resetPage: true }); });
      input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') e.preventDefault();
      });
    });
  }

  function attachTable(table) {
    if (!table.querySelector('tbody tr') || table.dataset.tableUi === '1') return;
    table.dataset.tableUi = '1';

    var toolbar = table.previousElementSibling;
    if (!toolbar || !toolbar.classList.contains('table-toolbar')) {
      toolbar = document.createElement('div');
      toolbar.className = 'table-toolbar';

      var sizeWrap = document.createElement('label');
      sizeWrap.className = 'table-page-size-label';
      sizeWrap.appendChild(document.createTextNode('Show '));
      var sizeSelect = document.createElement('select');
      sizeSelect.className = 'form-select form-select-sm table-page-size';
      PAGE_SIZES.forEach(function (n) {
        var opt = document.createElement('option');
        opt.value = String(n);
        opt.textContent = String(n);
        if (n === 10) opt.selected = true;
        sizeSelect.appendChild(opt);
      });
      sizeWrap.appendChild(sizeSelect);
      sizeWrap.appendChild(document.createTextNode(' rows'));

      var input = document.createElement('input');
      input.type = 'search';
      input.className = 'form-control table-search-input';
      input.placeholder = 'Search all columns...';
      input.setAttribute('aria-label', 'Search all columns');
      input.autocomplete = 'off';

      toolbar.appendChild(sizeWrap);
      toolbar.appendChild(input);
      table.parentNode.insertBefore(toolbar, table);
      table._searchInput = input;
      table._pageSizeSelect = sizeSelect;

      input.addEventListener('input', function () { refreshTable(table, { resetPage: true }); });
      input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') e.preventDefault();
      });
      sizeSelect.addEventListener('change', function () { refreshTable(table, { resetPage: true }); });
    } else {
      table._searchInput = toolbar.querySelector('.table-search-input');
      table._pageSizeSelect = toolbar.querySelector('.table-page-size');
    }

    bindColumnFilters(table);

    var pager = table.nextElementSibling;
    if (!pager || !pager.classList.contains('table-pager')) {
      pager = document.createElement('div');
      pager.className = 'table-pager';
      table.parentNode.insertBefore(pager, table.nextSibling);
    }
    table._pager = pager;

    var hint = pager.nextElementSibling;
    if (!hint || !hint.classList.contains('empty-hint')) {
      hint = document.createElement('p');
      hint.className = 'muted empty-hint';
      hint.hidden = true;
      hint.textContent = 'No matching rows.';
      table.parentNode.insertBefore(hint, pager.nextSibling);
    }
    table._hint = hint;

    if (!pager.dataset.bound) {
      pager.dataset.bound = '1';
      pager.addEventListener('click', function (e) {
        var btn = e.target.closest('button[data-page]');
        if (!btn) return;
        getState(table).page = parseInt(btn.getAttribute('data-page'), 10);
        refreshTable(table);
      });
    }

    refreshTable(table);
  }

  document.querySelectorAll('table').forEach(attachTable);

  function bindAssessmentProgress() {
    var overlay = document.getElementById('assessmentProgressOverlay');
    var bar = document.getElementById('assessmentProgressBar');
    var pctLabel = document.getElementById('assessmentProgressPct');
    var track = overlay ? overlay.querySelector('.assessment-progress-track') : null;
    if (!overlay || !bar || !pctLabel) return;
    if (overlay.dataset.bound === '1') return;
    overlay.dataset.bound = '1';

    var timer = null;
    var current = 1;

    function setProgress(value) {
      current = Math.max(1, Math.min(100, Math.round(value)));
      bar.style.width = current + '%';
      pctLabel.textContent = current + '%';
      if (track) track.setAttribute('aria-valuenow', String(current));
    }

    function stopTicker() {
      if (timer) {
        clearInterval(timer);
        timer = null;
      }
    }

    function startTicker() {
      stopTicker();
      setProgress(1);
      timer = setInterval(function () {
        if (current >= 92) return;
        var step = current < 40 ? 3 : current < 70 ? 2 : 1;
        setProgress(current + step);
      }, 450);
    }

    function showOverlay() {
      overlay.hidden = false;
      overlay.removeAttribute('hidden');
      overlay.classList.add('is-visible');
      overlay.setAttribute('aria-busy', 'true');
      document.body.style.overflow = 'hidden';
      startTicker();
    }

    function hideOverlay() {
      stopTicker();
      overlay.hidden = true;
      overlay.classList.remove('is-visible');
      overlay.setAttribute('aria-busy', 'false');
      document.body.style.overflow = '';
      setProgress(1);
    }

    function finishAndRedirect(url) {
      stopTicker();
      setProgress(100);
      setTimeout(function () {
        window.location.href = url;
      }, 350);
    }

    document.querySelectorAll('form.js-run-assessment').forEach(function (form) {
      form.addEventListener('submit', function (e) {
        e.preventDefault();
        e.stopPropagation();
        var btn = form.querySelector('button[type="submit"]');
        if (btn) {
          btn.disabled = true;
          btn.dataset.originalText = btn.textContent;
          btn.textContent = 'Running...';
        }

        showOverlay();

        fetch(form.action, {
          method: 'POST',
          body: new FormData(form),
          headers: {
            'X-Requested-With': 'XMLHttpRequest',
            'Accept': 'application/json'
          },
          credentials: 'same-origin'
        })
          .then(function (res) {
            if (!res.ok) throw new Error('Assessment request failed (' + res.status + ').');
            return res.json();
          })
          .then(function (data) {
            var url = (data && data.redirectUrl) || form.getAttribute('data-fallback') || '/Assessments';
            finishAndRedirect(url);
          })
          .catch(function (err) {
            hideOverlay();
            if (btn) {
              btn.disabled = false;
              btn.textContent = btn.dataset.originalText || 'Run assessment';
            }
            alert(err && err.message ? err.message : 'Assessment failed to start.');
          });
      });
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', bindAssessmentProgress);
  } else {
    bindAssessmentProgress();
  }
})();
