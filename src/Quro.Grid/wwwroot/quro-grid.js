// quro-grid.js — QuroGrid의 얇은 JS 층 (Spec 4.2).
// 소유: 활성 셀(포커스)·범위 선택·키보드 이동·편집 중 키 입력·클립보드 (R1).
// 서버 호출은 편집 시작·커밋·취소·행 추가·행 삭제·붙여넣기(1회, R3)·활성 행 변경(구독 시) 때만 (R2).
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
    grids.set(root, s);
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
    clearTimeout(s.notifyTimer);
    s.style.remove();
    grids.delete(root);
}

// ── 이벤트 ──

const HANDLED = new Set(['Enter', 'Tab', 'Escape', 'F2', 'Insert', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight']);

function isChar(e) {
    return e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey;
}

function isRowDelete(e) {
    return e.key === 'Delete' && e.ctrlKey;
}

function onKeyDown(s, e) {
    // 편집 중이고 밀린 동작이 없으면 문자·화살표는 에디터가 그대로 받는다 (서버 호출 없음, R2)
    if (s.editing && s.pending === 0 && !['Enter', 'Tab', 'Escape'].includes(e.key)) return;
    if (!HANDLED.has(e.key) && !isChar(e) && !isRowDelete(e)) return;

    e.preventDefault();
    const key = { key: isRowDelete(e) ? 'RowDelete' : e.key, shift: e.shiftKey, char: isChar(e) };
    enqueue(s, () => handleKey(s, key));
}

function onMouseDown(s, e) {
    if (e.button !== 0 || e.target.closest('.quro-editor')) return;
    const cell = e.target.closest('.quro-body [role=gridcell][data-field]');
    if (!cell) return;
    const target = position(cell);
    const extend = e.shiftKey && !!s.anchor;
    s.dragging = true;
    enqueue(s, async () => {
        if (s.editing && !(await commit(s))) return;
        activate(s, target.row, target.field, extend);
    });
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

/** Ctrl+C: 선택 범위의 표시 값을 엑셀 형식(탭·줄바꿈)으로 */
function onCopy(s, e) {
    if (!hasFocus(s) || !s.active) return;
    const { rowEls, fields } = selection(s);
    const text = rowEls
        .map(rowEl => fields
            .map(f => quote(rowEl.querySelector(`:scope > [data-field="${CSS.escape(f)}"]`)?.textContent.trim() ?? ''))
            .join('\t'))
        .join('\r\n');
    e.clipboardData.setData('text/plain', text);
    e.preventDefault();
}

/** Ctrl+V: 엑셀 형식을 행 × 열로 나눠 선택 범위 왼쪽 위부터. 서버 호출 1회 (R3) */
function onPaste(s, e) {
    if (!hasFocus(s) || !s.active) return;
    const cells = parseTsv(e.clipboardData.getData('text/plain'));
    e.preventDefault();
    if (cells.length === 0) return;
    const { top, left } = selection(s);
    enqueue(s, () => s.dotnet.invokeMethodAsync('Paste', top, left, cells));
}

function onDblClick(s, e) {
    const cell = e.target.closest('.quro-body [role=gridcell][data-field]');
    if (!cell) return;
    const target = position(cell);
    enqueue(s, async () => {
        activate(s, target.row, target.field);
        if (!s.editing) await beginEdit(s, null);
    });
}

function onFocusOut(s, e) {
    // 그리드 밖을 클릭하면 편집 중인 값을 커밋한다 (셀 이탈)
    if (s.editing && !s.root.contains(e.relatedTarget)) enqueue(s, () => s.editing ? commit(s) : null);
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

    if (k.char) {
        if (s.editing) appendText(s.editing.input, k.key);
        else if (editable(currentCell(s))) await beginEdit(s, k.key);   // 문자 입력: 덮어쓰기
        return;
    }

    switch (k.key) {
        case 'F2':
            if (!s.editing && editable(currentCell(s))) await beginEdit(s, null);   // 기존 값 유지
            return;
        case 'Escape':
            if (s.editing) await cancel(s);
            return;
        case 'Enter':
            if (s.editing && !(await commit(s))) return;
            await moveEnter(s);
            return;
        case 'Tab':
            if (s.editing && !(await commit(s))) return;
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
        case 'ArrowDown': return moveBy(s, 1, 0, k.shift);
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

async function commit(s) {
    const { row, field, input } = s.editing;
    if (!(await s.dotnet.invokeMethodAsync('CommitEdit', row, field, input.value))) {
        input.focus();   // 해석 실패·OnCellValueChanging 취소: 편집 유지
        return false;
    }
    s.editing = null;
    s.root.focus({ preventScroll: true });
    return true;
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
