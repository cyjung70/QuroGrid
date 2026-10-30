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
        if (!e.isComposing) filter(root, e.target);   // IME: filter when composition ends
    });
    root.addEventListener('compositionend', e => filter(root, e.target));

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
