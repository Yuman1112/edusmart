(() => {
    function init() {
        const form = document.getElementById('class-create-form');
        if (!form) return;
        const byId = id => document.getElementById(id);
        const stage = byId('EducationStage');
        const level = byId('AcademicLevel');
        const subject = byId('SubjectName');
        const from = byId('StartDate');
        const to = byId('EndDate');
        const duration = byId('LessonDurationMinutes');
        const tutor = byId('TutorUserId');
        const day = byId('DayOfWeek');
        const start = byId('StartTime');
        const end = byId('EndTime');
        const chosenDay = byId('ChosenClassDay');
        const submit = byId('class-create-submit');
        const status = byId('tutor-scheduling-status');
        const list = byId('tutor-scheduling-slots');
        const checked = byId('tutor-scheduling-checked');
        const results = byId('tutor-scheduling-results');
        const signature = () => [stage.value, level.value, subject.value, from.value, to.value, duration.value, tutor.value].join('|');
        const initialChoice = { day: day.value, start: start.value.slice(0, 5), end: end.value.slice(0, 5) };
        let restoreChoice = initialChoice.day ? initialChoice : null;
        let selected = null;
        let verifiedSignature = '';
        let controller;
        let sequence = 0;
        let busy = false;
        let submitting = false;
        let timer;
        let renderedSlots = '';
        let successTimer;
        const notify = () => {
            day.dispatchEvent(new Event('change', { bubbles: true }));
            start.dispatchEvent(new Event('change', { bubbles: true }));
        };
        const show = (message, kind = 'info') => {
            clearTimeout(successTimer);
            status.hidden = false;
            status.className = 'alert alert-' + kind;
            status.textContent = message;
            if (kind === 'success') successTimer = setTimeout(() => { status.hidden = true; }, 5000);
        };
        function clearSelection() {
            selected = null;
            day.value = ''; start.value = ''; end.value = ''; chosenDay.value = '';
            submit.disabled = true;
            notify();
        }
        function markButtons() {
            list.querySelectorAll('button[data-slot-key]').forEach(button => {
                const active = selected?.key === button.dataset.slotKey;
                button.classList.toggle('selected', active);
                button.setAttribute('aria-pressed', active ? 'true' : 'false');
                button.disabled = busy || verifiedSignature !== signature();
            });
        }
        function apply(slot, announce = true) {
            selected = slot;
            day.value = slot.day;
            chosenDay.value = slot.day;
            start.value = slot.start;
            end.value = slot.end;
            if (announce) notify();
            markButtons();
            submit.disabled = busy || verifiedSignature !== signature();
            if (announce) show('Teaching time selected.', 'success');
        }
        function render(slots) {
            const key = JSON.stringify(slots);
            if (key === renderedSlots) { markButtons(); return; }
            renderedSlots = key;
            list.replaceChildren();
            let firstGroup = true;
            for (const name of ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']) {
                const choices = slots.filter(slot => slot.day === name);
                if (!choices.length) continue;
                const group = document.createElement('details');
                group.className = 'tutor-slot-day';
                group.open = selected ? selected.day === name : firstGroup;
                firstGroup = false;
                const heading = document.createElement('summary');
                heading.textContent = `${name} · ${choices.length} ${choices.length === 1 ? "time" : "times"}`;
                const buttons = document.createElement('div');
                buttons.className = 'tutor-slot-buttons';
                choices.forEach(slot => {
                    const button = document.createElement('button');
                    button.type = 'button';
                    button.className = 'outline-button tutor-slot-button';
                    button.textContent = `${slot.start}–${slot.end}`;
                    button.dataset.slotKey = slot.key;
                    button.title = `First lesson ${slot.firstDate}; ${slot.lessonCount} lesson dates checked`;
                    button.addEventListener('click', () => {
                        if (!busy && verifiedSignature === signature()) apply(slot);
                    });
                    buttons.append(button);
                });
                group.append(heading, buttons);
                list.append(group);
            }
            markButtons();
        }
        function ready() {
            const minutes = Number(duration.value);
            return stage.value && level.value && subject.value && from.value
                && Number.isInteger(minutes) && minutes >= 30 && minutes <= 240
                && from.checkValidity() && to.checkValidity();
        }
        async function refresh(force = false) {
            if (submitting || document.hidden || (busy && !force)) return;
            results.hidden = !tutor.value;
            controller?.abort();
            const request = ++sequence;
            const quiet = !force && verifiedSignature === signature();
            verifiedSignature = '';
            submit.disabled = true;
            if (!quiet) checked.textContent = '';
            if (!ready()) {
                busy = false;
                clearSelection();
                list.replaceChildren(); renderedSlots = '';
                tutor.disabled = true;
                show('Complete the subject, level, term dates and lesson duration (30–240 minutes).');
                return;
            }
            busy = true;
            markButtons();
            if (!quiet) show('Checking available teaching times…');
            const requestSignature = signature();
            const requestedTutor = tutor.value;
            controller = new AbortController();
            const url = new URL(form.dataset.schedulingUrl, location.origin);
            const values = {
                EducationStage: stage.value, AcademicLevel: level.value, SubjectName: subject.value,
                StartDate: from.value, EndDate: to.value, DurationMinutes: duration.value, TutorUserId: requestedTutor
            };
            Object.entries(values).forEach(([name, value]) => { if (value) url.searchParams.set(name, value); });
            try {
                const response = await fetch(url, {
                    signal: controller.signal, cache: 'no-store', credentials: 'same-origin',
                    headers: { Accept: 'application/json' }
                });
                if (!response.headers.get('content-type')?.includes('application/json')) throw new Error('Could not load availability. Check your connection or session, then reload.');
                const data = await response.json();
                if (!response.ok) throw new Error(response.status === 400 ? data.message : 'Could not load availability. Please retry.');
                if (request !== sequence || signature() !== requestSignature) return;
                const optionsKey = JSON.stringify(data.tutors);
                if (tutor.dataset.optionsKey !== optionsKey) {
                    tutor.replaceChildren();
                    const placeholder = document.createElement('option');
                    placeholder.value = ''; placeholder.textContent = 'Select qualified tutor';
                    tutor.append(placeholder);
                    data.tutors.forEach(item => {
                        const option = document.createElement('option');
                        option.value = item.id; option.textContent = item.label;
                        tutor.append(option);
                    });
                    tutor.value = data.tutors.some(item => item.id === requestedTutor) ? requestedTutor : '';
                    tutor.dataset.optionsKey = optionsKey;
                }
                tutor.disabled = data.tutors.length === 0;
                results.hidden = !data.selectedEligible;
                busy = false;
                verifiedSignature = signature();
                if (!data.selectedEligible) clearSelection();
                const wanted = selected || restoreChoice;
                restoreChoice = null;
                const match = wanted && data.slots.find(slot =>
                    (slot.day === wanted.day || slot.dayNumber.toString() === wanted.day)
                    && slot.start === wanted.start && slot.end === wanted.end);
                if (!match) clearSelection();
                render(data.slots);
                checked.textContent = 'Last checked: ' + data.checkedAt;
                if (match) {
                    apply(match, false);
                    if (!quiet) status.hidden = true;
                } else if (wanted) {
                    show('The selected time is no longer available. Choose another time.', 'warning');
                } else if (!quiet) {
                    show(
                        data.message,
                        !data.selectedEligible || data.slots.length ? 'info' : 'warning'
                    );
                }
            } catch (error) {
                if (error.name === 'AbortError' || request !== sequence) return;
                verifiedSignature = '';
                show(error.message || 'Unable to check availability. Confirmation is disabled.', 'warning');
            } finally {
                if (request === sequence) { busy = false; markButtons(); }
            }
        }
        function changed() {
            results.hidden = true;
            clearTimeout(timer);
            controller?.abort(); ++sequence; busy = false;
            restoreChoice = null; verifiedSignature = '';
            clearSelection(); list.replaceChildren(); renderedSlots = '';
            show('Updating available tutors and weekly times…');
            timer = setTimeout(() => refresh(true), 350);
        }
        [stage, level, subject, from, to, duration, tutor].forEach(field => {
            field.addEventListener('change', changed);
        });
        [from, to, duration].forEach(field => field.addEventListener('input', changed));
        document.addEventListener('visibilitychange', () => {
            if (document.hidden) {
                controller?.abort(); ++sequence; busy = false;
                verifiedSignature = ''; submit.disabled = true;
            } else refresh(true);
        });
        window.addEventListener('focus', () => refresh());
        form.addEventListener('submit', event => {
            if (busy || !selected || verifiedSignature !== signature()
                || day.value !== selected.day || start.value.slice(0, 5) !== selected.start || end.value.slice(0, 5) !== selected.end) {
                event.preventDefault();
                show('Select a currently available weekly time before creating the class.', 'warning');
                refresh(true); return;
            }
            if (window.jQuery?.fn?.valid && !window.jQuery(form).valid()) { event.preventDefault(); return; }
            submitting = true;
            controller?.abort();
            submit.disabled = true;
        }, true);
        window.addEventListener('pageshow', () => { submitting = false; refresh(true); });
        setInterval(() => refresh(), 10000);
        clearSelection();
        refresh(true);
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
