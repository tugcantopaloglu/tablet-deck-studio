'use strict';
let featureView='',arranging=false;
let librarySounds=[],librarySelection=new Set(),libraryLimit=80,libraryPage=0,libraryLoading=false,librarySignature='';
let libraryFavourites=[],libraryOnlyFavourites=false;
function favouriteView(sounds,favourites,only){
  if(!only)return sounds;
  return favourites.map(f=>{const found=sounds.filter(s=>s.key===f.key);return found.length===1?found[0]:{index:0,key:f.key,name:f.name,artist:'',categories:[],unavailable:true,reason:found.length?'Duplicated identity':'Not in the current library'};});
}
let appSessions=[],appMixerLoading=false,appMixerDragging=false;
const featureHtml=(element,html)=>{element.innerHTML=DOMPurify.sanitize(html);};
panel.addEventListener('close',()=>{if(!panel.open)featureView='';appMixerDragging=false;});

async function showSoundpadLibrary(){
  if(preview){notice('Connect a PC to browse its Soundpad library.');return;}
  if(dirty){notice('Save or discard your editor changes before opening the library.',true);return;}
  featureView='library';editing=false;librarySelection=new Set();libraryLimit=80;libraryPage=0;librarySignature='';
  openPanel('Soundpad library',`<p class="help">Search your running Soundpad library by sound name, artist, or category. Nothing plays until you press Play.</p><div class="form-grid"><label>Search sounds<input id="library-search" type="search" placeholder="Search your sounds" autocomplete="off"></label><label>Category<select id="library-category" aria-label="Category"><option value="">All sounds</option></select></label><label>Playback output<select id="library-output" aria-label="Playback output">${options({mic:'Microphone',speakers:'Speakers',both:'Speakers + microphone',default:'Soundpad default'},'mic')}</select></label>${!native?`<label>Add selected sounds to<select id="library-page" aria-label="Add selected sounds to">${pages.map(p=>`<option value="${esc(p.id)}" ${p.id===pageId?'selected':''}>${esc(p.name)}</option>`).join('')}</select></label>`:''}</div><div class="row"><button id="library-refresh" class="quiet">Refresh library</button>${!native?'<button id="library-add" class="primary" disabled>Add selected buttons</button>':''}<span id="library-count" class="help" role="status">Loading…</span></div><p id="library-status" class="help" role="status"></p><div id="library-results" class="library-results"></div><button id="library-more" class="quiet" hidden>Show more sounds</button>`);
  const pager=document.createElement('div');pager.className='row library-pagination';featureHtml(pager,'<button id="library-prev" class="quiet">Previous</button><label>Sound page<select id="library-page-number" aria-label="Sound page"></select></label><button id="library-next" class="quiet">Next</button><span class="help">Auto sync every 5 seconds</span>');$('#library-results').before(pager);
  $('#library-prev').onclick=()=>{libraryPage--;renderSoundpadResults();};$('#library-next').onclick=()=>{libraryPage++;renderSoundpadResults();};$('#library-page-number').onchange=()=>{libraryPage=Number($('#library-page-number').value);renderSoundpadResults();};
  $('#library-search').oninput=()=>{libraryPage=0;renderSoundpadResults();};$('#library-category').onchange=()=>{libraryPage=0;renderSoundpadResults();};
  const filter=document.createElement('button');filter.id='library-favourites';filter.className='quiet favourites-filter';filter.textContent='Favourites';filter.setAttribute('aria-pressed',String(libraryOnlyFavourites));$('#library-refresh').after(filter);filter.onclick=()=>{libraryOnlyFavourites=!libraryOnlyFavourites;filter.setAttribute('aria-pressed',String(libraryOnlyFavourites));libraryLimit=80;renderSoundpadResults();};
  $('#library-refresh').onclick=()=>loadSoundpadLibrary(true);
  if(typeof showRecentSounds==='function'){const recent=document.createElement('button');recent.className='quiet';recent.textContent='Recent sounds';recent.onclick=showRecentSounds;$('#library-refresh').after(recent);}
  $('#library-more').onclick=()=>{libraryPage++;renderSoundpadResults();};
  if($('#library-add'))$('#library-add').onclick=importSoundpadSelection;
  if(!native){const row=document.createElement('div');row.className='row';for(const [text,link] of [['Refresh linked indices',false],['Link old buttons',true]]){const button=document.createElement('button');button.className='quiet';button.textContent=text;button.onclick=()=>remapSoundpad(link);row.appendChild(button);}$('#library-results').before(row);}
  await loadSoundpadLibrary(false);
}
async function loadSoundpadLibrary(force,quiet=false){
  if(libraryLoading)return;
  const generation=panelGeneration;
  const button=$('#library-refresh');if(!button)return;libraryLoading=true;button.disabled=true;if(!quiet)$('#library-status').textContent='Reading Soundpad…';
  try {
    const results=await Promise.allSettled([api('/api/soundpad/library',force?{}:undefined),api('/api/soundpad/favourites')]);if(featureView!=='library'||panelGeneration!==generation||!panel.open)return;
    if(results[1].status==='fulfilled')libraryFavourites=results[1].value.favourites;
    if(results[0].status==='rejected'){librarySounds=[];renderSoundpadResults();throw results[0].reason;}
    const result=results[0].value,focusTarget=quiet?captureLibraryFocus(null,false):null,scrollTop=$('#panel-body').scrollTop;
    const selected=librarySounds.filter(s=>librarySelection.has(s.index));const signature=JSON.stringify([result.sounds,libraryFavourites,result.warning]);const unchanged=quiet&&librarySignature===signature;
    librarySounds=result.sounds;librarySelection=new Set(librarySounds.filter(s=>selected.some(old=>old.key?old.key===s.key:old.index===s.index&&old.name===s.name)).map(s=>s.index));librarySignature=signature;
    if(unchanged)return;
    const categories=[...new Set(librarySounds.flatMap(s=>s.categories))].sort((a,b)=>a.localeCompare(b));
    const category=$('#library-category').value;featureHtml($('#library-category'),'<option value="">All sounds</option>'+categories.map(c=>`<option value="${esc(c)}">${esc(c)}</option>`).join(''));if(categories.includes(category))$('#library-category').value=category;
    $('#library-status').textContent=results[1].status==='rejected'?'Favourites could not be loaded. Refresh to retry.':result.warning||'Star a sound to keep it in Favourites. Saved identities follow index changes; missing or duplicated sounds cannot play from Favourites.';renderSoundpadResults();if(quiet){restoreLibraryFocus(focusTarget);$('#panel-body').scrollTop=scrollTop;}
  }catch(error){if(featureView==='library'&&panelGeneration===generation&&panel.open){$('#library-status').textContent=error.message;renderSoundpadResults();}}
  finally{libraryLoading=false;if(featureView==='library'&&panelGeneration===generation&&panel.open)$('#library-refresh').disabled=false;}
}
setInterval(()=>{if(featureView==='library'&&panel.open&&!document.hidden)loadSoundpadLibrary(false,true);},5000);
function renderSoundpadResults(){
  if(featureView!=='library')return;
  const query=$('#library-search').value.trim().toLocaleLowerCase(),category=$('#library-category').value;
  const matches=favouriteView(librarySounds,libraryFavourites,libraryOnlyFavourites).filter(s=>(!category||s.categories.includes(category))&&(!query||[s.name,s.artist,...s.categories].join(' ').toLocaleLowerCase().includes(query)));
  const count=Math.max(1,Math.ceil(matches.length/libraryLimit));libraryPage=Math.max(0,Math.min(libraryPage,count-1));const start=libraryPage*libraryLimit,end=Math.min(start+libraryLimit,matches.length);
  const updateCount=()=>{$('#library-count').textContent=`${matches.length?start+1:0}–${end} of ${matches.length} ${libraryOnlyFavourites?'favourite ':''}sounds · ${librarySounds.length} in Soundpad · ${librarySelection.size} selected`;};updateCount();
  featureHtml($('#library-page-number'),Array.from({length:count},(_,i)=>`<option value="${i}" ${i===libraryPage?'selected':''}>${i+1} of ${count}</option>`).join(''));$('#library-prev').disabled=libraryPage===0;$('#library-next').disabled=libraryPage>=count-1;
  if($('#library-add'))$('#library-add').disabled=librarySelection.size===0;
  const favourites=new Set(libraryFavourites.map(f=>f.key));
  featureHtml($('#library-results'),matches.length?matches.slice(start,end).map(s=>`<div class="library-row">${!native&&!s.unavailable?`<input class="sound-select" type="checkbox" data-select-sound="${s.index}" aria-label="Select ${esc(s.name)}" ${librarySelection.has(s.index)?'checked':''}>`:''}<div class="library-name"><strong>${esc(s.name)}</strong><span>${esc(s.unavailable?s.reason:[s.artist,...s.categories].filter(Boolean).join(' · '))||'All sounds'} ${s.unavailable?'':`<span class="badge">#${s.index}</span>`}</span></div>${s.key?`<button class="quiet favourite-toggle ${favourites.has(s.key)?'is-favourite':''}" data-favourite-key="${esc(s.key)}" aria-pressed="${favourites.has(s.key)}" aria-label="${favourites.has(s.key)?'Remove':'Add'} favourite ${esc(s.name)}">${icon('star')}</button>`:''}<button class="quiet" data-play-sound="${s.index}" data-play-key="${esc(s.key||'')}" ${s.unavailable?'disabled':''} aria-label="Play ${esc(s.name)}">Play</button></div>`).join(''):`<div class="feature-empty"><strong>${libraryOnlyFavourites&&!libraryFavourites.length?'No favourites yet':'No matching sounds'}</strong><p>${libraryOnlyFavourites?'Star sounds in the full library, or try another search.':'Add sounds in Soundpad or try another search.'}</p></div>`);
  $('#library-more').hidden=true;
  document.querySelectorAll('[data-select-sound]').forEach(input=>input.onchange=()=>{const id=Number(input.dataset.selectSound);input.checked?librarySelection.add(id):librarySelection.delete(id);$('#library-add').disabled=librarySelection.size===0;updateCount();});
  document.querySelectorAll('[data-play-sound]').forEach(button=>button.onclick=async()=>{button.disabled=true;try{await api('/api/soundpad/play',{index:Number(button.dataset.playSound),key:button.dataset.playKey||'',output:$('#library-output').value});notice('Soundpad accepted playback.');}catch(error){notice(error.message,true);}finally{if(button.isConnected)button.disabled=false;}});
  document.querySelectorAll('[data-favourite-key]').forEach(button=>button.onclick=()=>toggleSoundpadFavourite(button));
}
async function toggleSoundpadFavourite(button){
  const key=button.dataset.favouriteKey,generation=panelGeneration,hadFocus=document.activeElement===button;
  button.disabled=true;
  try{
    await api('/api/soundpad/favourites',{key,selected:!libraryFavourites.some(f=>f.key===key)});const result=await api('/api/soundpad/favourites');
    if(featureView!=='library'||panelGeneration!==generation||!panel.open)return;
    const focusTarget=captureLibraryFocus(button,hadFocus);
    libraryFavourites=result.favourites;renderSoundpadResults();
    restoreLibraryFocus(focusTarget);
  }catch(error){if(featureView==='library'&&panelGeneration===generation&&panel.open)$('#library-status').textContent=error.message;}
  finally{if(button.isConnected)button.disabled=false;}
}
function captureLibraryFocus(button,hadFocus){
  const node=document.activeElement;
  if($('#library-results')?.contains(node)){
    for(const [attribute,property] of [['data-favourite-key','favouriteKey'],['data-play-sound','playSound'],['data-select-sound','selectSound']]){
      if(node.dataset?.[property]===undefined)continue;
      const value=node.dataset[property],sound=librarySounds.find(s=>s.index===Number(value));
      return{attribute,property,value,key:property==='favouriteKey'?value:sound?.key||''};
    }
  }
  return hadFocus&&(node===button||node===document.body)?{attribute:'data-favourite-key',property:'favouriteKey',value:button.dataset.favouriteKey,key:button.dataset.favouriteKey}:null;
}
function restoreLibraryFocus(target){
  if(!target)return;
  const candidates=[...document.querySelectorAll('['+target.attribute+']')];
  const exact=candidates.find(node=>node.dataset[target.property]===target.value);
  const linked=target.key&&candidates.find(node=>target.property==='favouriteKey'?node.dataset.favouriteKey===target.key:librarySounds.some(s=>s.index===Number(node.dataset[target.property])&&s.key===target.key));
  ((target.key?linked:exact)||$('#library-page-number')||$('#library-favourites'))?.focus({preventScroll:true});
}
async function importSoundpadSelection(){
  const button=$('#library-add');button.disabled=true;
  try{await api('/api/admin/soundpad/import',{pageId:$('#library-page').value,indices:[...librarySelection],output:$('#library-output').value});librarySelection.clear();renderSoundpadResults();notice('Soundpad buttons added to your page.');await refresh(true);}catch(error){notice(error.message,true);}
  finally{if(featureView==='library')$('#library-add').disabled=librarySelection.size===0;}
}

async function showAppMixer(){
  if(preview){notice('Connect a PC to control Windows app volumes.');return;}
  if(dirty){notice('Save or discard your editor changes first.',true);return;}
  featureView='apps';editing=false;
  openPanel('Windows app volumes','<p class="help">These sliders control app playback sessions in Windows. OBS input faders remain separate. Start playback in an app if it is missing, then refresh.</p><div class="row"><label>Find an app<input id="app-search" type="search" placeholder="Discord, game, music…" autocomplete="off"></label><button id="apps-refresh" class="quiet">Refresh apps</button></div><p id="apps-status" class="help" role="status">Finding audio sessions…</p><div id="app-session-list" class="app-session-list"></div>');
  $('#app-search').oninput=renderAppSessions;$('#apps-refresh').onclick=loadAppSessions;await loadAppSessions();
}
async function loadAppSessions(){
  if(featureView!=='apps'||appMixerLoading||appMixerDragging)return;appMixerLoading=true;
  try{const result=await api('/api/apps');if(featureView!=='apps')return;appSessions=result.apps;$('#apps-status').textContent=`${appSessions.length} audio session${appSessions.length===1?'':'s'} · changes also appear in Windows Volume Mixer`;renderAppSessions();}
  catch(error){if(featureView==='apps')$('#apps-status').textContent=error.message;}
  finally{appMixerLoading=false;}
}
function renderAppSessions(){
  if(featureView!=='apps'||appMixerDragging)return;
  const focusVolume=document.activeElement?.dataset.appVolume,focusMute=document.activeElement?.dataset.appMute;
  const query=$('#app-search').value.trim().toLocaleLowerCase();const filtered=appSessions.filter(s=>(s.name+' '+s.device).toLocaleLowerCase().includes(query));
  featureHtml($('#app-session-list'),filtered.length?filtered.map(s=>`<section class="app-session" data-session="${esc(s.id)}"><div class="app-session-heading"><div><strong>${esc(s.name)}</strong><span>${esc(s.device)} · ${s.active?'Playing':'Idle'}</span></div><button class="mute app-mute ${s.muted?'is-muted':''}" data-app-mute="${esc(s.id)}" aria-label="${s.muted?'Unmute':'Mute'} ${esc(s.name)}" aria-pressed="${s.muted}">${s.muted?'Unmute':'Mute'}</button></div><div class="app-slider-row"><input type="range" class="app-volume" data-app-volume="${esc(s.id)}" min="0" max="100" step="1" value="${Math.round(s.percent)}" aria-label="${esc(s.name)} playback volume"><output>${Math.round(s.percent)}%</output></div></section>`).join(''):'<div class="feature-empty"><strong>No matching playback sessions</strong><p>Play audio in the app you want to control, then select Refresh apps.</p></div>');
  document.querySelectorAll('[data-app-volume]').forEach(input=>{
    input.onpointerdown=()=>appMixerDragging=true;input.onpointercancel=()=>{appMixerDragging=false;};
    input.oninput=()=>{appMixerDragging=true;input.nextElementSibling.textContent=input.value+'%';if(typeof queueAppVolume==='function')queueAppVolume(input.dataset.appVolume,Number(input.value)).catch(()=>{});};
    input.onchange=async()=>{try{if(typeof queueAppVolume==='function')await queueAppVolume(input.dataset.appVolume,Number(input.value));else await api('/api/apps',{id:input.dataset.appVolume,percent:Number(input.value)});}catch(error){notice(error.message,true);}finally{appMixerDragging=false;await loadAppSessions();}};
    input.onpointerup=()=>{setTimeout(()=>{if(appMixerDragging){appMixerDragging=false;loadAppSessions();}},150);};
  });
  document.querySelectorAll('[data-app-mute]').forEach(button=>button.onclick=async()=>{button.disabled=true;const s=appSessions.find(s=>s.id===button.dataset.appMute);try{await api('/api/apps',{id:s.id,muted:!s.muted});await loadAppSessions();}catch(error){notice(error.message,true);}finally{if(button.isConnected)button.disabled=false;}});
  if(focusVolume)[...document.querySelectorAll('[data-app-volume]')].find(el=>el.dataset.appVolume===focusVolume)?.focus({preventScroll:true});
  else if(focusMute)[...document.querySelectorAll('[data-app-mute]')].find(el=>el.dataset.appMute===focusMute)?.focus({preventScroll:true});
}
setInterval(()=>{if(featureView==='apps'&&panel.open&&!document.hidden)loadAppSessions();},2500);

function showTabletTools(){
  featureView='tools';
  openPanel('Your board','<section class="setup"><h3>Arrange your buttons</h3><p>Move existing buttons on this tablet. Adding actions, changing PC targets, and importing Soundpad buttons stay in the Windows editor.</p><div class="row"><button id="tablet-arrange" class="primary">Arrange buttons</button><button id="tablet-library" class="quiet">Soundpad library</button><button id="tablet-connection" class="quiet">Connection settings</button></div><p class="help">While arranging, drag keys or tap one to choose any position. Save order to sync the layout, or Cancel to keep the previous order.</p></section>');
  $('#tablet-arrange').onclick=startArrangement;$('#tablet-library').onclick=showSoundpadLibrary;$('#tablet-connection').onclick=connectionForm;
}
async function startArrangement(){
  if(pages.find(p=>p.id===pageId)?.liveScenes||pages.find(p=>p.id===pageId)?.liveSoundpad){notice('Live pages follow their source library. Disable live sync in the PC board editor to arrange saved manual buttons.',true);return;}
  if(preview){notice('Connect a PC to save a layout.');return;}
  if(dirty){notice('Save or discard your editor changes before arranging.',true);return;}
  try{const board=await api('/api/board');pages=board.pages;}catch(error){notice(error.message,true);return;}
  if(pages.find(p=>p.id===pageId)?.liveScenes||pages.find(p=>p.id===pageId)?.liveSoundpad){notice('This page now follows a live source. Arrange its saved buttons after disabling live sync on the PC.',true);return;}
  panel.close();arranging=true;document.body.classList.add('arranging');$('#soundpad-button').hidden=true;$('#pair-button').hidden=true;$('#arrange-cancel').hidden=false;$('#edit-button').textContent='Save order';renderBoard();notice('Drag buttons, or tap one to choose its position. Actions stay off until you finish.');
}
function endArrangement(){arranging=false;document.body.classList.remove('arranging');$('#soundpad-button').hidden=false;$('#pair-button').hidden=native||preview;$('#arrange-cancel').hidden=true;featureHtml($('#edit-button'),icon('settings')+'<span>Edit</span>');}
async function saveArrangement(){
  $('#edit-button').disabled=true;
  try{await api('/api/layout',{pages:pages.map(p=>({id:p.id,buttonIds:p.buttons.map(b=>b.id)}))});endArrangement();await refresh(true);notice('Button order saved.');}catch(error){notice(error.message,true);}finally{$('#edit-button').disabled=false;}
}
if($('#arrange-cancel'))$('#arrange-cancel').onclick=async()=>{endArrangement();await refresh(true);};
if($('#soundpad-button'))$('#soundpad-button').onclick=showSoundpadLibrary;if($('#apps-button'))$('#apps-button').onclick=showAppMixer;

function bindPointerOrder(container,selector,handleSelector,commit,tap){
  let drag=null;
  container.onpointerdown=event=>{
    if(event.button!==0||!event.isPrimary)return;
    const handle=event.target.closest(handleSelector);const item=handle?.closest(selector);if(!item||!container.contains(item))return;
    drag={item,pointer:event.pointerId,startX:event.clientX,startY:event.clientY,original:[...container.querySelectorAll(selector)],moved:false};
    container.setPointerCapture(event.pointerId);item.classList.add('dragging');event.preventDefault();
  };
  container.onpointermove=event=>{
    if(!drag||event.pointerId!==drag.pointer)return;
    if(Math.hypot(event.clientX-drag.startX,event.clientY-drag.startY)<6&&!drag.moved)return;
    drag.moved=true;
    const hit=document.elementFromPoint(event.clientX,event.clientY)?.closest(selector);if(!hit||hit===drag.item||!container.contains(hit))return;
    const list=[...container.querySelectorAll(selector)];const from=list.indexOf(drag.item),to=list.indexOf(hit);
    container.insertBefore(drag.item,to>from?hit.nextSibling:hit);
  };
  const finish=(event,cancel)=>{
    if(!drag||event.pointerId!==drag.pointer)return;
    const current=drag;drag=null;current.item.classList.remove('dragging');
    if(container.hasPointerCapture(event.pointerId))container.releasePointerCapture(event.pointerId);
    if(cancel){current.original.forEach(node=>container.appendChild(node));return;}
    if(current.moved)commit([...container.querySelectorAll(selector)].map(node=>node.dataset.orderId));
    else if(tap)tap(current.item.dataset.orderId);
  };
  container.onpointerup=event=>finish(event,false);container.onpointercancel=event=>finish(event,true);
}
function attachBoardReorder(){
  const container=$('#keys');
  if(!arranging){container.onpointerdown=null;container.onpointermove=null;container.onpointerup=null;container.onpointercancel=null;return;}
  const page=pages.find(p=>p.id===pageId);if(!page)return;
  for(const button of container.querySelectorAll('.key')){button.classList.add('arrange-key');button.classList.remove('active');button.querySelector('.active-tag')?.remove();button.dataset.orderId=button.dataset.id;button.onclick=event=>{if(event.detail===0)showMovePosition(page,button.dataset.id);};button.setAttribute('aria-description','Drag to move, tap to choose a position, or use Alt plus an arrow key.');button.onkeydown=event=>{if(!event.altKey||!['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(event.key))return;event.preventDefault();const index=page.buttons.findIndex(b=>b.id===button.dataset.id),offset={ArrowLeft:-1,ArrowRight:1,ArrowUp:-boardColumns(),ArrowDown:boardColumns()}[event.key],next=index+offset;if(next<0||next>=page.buttons.length)return;const [action]=page.buttons.splice(index,1);page.buttons.splice(next,0,action);slice=Math.floor(next/boardCapacity());renderBoard();[...$('#keys').querySelectorAll('.key')].find(b=>b.dataset.id===action.id)?.focus();};}
  bindPointerOrder(container,'.key','.key',ids=>{const start=slice*boardCapacity();const lookup=new Map(page.buttons.map(b=>[b.id,b]));page.buttons.splice(start,ids.length,...ids.map(id=>lookup.get(id)));renderBoard();},id=>showMovePosition(page,id));
}
function showMovePosition(page,id){
  const index=page.buttons.findIndex(button=>button.id===id);if(index<0)return;
  featureView='move';
  openPanel('Move button',`<p>Choose where to put <strong>${esc(page.buttons[index].label)}</strong> in ${esc(page.name)}. You can move across all button groups, including full-pad layouts.</p><label>Move to position<select id="move-position" aria-label="Move to position">${page.buttons.map((button,i)=>`<option value="${i}" ${i===index?'selected':''}>${i+1} · ${esc(button.label)}</option>`).join('')}</select></label><div class="row"><button id="move-position-apply" class="primary">Move button</button></div><p class="help">This changes the arrangement only. Choose Save order on the board to keep it, or Cancel to discard it.</p>`);
  $('#move-position-apply').onclick=()=>{const destination=Number($('#move-position').value);const [action]=page.buttons.splice(index,1);page.buttons.splice(destination,0,action);slice=Math.floor(destination/boardCapacity());panel.close();renderBoard();notice('Button moved. Choose Save order to keep your changes.');};
}
function attachEditorReorder(){
  if(!panel.open||!$('#edit-page'))return;featureView='editor';
  const list=$('.editor-list');if(list.querySelector('.editor-sort-row'))return;const page=config.pages[selectedPage];
  const sync=document.createElement('label');sync.className='check';featureHtml(sync,`<input id="page-live-scenes" type="checkbox" ${page.liveScenes?'checked':''}>Keep this page synced with live OBS`);$('#page-name').parentElement.after(sync);
  const help=document.createElement('p');help.className='help';help.textContent='Live mode follows scene additions, names, removals and OBS order. Saved manual buttons are retained and return when you disable it.';sync.after(help);
  $('#page-live-scenes').onchange=()=>{page.liveScenes=$('#page-live-scenes').checked;if(page.liveScenes)page.liveSoundpad=false;markDirty();renderEditor();};
  const soundSync=document.createElement('label');soundSync.className='check';featureHtml(soundSync,`<input id="page-live-soundpad" type="checkbox" ${page.liveSoundpad?'checked':''}>Use the full live Soundpad library on this page`);help.after(soundSync);
  const soundHelp=document.createElement('p');soundHelp.className='help';soundHelp.textContent='Every loaded Soundpad sound becomes a pad button, with search, categories and page selection. One live Soundpad page per profile; enabling this restores manual buttons on the previous live Soundpad page. Saved buttons are retained.';soundSync.after(soundHelp);
  $('#page-live-soundpad').onchange=()=>{page.liveSoundpad=$('#page-live-soundpad').checked;if(page.liveSoundpad){page.liveScenes=false;for(const other of config.pages)if(other!==page)other.liveSoundpad=false;}markDirty();renderEditor();};
  if(page.liveSoundpad){const output=document.createElement('label');featureHtml(output,`Soundpad playback output<select id="page-soundpad-output"><option value="mic">Discord / microphone</option><option value="both">Speakers and microphone</option><option value="speakers">Speakers only</option></select>`);soundHelp.after(output);$('#page-soundpad-output').value=page.liveSoundpadOutput||'mic';$('#page-soundpad-output').onchange=()=>{page.liveSoundpadOutput=$('#page-soundpad-output').value;markDirty();};}
  const live=!!page.liveScenes||!!page.liveSoundpad;list.hidden=live;$('.editor-form').hidden=live;$('#add-button').hidden=live;if(live)return;
  for(const button of list.querySelectorAll('[data-edit-index]')){
    const row=document.createElement('div');row.className='editor-sort-row';row.dataset.orderId=page.buttons[Number(button.dataset.editIndex)].id;
    button.before(row);row.appendChild(button);
    const handle=document.createElement('button');handle.className='quiet drag-handle';handle.type='button';handle.setAttribute('aria-label','Drag '+page.buttons[Number(button.dataset.editIndex)].label+' to reorder');featureHtml(handle,icon('list'));row.appendChild(handle);
  }
  bindPointerOrder(list,'.editor-sort-row','.drag-handle',ids=>{const selected=page.buttons[selectedButton]?.id,lookup=new Map(page.buttons.map(a=>[a.id,a]));page.buttons=ids.map(id=>lookup.get(id));selectedButton=Math.max(0,page.buttons.findIndex(a=>a.id===selected));markDirty();renderEditor();});
  const toolbar=document.createElement('div');toolbar.className='row feature-editor-tools';
  const browse=document.createElement('button');browse.className='quiet';browse.textContent='Browse Soundpad';browse.onclick=showSoundpadLibrary;
  const arrange=document.createElement('button');arrange.className='quiet';arrange.textContent='Arrange board';arrange.onclick=startArrangement;
  toolbar.appendChild(browse);if(typeof desktopConsole==='undefined'||!desktopConsole)toolbar.appendChild(arrange);$('.editor-layout').before(toolbar);
  const desktop=document.createElement('button');desktop.className='quiet';desktop.textContent='Desktop settings';desktop.onclick=showDesktopSettings;toolbar.appendChild(desktop);
}
