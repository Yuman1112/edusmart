document.addEventListener('DOMContentLoaded',()=>{
    const form=document.getElementById('progress-form');if(!form)return;
    const confirmation=form.querySelector('[name="ConfirmCompleted"]');
    form.addEventListener('change',event=>{
        confirmation?.setCustomValidity('');
        const row=event.target.closest('tr');if(!row)return;
        const select=row.querySelector('.progress-percent'),done=row.querySelector('.progress-done-toggle');
        if(!select||!done||done.disabled)return;
        if(event.target===done){
            if(done.checked){select.dataset.partial=select.value;select.value='100';}
            else select.value=select.dataset.partial||select.dataset.initial||'0';
        }
        if(event.target===select){done.checked=select.value==='100';if(!done.checked)select.dataset.partial=select.value;}
        row.classList.toggle('progress-done',done.checked);
    });
    form.addEventListener('submit',event=>{
        const completed=[...form.querySelectorAll('.progress-percent')].some(s=>s.value==='100'&&s.dataset.initial!=='100');
        if(completed&&confirmation&&!confirmation.checked){event.preventDefault();confirmation.setCustomValidity('Confirm that newly completed subchapters will be locked.');confirmation.reportValidity();}
    });
});
