// quro-grid.js — QuroGrid의 얇은 JS 층 (Spec 4.2).
// 소유: 활성 셀(포커스)·범위 선택·키보드 이동·편집 중 키 입력·클립보드 (R1).
// 서버 호출은 편집 시작·커밋·취소·행 추가·행 삭제·붙여넣기(1회, R3)·활성 행 변경(구독 시)·
// 코드도움(F4)·선택 목록 열기(Alt+↓)·체크 토글·버튼 클릭 때만 (R2).
// 셀마다 핸들러를 달지 않고 그리드 루트 하나에서 이벤트를 받아 data-row / data-field로 좌표를 해석한다 (R5).

const grids = new Map();
let nextId = 1;

export function init(root, dotnet, options) {
    const id = `q${nextId++}`;
    root.setAttribute('data-quro-grid', id);

    // 활성 셀 표시는 서버 렌더와 충돌하지 않도록 셀 class가 아닌 별도 style 규칙으로 그린다.
    const style = document.createElement('style');
    document.head.appendChild(style);

    const s = {
        root, dotnet, options, id, style,
        active: null,      // { row: number, field: string } — 선택 범위의 움직이는 끝
        anchor: null,      // 선택 범위의 고정 끝 (Shift+이동·드래그)
        dragging: false,
        editing: null,     // { row, field, input }
        queue: Promise.resolve(),
        pending: 0,        // 처리 대기 중인 동작 수 (서버 응답 대기 포함)
    };
    s.onKeyDown = e => onKeyDown(s, e);
    s.onMouseDown = e => onMouseDown(s, e);
    s.onDblClick = e => onDblClick(s, e);
    s.onFocusOut = e => onFocusOut(s, e);
    s.onMouseMove = e => onMouseMove(s, e);
    s.onMouseUp = () => { s.dragging = false; };
    s.onCopy = e => onCopy(s, e);
    s.onScroll = () => placePicker(s);
    s.onReconnect = e => onReconnectState(s, e.detail?.state);
    s.onLinkClick = e => onLinkClick(s, e);
    s.onPaste = e => onPaste(s, e);
    root.addEventListener('keydown', s.onKeyDown);
    root.addEventListener('mousedown', s.onMouseDown);
    root.addEventListener('mousemove', s.onMouseMove);
    root.addEventListener('dblclick', s.onDblClick);
    root.addEventListener('focusout', s.onFocusOut);
    // 포커스된 div에는 copy·paste 이벤트가 오지 않을 수 있어 문서에서 받고 그리드 포커스 여부로 거른다
    document.addEventListener('mouseup', s.onMouseUp);
    document.addEventListener('copy', s.onCopy);
    document.addEventListener('paste', s.onPaste);
    document.addEventListener('scroll', s.onScroll, true);   // 그리드·페이지 스크롤 시 선택 목록 위치를 따라간다
    // Blazor 재연결 상태 (앱의 #components-reconnect-modal에서 발생, 버블링하지 않으므로 캡처로 받는다)
    document.addEventListener('components-reconnect-state-changed', s.onReconnect, true);
    // 미저장 변경이 있을 때 링크 이동 확인 (A-20). 향상된 탐색(enhanced navigation)은 서버의 NavigationLock을 거치지 않는다.
    document.addEventListener('click', s.onLinkClick, true);
    grids.set(root, s);

    // 이전 회로에서 미저장 변경을 남긴 채 연결이 끊겼다면, 다시 연 이 화면에서 알린다 (GS-048, A-21)
    if (takeLostFlag()) root.setAttribute('data-lost', '');
}

export function dispose(root) {
    const s = grids.get(root);
    if (!s) return;
    root.removeEventListener('keydown', s.onKeyDown);
    root.removeEventListener('mousedown', s.onMouseDown);
    root.removeEventListener('dblclick', s.onDblClick);
    root.removeEventListener('focusout', s.onFocusOut);
    root.removeEventListener('mousemove', s.onMouseMove);
    document.removeEventListener('mouseup', s.onMouseUp);
    document.removeEventListener('copy', s.onCopy);
    document.removeEventListener('paste', s.onPaste);
    document.removeEventListener('scroll', s.onScroll, true);
    document.removeEventListener('components-reconnect-state-changed', s.onReconnect, true);
    document.removeEventListener('click', s.onLinkClick, true);
    clearTimeout(s.notifyTimer);
    s.style.remove();
    s.resizeStyle?.remove();
    grids.delete(root);
}

// ── 이벤트 ──

const HANDLED = new Set(['Enter', 'Tab', 'Escape', 'F2', 'F4', 'Insert', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight']);

function isChar(e) {
    return e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey;
}

function isRowDelete(e) {
    return e.key === 'Delete' && e.ctrlKey;
}

function isPickerKey(e) {
    return e.key === 'ArrowDown' && e.altKey;
}

function onKeyDown(s, e) {
    if (isOffline(s)) {   // 연결 끊김 중 입력 잠금 (R7)
        e.preventDefault();
        return;
    }
    // 선택 목록(달력·Combo 목록, MudBlazor) 안의 키는 목록이 받는다. Esc만 편집 취소.
    if (e.target.closest('.quro-picker')) {
        if (e.key === 'Escape') {
            e.preventDefault();
            enqueue(s, () => s.editing ? cancel(s) : null);
        }
        return;
    }
    // 편집 중이고 밀린 동작이 없으면 문자·화살표는 에디터가 그대로 받는다 (서버 호출 없음, R2)
    if (s.editing && s.pending === 0 && !['Enter', 'Tab', 'Escape', 'F4'].includes(e.key) && !isPickerKey(e)) return;
    if (!HANDLED.has(e.key) && !isChar(e) && !isRowDelete(e)) return;

    e.preventDefault();
    const key = { key: isRowDelete(e) ? 'RowDelete' : e.key, shift: e.shiftKey, alt: e.altKey, char: isChar(e) };
    enqueue(s, () => handleKey(s, key));
}

function onMouseDown(s, e) {
    if (e.target.closest('.quro-notice-lost')) {   // 안내를 누르면 닫는다
        s.root.removeAttribute('data-lost');
        return;
    }
    if (isOffline(s)) {
        e.preventDefault();
        return;
    }
    if (e.target.closest('.quro-picker')) {
        // 목록 항목은 포커스를 받지 않을 수 있어(relatedTarget 없음) 셀 이탈로 보지 않게 한다
        s.pickerMouseDown = true;
        setTimeout(() => { s.pickerMouseDown = false; });
        return;
    }
    if (e.button !== 0 || e.target.closest('.quro-editor')) return;
    const handle = e.target.closest('.quro-head .quro-resize');
    if (handle) {
        startResize(s, handle.closest('[role=columnheader]'), e);
        return;
    }
    const header = e.target.closest('.quro-head [role=columnheader][data-field]');
    if (header) {
        // 헤더 클릭 정렬 (A-22): 편집 중이면 먼저 커밋. 포커스는 그리드에 둔다.
        e.preventDefault();
        const field = header.dataset.field;
        const additive = e.shiftKey;
        enqueue(s, async () => {
            if (s.editing && !(await commit(s))) return;
            await s.dotnet.invokeMethodAsync('Sort', field, additive);
        });
        return;
    }
    const cell = e.target.closest('.quro-body [role=gridcell][data-field]');
    if (!cell) return;
    const target = position(cell);
    const extend = e.shiftKey && !!s.anchor;
    // 셀 안 체크 표시·버튼을 누르면 활성화 후 바로 동작 (셀마다 핸들러를 달지 않는다, R5)
    const type = e.target.closest('.quro-check') ? 'check' : e.target.closest('.quro-button') ? 'button' : null;
    s.dragging = true;
    enqueue(s, async () => {
        if (s.editing && !(await commit(s))) return;
        activate(s, target.row, target.field, extend);
        if (type) await cellAction(s, type);
    });
}

// ── 컬럼 리사이즈 (GS-053, A-23): 드래그 중에는 브라우저에서만 그리고, 끝날 때 서버 1회 (R1) ──

const MIN_WIDTH = 40;

function startResize(s, header, e) {
    e.preventDefault();
    const field = header.dataset.field;
    const startX = e.clientX;
    const startWidth = Math.round(header.getBoundingClientRect().width);
    // 오른쪽에 있는 고정 열은 너비가 바뀐 만큼 위치(left)도 옮긴다
    const headers = [...header.parentElement.children];
    const frozenAfter = header.classList.contains('quro-frozen')
        ? headers.slice(headers.indexOf(header) + 1).filter(h => h.classList.contains('quro-frozen'))
            .map(h => ({ field: h.dataset.field, left: parseFloat(getComputedStyle(h).left) || 0 }))
        : [];
    let width = startWidth;

    const move = ev => {
        width = Math.max(MIN_WIDTH, Math.round(startWidth + ev.clientX - startX));
        drawResize(s, field, width, frozenAfter.map(f => ({ ...f, left: f.left + width - startWidth })));
    };
    const up = () => {
        document.removeEventListener('mousemove', move, true);
        document.removeEventListener('mouseup', up, true);
        if (width === startWidth) {
            drawResize(s, null);
            return;
        }
        enqueue(s, async () => {
            await s.dotnet.invokeMethodAsync('ColumnResized', field, width);
            drawResize(s, null);   // 이제 서버가 그린 너비를 쓴다
        });
    };
    document.addEventListener('mousemove', move, true);
    document.addEventListener('mouseup', up, true);
}

/** 드래그 중 너비를 별도 style 규칙으로 덮는다 (서버 렌더와 충돌하지 않게). field가 null이면 지운다. */
function drawResize(s, field, width, shifted = []) {
    s.resizeStyle ??= document.head.appendChild(document.createElement('style'));
    if (!field) {
        s.resizeStyle.textContent = '';
        return;
    }
    const grid = `[data-quro-grid="${s.id}"]`;
    const cells = f => `${grid} [data-field="${CSS.escape(f)}"], ${grid} [data-footer-field="${CSS.escape(f)}"]`;
    s.resizeStyle.textContent = [
        `${cells(field)} { width: ${width}px !important; }`,
        ...shifted.map(f => `${cells(f.field)} { left: ${f.left}px !important; }`),
    ].join('\n');
}

/** 드래그로 범위 선택 (서버 호출 없음) */
function onMouseMove(s, e) {
    if (!s.dragging || !(e.buttons & 1) || s.editing) return;
    const cell = e.target.closest('.quro-body [role=gridcell][data-field]');
    if (!cell) return;
    const target = position(cell);
    if (same(target, s.active)) return;
    enqueue(s, () => { if (!s.editing) activate(s, target.row, target.field, true); });
}

function hasFocus(s) {
    return !s.editing && s.root.contains(document.activeElement);
}

/** Ctrl+C: 선택 범위의 표시 값을 엑셀 형식(탭·줄바꿈)으로. 표시와 값이 다른 셀(Check·Button)은 data-copy. */
function onCopy(s, e) {
    if (!hasFocus(s) || !s.active) return;
    const { rowEls, fields } = selection(s);
    const copyText = cell => cell ? (cell.dataset.copy ?? cell.textContent.trim()) : '';
    const text = rowEls
        .map(rowEl => fields
            .map(f => quote(copyText(rowEl.querySelector(`:scope > [data-field="${CSS.escape(f)}"]`))))
            .join('\t'))
        .join('\r\n');
    e.clipboardData.setData('text/plain', text);
    e.preventDefault();
}

/** Ctrl+V: 엑셀 형식을 행 × 열로 나눠 선택 범위 왼쪽 위부터. 서버 호출 1회 (R3) */
function onPaste(s, e) {
    if (!hasFocus(s) || !s.active) return;
    if (isOffline(s)) {
        e.preventDefault();
        return;
    }
    const cells = parseTsv(e.clipboardData.getData('text/plain'));
    e.preventDefault();
    if (cells.length === 0) return;
    const { top, left } = selection(s);
    enqueue(s, () => s.dotnet.invokeMethodAsync('Paste', top, left, cells));
}

function onDblClick(s, e) {
    if (e.target.closest('.quro-picker') || isOffline(s)) return;
    const cell = e.target.closest('.quro-body [role=gridcell][data-field]');
    if (!cell || !hasEditor(cell)) return;
    const target = position(cell);
    enqueue(s, async () => {
        activate(s, target.row, target.field);
        if (!s.editing) await beginEdit(s, null);
    });
}

function onFocusOut(s, e) {
    // 그리드 밖을 클릭하면 편집 중인 값을 커밋한다 (셀 이탈). 에디터에서 포커스가 나간 경우만 본다.
    // 건너뛰는 경우: 처리 중인 동작이 있음(커밋·F4 중 앱의 코드도움 팝업이 포커스를 가져감),
    // 선택 목록을 누름(목록 항목은 포커스를 받지 않을 수 있음), 연결 끊김 중(재연결 안내가 포커스를 가져감), 그리드 안으로 이동.
    const input = e.target;
    if (!s.editing || input !== s.editing.input || s.pending > 0 || s.pickerMouseDown || isOffline(s)
        || s.root.contains(e.relatedTarget)) return;
    // 서버가 편집을 끝내며 에디터를 지울 때(선택 목록에서 고름)도 focusout이 온다. 지워진 에디터는 커밋하지 않는다.
    setTimeout(() => {
        if (input.isConnected && s.editing?.input === input) enqueue(s, () => s.editing?.input === input ? commit(s) : null);
    });
}

/** 키·클릭 동작을 순서대로 처리한다. 서버 응답을 기다리는 동안 들어온 입력도 유실 없이 이어서 처리된다. */
function enqueue(s, action) {
    s.pending++;
    s.queue = s.queue
        .then(action)
        .catch(err => console.error('quro-grid', err))
        .finally(() => s.pending--);
}

async function handleKey(s, k) {
    if (!s.active) {
        const first = cellsOf(rows(s)[0] ?? null)[0];
        if (first) {
            const p = position(first);
            activate(s, p.row, p.field);
        }
        return;
    }

    const type = cellType(currentCell(s));
    if (k.char) {
        if (s.editing) appendText(s.editing.input, k.key);
        else if (type === 'check' || type === 'button') { if (k.key === ' ') await cellAction(s, type); }   // Space: 토글·클릭
        else if (hasEditor(currentCell(s))) await beginEdit(s, k.key);   // 문자 입력: 덮어쓰기
        return;
    }

    switch (k.key) {
        case 'F2':
            if (!s.editing && hasEditor(currentCell(s))) await beginEdit(s, null);   // 기존 값 유지
            return;
        case 'F4':
            if (type === 'popup') await requestPopup(s);
            return;
        case 'Escape':
            if (s.editing) await cancel(s);
            return;
        case 'Enter':
            if (!s.editing && type === 'button') return cellAction(s, type);   // Button 셀: 클릭, 이동 없음
            if (s.editing && !(await commit(s, true))) return;
            await moveEnter(s);
            return;
        case 'Tab':
            if (s.editing && !(await commit(s, true))) return;
            moveToEditable(s, k.shift ? -1 : 1);
            return;
        case 'Insert':
            if (!s.editing) await addRow(s, s.active.row);   // 활성 행 바로 아래 (A-15)
            return;
        case 'RowDelete':
            // 선택 범위의 행을 한 번에 (R3). 남은 행으로의 이동은 서버 렌더 뒤 ensureActive가 한다.
            if (!s.editing) await s.dotnet.invokeMethodAsync('DeleteRows', selectedKeys(s));
            return;
        case 'ArrowUp': return moveBy(s, -1, 0, k.shift);
        case 'ArrowDown': return k.alt ? openPicker(s) : moveBy(s, 1, 0, k.shift);
        case 'ArrowLeft': return moveBy(s, 0, -1, k.shift);
        case 'ArrowRight': return moveBy(s, 0, 1, k.shift);
    }
}

// ── 편집 (서버 호출) ──

async function beginEdit(s, initialText) {
    const { row, field } = s.active;
    if (!(await s.dotnet.invokeMethodAsync('BeginEdit', row, field))) return false;

    const input = await waitFor(() => cellAt(s, row, field)?.querySelector('.quro-editor'));
    if (!input) return false;
    if (initialText !== null) input.value = initialText;
    input.focus();
    input.setSelectionRange(input.value.length, input.value.length);
    s.editing = { row, field, input };
    return true;
}

/** 셀 이탈 커밋. byKey(Enter·Tab)면 Popup 셀 코드도움 팝업이 닫힌 뒤에도 포커스를 그리드로 되찾는다. */
async function commit(s, byKey = false) {
    const { row, field, input } = s.editing;
    if (!(await s.dotnet.invokeMethodAsync('CommitEdit', row, field, input.value))) {
        input.focus();   // 해석 실패·OnCellValueChanging 취소·0건·팝업 취소: 편집 유지
        if (byKey) reclaimFocus(s, input);
        return false;
    }
    s.editing = null;
    s.root.focus({ preventScroll: true });
    if (byKey) reclaimFocus(s, s.root);
    return true;
}

/**
 * 앱이 띄운 팝업(MudDialog 등)이 닫히면서 포커스가 문서(body)로 빠지는 경우가 있어,
 * 잠시(500ms) 지켜보며 포커스가 아무 곳에도 없으면 그리드로 되돌린다. 사용자가 다른 요소를 고른 경우는 건드리지 않는다.
 */
function reclaimFocus(s, el) {
    const until = performance.now() + 500;
    const tick = () => {
        const a = document.activeElement;
        if (el.isConnected && a !== el && (!a || a === document.body)) el.focus({ preventScroll: true });
        if (performance.now() < until) requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
}

/** F4 코드도움 (GS-046). 앱이 팝업에서 코드를 고르면 서버가 확정하고 true. 이동하지 않는다. */
async function requestPopup(s) {
    const { row, field } = s.active;
    const text = s.editing ? s.editing.input.value : (currentCell(s)?.textContent.trim() ?? '');
    const chosen = await s.dotnet.invokeMethodAsync('RequestPopup', row, field, text);
    if (s.editing && !chosen) {
        s.editing.input.focus();   // 팝업을 닫았으면 입력을 이어 간다
        reclaimFocus(s, s.editing.input);
        return;
    }
    s.editing = null;
    s.root.focus({ preventScroll: true });
    reclaimFocus(s, s.root);
}

/** Alt+↓: Date 달력·Combo 목록 (Addendum A-17). 편집 중이 아니면 편집을 시작하고 연다. 고르면 서버가 확정 후 endEdit. */
async function openPicker(s) {
    const cell = currentCell(s);
    const type = cellType(cell);
    if ((type !== 'date' && type !== 'combo') || !editable(cell)) return;
    if (!s.editing && !(await beginEdit(s, null))) return;
    if (!(await s.dotnet.invokeMethodAsync('OpenPicker', s.editing.row, s.editing.field, s.editing.input.value))) return;
    await waitFor(() => s.root.querySelector('.quro-picker'));
    placePicker(s);
}

/** 선택 목록을 편집 셀 바로 아래(공간이 없으면 위)에 둔다. 화면 기준(fixed)이라 그리드 스크롤 영역에 잘리지 않는다. */
function placePicker(s) {
    const picker = s.root.querySelector('.quro-picker');
    const cell = picker?.closest('[role=gridcell]');
    if (!cell) return;
    const r = cell.getBoundingClientRect();
    const below = r.bottom + picker.offsetHeight <= window.innerHeight;
    picker.style.left = `${Math.max(0, Math.min(r.left, window.innerWidth - picker.offsetWidth))}px`;
    picker.style.top = `${below ? r.bottom : Math.max(0, r.top - picker.offsetHeight)}px`;
}

/** Check 토글 · Button 클릭 (서버 호출 1회) */
function cellAction(s, type) {
    return s.dotnet.invokeMethodAsync(type === 'check' ? 'ToggleCheck' : 'ClickButton', s.active.row, s.active.field);
}

async function cancel(s) {
    s.editing = null;
    await s.dotnet.invokeMethodAsync('CancelEdit');
    s.root.focus({ preventScroll: true });
}

function appendText(input, text) {
    input.value += text;
    input.setSelectionRange(input.value.length, input.value.length);
}

// ── 이동 (서버 호출 없음) ──

function moveBy(s, dRow, dCol, extend) {
    const all = rows(s);
    const r = all.findIndex(el => rowKey(el) === s.active.row);
    const cells = cellsOf(all[r]);
    const c = cells.findIndex(el => el.dataset.field === s.active.field);
    const nr = Math.min(Math.max(r + dRow, 0), all.length - 1);
    const target = cellsOf(all[nr])[Math.min(Math.max(c + dCol, 0), cells.length - 1)];
    activate(s, rowKey(all[nr]), target.dataset.field, extend);
}

/** Tab 순서: 편집 가능한 셀만, 행 끝에서 다음 행 첫 셀. 끝이면 제자리. 이동했으면 true. */
function moveToEditable(s, dir) {
    const order = rows(s).flatMap(el => cellsOf(el).filter(editable));
    const i = order.findIndex(el => same(position(el), s.active));
    const next = i < 0 ? order[0] : order[i + dir];
    if (!next) return false;
    const p = position(next);
    activate(s, p.row, p.field);
    return true;
}

/** Enter: 기본 오른쪽(Tab과 같은 순서), 옵션으로 아래. 마지막이면 옵션에 따라 새 행 (GS-003). */
async function moveEnter(s) {
    let moved;
    if (s.options.enterDirection === 'down') {
        const all = rows(s);
        const r = all.findIndex(el => rowKey(el) === s.active.row);
        moved = r + 1 < all.length;
        if (moved) activate(s, rowKey(all[r + 1]), s.active.field);
    } else {
        moved = moveToEditable(s, 1);
    }

    if (!moved && s.options.addRowOnLastEnter) await addRow(s, null);
}

/** 새 행을 추가하고(서버) 그 행의 첫 편집 가능 셀로. afterRow가 null이면 목록 끝. */
async function addRow(s, afterRow) {
    const key = await s.dotnet.invokeMethodAsync('AddRow', afterRow);
    const row = await waitFor(() => s.root.querySelector(`.quro-body [role=row][data-row="${key}"]`));
    const first = cellsOf(row).find(editable);
    if (first) activate(s, key, first.dataset.field);
}

/** 활성 셀 이동. extend면 고정 끝(anchor)을 두고 범위를 넓힌다. 아니면 범위는 한 셀로 줄어든다. */
function activate(s, row, field, extend = false) {
    s.active = { row, field };
    if (!extend || !s.anchor) s.anchor = { row, field };

    const sel = selection(s);
    s.root.setAttribute('data-active-row', String(row));
    s.root.setAttribute('data-active-field', field);
    s.root.setAttribute('data-selection', `${sel.top},${sel.left},${sel.bottom},${sel.right}`);

    // 서버 렌더가 셀 class를 바꿔도 지워지지 않도록 별도 style 규칙으로 그린다
    const scope = `[data-quro-grid="${s.id}"]`;
    let css = `${scope} ${cellSelector(row, field)}{outline:2px solid var(--quro-active,#2e6be6);outline-offset:-2px}`;
    if (sel.rowEls.length > 1 || sel.fields.length > 1) {
        const selectors = sel.rowEls.flatMap(r => sel.fields.map(f => `${scope} ${cellSelector(rowKey(r), f)}`));
        css += `${selectors.join(',')}{background:var(--quro-selected,#dbe7fd)}`;
    }
    s.style.textContent = css;
    currentCell(s)?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    if (!s.editing && document.activeElement !== s.root) s.root.focus({ preventScroll: true });
    notifySelection(s);
}

/**
 * 활성 행이 바뀌면 서버에 알린다 (Addendum A-13). 앱이 구독한 경우에만, 같은 행 안의 이동은 보내지 않는다.
 * 빠르게 여러 행을 지나가면 멈춘 행만 보낸다 (100ms, 잠정).
 */
function notifySelection(s) {
    if (!s.options.notifySelection || s.active.row === s.notifiedRow) return;
    clearTimeout(s.notifyTimer);
    s.notifyTimer = setTimeout(() => {
        const row = s.active?.row;
        if (row === undefined || row === s.notifiedRow || !currentCell(s)) return;
        s.notifiedRow = row;
        s.dotnet.invokeMethodAsync('SelectionChanged', row).catch(err => console.error('quro-grid', err));
    }, 100);
}

/** 선택 범위의 행 키 (표시 순서). 삭제 뒤 활성 셀을 둘 위치로 범위 첫 행의 순번을 기억한다. */
function selectedKeys(s) {
    if (!s.active) return [];
    const { rowEls } = selection(s);
    s.activeIndex = rows(s).indexOf(rowEls[0]);
    return rowEls.map(rowKey);
}

export function selectedRowKeys(root) {
    const s = grids.get(root);
    return s ? selectedKeys(s) : [];
}

/**
 * 앱 안 화면 이동 확인 (Addendum A-20, 서버 NavigationLock이 호출). 문구는 서버가 리소스 키로 해석해 넘긴다.
 * 방금 링크 클릭에서 확인했다면 다시 묻지 않는다.
 */
export function confirmLeave(root, message) {
    const s = grids.get(root);
    if (s && performance.now() - (s.leaveConfirmedAt ?? -Infinity) < 2000) return true;
    return window.confirm(message);
}

/** 같은 사이트 링크를 누를 때 미저장 변경이 있으면 확인한다. 취소하면 이동(Blazor 탐색 포함)을 막는다. */
function onLinkClick(s, e) {
    const message = s.root.dataset.leaveMessage;   // ConfirmLeave일 때만 서버가 그린다
    if (!message || !s.root.isConnected || !s.root.hasAttribute('data-dirty')) return;
    if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
    const link = e.target.closest?.('a[href]');
    if (!link || link.target && link.target !== '_self' || link.hasAttribute('download')) return;
    const url = new URL(link.href, document.baseURI);
    if (url.origin !== location.origin || (url.pathname === location.pathname && url.search === location.search)) return;

    if (window.confirm(message)) {
        s.leaveConfirmedAt = performance.now();
    } else {
        e.preventDefault();
        e.stopImmediatePropagation();
    }
}

// ── 연결 끊김 (R7, Addendum A-21) ──

const LOST_KEY = () => `quro-grid-lost:${location.pathname}`;

function isOffline(s) {
    return s.root.hasAttribute('data-offline');
}

/**
 * Blazor 재연결 상태. hide = 연결됨, rejected = 회로 없음(앱이 페이지를 다시 연다), 그 밖(show·retrying·failed…) = 끊김.
 * 회로가 바뀌어 다시 그려진 화면의 이전 인스턴스(루트가 문서에서 빠짐)는 무시한다.
 */
function onReconnectState(s, state) {
    if (!s.root.isConnected || state === 'rejected') return;
    setOffline(s, state !== 'hide');
}

async function setOffline(s, offline) {
    if (offline === isOffline(s)) return;
    if (offline) {
        s.root.setAttribute('data-offline', '');
        if (s.editing) s.editing.input.readOnly = true;
        // 이 회로로 돌아오지 못하면 변경은 사라진다. 다시 연 화면에서 알릴 수 있게 표시만 남긴다 (데이터는 남기지 않음, O-03)
        if (s.root.hasAttribute('data-dirty')) storeLostFlag(true);
        return;
    }

    s.root.removeAttribute('data-offline');
    if (s.editing) {
        s.editing.input.readOnly = false;
        s.editing.input.focus();
    }
    // 같은 회로로 돌아왔는지 확인한다. 회로가 새로 시작됐으면(세션 복원) 이 화면은 곧 다시 그려지고,
    // 새 인스턴스가 표시를 읽어 안내한다. 회로마다 객체 번호가 1부터라 응답 값(인스턴스 ID)으로 구별한다.
    const same = await s.dotnet.invokeMethodAsync('InstanceId').then(id => id === s.options.instanceId, () => false);
    if (same) storeLostFlag(false);
}

function storeLostFlag(on) {
    try {
        if (on) sessionStorage.setItem(LOST_KEY(), '1');
        else sessionStorage.removeItem(LOST_KEY());
    } catch { /* 저장소를 쓸 수 없으면 안내만 생략 */ }
}

function takeLostFlag() {
    try {
        const lost = sessionStorage.getItem(LOST_KEY()) === '1';
        sessionStorage.removeItem(LOST_KEY());
        return lost;
    } catch {
        return false;
    }
}

/** 선택 목록에서 골라 서버가 값을 확정하고 편집을 끝냈을 때 서버가 호출한다 (Addendum A-17). */
export function endEdit(root) {
    const s = grids.get(root);
    if (!s) return;
    s.editing = null;
    s.root.focus({ preventScroll: true });
}

/** 행 삭제 렌더 뒤 서버가 호출한다: 활성 셀이 사라졌으면 같은 순번(없으면 마지막) 행의 같은 열로. */
export function ensureActive(root) {
    const s = grids.get(root);
    if (!s || !s.active || currentCell(s)) return;
    const all = rows(s);
    if (all.length === 0) {
        s.active = s.anchor = null;
        s.style.textContent = '';
        return;
    }
    const row = all[Math.min(Math.max(s.activeIndex ?? 0, 0), all.length - 1)];
    const cells = cellsOf(row);
    const cell = cells.find(c => c.dataset.field === s.active.field) ?? cells[0];
    activate(s, rowKey(row), cell.dataset.field);
}

// ── 선택 범위 ──

/** anchor와 active를 꼭짓점으로 하는 사각형 (표시 순서 기준). top·left·bottom·right는 행 키·필드. */
function selection(s) {
    const all = rows(s);
    const fieldsAll = cellsOf(all[0]).map(c => c.dataset.field);
    const r1 = all.findIndex(el => rowKey(el) === s.anchor.row);
    const r2 = all.findIndex(el => rowKey(el) === s.active.row);
    const c1 = fieldsAll.indexOf(s.anchor.field);
    const c2 = fieldsAll.indexOf(s.active.field);
    const rowEls = all.slice(Math.min(r1, r2), Math.max(r1, r2) + 1);
    const fields = fieldsAll.slice(Math.min(c1, c2), Math.max(c1, c2) + 1);
    return {
        rowEls, fields,
        top: rowKey(rowEls[0]), bottom: rowKey(rowEls[rowEls.length - 1]),
        left: fields[0], right: fields[fields.length - 1],
    };
}

// ── 클립보드 (엑셀 형식: 탭·줄바꿈, 특수 문자가 있으면 큰따옴표) ──

function quote(text) {
    return /[\t\r\n"]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

function parseTsv(text) {
    const result = [];
    let row = [], cell = '', quoted = false;
    text = text.replace(/\r\n?/g, '\n');
    for (let i = 0; i < text.length; i++) {
        const ch = text[i];
        if (quoted) {
            if (ch !== '"') cell += ch;
            else if (text[i + 1] === '"') { cell += '"'; i++; }
            else quoted = false;
        } else if (ch === '"' && cell === '') quoted = true;
        else if (ch === '\t') { row.push(cell); cell = ''; }
        else if (ch === '\n') { row.push(cell); result.push(row); row = []; cell = ''; }
        else cell += ch;
    }
    if (cell !== '' || row.length > 0) { row.push(cell); result.push(row); }   // 마지막 줄바꿈은 빈 행이 아님
    return result;
}

// ── DOM 좌표 ──

function rows(s) {
    return [...s.root.querySelectorAll('.quro-body [role=row][data-row]')];
}

function cellsOf(row) {
    return row ? [...row.querySelectorAll('[role=gridcell][data-field]')] : [];
}

function cellSelector(row, field) {
    return `[role=row][data-row="${row}"] > [data-field="${CSS.escape(field)}"]`;
}

function cellAt(s, row, field) {
    return s.root.querySelector(`.quro-body ${cellSelector(row, field)}`);
}

function currentCell(s) {
    return s.active ? cellAt(s, s.active.row, s.active.field) : null;
}

function rowKey(rowEl) {
    return Number(rowEl.dataset.row);
}

function position(cell) {
    return { row: rowKey(cell.closest('[role=row]')), field: cell.dataset.field };
}

function same(a, b) {
    return a && b && a.row === b.row && a.field === b.field;
}

function editable(cell) {
    return !!cell && cell.getAttribute('aria-readonly') !== 'true';
}

/** 셀 종류 (서버가 data-type으로 표시: date·combo·check·popup·button, Text·Number·Currency는 없음) */
function cellType(cell) {
    return cell?.dataset.type ?? null;
}

/** 에디터를 여는 셀인지 (Check·Button은 에디터 없이 토글·클릭) */
function hasEditor(cell) {
    const type = cellType(cell);
    return editable(cell) && type !== 'check' && type !== 'button';
}

/** 서버 렌더 결과가 DOM에 반영될 때까지 기다린다 (최대 3초). */
function waitFor(find) {
    return new Promise(resolve => {
        const started = performance.now();
        const tick = () => {
            const found = find();
            if (found || performance.now() - started > 3000) resolve(found ?? null);
            else requestAnimationFrame(tick);
        };
        tick();
    });
}
