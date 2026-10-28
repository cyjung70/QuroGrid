// quro-grid.js — QuroGrid의 얇은 JS 층 (Spec 4.2).
// 소유: 활성 셀(포커스)·키보드 이동·편집 중 키 입력 (R1). 서버 호출은 편집 시작·커밋·취소·행 추가 때만 (R2).
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
        active: null,      // { row: number, field: string }
        editing: null,     // { row, field, input }
        queue: Promise.resolve(),
        pending: 0,        // 처리 대기 중인 동작 수 (서버 응답 대기 포함)
    };
    s.onKeyDown = e => onKeyDown(s, e);
    s.onMouseDown = e => onMouseDown(s, e);
    s.onDblClick = e => onDblClick(s, e);
    s.onFocusOut = e => onFocusOut(s, e);
    root.addEventListener('keydown', s.onKeyDown);
    root.addEventListener('mousedown', s.onMouseDown);
    root.addEventListener('dblclick', s.onDblClick);
    root.addEventListener('focusout', s.onFocusOut);
    grids.set(root, s);
}

export function dispose(root) {
    const s = grids.get(root);
    if (!s) return;
    root.removeEventListener('keydown', s.onKeyDown);
    root.removeEventListener('mousedown', s.onMouseDown);
    root.removeEventListener('dblclick', s.onDblClick);
    root.removeEventListener('focusout', s.onFocusOut);
    s.style.remove();
    grids.delete(root);
}

// ── 이벤트 ──

const HANDLED = new Set(['Enter', 'Tab', 'Escape', 'F2', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight']);

function isChar(e) {
    return e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey;
}

function onKeyDown(s, e) {
    // 편집 중이고 밀린 동작이 없으면 문자·화살표는 에디터가 그대로 받는다 (서버 호출 없음, R2)
    if (s.editing && s.pending === 0 && !['Enter', 'Tab', 'Escape'].includes(e.key)) return;
    if (!HANDLED.has(e.key) && !isChar(e)) return;

    e.preventDefault();
    const key = { key: e.key, shift: e.shiftKey, char: isChar(e) };
    enqueue(s, () => handleKey(s, key));
}

function onMouseDown(s, e) {
    if (e.target.closest('.quro-editor')) return;
    const cell = e.target.closest('.quro-body [role=gridcell][data-field]');
    if (!cell) return;
    const target = position(cell);
    enqueue(s, async () => {
        if (s.editing && !(await commit(s))) return;
        activate(s, target.row, target.field);
    });
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
        case 'ArrowUp': return moveBy(s, -1, 0);
        case 'ArrowDown': return moveBy(s, 1, 0);
        case 'ArrowLeft': return moveBy(s, 0, -1);
        case 'ArrowRight': return moveBy(s, 0, 1);
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

function moveBy(s, dRow, dCol) {
    const all = rows(s);
    const r = all.findIndex(el => rowKey(el) === s.active.row);
    const cells = cellsOf(all[r]);
    const c = cells.findIndex(el => el.dataset.field === s.active.field);
    const nr = Math.min(Math.max(r + dRow, 0), all.length - 1);
    const target = cellsOf(all[nr])[Math.min(Math.max(c + dCol, 0), cells.length - 1)];
    activate(s, rowKey(all[nr]), target.dataset.field);
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

    if (!moved && s.options.addRowOnLastEnter) {
        const key = await s.dotnet.invokeMethodAsync('AddRow');
        const row = await waitFor(() => s.root.querySelector(`.quro-body [role=row][data-row="${key}"]`));
        const first = cellsOf(row).find(editable);
        if (first) activate(s, key, first.dataset.field);
    }
}

function activate(s, row, field) {
    s.active = { row, field };
    s.root.setAttribute('data-active-row', String(row));
    s.root.setAttribute('data-active-field', field);
    s.style.textContent =
        `[data-quro-grid="${s.id}"] [role=row][data-row="${row}"] > [data-field="${CSS.escape(field)}"]` +
        `{outline:2px solid var(--quro-active,#2e6be6);outline-offset:-2px}`;
    currentCell(s)?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    if (!s.editing && document.activeElement !== s.root) s.root.focus({ preventScroll: true });
}

// ── DOM 좌표 ──

function rows(s) {
    return [...s.root.querySelectorAll('.quro-body [role=row][data-row]')];
}

function cellsOf(row) {
    return row ? [...row.querySelectorAll('[role=gridcell][data-field]')] : [];
}

function cellAt(s, row, field) {
    return s.root.querySelector(`.quro-body [role=row][data-row="${row}"] > [data-field="${CSS.escape(field)}"]`);
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
