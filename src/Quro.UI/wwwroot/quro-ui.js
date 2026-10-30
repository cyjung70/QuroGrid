// QuroUI browser layer (Control Spec 8, R2): character filtering and select-all run here, so typing never calls the
// server. Options come from data attributes on the control root and are read at event time (no call when they change).

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
