(() => {
    const form=document.getElementById('teaching-request-form');if(!form)return;
    let submitting=false;
    const snapshot=()=>JSON.stringify(Array.from(new FormData(form),([key,value])=>[key,value instanceof File?(value.name?[value.name,value.size,value.lastModified]:null):value]));
    const initial=snapshot();
    const dirty=()=>snapshot()!==initial;
    form.addEventListener('submit',event=>{if(!event.defaultPrevented&&form.checkValidity())submitting=true;});
    document.addEventListener('click',event=>{
        const link=event.target.closest?.('a[href]');
        if(!link||event.defaultPrevented||event.ctrlKey||event.metaKey||event.shiftKey||event.altKey||link.target==='_blank'||!dirty())return;
        const url=new URL(link.href,location.href);if(url.protocol!=='http:'&&url.protocol!=='https:')return;
        if(url.href===location.href||url.origin!==location.origin)return;
        if(!confirm('You have unsaved changes. Leave without submitting?')){event.preventDefault();event.stopImmediatePropagation();}else submitting=true;
    });
    window.addEventListener('beforeunload',event=>{if(!submitting&&dirty()){event.preventDefault();event.returnValue='';}});
    window.addEventListener('pageshow',()=>{submitting=false;});
})();
