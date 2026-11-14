(() => {
    'use strict';
    const size = 25, states = new WeakMap();
    const collator = new Intl.Collator('en', { numeric:true, sensitivity:'base' });
    function tableCard(table) {
        const region = table.closest('.table-responsive, .billing-table-wrapper, .payment-table-wrapper, .receipt-table-wrapper, .availability-scroll, .reports-table-wrap, .enrollment-duplicate-table-wrap') || table;
        const parent = region.parentElement;
        if (parent?.classList.contains('shared-table-card')) return parent;
        if (parent?.matches('.dashboard-card,.card,.billing-table-card,.billing-detail-card,.payment-card,.receipt-card,.tutor-management-list-card,.class-form-card,.class-details-card,.reports-card') && parent.querySelectorAll('table').length === 1) {
            parent.classList.add('shared-table-card'); return parent;
        }
        const card = document.createElement('div');card.className='shared-table-card';region.before(card);card.append(region);return card;
    }
    function records(table) { return Array.from(table.tBodies).flatMap(b => [...b.rows]).filter(r => !(r.cells.length === 1 && r.cells[0].colSpan > 1)); }
    function excluded(th) { return /^(no\.?|#|actions?|review|select)$/i.test(th.textContent.trim()) || th.dataset.sortable === 'false' || !!th.querySelector('input'); }
    function value(cell) {
        if (!cell) return '';
        if (cell.dataset.sortValue !== undefined) return cell.dataset.sortValue;
        const date = cell.querySelector('time[datetime]'); if(date) return date.dateTime;
        return (cell.innerText || cell.textContent).trim().replace(/\s+/g,' ');
    }
    function compare(a,b) {
        const number = v => /^(?:RM|MYR)?\s*-?[\d,]+(?:\.\d+)?(?:\s*%)?$/.test(v) ? Number(v.replace(/RM|MYR|,|%|\s/g,'')) : NaN;
        const x=number(a),y=number(b);if(Number.isFinite(x)&&Number.isFinite(y)) return x-y;
        // Parse explicit day/month-name dates, never guess ambiguous numeric dates.
        const datePattern=/^\d{1,2} [A-Za-z]{3,9} \d{4}(?:\s.*)?$/;
        if(datePattern.test(a)&&datePattern.test(b)) { const d=Date.parse(a.replace(/ MYT$/, '')),e=Date.parse(b.replace(/ MYT$/, ''));if(!isNaN(d)&&!isNaN(e))return d-e; }
        return collator.compare(a,b);
    }
    function pageUrl(state,page) {
        const url=new URL(location.href);url.hash='';
        for(const key of [...url.searchParams.keys()]) if(key.toLowerCase()===state.param.toLowerCase())url.searchParams.delete(key);
        url.searchParams.set(state.param,String(page));return url;
    }
    // Preserve explicit page-specific Popper options. New AJAX rows receive the
    // same defaults without changing table scrolling or card border clipping.
    function prepareDropdowns(table) {
        table.querySelectorAll('[data-bs-toggle="dropdown"]').forEach(toggle=>{
            if(!toggle.hasAttribute('data-bs-popper-config'))toggle.setAttribute('data-bs-popper-config',JSON.stringify({strategy:'fixed'}));
            if(!toggle.hasAttribute('data-bs-boundary'))toggle.setAttribute('data-bs-boundary','viewport');
        });
    }
    function init(table) {
        if (table.dataset.serverManaged === "true") return;
        prepareDropdowns(table);
        if(table.dataset.tablePaged==='true' && states.has(table))return;
        const previous = states.get(table);
        previous?.observer?.disconnect();
        if(previous?.clickHandler)table.removeEventListener('click',previous.clickHandler,true);
        const card=tableCard(table), remote=!!table.dataset.sourceTable;
        table.classList.add('edusmart-data-table');table.dataset.tablePaged='true';

        // Editable schedule grids retain their controls; document detail tables retain
        // their existing optional footer behavior.
        const manual=table.dataset.pagination==='off' && !remote;
        let nav=card.querySelector(':scope > nav, :scope > .table-pagination');
        if(remote && !nav)nav=card.parentElement?.querySelector('nav.material-pagination,nav.assessment-pagination');
        if(!nav && !manual) {nav=document.createElement('nav');card.append(nav);}
        if(nav){nav.className='table-pagination';nav.setAttribute('aria-label','Table pages');}
        const state={table,card,nav,remote,loaded:false,page:Number(table.dataset.currentPage)||1,pages:Number(table.dataset.pageCount)||1,
            total:Number(table.dataset.totalRows)||0,param:table.dataset.pageParam||'page',column:-1,direction:1,busy:false,observer:null};
        states.set(table,state);
        const header=table.tHead?.rows[table.tHead.rows.length-1];
        const noColumn=header?[...header.cells].findIndex(th=>/^(no\.?|#)$/i.test(th.textContent.trim())):-1;
        function rowNumbers(rows) { if(noColumn<0)return;rows.forEach((r,i)=>{const cell=r.cells[noColumn],n=String((state.remote&&!state.loaded?(state.page-1)*size:0)+i+1);if(cell && cell.textContent.trim()!==n)cell.textContent=n;}); }
        function draw(focus=false) {
            const rows=records(table).filter(r=>!r.hidden&&r.style.display!=='none');
            if(!state.remote||state.loaded){state.total=rows.length;state.pages=Math.max(1,Math.ceil(rows.length/size));}
            state.page=Math.max(1,Math.min(state.page,state.pages));rowNumbers(rows);
            if(!state.remote||state.loaded)rows.forEach((r,i)=>r.classList.toggle('table-page-hidden',!manual&&(i<(state.page-1)*size||i>=state.page*size)));
            if(!nav)return;
            nav.hidden=table.dataset.pagination==='when-needed'&&state.total<=size;
            const summary=document.createElement('span');summary.className='table-pagination-summary';
            summary.textContent=state.total?`${(state.page-1)*size+1}–${Math.min(state.page*size,state.total)} of ${state.total} rows · 25 per page`:'0 rows · 25 per page';
            const controls=document.createElement('div');controls.className='table-pagination-controls';
            function add(label,target,disabled=false,current=false){
                const el=document.createElement(state.remote&&!state.loaded&&!disabled&&!current?'a':'button');
                if(el.tagName==='BUTTON')el.type='button';else el.href=pageUrl(state,target).href;
                el.className='table-page-button';el.textContent=label;
                if(disabled||state.busy){el.disabled=true;el.setAttribute('aria-disabled','true');}
                if(current){el.classList.add('active');el.setAttribute('aria-current','page');el.setAttribute('aria-label',`Page ${target}, current page`);}
                el.addEventListener('click',event=>{if(disabled||current||state.busy){event.preventDefault();return;}if(!state.remote||state.loaded){event.preventDefault();state.page=target;draw(true);}});
                controls.append(el);
            }
            add('First',1,state.page===1);add('Previous',state.page-1,state.page===1);
            for(let n=Math.max(1,state.page-2);n<=Math.min(state.pages,state.page+2);n++)add(String(n),n,false,n===state.page);
            add('Next',state.page+1,state.page===state.pages);add('Last',state.pages,state.page===state.pages);
            nav.replaceChildren(summary,controls);if(focus)controls.querySelector('[aria-current]')?.focus();
        }
        async function sort(index,button){
            if(state.busy)return;state.busy=true;table.setAttribute('aria-busy','true');draw();
            const old=card.querySelector('.table-sort-feedback');if(old)old.remove();
            try{
                if (state.remote) throw Error("Server-paged tables require a server sort link.");
                const direction=state.column===index?-state.direction:1;
                const rows=records(table);rows.sort((a,b)=>direction*compare(value(a.cells[index]),value(b.cells[index])));
                state.observer?.disconnect();const body=table.tBodies[0];rows.forEach(r=>body.append(r));
                state.column=index;state.direction=direction;state.page=1;
                [...header.cells].forEach((th,i)=>{if(th.querySelector('[data-table-sort]'))th.setAttribute('aria-sort',i===index?(direction===1?'ascending':'descending'):'none');});
                observe();
            }catch(error){const notice=document.createElement('p');notice.className='table-sort-feedback text-danger p-3';notice.setAttribute('role','alert');notice.textContent='Unable to sort all records. Please reload and try again.';card.append(notice);}
            finally{state.busy=false;table.removeAttribute('aria-busy');draw();button.focus();}
        }
        if(header && [...header.cells].every(th=>th.colSpan===1)) [...header.cells].forEach((th,index)=>{
            if(excluded(th))return;
            // Existing server sort links keep their endpoint/filter logic.
            const serverLink=remote?th.querySelector('a[href]'):null;
            if(serverLink){serverLink.classList.add('table-sort-button');if(!serverLink.querySelector('.bi')){const arrow=document.createElement('span');arrow.className='table-sort-arrow';arrow.textContent='↕';arrow.setAttribute('aria-hidden','true');serverLink.append(' ',arrow);}return;}
            const label=(th.innerText||th.textContent).trim().replace(/[↑↓↕⇅]/g,'').trim();
            if (remote) {
                const key=th.dataset.sortKey;
                if(!key)return; // Server tables never silently sort only the current page.
                const url=pageUrl(state,1), param=th.dataset.sortParam || 'tableSort';
                let current, direction;
                if(param==='Sort') {
                    current=url.searchParams.get('Sort') || url.searchParams.get('sort') || '';
                    direction=current===`${key}_asc`?'desc':'asc';
                    for(const name of [...url.searchParams.keys()])if(name.toLowerCase()==='sort')url.searchParams.delete(name);
                    url.searchParams.set('Sort',`${key}_${direction}`);
                    th.setAttribute('aria-sort',current===`${key}_asc`?'ascending':current===`${key}_desc`?'descending':'none');
                } else {
                    current=url.searchParams.get('tableSort');
                    const descending=url.searchParams.get('tableDirection')==='desc';
                    direction=current===key&&!descending?'desc':'asc';
                    url.searchParams.set('tableSort',key);url.searchParams.set('tableDirection',direction);
                    th.setAttribute('aria-sort',current===key?(descending?'descending':'ascending'):'none');
                }
                const link=document.createElement('a');link.href=url.href;link.className='table-sort-button';
                if(table.dataset.sourceTable==='assessments'||table.dataset.sourceTable==='progress')link.setAttribute('data-assessment-nav','');
                const arrow=document.createElement('span');arrow.className='table-sort-arrow';arrow.textContent=th.getAttribute('aria-sort')==='ascending'?'↑':th.getAttribute('aria-sort')==='descending'?'↓':'↕';arrow.setAttribute('aria-hidden','true');
                link.append(document.createTextNode(label+' '),arrow);link.setAttribute('aria-label',`Sort by ${label} ${direction==='asc'?'ascending':'descending'}`);th.replaceChildren(link);return;
            }
            const button=document.createElement('button');button.type='button';button.className='table-sort-button';button.dataset.tableSort=String(index);button.append(document.createTextNode(label+' '));
            const arrow=document.createElement('span');arrow.className='table-sort-arrow';arrow.textContent='↕';arrow.setAttribute('aria-hidden','true');button.append(arrow);
            button.setAttribute('aria-label',`Sort by ${label}`);th.replaceChildren(button);th.setAttribute('aria-sort','none');
        });
        // Capture stops old per-page header handlers from sorting a second time.
        state.clickHandler=event=>{const button=event.target.closest('[data-table-sort]');if(!button||button.closest('table')!==table)return;event.preventDefault();event.stopImmediatePropagation();void sort(Number(button.dataset.tableSort),button);};
        table.addEventListener('click',state.clickHandler,true);
        function observe(){if(!state.observer)return;Array.from(table.tBodies).forEach(body=>state.observer.observe(body,{childList:true,subtree:true,attributes:true,attributeFilter:['style','hidden']}));}
        state.observer=new MutationObserver(()=>{prepareDropdowns(table);state.page=1;draw();});observe();draw();
    }
    function enhance(root=document){root.querySelectorAll('table').forEach(init);}
    window.EduSmartTables={enhance};
    document.addEventListener('DOMContentLoaded',()=>{
        enhance();
        // AJAX result panels also receive the same footer/sort controls.
        let queued=false;
        new MutationObserver(changes=>{if(queued||!changes.some(c=>[...c.addedNodes].some(n=>n.nodeType===1&&(n.matches?.('table')||n.querySelector?.('table')))))return;queued=true;queueMicrotask(()=>{queued=false;enhance();});}).observe(document.body,{childList:true,subtree:true});
    });
})();
