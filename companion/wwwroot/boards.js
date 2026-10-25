'use strict';
function boardPanel(title,html){openPanel(title,html+'<p id="board-status" role="alert" class="help"></p>');}
function boardError(message){const status=$('#board-status');if(status)status.textContent=message;else notice(message,true);}
function ownsBoardView(generation,view){return panelGeneration===generation&&featureView===view&&panel.open&&!dirty&&!arranging;}
function beginBoardView(view,title){featureView=view;editing=false;boardPanel(title,'<p role="status">Loading…</p>');return panelGeneration;}
function canChangeBoard(){
  if(dirty||arranging){boardError('Save or discard your edits before changing profiles.');return false;}
  if(busy||executionStatus?.active||hasActiveHold()){boardError('Finish the running action and release held keys first.');return false;}
  return true;
}
async function showProfiles(){
  if(preview){notice('Connect a PC to use board profiles.');return;}
  if(!canChangeBoard())return;
  let generation=beginBoardView('profiles','Board profiles');
  try{
    const result=await api('/api/profiles');if(!ownsBoardView(generation,'profiles'))return;
    boardPanel('Board profiles',`<p>Switch the pages and buttons together. Pairing, OBS connection, audio inputs, and Soundpad favourites are shared by all profiles.</p>${result.warning?`<p role="alert">${esc(result.warning)}</p>`:''}<div class="profile-list">${result.profiles.map(p=>`<div class="profile-row"><div><strong>${esc(p.name)}</strong><span>${p.pages} pages · ${p.buttons} buttons${p.active?' · Current profile':''}</span></div><button class="${p.active?'quiet':'primary'}" data-use-profile="${esc(p.id)}" ${p.active?'disabled':''}>${p.active?'Current':'Switch'}</button></div>`).join('')}</div>${!native?`<h3>Manage profiles</h3><div class="form-grid"><label>Profile<select id="manage-profile">${result.profiles.map(p=>`<option value="${esc(p.id)}">${esc(p.name)}</option>`).join('')}</select></label><label>Profile name<input id="profile-name" maxlength="30" value="${esc(result.profiles[0].name)}"></label></div><div class="row"><button id="profile-rename" class="quiet">Rename</button><button id="profile-delete" class="danger" disabled>Delete profile</button><button id="open-backups" class="quiet">Backups and restore</button></div><h3>Create a profile</h3><label>New profile name<input id="new-profile-name" maxlength="30" placeholder="Gaming, Recording, Discord…"></label><div class="row"><button id="profile-empty" class="primary">Create empty</button><button id="profile-copy" class="quiet">Duplicate current</button></div><p class="help">Up to 10 profiles. New profiles are added without switching. Delete requires a different profile to be active. A backup is kept before each edit.</p>`:'<p class="help">Create, duplicate, rename, and delete profiles in the PC editor.</p>'}`);
    generation=panelGeneration;
    for(const button of document.querySelectorAll('[data-use-profile]'))button.onclick=async()=>{if(!ownsBoardView(generation,'profiles')||!canChangeBoard())return;button.disabled=true;try{await api('/api/profile',{id:button.dataset.useProfile});if(ownsBoardView(generation,'profiles'))panel.close();lastBoard=0;await refresh(true);notice('Profile switched.');}catch(error){if(ownsBoardView(generation,'profiles'))boardError(error.message);if(button.isConnected)button.disabled=false;}};
    if(native)return;
    $('#manage-profile').onchange=()=>{const selected=result.profiles.find(p=>p.id===$('#manage-profile').value);$('#profile-name').value=selected.name;$('#profile-delete').disabled=selected.active;};
    let saving=false;
    async function manage(body,button){if(saving||!ownsBoardView(generation,'profiles')||!canChangeBoard())return;saving=true;button.disabled=true;try{await api('/api/admin/profiles',body);lastBoard=0;await refresh(true);if(ownsBoardView(generation,'profiles'))await showProfiles();notice('Profiles saved.');}catch(error){if(ownsBoardView(generation,'profiles'))boardError(error.message);if(button.isConnected)button.disabled=false;}finally{saving=false;}}
    $('#profile-rename').onclick=()=>manage({operation:'rename',id:$('#manage-profile').value,name:$('#profile-name').value},$('#profile-rename'));
    $('#profile-delete').onclick=async()=>{const id=$('#manage-profile').value,selected=result.profiles.find(p=>p.id===id);if(await confirmAction('Delete '+selected.name+'?','Its pages and buttons will be removed. An automatic backup is kept for recovery.'))await manage({operation:'delete',id,confirmed:true},$('#profile-delete'));};
    for(const [selector,duplicate] of [['#profile-empty',false],['#profile-copy',true]])$(selector).onclick=()=>manage({operation:'create',name:$('#new-profile-name').value,duplicate},$(selector));
    $('#open-backups').onclick=showBackups;
  }catch(error){if(ownsBoardView(generation,'profiles'))boardError(error.message);}
}
async function showBackups(){
  if(native||preview||!canChangeBoard())return;
  let generation=beginBoardView('backups','Backups and restore');
  try{
    const result=await api('/api/admin/backups');if(!ownsBoardView(generation,'backups'))return;
    boardPanel('Backups and restore',`<p>Keep the latest ${result.limit} snapshots of all profiles and favourites. Snapshots are stored privately on this PC. OBS passwords and tablet credentials are excluded; restoring keeps your current credentials.</p><div class="row"><button id="backup-now" class="primary">Create backup now</button><button id="back-to-profiles" class="quiet">Back to profiles</button></div><div class="backup-list">${result.backups.length?result.backups.map(b=>`<div class="profile-row"><div><strong>${esc(new Date(b.created).toLocaleString())}</strong><span>${esc(b.reason)} · ${b.profiles} profiles · ${b.buttons} buttons · ${b.favourites} favourites</span></div><button class="quiet" data-preview-backup="${esc(b.id)}">Preview</button></div>`).join(''):'<p>No backups yet. Create one now, or edit your board to make the first automatic snapshot.</p>'}</div><p class="help">Profile switches do not fill the history. Older snapshots are removed after 20 newer backups. Sound files and apps are referenced, not copied. A damaged config can recover from the newest valid snapshot; its OBS password must be entered again.</p>`);
    $('#back-to-profiles').onclick=showProfiles;
    generation=panelGeneration;
    $('#backup-now').onclick=async()=>{if(!ownsBoardView(generation,'backups'))return;const button=$('#backup-now');button.disabled=true;try{await api('/api/admin/backups',{});if(ownsBoardView(generation,'backups'))await showBackups();notice('Backup created.');}catch(error){if(ownsBoardView(generation,'backups'))boardError(error.message);if(button.isConnected)button.disabled=false;}};
    for(const button of document.querySelectorAll('[data-preview-backup]'))button.onclick=()=>previewBackup(button.dataset.previewBackup);
  }catch(error){if(ownsBoardView(generation,'backups'))boardError(error.message);}
}
async function previewBackup(id){
  if(!canChangeBoard())return;
  let generation=beginBoardView('backup-preview','Preview backup');
  try{
    const result=await api('/api/admin/backups/preview',{id});if(!ownsBoardView(generation,'backup-preview'))return;
    const b=result.backup;
    boardPanel('Preview backup',`<p>${esc(new Date(b.created).toLocaleString())} · ${esc(b.reason)}</p><h3>Profiles to restore</h3><ul class="backup-profile-summary">${result.profiles.map(p=>`<li><strong>${esc(p.name)}</strong> · ${p.pages} pages · ${p.buttons} buttons${p.active?' · opens after restore':''}</li>`).join('')}</ul><p>${b.favourites} Soundpad favourites will be restored. Your ${result.current.length} current profiles will be replaced. The current board is backed up first. Pairing, certificate, and your current OBS password are kept.</p><div class="row"><button id="restore-backup" class="danger">Restore this backup</button><button id="back-to-backups" class="quiet">Back to backups</button></div>`);
    $('#back-to-backups').onclick=showBackups;
    generation=panelGeneration;
    $('#restore-backup').onclick=async()=>{if(!ownsBoardView(generation,'backup-preview')||!canChangeBoard())return;if(!await confirmAction('Restore this backup?','Replace all current profiles and favourites with the previewed snapshot. Current credentials are preserved and a backup is made first.'))return;if(!ownsBoardView(generation,'backup-preview'))return;const button=$('#restore-backup');button.disabled=true;try{await api('/api/admin/backups/restore',{id,confirmed:true});if(ownsBoardView(generation,'backup-preview'))panel.close();lastBoard=0;await refresh(true);notice('Backup restored.');}catch(error){if(ownsBoardView(generation,'backup-preview'))boardError(error.message);if(button.isConnected)button.disabled=false;}};
  }catch(error){if(ownsBoardView(generation,'backup-preview'))boardError(error.message);}
}
