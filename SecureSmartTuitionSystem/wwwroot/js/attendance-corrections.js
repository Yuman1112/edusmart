(() => {
    const modal = document.getElementById('correction-modal');

    if (!modal) return;

    const form = document.getElementById('correction-review-form');
    const remark = document.getElementById('correction-review-remark');
    const error = document.getElementById('correction-review-error');

    let canReview = false;

    const detail = (name, value) => {
        modal.querySelector(
            `[data-detail="${name}"]`
        ).textContent = value || '—';
    };

    const validate = () => {
        const value = remark.value.trim();

        const message = !value
            ? 'Please enter review remarks.'
            : value.length > 500
                ? 'Please enter no more than 500 characters.'
                : '';

        error.textContent = message;

        remark.classList.toggle(
            'input-validation-error',
            !!message
        );

        remark.setAttribute(
            'aria-invalid',
            message ? 'true' : 'false'
        );

        return !message;
    };

    modal.addEventListener('show.bs.modal', event => {
        const button = event.relatedTarget;

        if (!button) {
            event.preventDefault();
            return;
        }

        const data = button.dataset;

        form.reset();

        canReview = data.canReview === 'true';

        document.getElementById(
            'correction-review-id'
        ).value = data.id;

        document.getElementById(
            'correction-review-version'
        ).value = data.version;

        document.getElementById(
            'correction-modal-title'
        ).textContent = canReview
                ? 'Review Attendance Correction'
                : 'Attendance Correction Details';

        detail('student', data.student);
        detail('lesson', `${data.class} · ${data.date}`);
        detail('current', data.current);
        detail('change', `${data.previous} → ${data.requested}`);
        detail('status', data.status);
        detail('reason', data.reason);
        detail('requester', data.requester);
        detail('requestedAt', data.requestedAt + ' MYT');

        detail(
            'reviewRemark',
            data.reviewRemark || (
                data.status === 'Pending'
                    ? 'Awaiting administrator review'
                    : '—'
            )
        );

        detail('reviewer', data.reviewer);

        detail(
            'reviewedAt',
            data.reviewedAt
                ? data.reviewedAt + ' MYT'
                : '—'
        );

        document.getElementById(
            'correction-review-fields'
        ).hidden = !canReview;

        document.getElementById(
            'correction-review-history'
        ).hidden = canReview;

        document.getElementById(
            'correction-reject'
        ).hidden = !canReview;

        document.getElementById(
            'correction-approve'
        ).hidden = !canReview;

        document.getElementById(
            'correction-close'
        ).textContent = canReview ? 'Cancel' : 'Close';

        remark.disabled = !canReview;

        error.textContent = '';
        remark.classList.remove('input-validation-error');
        remark.setAttribute('aria-invalid', 'false');
    });

    remark.addEventListener('input', () => {
        if (remark.getAttribute('aria-invalid') === 'true') {
            validate();
        }
    });

    form.addEventListener('submit', event => {
        if (!canReview) {
            event.preventDefault();
            return;
        }

        if (!validate()) {
            event.preventDefault();
            remark.focus();
        }
    });
})();