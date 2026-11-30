(function () {
    'use strict';
    const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
    function remainingDates(start, day, termStart, termEnd, startTime, now, cancelled = []) {
        if (!/^\d{4}-\d{2}-\d{2}$/.test(start || '')) return [];
        const [year, month] = start.split('-').map(Number);
        const result = [];
        for (let d = 1; d <= 28; d++) {
            const date = new Date(year, month - 1, d);
            const iso = `${year}-${String(month).padStart(2, '0')}-${String(d).padStart(2, '0')}`;
            if (iso < start || iso < termStart || iso > termEnd || days[date.getDay()] !== day || cancelled.includes(iso)) continue;
            const time = /^(\d{1,2}):(\d{2})\s*(AM|PM)$/i.exec(startTime || '');
            if (time && now) {
                const hour = Number(time[1]) % 12 + (time[3].toUpperCase() === 'PM' ? 12 : 0);
                const localStamp = `${iso}T${String(hour).padStart(2, '0')}:${time[2]}:00`;
                if (localStamp <= now) continue;
            }
            result.push(iso);
        }
        return result;
    }
    function update() {
        const input = document.getElementById('requestedStartDate');
        const section = document.getElementById('finalTwoWeeksSection');
        const checkbox = document.getElementById('finalTwoWeeksConfirmed');
        const list = document.getElementById('remainingLessonWarnings');
        const error = document.getElementById('noRemainingLessons');
        if (!input || !section || !checkbox || !list || !error) return;
        list.replaceChildren();
        const empty = [];
        let required = false;
        // Server-provided Malaysia time, advanced by elapsed time; browser timezone is irrelevant.
        const form = document.getElementById('enrollmentOptionsForm');
        const elapsed = Date.now() - loadedAt;
        const seed = new Date((form.dataset.now || '') + 'Z');
        const now = Number.isNaN(seed.getTime()) ? null : new Date(seed.getTime() + elapsed).toISOString().slice(0, 19);
        if (!input.value) document.querySelectorAll('.remaining-lessons-summary').forEach(x => { x.textContent = ''; });
        if (input.value) document.querySelectorAll('.option-class-item').forEach(card => {
            const displayName = (card.dataset.name || 'Selected class').replace(/(?:\s*·\s*|\s+-\s+)TU[0-9]+\b/gi, '').trim();
            const dates = remainingDates(input.value, card.dataset.day, card.dataset.termStart,
                card.dataset.termEnd, card.dataset.start, now, (card.dataset.cancelled || '').split(','));
            const labels = dates.map(d => new Date(d + 'T00:00:00').toLocaleDateString('en-GB',
                { day: 'numeric', month: 'short', year: 'numeric' }));
            const summary = card.querySelector('.remaining-lessons-summary');
            if (summary) summary.textContent = dates.length
                ? `Start-month regular lessons (${dates.length}): ${labels.join(', ')}`
                : 'No regular lessons remain in the selected start month.';
            if (dates.length === 0) empty.push(displayName);
            else if (dates.length <= 2) {
                required = true;
                const item = document.createElement('li');
                item.textContent = `${displayName} (${card.dataset.day}): ${labels.join(', ')} — ${dates.length} regular lesson(s).`;
                list.append(item);
            }
        });
        error.hidden = empty.length === 0;
        const emptyList = document.getElementById('noRemainingLessonsList');
        if (emptyList) {
            emptyList.replaceChildren();
            empty.forEach(name => {
                const item = document.createElement('li');
                const title = document.createElement('strong');
                title.textContent = (name || 'Selected class').replace(/(?:\s*·\s*|\s+-\s+)TU[0-9]+\b/gi, '').trim();
                item.style.marginBottom = '.5rem';
                item.append(title);
                emptyList.append(item);
            });
        }
        input.setCustomValidity(empty.length ? 'Some selected classes have no regular lessons remaining in this month.' : '');
        section.hidden = !required;
        checkbox.required = required;
        if (!required) checkbox.checked = false;
    }
    const loadedAt = Date.now();
    window.EduSmartEnrollmentLessons = { remainingDates, update };
    document.addEventListener('DOMContentLoaded', function () {
        const input = document.getElementById('requestedStartDate');
        const form = document.getElementById('enrollmentOptionsForm');
        input?.addEventListener('change', function () {
            const checkbox = document.getElementById('finalTwoWeeksConfirmed');
            if (checkbox) checkbox.checked = false;
            update();
        });
        form?.addEventListener('submit', function (event) {
            update();
            if (!form.reportValidity()) event.preventDefault();
        });
        update();
    });
})();
