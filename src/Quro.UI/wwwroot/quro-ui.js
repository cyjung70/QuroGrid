// QuroUI browser layer (Control Spec 8·9, R2): character filtering, select-all and the combo list (open, filter,
// arrow keys) run here, so typing never calls the server. Options come from data attributes on the control root and are read at event time (no call when they change).

const filters = {
    digits: /[^0-9]/g,
    alpha: /[^\p{L}]/gu,
    alphanumeric: /[^\p{L}0-9]/gu,
};

const attached = new WeakSet();

/** Listens on a control root once. Marks it with data-quro-attached. */
export function attach(root) {
    if (!root || attached.has(root)) return;
    attached.add(root);

    root.addEventListener('input', e => {
        if (e.isComposing) return;   // IME: filter when composition ends
        filter(root, e.target);
        applyMask(root, e.target);
    });
    root.addEventListener('compositionend', e => {
        filter(root, e.target);
        applyMask(root, e.target);
    });

    let selectOnMouseUp = false;
    root.addEventListener('focusin', e => {
        if (root.dataset.quroAutoselect !== 'true' || !(e.target instanceof HTMLInputElement)) return;
        e.target.select();
        selectOnMouseUp = true;   // a click would place the caret on mouseup and drop the selection
    });
    root.addEventListener('mouseup', e => {
        if (!selectOnMouseUp) return;
        selectOnMouseUp = false;
        e.preventDefault();
    });
    root.addEventListener('focusout', () => { selectOnMouseUp = false; });

    if (root.classList.contains('quro-ui-combo')) attachCombo(root);
    if (root.classList.contains('quro-ui-date')) attachDate(root);

    root.dataset.quroAttached = '';
}

function filter(root, input) {
    const pattern = filters[root.dataset.quroChars];
    if (!pattern || !(input instanceof HTMLInputElement)) return;
    const text = input.value;
    const clean = text.replace(pattern, '');
    if (clean === text) return;
    // keep the caret after the characters that stayed
    const caret = (input.selectionStart ?? text.length);
    const kept = text.slice(0, caret).replace(pattern, '').length;
    input.value = clean;
    input.setSelectionRange(kept, kept);
}

// ── mask box (GS-U19, A-69) ──
// Same rules as QuroMask on the server: 0 digit, A letter of any language, * letter or digit, \ makes the next
// character fixed. While typing, characters that don't fit their position are dropped and the fixed characters
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
function typedChars(slots, text) {
    const inputs = slots.filter(s => s.test);
    const typed = [];
    for (const ch of text) {
        if (typed.length === inputs.length) break;
        if (inputs[typed.length].test(ch)) typed.push(ch);
    }
    return typed;
}

function applyMask(root, input) {
    const mask = root.dataset.quroMask;
    if (!mask || !(input instanceof HTMLInputElement)) return;
    const slots = parseMask(mask);
    const text = input.value;
    const caret = input.selectionStart ?? text.length;
    const typed = typedChars(slots, text);
    const before = typedChars(slots, text.slice(0, caret)).length;

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

// ── combo box (GS-U09) ──
// The server renders every item as li[data-index]. A choice is sent once through the hidden input's change event:
// "" (empty item / none checked), "2", or "0,3" for multi-select. Free text is not kept: closing the list restores
// the last choice (data-quro-display).
function attachCombo(root) {
    const input = () => root.querySelector('input.quro-ui-input');
    const items = () => [...root.querySelectorAll('.quro-ui-combo-list li[data-index]')];
    const shown = () => items().filter(li => !li.hidden);
    const multiple = () => root.dataset.quroMultiple === 'true';
    const isOpen = () => root.hasAttribute('data-quro-open');
    let active = null;

    function setActive(li) {
        active?.removeAttribute('data-quro-active');
        active = li ?? null;
        if (!active) return;
        active.setAttribute('data-quro-active', '');
        active.scrollIntoView?.({ block: 'nearest' });
    }

    function open() {
        if (isOpen() || root.dataset.quroReadonly === 'true' || input().disabled) return;
        root.setAttribute('data-quro-open', '');
        input().setAttribute('aria-expanded', 'true');
        setActive(items().find(li => li.getAttribute('aria-selected') === 'true') ?? shown()[0]);
    }

    function close(restore) {
        if (isOpen()) {
            root.removeAttribute('data-quro-open');
            input().setAttribute('aria-expanded', 'false');
            setActive(null);
            items().forEach(li => { li.hidden = false; });
        }
        if (restore) input().value = root.dataset.quroDisplay ?? '';
    }

    function filterList() {
        const query = input().value.trim().toLowerCase();
        open();
        items().forEach(li => {
            li.hidden = query !== '' && (li.dataset.index === '' || !(li.dataset.search ?? '').includes(query));
        });
        setActive(shown()[0]);
    }

    function move(step) {
        const list = shown();
        if (list.length === 0) return;
        const at = list.indexOf(active);
        setActive(list[at < 0 ? 0 : Math.min(list.length - 1, Math.max(0, at + step))]);
    }

    function send(payload, display) {
        const choice = root.querySelector('input.quro-ui-combo-choice');
        choice.value = payload;
        choice.dispatchEvent(new Event('change', { bubbles: true }));
        root.dataset.quroDisplay = display;
        input().value = display;
    }

    function choose(li) {
        if (!li) return;
        if (multiple()) {
            const options = items().filter(x => x.dataset.index !== '');
            if (li.dataset.index === '') options.forEach(x => x.setAttribute('aria-checked', 'false'));
            else li.setAttribute('aria-checked', li.getAttribute('aria-checked') === 'true' ? 'false' : 'true');
            const checked = options.filter(x => x.getAttribute('aria-checked') === 'true');
            send(checked.map(x => x.dataset.index).join(','), checked.map(x => x.dataset.display).join(', '));
            return;
        }
        items().forEach(x => x.setAttribute('aria-selected', x === li ? 'true' : 'false'));
        send(li.dataset.index, li.dataset.display ?? '');
        close(false);
    }

    root.addEventListener('input', e => {
        if (e.target === input() && !multiple()) filterList();
    });

    root.addEventListener('keydown', e => {
        if (e.target !== input()) return;
        switch (e.key) {
            case 'ArrowDown':
                if (isOpen()) move(1); else open();
                break;
            case 'ArrowUp':
                if (isOpen()) move(-1); else open();
                break;
            case 'Enter':
                if (!isOpen() || !active) return;
                choose(active);
                break;
            case ' ':
                if (!multiple()) return;
                if (isOpen() && active) choose(active); else open();
                break;
            case 'Escape':
                if (!isOpen()) return;
                close(true);
                break;
            default:
                return;
        }
        e.preventDefault();
    });

    // keep focus in the input while the list or the arrow is clicked
    root.addEventListener('mousedown', e => {
        if (e.target.closest('.quro-ui-combo-list, .quro-ui-combo-toggle')) e.preventDefault();
    });

    root.addEventListener('click', e => {
        const li = e.target.closest('.quro-ui-combo-list li[data-index]');
        if (li) {
            choose(li);
            return;
        }
        if (e.target === input() || e.target.closest('.quro-ui-combo-toggle')) {
            input().focus();
            if (isOpen()) close(true); else open();
        }
    });

    root.addEventListener('focusout', e => {
        if (!root.contains(e.relatedTarget)) close(true);
    });
}

// ── date picker (GS-U13) ──
// The calendar is drawn here with Intl in the culture of data-quro-culture; moving between months never calls the
// server. A picked day is written to the box as yyyy-MM-dd (month mode yyyy-MM) and sent with one change event; the
// server shows it again in the culture's format.
function attachDate(root) {
    const pop = () => root.querySelector('.quro-ui-date-pop');
    const isOpen = () => root.hasAttribute('data-quro-open');
    const monthMode = () => root.dataset.quroMonth === 'true';
    const culture = () => root.dataset.quroCulture || undefined;
    let target = null;
    let view = null;   // { year, month } with month 0-11

    const pad = (n, width) => String(n).padStart(width, '0');
    const iso = (y, m, d) => d === undefined ? `${pad(y, 4)}-${pad(m + 1, 2)}` : `${pad(y, 4)}-${pad(m + 1, 2)}-${pad(d, 2)}`;
    const utc = (y, m, d) => new Date(Date.UTC(y, m, d));
    const format = (date, options) => new Intl.DateTimeFormat(culture(), { ...options, timeZone: 'UTC' }).format(date);

    function startOf(input) {
        const text = input.dataset.iso
            || [...root.querySelectorAll('input.quro-ui-input')].map(i => i.dataset.iso).find(Boolean);
        const m = /^(\d{4})-(\d{2})/.exec(text ?? '');
        const today = new Date();
        return m ? { year: +m[1], month: +m[2] - 1 } : { year: today.getFullYear(), month: today.getMonth() };
    }

    function button(cls, text, data) {
        const b = document.createElement('button');
        b.type = 'button';
        b.tabIndex = -1;
        b.className = cls;
        b.textContent = text;
        Object.assign(b.dataset, data);
        return b;
    }

    function draw() {
        const selected = target.dataset.iso ?? '';
        const now = new Date();
        const head = document.createElement('div');
        head.className = 'quro-ui-cal-head';
        const title = document.createElement('span');
        title.className = 'quro-ui-cal-title';
        title.textContent = monthMode()
            ? format(utc(view.year, 0, 1), { year: 'numeric' })
            : format(utc(view.year, view.month, 1), { year: 'numeric', month: 'long' });
        head.append(button('quro-ui-cal-nav', '‹', { nav: '-1' }), title, button('quro-ui-cal-nav', '›', { nav: '1' }));
        const grid = document.createElement('div');
        grid.className = monthMode() ? 'quro-ui-cal-months' : 'quro-ui-cal-days';

        if (monthMode()) {
            for (let m = 0; m < 12; m++) {
                const value = iso(view.year, m);
                const b = button('quro-ui-cal-month', format(utc(view.year, m, 1), { month: 'short' }), { value });
                if (value === selected) b.setAttribute('aria-selected', 'true');
                if (value === iso(now.getFullYear(), now.getMonth())) b.dataset.today = '';
                grid.append(b);
            }
        } else {
            const first = +(root.dataset.quroFirstday || 0);
            for (let i = 0; i < 7; i++) {
                const dow = document.createElement('span');
                dow.className = 'quro-ui-cal-dow';
                dow.textContent = format(utc(2023, 0, 1 + (first + i) % 7), { weekday: 'short' });   // 2023-01-01 is a Sunday
                grid.append(dow);
            }
            const lead = (utc(view.year, view.month, 1).getUTCDay() - first + 7) % 7;
            for (let i = 0; i < 42; i++) {
                const date = utc(view.year, view.month, 1 - lead + i);
                const value = iso(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate());
                const b = button('quro-ui-cal-day', String(date.getUTCDate()), { value });
                if (date.getUTCMonth() !== view.month) b.dataset.outside = '';
                if (value === selected) b.setAttribute('aria-selected', 'true');
                if (value === iso(now.getFullYear(), now.getMonth(), now.getDate())) b.dataset.today = '';
                grid.append(b);
            }
        }
        pop().replaceChildren(head, grid);
    }

    function open(input) {
        if (root.dataset.quroReadonly === 'true' || input.disabled) return;
        target = input;
        view = startOf(input);
        root.dataset.quroPart = input.dataset.part;
        root.setAttribute('data-quro-open', '');
        draw();
    }

    function close() {
        if (!isOpen()) return;
        root.removeAttribute('data-quro-open');
        pop().replaceChildren();
        target = null;
    }

    function pick(value) {
        const input = target;
        close();
        input.value = value;
        input.dispatchEvent(new Event('change', { bubbles: true }));
    }

    // keep focus in the box while the calendar or the icon is clicked
    root.addEventListener('mousedown', e => {
        if (e.target.closest('.quro-ui-date-pop, .quro-ui-date-toggle')) e.preventDefault();
    });

    root.addEventListener('click', e => {
        const nav = e.target.closest('[data-nav]');
        if (nav) {
            const d = monthMode() ? utc(view.year + +nav.dataset.nav, 0, 1) : utc(view.year, view.month + +nav.dataset.nav, 1);
            view = { year: d.getUTCFullYear(), month: d.getUTCMonth() };
            draw();
            return;
        }
        const choice = e.target.closest('.quro-ui-date-pop [data-value]');
        if (choice) {
            pick(choice.dataset.value);
            return;
        }
        const input = e.target.closest('input.quro-ui-input')
            ?? (e.target.closest('.quro-ui-date-toggle') ? root.querySelector('input.quro-ui-input:last-of-type') : null);
        if (!input) return;
        input.focus();
        if (isOpen() && target === input) close(); else open(input);
    });

    root.addEventListener('keydown', e => {
        if (!(e.target instanceof HTMLInputElement)) return;
        if (e.key === 'Escape' && isOpen()) close();
        else if (e.key === 'ArrowDown' && e.altKey) open(e.target);
        else return;
        e.preventDefault();
    });

    root.addEventListener('focusout', e => {
        if (!root.contains(e.relatedTarget) || e.relatedTarget !== target && e.relatedTarget instanceof HTMLInputElement) close();
    });
}
