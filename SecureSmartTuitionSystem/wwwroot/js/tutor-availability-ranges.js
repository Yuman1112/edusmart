(() => {
    'use strict';
    const form = document.getElementById('tutor-availability-form');
    const editor = document.getElementById('availability-range-editor');
    if (!form || !editor) return;
    const boxes = [...form.querySelectorAll('input[name="SelectedSlots"]')];
    const days = [...editor.querySelectorAll('[data-range-day]')];
    const start = editor.querySelector('[data-range-start]');
    const end = editor.querySelector('[data-range-end]');
    const add = editor.querySelector('[data-range-add]');
    const cancel = editor.querySelector('[data-range-cancel]');
    const list = editor.querySelector('[data-range-list]');
    const message = editor.querySelector('[data-range-message]');
    const total = editor.querySelector('[data-range-total]');
    const names = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
    const time = slot => `${String(Math.floor(slot / 2)).padStart(2, '0')}:${slot % 2 ? '30' : '00'}`;
    const values = () => new Set(boxes.filter(b => b.checked).map(b => Number(b.value)));
    let editing = null;
    let draft = false;
    function say(text, error = true) {
        message.textContent = text;
        message.className = error ? 'text-danger' : 'form-text';
        message.hidden = !text;
    }
    function ranges() {
        const sorted = [...values()].sort((a, b) => a - b);
        const result = [];
        for (let i = 0; i < sorted.length; i++) {
            const first = sorted[i];
            let last = first;
            while (i + 1 < sorted.length && sorted[i + 1] === last + 1 && Math.floor(sorted[i + 1] / 48) === Math.floor(first / 48)) last = sorted[++i];
            result.push({ day: Math.floor(first / 48), start: first % 48, end: last % 48 + 1 });
        }
        return result;
    }
    function reset() {
        editing = null; draft = false;
        days.forEach(d => d.checked = false);
        start.value = '38'; end.value = '42';
        add.textContent = '+ Add Time';
        cancel.hidden = true;
    }
    function render() {
        const current = ranges();
        total.textContent = `${values().size / 2} / 45 h per week`;
        list.replaceChildren();
        if (!current.length) {
            const p = document.createElement('p');
            p.className = 'availability-range-empty';
            p.textContent = 'No available times added. Choose days and a time above to get started.';
            list.append(p);
        }
        current.forEach(range => {
            const row = document.createElement('div'); row.className = 'availability-range-row';
            const day = document.createElement('strong'); day.textContent = names[range.day];
            const span = document.createElement('span'); span.textContent = `${time(range.start)}–${time(range.end)}`;
            const hours = document.createElement('span'); hours.textContent = `${(range.end - range.start) / 2} h`;
            const actions = document.createElement('div'); actions.className = 'availability-range-actions';
            const edit = document.createElement('button'); edit.type = 'button'; edit.className = 'outline-button'; edit.textContent = 'Edit';
            edit.setAttribute('aria-label', `Edit ${names[range.day]} ${span.textContent}`);
            edit.addEventListener('click', () => {
                editing = range; draft = true;
                days.forEach(d => d.checked = Number(d.value) === range.day);
                start.value = String(range.start); end.value = String(range.end);
                add.textContent = 'Apply Change'; cancel.hidden = false;
                say('Edit the day or time, then select Apply Change. Your saved times stay in place until applied.', false);
                start.focus(); editor.scrollIntoView({behavior: 'smooth', block: 'nearest'});
            });
            const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'outline-button'; remove.textContent = 'Remove';
            remove.setAttribute('aria-label', `Remove ${names[range.day]} ${span.textContent}`);
            remove.addEventListener('click', () => {
                const next = values();
                for (let s = range.start; s < range.end; s++) next.delete(range.day * 48 + s);
                reset(); commit(next); say('Time removed from your draft. Select Save Availability to keep this change.', false);
            });
            actions.append(edit, remove); row.append(day, span, hours, actions); list.append(row);
        });
    }
    function commit(next) {
        boxes.forEach(b => b.checked = next.has(Number(b.value)));
        render();
        // Reuse the existing save, stale-version and daily/weekly validation flow.
        boxes[0]?.dispatchEvent(new Event('change', {bubbles: true}));
    }
    add.addEventListener('click', () => {
        const selectedDays = days.filter(d => d.checked).map(d => Number(d.value));
        const from = Number(start.value), to = Number(end.value);
        if (!selectedDays.length) return say('Choose at least one day.');
        if (from < 16 || to > 44 || from >= to) return say('Choose an end time after the start time, within 08:00–22:00.');
        const next = values();
        if (editing) for (let s = editing.start; s < editing.end; s++) next.delete(editing.day * 48 + s);
        for (const day of selectedDays) {
            for (let s = from; s < to; s++) {
                if (next.has(day * 48 + s)) return say(`${names[day]} overlaps an existing time. Edit that time or choose a different range.`);
                next.add(day * 48 + s);
            }
        }
        for (let day = 1; day <= 6; day++) {
            if ([...next].filter(v => Math.floor(v / 48) === day).length > 16) return say(`${names[day]} would exceed eight hours. No changes were applied.`);
        }
        if (next.size > 90) return say('This would exceed 45 hours per week. No changes were applied.');
        reset(); commit(next); say('Times added to your draft. Select Save Availability to keep your changes.', false);
    });
    cancel.addEventListener('click', () => { reset(); say(''); });
    days.forEach(d => d.addEventListener('change', () => draft = editing !== null || days.some(x => x.checked)));
    [start, end].forEach(input => input.addEventListener('change', () => draft = editing !== null || days.some(x => x.checked)));
    form.addEventListener('submit', event => {
        if (draft) {
            event.preventDefault(); event.stopImmediatePropagation();
            say(editing ? 'Select Apply Change or Cancel Edit before saving.' : 'Select Add Time to include your selected days and time before saving.');
            add.focus();
        }
    }, true);
    window.addEventListener('beforeunload', event => {
        if (draft) { event.preventDefault(); event.returnValue = ''; }
    });
    form.querySelector('[data-availability-grid-fallback]').hidden = true;
    editor.hidden = false;
    reset(); render();
    const controls = editor.querySelector('[data-range-controls]');
    if (controls) controls.disabled = false;
    editor.querySelector('[data-range-preparing]')?.remove();
})();
