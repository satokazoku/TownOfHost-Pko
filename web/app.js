async function fetchPresets({ page = 1, per_page = 12, tags = '', version = '', sort = 'new', q = '' } = {}){
  const params = new URLSearchParams({ page, per_page, tags, version, sort, q });
  const res = await fetch(API_BASE + '?' + params.toString());
  if(!res.ok) throw new Error('failed');
  return res.json();
}

function el(sel, root=document){return root.querySelector(sel)}

function renderList(data){
  const list = el('#list'); list.innerHTML='';
  const tpl = document.getElementById('card-tpl');
  data.presets.forEach(p=>{
    const node = tpl.content.firstElementChild.cloneNode(true);
    node.querySelector('.name').textContent = p.name;
    node.querySelector('.meta').textContent = `${p.creator} • v${p.version} • ${p.downloads} dl • ${p.created_at}`;
    node.querySelector('.tags').textContent = (p.tags || '').split(',').filter(Boolean).map(t=>'#'+t).join(' ');
    node.querySelector('.btn-copy').addEventListener('click', ()=>navigator.clipboard.writeText(p.config_json));
    node.querySelector('.btn-save').addEventListener('click',()=>{
      fetch(API_BASE + '/' + p.id + '/download?as_file=1').then(r=>r.blob()).then(b=>{
        const a = document.createElement('a'); a.href = URL.createObjectURL(b); a.download = (p.name||'preset') + '.txt'; a.click();
      });
    });
    node.querySelector('.btn-load').addEventListener('click', ()=>{
      const port = prompt('ローカルポートを入力してください (例:50080)', '50080');
      if(!port) return;
      fetch('http://127.0.0.1:'+port+'/load-preset', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify({ config_json: p.config_json, name: p.name, creator: p.creator, version: p.version }) })
        .then(r=>{ if(r.ok) alert('ロード要求を送信しました'); else alert('ロード失敗'); })
        .catch(()=>alert('ローカル接続に失敗しました'));
    });
    list.appendChild(node);
  });
  renderPagination(data.page, Math.ceil(data.total / data.per_page));
}

function renderPagination(page, totalPages){
  const p = el('#pagination'); p.innerHTML='';
  for(let i=1;i<=totalPages && i<=20;i++){
    const btn = document.createElement('button'); btn.textContent = i; if(i===page) btn.disabled=true;
    btn.addEventListener('click',()=>load({ page: i }));
    p.appendChild(btn);
  }
}

async function load(opts={}){
  const q = el('#search').value.trim();
  const tags = el('#tags').value.trim();
  const version = el('#version').value.trim();
  const sort = el('#sort').value;
  try{
    const data = await fetchPresets({ q, tags, version, sort, ...opts });
    renderList(data);
  }catch(e){ alert('一覧取得に失敗しました'); }
}

document.getElementById('btnSearch').addEventListener('click', ()=>load({ page:1 }));
window.addEventListener('load', ()=>load({ page:1 }));
