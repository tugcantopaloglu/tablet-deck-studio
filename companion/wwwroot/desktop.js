'use strict';
// PC-only setup never runs a deck action and uses the existing authenticated panel.
function desktopCanNavigate(){
  if(dirty||arranging){notice('Save or discard your changes before opening PC setup.',true);return false;}
  return !native&&!preview;
}
function desktopError(message){const target=$('#desktop-feedback');if(target)target.textContent=message;else notice(message,true);}
function desktopView(title,html){openPanel(title,html+'<p id="desktop-feedback" class="help" role="alert"></p>');}
function ownsDesktop(generation,view){return panel.open&&panelGeneration===generation&&featureView===view;}
function beginDesktop(view,title){featureView=view;editing=false;desktopView(title,'<p role="status">Checking this PC…</p>');return panelGeneration;}
function setupRow(title,status,help,button,label){
  return `<section class="setup-row"><div><h3>${esc(title)}</h3><strong>${esc(status)}</strong><p class="help">${esc(help)}</p></div><button id="${button}" class="quiet">${esc(label)}</button></section>`;
}
function setupSummary(info,desktop,obs,backups,profiles){
  const active=profiles.profiles.find(p=>p.active),inputs=['mic','desktop'].filter(ch=>obs.inputs?.[ch]?.available).length;
  return {paired:info.devices.length,obs:obs.obsConnected?`Connected · ${inputs}/2 audio inputs ready`:'Not connected',
    obsHelp:obs.obsConnected?`Current scene: ${obs.scene||'None'}. Checks do not start a stream or recording.`:'Open OBS on this PC, enable its WebSocket server, then save the port and password here.',
    board:active?`${active.name} · ${active.pages} pages · ${active.buttons} buttons`:'No active profile',
    backup:backups.backups.length?`${backups.backups.length} of ${backups.limit} snapshots saved`:'No snapshots yet',usb:desktop.usbStatus};
}
async function showDesktopHub(){
  if(!desktopCanNavigate())return;
  let generation=beginDesktop('pc-setup','PC setup');
  const routes=['/api/admin/info','/api/admin/desktop','/api/state','/api/admin/backups','/api/profiles'];
  const results=await Promise.allSettled(routes.map(route=>api(route)));
  if(!ownsDesktop(generation,'pc-setup'))return;
  const failed=results.find(result=>result.status==='rejected');
  if(failed){
    desktopView('PC setup','<p>Could not check this PC. If you reopened an old browser tab, use the Windows tray menu → Open control panel to unlock it again.</p><button id="setup-retry" class="primary">Retry checks</button>');
    desktopError(failed.reason.message);$('#setup-retry').onclick=showDesktopHub;return;
  }
  const [info,desktop,obs,backups,profiles]=results.map(result=>result.value),summary=setupSummary(info,desktop,obs,backups,profiles);
  desktopView('PC setup',`<p class="help">Set up this PC, then use the tablet to run your board. Closing this panel keeps the companion running.</p>${profiles.warning?`<p role="alert">${esc(profiles.warning)}</p>`:''}<div class="row"><button id="setup-refresh" class="quiet">Refresh checks</button><span class="help">Checked ${esc(new Date().toLocaleTimeString())}</span></div><div class="setup-checks">${
    setupRow('Tablet connection',summary.paired?`${summary.paired} paired tablet${summary.paired===1?'':'s'}`:'Pair your first tablet',summary.paired?'Pairing is saved. This count does not mean a tablet is currently connected.':'Keep the tablet and PC on the same Wi-Fi, or connect a USB data cable.','setup-pair',summary.paired?'Pair / manage tablets':'Pair tablet')+
    setupRow('OBS Studio',summary.obs,summary.obsHelp,'setup-obs','Set up OBS')+
    setupRow('Your board',summary.board,'Edit buttons on the PC. Save changes to sync them to your paired tablet.','setup-edit','Edit buttons')+
    setupRow('Profiles and backups',summary.backup,'Create different boards and restore earlier snapshots. Sound files are referenced, not copied.','setup-profiles','Manage profiles')+
    setupRow('Soundpad','Library and favourites','Open Soundpad on the PC, then browse and star sounds here. Choose Speakers + microphone so Discord can receive them.','setup-sounds','Browse sounds')+
    setupRow('Windows and USB',summary.usb,'Choose startup, automatic USB reconnect, and update notifications.','setup-windows','Windows options')
  }</div><div class="row"><button id="setup-backups" class="quiet">Backups and restore</button><button id="setup-board" class="primary">Use board</button></div><p class="help">Discord uses the microphone enabled in Soundpad. Noise suppression and voice processing can filter sounds; rehearse with Discord’s local microphone test before streaming.</p>`);
  generation=panelGeneration;
  const actions={'#setup-refresh':showDesktopHub,'#setup-pair':showPairing,'#setup-obs':showObsSetup,'#setup-edit':showEditor,'#setup-profiles':showProfiles,'#setup-backups':showBackups,'#setup-sounds':showSoundpadLibrary,'#setup-windows':showDesktopSettings,'#setup-board':()=>panel.close()};
  for(const [id,action] of Object.entries(actions))$(id).onclick=()=>{if(ownsDesktop(generation,'pc-setup'))return action();};
}
async function showObsSetup(){
  if(!desktopCanNavigate())return;
  let generation=beginDesktop('obs-setup','Set up OBS');
  try{
    const config=await api('/api/admin/config');if(!ownsDesktop(generation,'obs-setup'))return;
    desktopView('Set up OBS',`<ol class="setup-instructions"><li>Open OBS Studio on this PC.</li><li>Choose <strong>Tools → WebSocket Server Settings</strong>, enable the server, and keep authentication enabled.</li><li>Copy the server port and password below. Save, then check the connection.</li></ol><form id="obs-setup-form"><div class="form-grid"><label>WebSocket port<input id="setup-obs-port" type="number" min="1" max="65535" required value="${config.obsPort}"></label><label>WebSocket password<input id="setup-obs-password" type="password" maxlength="512" autocomplete="new-password" placeholder="${config.passwordConfigured?'Saved — leave blank to keep':'Enter the OBS password'}"></label><label>Microphone input name<input id="setup-mic" maxlength="200" list="setup-inputs" value="${esc(config.micInput)}"></label><label>Desktop audio input name<input id="setup-desktop" maxlength="200" list="setup-inputs" value="${esc(config.desktopInput)}"></label></div><datalist id="setup-inputs"></datalist><label class="check"><input id="setup-clear-password" type="checkbox">Clear saved password</label><p class="help">Input names must match OBS. You can connect first, then use Find inputs. These settings apply to every board profile.</p><div class="row"><button id="setup-obs-save" type="submit" class="primary">Save OBS settings</button><button id="setup-obs-test" type="button" class="quiet">Check saved connection</button><button id="setup-obs-inputs" type="button" class="quiet">Find inputs</button></div></form><p id="setup-obs-result" class="help" role="status">Checks use the saved settings and do not start streaming or recording.</p><div class="row"><button id="setup-obs-back" class="quiet">Back to PC setup</button></div>`);
    generation=panelGeneration;let working=false;
    const owns=()=>ownsDesktop(generation,'obs-setup');
    const feedback=message=>{if(owns())desktopError(message);};
    const setWorking=value=>{working=value;if(owns())for(const control of $('#obs-setup-form').querySelectorAll('input,button'))control.disabled=value;};
    $('#obs-setup-form').oninput=()=>{dirty=true;};
    async function check(){
      const status=await api('/api/state');if(!owns())return;
      $('#setup-obs-result').textContent=status.obsConnected?`OBS connected. Mic: ${status.inputs?.mic?.available?'ready':'input not found'}. Desktop audio: ${status.inputs?.desktop?.available?'ready':'input not found'}.`:'OBS is not connected. Check the server settings in OBS, port, and password. '+(status.error||'');
    }
    $('#obs-setup-form').onsubmit=async event=>{
      event.preventDefault();if(working||!owns())return;
      const password=$('#setup-obs-password').value,clear=$('#setup-clear-password').checked;
      if(clear&&password){feedback('Choose either a replacement password or Clear saved password.');return;}
      const body={port:Number($('#setup-obs-port').value),micInput:$('#setup-mic').value,desktopInput:$('#setup-desktop').value,passwordMode:clear?'clear':password?'replace':'keep',password};
      if(!Number.isInteger(body.port)||body.port<1||body.port>65535){feedback('Enter a port from 1 to 65535.');return;}
      setWorking(true);feedback('');
      try{
        if(clear&&!await confirmAction('Clear the OBS password?','This removes the saved password for every profile. Keep authentication enabled in OBS and enter a replacement to reconnect.'))return;
        if(!owns())return;
        await api('/api/admin/obs-settings',body);if(!owns())return;
        dirty=false;$('#setup-obs-password').value='';$('#setup-clear-password').checked=false;
        config.passwordConfigured=body.passwordMode==='replace'||(body.passwordMode==='keep'&&config.passwordConfigured);
        $('#setup-obs-password').placeholder=config.passwordConfigured?'Saved — leave blank to keep':'Enter the OBS password';
        feedback('OBS settings saved. Checking the connection…');await check();feedback('OBS settings saved.');
      }catch(error){feedback(error.message);}finally{setWorking(false);}
    };
    $('#setup-obs-test').onclick=async()=>{if(working||!owns())return;setWorking(true);feedback('');try{await check();}catch(error){feedback(error.message);}finally{setWorking(false);}};
    $('#setup-obs-inputs').onclick=async()=>{
      if(working||!owns())return;setWorking(true);feedback('');
      try{const result=await api('/api/admin/obs');if(!owns())return;const names=result.inputs.inputs.map(input=>input.inputName);$('#setup-inputs').innerHTML=DOMPurify.sanitize(names.map(name=>`<option value="${esc(name)}"></option>`).join(''));$('#setup-obs-result').textContent=names.length?'Inputs found. Choose an exact name in the microphone and desktop fields, then save.':'OBS has no inputs yet. Add audio inputs in OBS, then try again.';}catch(error){feedback('Could not find inputs. Save the OBS connection first. '+error.message);}finally{setWorking(false);}
    };
    $('#setup-obs-back').onclick=()=>{if(!working)showDesktopHub();};
  }catch(error){if(ownsDesktop(generation,'obs-setup'))desktopError(error.message);}
}
async function initializeDesktopSetup(){
  if(native||preview)return;
  document.body.classList.add('pc-editor');
  $('#pc-setup').hidden=false;$('#pc-setup').onclick=showDesktopHub;
  const generation=panelGeneration;
  try{
    if(sessionStorage.getItem('deckSetupSeen'))return;
    const info=await api('/api/admin/info');
    if(panel.open||panelGeneration!==generation||dirty||arranging)return;
    sessionStorage.setItem('deckSetupSeen','1');
    if(!info.devices.length)await showDesktopHub();
  }catch{/* The visible PC setup entry provides retry/unlock instructions. */}
}
document.addEventListener('DOMContentLoaded',initializeDesktopSetup,{once:true});
