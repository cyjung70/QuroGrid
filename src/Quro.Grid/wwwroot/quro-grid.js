// quro-grid.js — QuroGrid의 얇은 JS 층 (Spec 4.2).
// 소유: 활성 셀(포커스)·활성 헤더·범위 선택·키보드 이동·편집 중 키 입력·클립보드 (R1).
// 서버 호출은 편집 시작·커밋·취소·행 추가·행 삭제·붙여넣기(1회, R3)·활성 행 변경(구독 시)·정렬·컬럼 너비·이동(놓을 때)·Undo/Redo·
// 코드도움(F4)·선택 목록 열기(Alt+↓)·체크 토글·버튼 클릭·필터 패널 열기·적용·닫기·그룹 펼침·접기 때만 (R2).
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
        header: null,      // 키보드로 헤더에 있을 때 그 필드 (GS-064). 이때 본문 활성 셀 표시는 없다
        group: null,       // 키보드로 그룹 머리 행에 있을 때 그 그룹 ID (GS-074). 활성 셀(열)은 기억해 둔다
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
    s.onScroll = () => { placePicker(s); placeFilter(s); };
    s.onDocMouseDown = e => onDocMouseDown(s, e);
    s.onReconnect = e => onReconnectState(s, e.detail?.state);
    s.onLinkClick = e => onLinkClick(s, e);
    s.onPaste = e => onPaste(s, e);
    s.onInput = e => { if (!e.isComposing) applyMask(e.target); };   // IME: apply when composition ends
    root.addEventListener('keydown', s.onKeyDown);
    root.addEventListener('input', s.onInput);
    root.addEventListener('compositionend', s.onInput);
    root.addEventListener('mousedown', s.onMouseDown);
    root.addEventListener('mousemove', s.onMouseMove);
    root.addEventListener('dblclick', s.onDblClick);
    root.addEventListener('focusout', s.onFocusOut);
    // 포커스된 div에는 copy·paste 이벤트가 오지 않을 수 있어 문서에서 받고 그리드 포커스 여부로 거른다
    document.addEventListener('mouseup', s.onMouseUp);
    document.addEventListener('mousedown', s.onDocMouseDown, true);   // 필터 패널 밖을 누르면 닫는다
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
    root.removeEventListener('input', s.onInput);
    root.removeEventListener('compositionend', s.onInput);
    root.removeEventListener('mousedown', s.onMouseDown);
    root.removeEventListener('dblclick', s.onDblClick);
    root.removeEventListener('focusout', s.onFocusOut);
    root.removeEventListener('mousemove', s.onMouseMove);
    document.removeEventListener('mouseup', s.onMouseUp);
    document.removeEventListener('mousedown', s.onDocMouseDown, true);
    document.removeEventListener('copy', s.onCopy);
    document.removeEventListener('paste', s.onPaste);
    document.removeEventListener('scroll', s.onScroll, true);
    document.removeEventListener('components-reconnect-state-changed', s.onReconnect, true);
    document.removeEventListener('click', s.onLinkClick, true);
    clearTimeout(s.notifyTimer);
    s.style.remove();
    s.resizeStyle?.remove();
    s.dragStyle?.remove();
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

function isUndoKey(e) {
    return (e.ctrlKey || e.metaKey) && !e.altKey && ['z', 'y'].includes(e.key.toLowerCase());
}

function isPickerKey(e) {
    return e.key === 'ArrowDown' && e.altKey;
}

function onKeyDown(s, e) {
    if (isOffline(s)) {   // 연결 끊김 중 입력 잠금 (R7)
        e.preventDefault();
        return;
    }
    // 필터 패널 안의 키는 입력란이 받는다. Enter 적용, Esc 닫기 (A-31)
    if (e.target.closest('.quro-filter-panel')) {
        if (e.key === 'Enter' || e.key === 'Escape') {
            e.preventDefault();
            enqueue(s, () => e.key === 'Enter' ? applyFilter(s) : closeFilter(s));
        }
        return;
    }
    // 선택 목록(달력·Combo 목록) 안의 키는 목록이 받는다. Esc만 편집 취소.
    if (e.target.closest('.quro-picker')) {
        if (e.key === 'Escape') {
            e.preventDefault();
            enqueue(s, () => s.editing ? cancel(s) : null);
        }
        return;
    }
    // 열린 선택 목록 (GS-091, A-68): 화살표는 강조 이동(브라우저만, R2), Enter는 강조 항목을 고른다(서버 1회)
    if (s.editing && pickerKey(s, e)) return;
    // Undo / Redo (GS-076, A-33): 편집 중이 아니면 서버 1회. 편집 중에는 입력란의 기본 Undo
    if (!s.editing && isUndoKey(e)) {
        e.preventDefault();
        const redo = e.key.toLowerCase() === 'y' || e.shiftKey;
        enqueue(s, () => s.editing ? null : s.dotnet.invokeMethodAsync(redo ? 'Redo' : 'Undo'));
        return;
    }
    // 편집 중이고 밀린 동작이 없으면 문자·화살표는 에디터가 그대로 받는다 (서버 호출 없음, R2)
    if (s.editing && s.pending === 0 && !['Enter', 'Tab', 'Escape', 'F4'].includes(e.key) && !isPickerKey(e)) return;
    if (!HANDLED.has(e.key) && !isChar(e) && !isRowDelete(e)) return;

    e.preventDefault();
    const key = { key: isRowDelete(e) ? 'RowDelete' : e.key, shift: e.shiftKey, alt: e.altKey, ctrl: e.ctrlKey, char: isChar(e) };
    enqueue(s, () => typeAheadFilter(s, key)
        || (s.header ? handleHeaderKey(s, key) : s.group ? handleGroupKey(s, key) : handleKey(s, key)));
}

/** 필터 패널이 열리는 동안 먼저 친 키는 패널이 받는다 (편집 시작 중 입력과 같음). 처리했으면 true */
function typeAheadFilter(s, k) {
    const input = document.activeElement;
    if (!input || !filterPanel(s)?.contains(input)) return false;
    if (k.char && input.type !== 'checkbox') appendText(input, k.key);
    else if (k.key === 'Enter') return applyFilter(s).then(() => true);
    else if (k.key === 'Escape') return closeFilter(s).then(() => true);
    return true;
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
        // 포커스는 에디터에 둔다. 목록을 눌러도 셀 이탈로 보지 않게 한다
        e.preventDefault();
        s.pickerMouseDown = true;
        setTimeout(() => { s.pickerMouseDown = false; });
        if (e.button === 0) pickerAction(s, e.target);
        return;
    }
    if (e.target.closest('.quro-filter-panel')) {
        // 패널 안 입력은 브라우저가 받는다. 적용·해제 버튼만 서버 1회 (해제 = 빈 입력으로 적용)
        const action = e.button === 0 && e.target.closest('[data-filter-action]')?.dataset.filterAction;
        if (action) {
            e.preventDefault();
            enqueue(s, () => applyFilter(s, action === 'clear'));
        }
        return;
    }
    if (e.button !== 0 || e.target.closest('.quro-editor')) return;
    const filterButton = e.target.closest('.quro-head .quro-filter-button');
    if (filterButton) {
        e.preventDefault();   // 정렬·드래그가 아니다
        if (!s.editing) s.root.focus({ preventScroll: true });   // 패널이 열리기 전에 친 키도 그리드가 받아 패널로 넘긴다
        const field = filterButton.closest('[role=columnheader]').dataset.field;
        enqueue(s, () => openFilter(s, field));
        return;
    }
    const groupHead = e.target.closest('.quro-body .quro-group-head-row');
    if (groupHead) {
        // 그룹 머리 행 클릭: 그 그룹으로 옮기고 펼침·접기 (서버 1회, GS-074)
        e.preventDefault();
        const id = groupHead.dataset.group;
        enqueue(s, async () => {
            if (s.editing && !(await commit(s))) return;
            const el = groupEl(s, id);
            if (!el) return;
            enterGroup(s, el);
            await toggleGroup(s, el, el.getAttribute('aria-expanded') !== 'true');
        });
        return;
    }
    const handle = e.target.closest('.quro-head .quro-resize');
    if (handle) {
        startResize(s, handle.closest('[role=columnheader]'), e);
        return;
    }
    const header = e.target.closest('.quro-head [role=columnheader][data-field]');
    if (header) {
        e.preventDefault();   // 포커스는 그리드(편집 중이면 에디터)에 둔다
        startHeaderDrag(s, header, e);
        return;
    }
    const cell = e.target.closest('.quro-body [role=gridcell][data-field]');
    if (!cell) return;
    const target = position(cell);
    const extend = e.shiftKey && !!s.anchor;
    // 셀 안 체크 표시·버튼을 누르면 활성화 후 바로 동작 (셀마다 핸들러를 달지 않는다, R5)
    const type = e.target.closest('.quro-check') ? 'check' : e.target.closest('.quro-button') ? 'button'
        : e.target.closest('.quro-image') ? 'image' : null;
    s.dragging = true;
    enqueue(s, async () => {
        if (s.editing && !(await commit(s))) return;
        activate(s, target.row, target.field, extend);
        if (type) await cellAction(s, type);
    });
}

// ── 헤더: 클릭 정렬 (A-22) / 드래그 이동 (GS-068). 5px 넘게 움직이면 드래그, 아니면 놓을 때 클릭 ──

const DRAG_THRESHOLD = 5;

function startHeaderDrag(s, header, e) {
    const field = header.dataset.field;
    const additive = e.shiftKey;
    const startX = e.clientX;
    let dragging = false;
    let target = null;   // 놓을 화면 위치 (보이는 컬럼 중)

    const move = ev => {
        if (!dragging && Math.abs(ev.clientX - startX) <= DRAG_THRESHOLD) return;
        dragging = true;
        target = dropTarget(s, field, ev.clientX);
        drawDrag(s, field, target);
    };
    const up = () => {
        document.removeEventListener('mousemove', move, true);
        document.removeEventListener('mouseup', up, true);
        drawDrag(s, null);
        if (!dragging) {
            // 클릭 정렬: 편집 중이면 먼저 커밋
            enqueue(s, async () => {
                if (s.editing && !(await commit(s))) return;
                await s.dotnet.invokeMethodAsync('Sort', field, additive);
            });
        } else if (target && target.index !== target.from) {
            // 드래그 중에는 브라우저에서만 표시하고, 놓을 때 서버 1회 (R1). 고정 열 경계는 서버가 맞춘다
            enqueue(s, () => s.dotnet.invokeMethodAsync('MoveColumn', field, target.index));
        }
    };
    document.addEventListener('mousemove', move, true);
    document.addEventListener('mouseup', up, true);
}

/** 포인터 아래 헤더의 왼쪽 절반이면 그 앞, 오른쪽 절반이면 뒤. index는 옮긴 뒤 위치, marker는 표시할 경계 */
function dropTarget(s, field, x) {
    const all = headers(s);
    const from = all.findIndex(h => h.dataset.field === field);
    let insert = all.length;
    for (let i = 0; i < all.length; i++) {
        const r = all[i].getBoundingClientRect();
        if (x < r.left + r.width / 2) { insert = i; break; }
    }
    const marker = insert < all.length ? { field: all[insert].dataset.field, side: 'left' } : { field: all[all.length - 1].dataset.field, side: 'right' };
    return { from, index: insert > from ? insert - 1 : insert, marker };
}

/** 드래그 중 표시: 잡은 헤더는 흐리게, 놓을 경계에 선. field가 null이면 지운다 */
function drawDrag(s, field, target) {
    s.dragStyle ??= document.head.appendChild(document.createElement('style'));
    if (!field) {
        s.dragStyle.textContent = '';
        return;
    }
    const head = `[data-quro-grid="${s.id}"] .quro-head`;
    const line = target.marker.side === 'left' ? 'inset 3px 0 0' : 'inset -3px 0 0';
    s.dragStyle.textContent = [
        `${head} [data-field="${CSS.escape(field)}"] { opacity: 0.5; }`,
        `${head} [data-field="${CSS.escape(target.marker.field)}"] { box-shadow: ${line} var(--qg-active); }`,
    ].join('\n');
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

/**
 * 경계 더블클릭: 헤더·그려진 셀·합계 중 가장 긴 내용에 맞춘다 (GS-065, 최소 40px). 바뀌면 서버 1회.
 * 잠깐 max-content로 그려 재고 바로 되돌린다 (그 사이 화면에는 그려지지 않는다).
 */
function autoFit(s, header) {
    const field = header.dataset.field;
    const grid = `[data-quro-grid="${s.id}"]`;
    const selector = `${grid} [data-field="${CSS.escape(field)}"], ${grid} [data-footer-field="${CSS.escape(field)}"]`;
    s.resizeStyle ??= document.head.appendChild(document.createElement('style'));
    s.resizeStyle.textContent = `${selector} { width: max-content !important; }`;
    const fit = Math.max(MIN_WIDTH, ...[...document.querySelectorAll(selector)].map(el => Math.ceil(el.getBoundingClientRect().width)));
    s.resizeStyle.textContent = '';
    if (fit === Math.round(header.getBoundingClientRect().width)) return;
    enqueue(s, () => s.dotnet.invokeMethodAsync('ColumnResized', field, fit));
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

/** Copy and paste turned off by the grid (Clipboard=false, G-03). Read from the markup so a change applies at once. */
function clipboardOff(s) {
    return s.root.dataset.clipboard === 'off';
}

function hasFocus(s) {
    return !s.editing && s.root.contains(document.activeElement);
}

/** Ctrl+C: 선택 범위의 표시 값을 엑셀 형식(탭·줄바꿈)으로. 표시와 값이 다른 셀(Check·Button)은 data-copy. */
function onCopy(s, e) {
    if (!hasFocus(s) || !s.active || clipboardOff(s)) return;
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
    if (clipboardOff(s)) {
        e.preventDefault();
        return;
    }
    if (isOffline(s)) {
        e.preventDefault();
        return;
    }
    const cells = parseTsv(e.clipboardData.getData('text/plain'));
    e.preventDefault();
    if (cells.length === 0) return;
    const { top, left } = selection(s);
    enqueue(s, () => sendPaste(s, top, left, cells));
}

// SignalR 한 메시지 크기 제한(Blazor Server 기본 32KB)보다 충분히 작게
const PASTE_INLINE_BYTES = 16 * 1024;

/**
 * 붙여넣기 전송 (호출 1회, R3). 큰 붙여넣기(500행 × 10열 등)는 메시지 크기 제한에 걸려 연결이 끊길 수 있으므로,
 * 값을 담아 두고 서버가 스트림으로 가져가게 한다 (PasteStream → pasteData).
 */
async function sendPaste(s, top, left, cells) {
    const data = new TextEncoder().encode(JSON.stringify(cells));
    if (data.length <= PASTE_INLINE_BYTES) return s.dotnet.invokeMethodAsync('Paste', top, left, cells);
    s.pasteData = data;
    try {
        return await s.dotnet.invokeMethodAsync('PasteStream', top, left);
    } finally {
        s.pasteData = null;
    }
}

/** 서버가 큰 붙여넣기 값을 스트림으로 가져간다 (IJSStreamReference). */
export function pasteData(root) {
    return grids.get(root)?.pasteData ?? new Uint8Array();
}

function onDblClick(s, e) {
    if (e.target.closest('.quro-picker') || isOffline(s)) return;
    const handle = e.target.closest('.quro-head .quro-resize');
    if (handle) {
        autoFit(s, handle.closest('[role=columnheader]'));
        return;
    }
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
        else if (type === 'check' || type === 'button' || type === 'image') { if (k.key === ' ') await cellAction(s, type); }   // Space: 토글·클릭
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
            if (!s.editing && (type === 'button' || type === 'image')) return cellAction(s, type);   // Button·Image 셀: 클릭, 이동 없음
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
            // 페이징 중에는 화면 밖까지 이어진 범위를 위치로 보낸다 (A-25). 서버가 필요한 범위를 적재한 뒤 삭제
            if (s.editing) return;
            if (s.options.paging) {
                const { topIndex, bottomIndex } = selection(s);
                await s.dotnet.invokeMethodAsync('DeleteRowRange', topIndex, bottomIndex);
            } else {
                await s.dotnet.invokeMethodAsync('DeleteRows', selectedKeys(s));
            }
            return;
        case 'ArrowUp': {
            // 헤더로 (GS-064): Ctrl+↑는 어느 행에서나, ↑는 첫 행에서. 위가 그룹 머리 행이면 그 행으로 (GS-074)
            if (k.ctrl) return enterHeader(s, s.active.field);
            if (!k.shift && s.options.paging && rowIndexOf(rowEl(s, s.active.row)) === 0) return enterHeader(s, s.active.field);
            if (!k.shift && !s.options.paging) {
                const prev = navNeighbor(s, rowEl(s, s.active.row), -1);
                if (!prev) return enterHeader(s, s.active.field);
                if (isGroupHead(prev)) return enterGroup(s, prev);
            }
            return moveBy(s, -1, 0, k.shift);
        }
        case 'ArrowDown': {
            if (k.alt) return openPicker(s);
            const next = !k.shift && !s.options.paging ? navNeighbor(s, rowEl(s, s.active.row), 1) : null;
            if (next && isGroupHead(next)) return enterGroup(s, next);
            return moveBy(s, 1, 0, k.shift);
        }
        case 'ArrowLeft': return moveBy(s, 0, -1, k.shift);
        case 'ArrowRight': return moveBy(s, 0, 1, k.shift);
    }
}

// ── 헤더 키보드 (GS-064): ←→ 헤더 이동, Enter 정렬(Shift+Enter 추가), ↓ 본문 첫 행. 그 밖의 키는 무시 ──

function headers(s) {
    return [...s.root.querySelectorAll('.quro-head [role=columnheader][data-field]')];
}

function enterHeader(s, field) {
    if (s.group) leaveGroup(s);
    s.header = field;
    s.root.setAttribute('data-active-header', field);
    const scope = `[data-quro-grid="${s.id}"]`;
    s.style.textContent = `${scope} .quro-head [data-field="${CSS.escape(field)}"]{outline:2px solid var(--qg-active);outline-offset:-2px}`;
    const el = headers(s).find(h => h.dataset.field === field);
    markSelected(s, el ? [el] : []);
    el?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
}

function leaveHeader(s) {
    s.header = null;
    s.root.removeAttribute('data-active-header');
}

async function handleHeaderKey(s, k) {
    const all = headers(s);
    const i = all.findIndex(h => h.dataset.field === s.header);
    switch (k.key) {
        case 'ArrowLeft':
        case 'ArrowRight': {
            const next = all[Math.min(Math.max(i + (k.key === 'ArrowLeft' ? -1 : 1), 0), all.length - 1)];
            if (next) enterHeader(s, next.dataset.field);
            return;
        }
        case 'Enter':
            await s.dotnet.invokeMethodAsync('Sort', s.header, k.shift);
            return;
        case 'ArrowDown': {
            if (k.alt) return openFilter(s, s.header);   // Alt+↓: 필터 패널 (A-31)
            if (s.options.paging) s.root.scrollTop = 0;   // 첫 행이 그려져 있지 않을 수 있다
            const first = await waitFor(() => s.root.querySelector(`.quro-body [role=row][aria-rowindex="${headerRows(s.root) + 1}"]`));
            if (!first) return;
            if (isGroupHead(first)) return enterGroup(s, first);   // 그룹핑 중 첫 행은 그룹 머리 행 (GS-074)
            const cells = cellsOf(first);
            const cell = cells.find(c => c.dataset.field === s.header) ?? cells[0];
            if (cell) activate(s, rowKey(first), cell.dataset.field);
            return;
        }
    }
}

// ── 헤더 필터 패널 (GS-071·GS-072, A-31): 열기·적용·닫기만 서버 1회. 패널 안 입력은 브라우저에서만 (R2) ──

function filterPanel(s) {
    return s.root.querySelector('.quro-filter-panel');
}

async function openFilter(s, field) {
    if (s.editing && !(await commit(s))) return;
    if (!(await s.dotnet.invokeMethodAsync('OpenFilter', field))) return;
    const panel = await waitFor(() => s.root.querySelector(`.quro-filter-panel[data-field="${CSS.escape(field)}"]`));
    if (!panel) return;
    placeFilter(s);
    panel.querySelector('[data-filter]')?.focus();
}

/** 패널 입력을 모아 적용한다 (clear면 빈 입력 = 그 컬럼 해제). 해석에 실패하면 패널이 남고 그 입력으로 포커스 */
async function applyFilter(s, clear = false) {
    const panel = filterPanel(s);
    if (!panel) return;
    const field = panel.dataset.field;
    const value = name => clear ? null : (panel.querySelector(`[data-filter=${name}]`)?.value ?? null);
    const options = [...panel.querySelectorAll('[data-filter=value]')];
    const input = {
        text: value('text'), min: value('min'), max: value('max'), from: value('from'), to: value('to'),
        values: clear || options.length === 0 ? null : options.filter(o => o.checked).map(o => o.value),
    };
    if (await s.dotnet.invokeMethodAsync('ApplyFilter', field, input)) {
        s.root.focus({ preventScroll: true });
        return;
    }
    const invalid = await waitFor(() => filterPanel(s)?.querySelector('[aria-invalid=true]'));
    invalid?.focus();
}

async function closeFilter(s) {
    if (!filterPanel(s)) return;
    await s.dotnet.invokeMethodAsync('CloseFilter');
    s.root.focus({ preventScroll: true });
}

/** 패널 밖(필터 버튼 제외)을 누르면 닫는다 */
function onDocMouseDown(s, e) {
    const panel = filterPanel(s);
    if (!panel || panel.contains(e.target) || (s.root.contains(e.target) && e.target.closest('.quro-filter-button'))) return;
    enqueue(s, () => filterPanel(s) ? s.dotnet.invokeMethodAsync('CloseFilter') : null);
}

/** 패널을 그 컬럼 헤더 바로 아래에 둔다 (화면 기준, 그리드 스크롤 영역에 잘리지 않게) */
function placeFilter(s) {
    const panel = filterPanel(s);
    const header = panel && headers(s).find(h => h.dataset.field === panel.dataset.field);
    if (!header) return;
    const r = header.getBoundingClientRect();
    panel.style.left = `${Math.max(0, Math.min(r.left, window.innerWidth - panel.offsetWidth))}px`;
    panel.style.top = `${r.bottom}px`;
}

// ── 그룹 머리 행 키보드 (GS-074, A-42): ↑↓ 이동(소계 행은 건너뜀), Space·Enter 펼침·접기, ← 접기, → 펼치기 ──

function isGroupHead(el) {
    return el.classList.contains('quro-group-head-row');
}

function groupEl(s, id) {
    return [...s.root.querySelectorAll('.quro-body .quro-group-head-row')].find(el => el.dataset.group === id) ?? null;
}

/** ↑↓ 이동 대상 (화면 순서): 데이터 행과 그룹 머리 행. 소계 행은 아니다 */
function navNeighbor(s, el, dir) {
    if (!el) return null;
    const all = [...s.root.querySelectorAll('.quro-body [role=row][data-row], .quro-body .quro-group-head-row')];
    return all[all.indexOf(el) + dir] ?? null;
}

function enterGroup(s, el) {
    if (s.header) leaveHeader(s);
    s.group = el.dataset.group;
    s.root.setAttribute('data-active-group', s.group);
    const scope = `[data-quro-grid="${s.id}"]`;
    s.style.textContent = `${scope} .quro-group-head-row[data-group="${CSS.escape(s.group)}"] > .quro-group-head-cell{outline:2px solid var(--qg-active);outline-offset:-2px}`;
    markSelected(s, [el]);
    el.scrollIntoView({ block: 'nearest' });
    if (document.activeElement !== s.root) s.root.focus({ preventScroll: true });
}

function leaveGroup(s) {
    s.group = null;
    s.root.removeAttribute('data-active-group');
}

/** 펼침·접기 (서버 1회). 이미 그 상태면 호출하지 않는다 */
async function toggleGroup(s, el, expand) {
    if ((el.getAttribute('aria-expanded') === 'true') === expand) return;
    await s.dotnet.invokeMethodAsync('ExpandGroup', el.dataset.group, expand);
}

async function handleGroupKey(s, k) {
    const el = groupEl(s, s.group);
    if (!el) return leaveGroup(s);   // 그룹이 사라짐 (그룹 해제·다시 조회)
    switch (k.key) {
        case 'ArrowUp':
        case 'ArrowDown': {
            const target = navNeighbor(s, el, k.key === 'ArrowUp' ? -1 : 1);
            if (!target) {
                if (k.key === 'ArrowUp') enterHeader(s, s.active?.field ?? headers(s)[0]?.dataset.field);
                return;
            }
            if (isGroupHead(target)) return enterGroup(s, target);
            const cells = cellsOf(target);
            const cell = cells.find(c => c.dataset.field === s.active?.field) ?? cells[0];   // 들어올 때의 열로
            if (cell) activate(s, rowKey(target), cell.dataset.field);
            return;
        }
        case ' ':
        case 'Enter':
            return toggleGroup(s, el, el.getAttribute('aria-expanded') !== 'true');
        case 'ArrowLeft': return toggleGroup(s, el, false);
        case 'ArrowRight': return toggleGroup(s, el, true);
    }
}

/** 스크린리더용 선택 표시 (GS-066): 이전 표시를 지우고 주어진 요소에 aria-selected */
function markSelected(s, els) {
    for (const el of s.root.querySelectorAll('[aria-selected=true]'))
        if (!el.closest('.quro-picker')) el.removeAttribute('aria-selected');
    for (const el of els) el.setAttribute('aria-selected', 'true');
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
    if (initialText !== null) applyMask(input);   // a typed character that doesn't fit the first position is dropped
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
 * 앱이 띄운 팝업(다이얼로그 등)이 닫히면서 포커스가 문서(body)로 빠지는 경우가 있어,
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
    const calendar = s.root.querySelector('.quro-picker .quro-calendar');
    if (calendar) {
        // 강조는 현재 값(없으면 오늘)에서 시작한다
        const now = new Date();
        const start = /^\d{4}-\d{2}-\d{2}$/.test(calendar.dataset.iso ?? '')
            ? calendar.dataset.iso
            : isoDate(new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate())));
        s.calendar = { year: +start.slice(0, 4), month: +start.slice(5, 7) - 1, active: start };
        drawCalendar(s, calendar);
    }
    const options = s.root.querySelectorAll('.quro-picker .quro-options [data-index]');
    if (options.length) {
        const selected = [...options].findIndex(o => o.getAttribute('aria-selected') === 'true');
        s.option = Math.max(0, selected);
        markOption(s);
    }
    placePicker(s);
}

const isoDate = d => `${String(d.getUTCFullYear()).padStart(4, '0')}-${String(d.getUTCMonth() + 1).padStart(2, '0')}-${String(d.getUTCDate()).padStart(2, '0')}`;

/** 열린 선택 목록의 키 (GS-091). 처리했으면 true. Alt·Ctrl·Shift 조합과 그 밖의 키(글자 등)는 에디터가 받는다 */
function pickerKey(s, e) {
    if (e.altKey || e.ctrlKey || e.metaKey || e.shiftKey) return false;
    const calendar = s.root.querySelector('.quro-picker .quro-calendar');
    const options = s.root.querySelectorAll('.quro-picker .quro-options [data-index]');
    if (calendar && s.calendar) {
        const step = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -7, ArrowDown: 7 }[e.key];
        if (step) {
            const [y, m, d] = s.calendar.active.split('-').map(Number);
            const next = new Date(Date.UTC(y, m - 1, d + step));
            s.calendar = { year: next.getUTCFullYear(), month: next.getUTCMonth(), active: isoDate(next) };
            drawCalendar(s, calendar);
        } else if (e.key === 'Enter') {
            const value = s.calendar.active;
            enqueue(s, () => s.dotnet.invokeMethodAsync('PickDate', value));
        } else return false;
    } else if (options.length && s.option !== undefined) {
        if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
            s.option = Math.min(options.length - 1, Math.max(0, s.option + (e.key === 'ArrowDown' ? 1 : -1)));
            markOption(s);
        } else if (e.key === 'Enter') {
            const index = s.option;
            enqueue(s, () => s.dotnet.invokeMethodAsync('PickOption', index));
        } else return false;
    } else return false;
    e.preventDefault();
    return true;
}

/** Combo 목록의 강조 항목 표시 (data-active) */
function markOption(s) {
    s.root.querySelectorAll('.quro-picker .quro-options [data-index]').forEach(o => {
        if (+o.dataset.index === s.option) {
            o.setAttribute('data-active', '');
            o.scrollIntoView({ block: 'nearest' });
        } else o.removeAttribute('data-active');
    });
}

/**
 * 선택 목록 안 누름 (A-67): 달 넘김은 브라우저에서 다시 그리고(서버 호출 없음, R2),
 * 날짜·항목을 고르면 서버 1회 (PickDate·PickOption). 서버가 확정하고 endEdit를 부른다.
 */
function pickerAction(s, target) {
    const nav = target.closest('.quro-calendar [data-nav]');
    if (nav) {
        const d = new Date(Date.UTC(s.calendar.year, s.calendar.month + +nav.dataset.nav, 1));
        s.calendar = { ...s.calendar, year: d.getUTCFullYear(), month: d.getUTCMonth() };
        drawCalendar(s, nav.closest('.quro-calendar'));
        placePicker(s);
        return;
    }
    const day = target.closest('.quro-calendar [data-value]');
    if (day) {
        enqueue(s, () => s.dotnet.invokeMethodAsync('PickDate', day.dataset.value));
        return;
    }
    const option = target.closest('.quro-options [data-index]');
    if (option) enqueue(s, () => s.dotnet.invokeMethodAsync('PickOption', +option.dataset.index));
}

/** 달력 그리기: 문화권(data-culture)의 달 이름·요일, 주 시작 요일(data-firstday), 6주. 선택 날짜는 data-iso */
function drawCalendar(s, calendar) {
    const { year, month } = s.calendar;
    const pad = (n, width) => String(n).padStart(width, '0');
    const iso = (y, m, d) => `${pad(y, 4)}-${pad(m + 1, 2)}-${pad(d, 2)}`;
    const utc = (y, m, d) => new Date(Date.UTC(y, m, d));
    const format = (date, options) =>
        new Intl.DateTimeFormat(calendar.dataset.culture || undefined, { ...options, timeZone: 'UTC' }).format(date);
    const element = (tag, cls, text) => {
        const el = document.createElement(tag);
        el.className = cls;
        if (text !== undefined) el.textContent = text;
        return el;
    };
    const button = (cls, text, data) => {
        const b = element('button', cls, text);
        b.type = 'button';
        b.tabIndex = -1;
        Object.assign(b.dataset, data);
        return b;
    };

    const head = element('div', 'quro-cal-head');
    head.append(button('quro-cal-nav', '‹', { nav: '-1' }),
        element('span', 'quro-cal-title', format(utc(year, month, 1), { year: 'numeric', month: 'long' })),
        button('quro-cal-nav', '›', { nav: '1' }));
    const days = element('div', 'quro-cal-days');
    const first = +(calendar.dataset.firstday || 0);
    for (let i = 0; i < 7; i++)   // 2023-01-01은 일요일
        days.append(element('span', 'quro-cal-dow', format(utc(2023, 0, 1 + (first + i) % 7), { weekday: 'short' })));
    const now = new Date();
    const today = iso(now.getFullYear(), now.getMonth(), now.getDate());
    const lead = (utc(year, month, 1).getUTCDay() - first + 7) % 7;
    for (let i = 0; i < 42; i++) {
        const date = utc(year, month, 1 - lead + i);
        const value = iso(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate());
        const b = button('quro-cal-day', String(date.getUTCDate()), { value });
        if (date.getUTCMonth() !== month) b.dataset.outside = '';
        if (value === calendar.dataset.iso) b.setAttribute('aria-selected', 'true');
        if (value === today) b.dataset.today = '';
        if (value === s.calendar.active) b.dataset.active = '';
        days.append(b);
    }
    calendar.replaceChildren(head, days);
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

/** Check 토글 · Button 클릭 · Image 클릭 (서버 호출 1회) */
function cellAction(s, type) {
    const method = type === 'check' ? 'ToggleCheck' : type === 'image' ? 'ClickCell' : 'ClickButton';
    return s.dotnet.invokeMethodAsync(method, s.active.row, s.active.field);
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

    // 페이징 중에는 그려진 마지막 행이 실제 마지막 행이 아닐 수 있어 새 행을 추가하지 않는다 (A-24)
    if (!moved && s.options.addRowOnLastEnter && !s.options.paging) await addRow(s, null);
}

/** 새 행을 추가하고(서버) 그 행의 첫 편집 가능 셀로. afterRow가 null이면 목록 끝. */
async function addRow(s, afterRow) {
    const key = await s.dotnet.invokeMethodAsync('AddRow', afterRow);
    if (s.options.paging) s.root.scrollTop = 0;   // 페이징 중 추가 행은 맨 위 (A-24)
    const row = await waitFor(() => s.root.querySelector(`.quro-body [role=row][data-row="${key}"]`));
    if (!row) return;
    const first = cellsOf(row).find(editable);
    if (first) activate(s, key, first.dataset.field);
}

/**
 * 활성 셀 이동. extend면 고정 끝(anchor)을 두고 범위를 넓힌다. 아니면 범위는 한 셀로 줄어든다.
 * focus가 false면 포커스를 가져오지 않는다 (앱이 DataSet 현재 행을 옮긴 경우, A-51).
 */
function activate(s, row, field, extend = false, focus = true) {
    // 화면 위치(aria-rowindex)도 기억한다: 페이징 중 범위의 끝 행이 화면 밖으로 나가 그려지지 않아도 범위를 안다 (A-25)
    const index = rowIndexOf(rowEl(s, row));
    if (s.header) leaveHeader(s);
    if (s.group) leaveGroup(s);
    s.active = { row, field, index };
    if (!extend || !s.anchor) s.anchor = { row, field, index };

    const sel = selection(s);
    s.root.setAttribute('data-active-row', String(row));
    s.root.setAttribute('data-active-field', field);
    s.root.setAttribute('data-selection', `${sel.top},${sel.left},${sel.bottom},${sel.right}`);

    // 서버 렌더가 셀 class를 바꿔도 지워지지 않도록 별도 style 규칙으로 그린다
    const scope = `[data-quro-grid="${s.id}"]`;
    let css = `${scope} ${cellSelector(row, field)}{outline:2px solid var(--qg-active);outline-offset:-2px}`;
    if (sel.rowEls.length > 1 || sel.fields.length > 1) {
        const selectors = sel.rowEls.flatMap(r => sel.fields.map(f => `${scope} ${cellSelector(rowKey(r), f)}`));
        css += `${selectors.join(',')}{background:var(--qg-selected)}`;
    }
    s.style.textContent = css;
    markSelected(s, sel.rowEls.flatMap(r => sel.fields.map(f => cellAt(s, rowKey(r), f))).filter(Boolean));
    currentCell(s)?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    if (focus && !s.editing && document.activeElement !== s.root) s.root.focus({ preventScroll: true });
    notifySelection(s);
}

/**
 * 활성 행이 바뀌면 서버에 알린다 (Addendum A-13·A-62: DataSet 현재 행을 맞추므로 항상). 같은 행 안의 이동은 보내지 않는다.
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

/** 선택 범위의 화면 위치 [위, 아래] (툴바 일괄 삭제, 페이징 중, A-25). */
export function selectedRange(root) {
    const s = grids.get(root);
    if (!s || !s.active) return null;
    const { topIndex, bottomIndex } = selection(s);
    return [topIndex, bottomIndex];
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

/**
 * 행 삭제·컬럼 숨김 렌더 뒤 서버가 호출한다: 활성 셀이 사라졌으면 같은 행(행이 없어졌으면 같은 순번, 없으면 마지막 행)의
 * 같은 열(숨겼으면 첫 열)로.
 */
export function ensureActive(root) {
    const s = grids.get(root);
    // 그룹 머리 행에 있으면 그대로 둔다: 그 그룹을 접어 활성 셀이 가려진 경우 (GS-074)
    if (!s || !s.active || currentCell(s) || (s.group && groupEl(s, s.group))) return;
    const all = rows(s);
    if (all.length === 0) {
        s.active = s.anchor = null;
        s.style.textContent = '';
        return;
    }
    const row = rowEl(s, s.active.row) ?? all[Math.min(Math.max(s.activeIndex ?? 0, 0), all.length - 1)];
    const cells = cellsOf(row);
    const cell = cells.find(c => c.dataset.field === s.active.field) ?? cells[0];
    activate(s, rowKey(row), cell.dataset.field);
}

/**
 * 앱(또는 다른 컨트롤)이 DataSet 현재 행을 옮긴 뒤 서버가 호출한다 (A-51, GS-085): 활성 셀을 그 행의 같은 열(없으면 첫 열)로.
 * 포커스는 가져오지 않고 서버로 되돌려 보내지 않는다. 편집 중이면 옮기지 않는다.
 * 페이징 중 그려지지 않은 행이면 index 위치로 스크롤한 뒤 그려지면 옮긴다 (index가 -1이면 그려진 경우에만).
 */
export function activateRow(root, row, index) {
    const s = grids.get(root);
    if (!s || s.editing) return;
    const place = () => {
        const el = rowEl(s, row);
        if (!el) return false;
        const cells = cellsOf(el);
        if (cells.length === 0) return true;
        const cell = cells.find(c => c.dataset.field === s.active?.field) ?? cells[0];
        s.notifiedRow = row;   // 서버가 이미 아는 행
        activate(s, row, cell.dataset.field, false, false);
        return true;
    };
    if (place() || !s.options.paging || index < 0) return;
    const height = parseFloat(getComputedStyle(s.root).getPropertyValue('--quro-row-height')) || 28;
    s.root.scrollTop = index * height;
    let frames = 0;
    const retry = () => { if (!place() && ++frames < 30) requestAnimationFrame(retry); };
    requestAnimationFrame(retry);
}

// ── 선택 범위 ──

/** anchor와 active를 꼭짓점으로 하는 사각형 (표시 순서 기준). top·left·bottom·right는 행 키·필드. */
/**
 * 선택 범위. 행은 화면 위치(aria-rowindex)로 정한다: 그려진 끝 행은 위치를 새로 읽고(정렬·추가로 바뀜),
 * 화면 밖으로 나간 끝 행은 기억한 위치를 쓴다 (페이징, A-25). rowEls는 범위 안의 그려진 행만.
 */
function selection(s) {
    const all = rows(s);
    for (const end of [s.anchor, s.active]) {
        const el = rowEl(s, end.row);
        if (el) end.index = rowIndexOf(el);
    }
    const fieldsAll = cellsOf(all[0]).map(c => c.dataset.field);
    const topIndex = Math.min(s.anchor.index, s.active.index);
    const bottomIndex = Math.max(s.anchor.index, s.active.index);
    const c1 = fieldsAll.indexOf(s.anchor.field);
    const c2 = fieldsAll.indexOf(s.active.field);
    const rowEls = all.filter(el => { const i = rowIndexOf(el); return i >= topIndex && i <= bottomIndex; });
    const fields = fieldsAll.slice(Math.min(c1, c2), Math.max(c1, c2) + 1);
    return {
        rowEls, fields, topIndex, bottomIndex,
        top: rowKey(rowEls[0]), bottom: rowKey(rowEls[rowEls.length - 1]),
        left: fields[0], right: fields[fields.length - 1],
    };
}

function rowEl(s, key) {
    return s.root.querySelector(`.quro-body [role=row][data-row="${key}"]`);
}

/** 화면 위치 (0부터). 서버가 aria-rowindex = 위치 + 헤더 행 수 + 1로 그린다 (다단 헤더면 헤더가 여러 행, GS-070). */
function rowIndexOf(el) {
    return el ? Number(el.getAttribute('aria-rowindex')) - headerRows(el.closest('[role=grid]')) - 1 : -1;
}

function headerRows(root) {
    return Number(root?.dataset.headerRows ?? 1);
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

// ── Mask cell editor (GS-095, A-56) ──
// Same rules as QuroMask on the server (0 digit, A letter of any language, * letter or digit, \ makes the next
// character fixed). While typing, characters that don't fit their position are dropped and the fixed characters
// between typed ones are added; the caret stays after the same typed character. The server reads the text on commit.
const maskCache = new Map();

function parseMask(mask) {
    if (maskCache.has(mask)) return maskCache.get(mask);
    const slots = [];
    for (let i = 0; i < mask.length; i++) {
        const c = mask[i];
        if (c === '\\' && i + 1 < mask.length) slots.push({ fixed: mask[++i] });
        else if (c === '0') slots.push({ test: ch => /[0-9]/.test(ch) });
        else if (c === 'A') slots.push({ test: ch => /\p{L}/u.test(ch) });
        else if (c === '*') slots.push({ test: ch => /[0-9]|\p{L}/u.test(ch) });
        else slots.push({ fixed: c });
    }
    maskCache.set(mask, slots);
    return slots;
}

/** Characters of text that fit the mask's input positions in order (fixed characters and misfits are dropped). */
function maskedChars(slots, text) {
    const inputs = slots.filter(x => x.test);
    const typed = [];
    for (const ch of text) {
        if (typed.length === inputs.length) break;
        if (inputs[typed.length].test(ch)) typed.push(ch);
    }
    return typed;
}

function applyMask(input) {
    if (!(input instanceof HTMLInputElement) || !input.classList.contains('quro-editor')) return;
    const mask = input.dataset.mask;
    if (!mask) return;
    const slots = parseMask(mask);
    const text = input.value;
    const caret = input.selectionStart ?? text.length;
    const typed = maskedChars(slots, text);
    const before = maskedChars(slots, text.slice(0, caret)).length;

    let shown = '';
    let next = 0;
    let newCaret = 0;
    for (const slot of slots) {
        if (next === typed.length) break;
        shown += slot.fixed ?? typed[next++];
        if (slot.test && next === before) newCaret = shown.length;
    }
    if (shown === text) return;
    input.value = shown;
    input.setSelectionRange(newCaret, newCaret);
}

/** 셀 종류 (서버가 data-type으로 표시: date·combo·check·popup·button·mask·image, Text·Number·Currency는 없음) */
function cellType(cell) {
    return cell?.dataset.type ?? null;
}

/** 에디터를 여는 셀인지 (Check·Button은 에디터 없이 토글·클릭) */
function hasEditor(cell) {
    const type = cellType(cell);
    return editable(cell) && type !== 'check' && type !== 'button' && type !== 'image';
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
