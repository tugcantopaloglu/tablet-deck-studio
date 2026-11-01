'use strict';
const monitorDefaults={twitch:'',kick:'',provider:'twitch',mode:'program',width:640,fps:2,layout:'both'};
function monitorChannel(provider,value){const channel=String(value||'').trim().replace(/^@/,'').toLowerCase();if(!channel)return '';if(!(provider==='twitch'?/^[a-z0-9_]{1,25}$/:provider==='kick'?/^[a-z0-9_-]{1,50}$/:/$a/).test(channel))throw Error('Enter only the '+(provider==='twitch'?'Twitch':'Kick')+' channel name, without a URL.');return channel;}
function validateMonitor(value){const next={...monitorDefaults,...value};next.twitch=monitorChannel('twitch',next.twitch);next.kick=monitorChannel('kick',next.kick);if(!['twitch','kick'].includes(next.provider)||!['program','preview'].includes(next.mode)||![320,640,960].includes(next.width)||![1,2,4].includes(next.fps)||!['both','video','chat'].includes(next.layout))throw Error('Choose supported monitor settings.');return next;}
function loadMonitor(){try{return validateMonitor(JSON.parse(localStorage.getItem('deckMonitorSettings')||'{}'));}catch{return{...monitorDefaults};}}
function chatUrls(provider,name,parent){const channel=monitorChannel(provider,name);if(!channel)throw Error('Add a '+provider+' channel in Monitor settings.');if(!/^[a-zA-Z0-9.-]+$/.test(parent))throw Error('Unsupported chat parent hostname.');return provider==='twitch'?{embed:`https://www.twitch.tv/embed/${channel}/chat?parent=${encodeURIComponent(parent)}&darkpopout`,popout:`https://www.twitch.tv/popout/${channel}/chat?popout=`}:{embed:`https://kick.com/popout/${channel}/chat`,popout:`https://kick.com/popout/${channel}/chat`};}
// One in-flight frame, no queue/replay. Old requests cannot resurrect a closed preview.
class PreviewLoop{
  constructor(fetchFrame,render,error,schedule=setTimeout,cancel=clearTimeout,now=()=>performance.now()){Object.assign(this,{fetchFrame,render,error,schedule,cancel,now});this.active=false;this.paused=true;this.pending=false;this.generation=0;this.failures=0;this.timer=null;}
  start(config){this.config={...config};this.active=true;this.failures=0;this.resume();}
  stop(){this.active=false;this.paused=true;this.generation++;this.cancel(this.timer);}
  pause(){this.paused=true;this.generation++;this.cancel(this.timer);}
  resume(){if(!this.active)return;this.paused=false;const generation=++this.generation;this.cancel(this.timer);this.tick(generation);}
  owns(generation){return this.active&&!this.paused&&this.generation===generation;}
  async tick(generation){
    if(!this.owns(generation))return;if(this.pending){this.timer=this.schedule(()=>this.tick(generation),100);return;}
    this.pending=true;const started=this.now();
    try{const frame=await this.fetchFrame({mode:this.config.mode,width:this.config.width});if(this.owns(generation)){this.failures=0;this.render(frame,this.now()-started);}}
    catch(error){if(this.owns(generation)){this.failures++;this.error(error.message);}}
    finally{this.pending=false;if(this.owns(generation)){const wait=this.failures?Math.min(5000,500*2**Math.min(this.failures,4)):Math.max(0,1000/this.config.fps-(this.now()-started));this.timer=this.schedule(()=>this.tick(generation),wait);}}
  }
}
let monitorSettings=loadMonitor(),monitorOpen=false,videoWanted=false,chatWanted=false,monitorSuspended=false,chatEpoch=0,chatMounted=false,chatFrame=null,chatBoundsTimer=null,lastFrameAt=0,previewLoop;
function monitorRunning(){return monitorOpen&&!document.hidden&&!monitorSuspended&&!panel.open&&!$('#confirm-dialog').open;}
function monitorMessage(id,text){const node=$(id);if(node&&node.textContent!==text)node.textContent=text;}
function monitorVideoError(message){monitorMessage('#preview-status','Preview unavailable · '+message);}
function renderPreview(frame,roundTrip){
  if(!monitorRunning()||!videoWanted)return;
  if(typeof frame.image!=='string'||!frame.image.startsWith('data:image/jpeg;base64,')||frame.image.length>525000)throw Error('The companion returned an invalid preview image.');
  $('#preview-image').src=frame.image;$('#preview-image').hidden=false;$('#preview-empty').hidden=true;lastFrameAt=performance.now()-Math.max(0,Number(frame.ageMs)||0)-roundTrip;
  $('#preview-image').alt='OBS '+frame.mode+' scene: '+frame.source;monitorMessage('#preview-source',frame.source);monitorMessage('#preview-status',`Live OBS preview · up to ${monitorSettings.fps} fps · no audio`);
}
function setMonitorOpen(value){
  if(preview){notice('Use the running companion and paired tablet for live monitoring.');return;}
  if(value&&(dirty||arranging)){notice('Save or discard your changes before opening monitoring.',true);return;}
  monitorOpen=!!value;$('#live-monitor').hidden=!monitorOpen;$('#workspace').classList.toggle('monitor-layout',monitorOpen);$('#monitor-toggle').setAttribute('aria-pressed',String(monitorOpen));
  if(!monitorOpen){videoWanted=false;chatWanted=false;previewLoop?.stop();hideMonitorChat();$('#preview-image').removeAttribute('src');$('#preview-image').hidden=true;$('#preview-empty').hidden=false;lastFrameAt=0;}
  if(typeof consoleViewChanged==='function')consoleViewChanged('overview');applyMonitorLayout();refreshMonitorActivity();
}
function applyMonitorLayout(){
  $('#monitor-video').hidden=monitorSettings.layout==='chat';$('#monitor-chat').hidden=monitorSettings.layout==='video';$('#live-monitor').classList.toggle('video-only',monitorSettings.layout==='video');$('#live-monitor').classList.toggle('chat-only',monitorSettings.layout==='chat');
  $('#chat-twitch').setAttribute('aria-pressed',String(monitorSettings.provider==='twitch'));$('#chat-kick').setAttribute('aria-pressed',String(monitorSettings.provider==='kick'));
  $('#preview-toggle').textContent=videoWanted?'Pause preview':'Start preview';$('#preview-toggle').setAttribute('aria-pressed',String(videoWanted));$('#chat-load').textContent=chatWanted?'Unload chat':'Load chat';$('#chat-popout').hidden=native;
}
function monitorBounds(){const rect=$('#chat-viewport').getBoundingClientRect(),area=$('#workspace').getBoundingClientRect();const left=Math.max(0,rect.left,area.left),top=Math.max(0,rect.top,area.top);return{left,top,width:Math.max(0,Math.min(rect.right,area.right,window.innerWidth)-left),height:Math.max(0,Math.min(rect.bottom,area.bottom,window.innerHeight)-top),viewportWidth:window.innerWidth};}
function hideMonitorChat(){chatEpoch++;chatMounted=false;if(chatFrame){chatFrame.remove();chatFrame=null;}if(native)nativeRequest('/native/chat',{operation:'hide'}).catch(()=>{});}
async function mountMonitorChat(){
  if(chatMounted||!chatWanted||!monitorRunning()||monitorSettings.layout==='video')return;
  const current=++chatEpoch;const provider=monitorSettings.provider,name=monitorSettings[provider];
  try{const urls=chatUrls(provider,name,location.hostname);monitorMessage('#chat-status','Opening '+provider+' public chat…');
    if(native){await nativeRequest('/native/chat',{operation:'show',provider,channel:name,session:current,bounds:monitorBounds()});if(current!==chatEpoch||!monitorRunning())return;chatMounted=true;}
    else{const frame=document.createElement('iframe');frame.title=(provider==='twitch'?'Twitch':'Kick')+' chat for '+name;frame.referrerPolicy='origin';frame.setAttribute('sandbox','allow-storage-access-by-user-activation allow-scripts allow-same-origin allow-popups allow-popups-to-escape-sandbox allow-modals');frame.src=urls.embed;frame.onload=()=>{if(current===chatEpoch)monitorMessage('#chat-status','Provider frame opened. If blank or restricted, use Open chat window.');};frame.onerror=()=>{if(current===chatEpoch)monitorMessage('#chat-status','Chat viewer unavailable. Check Internet access or open the provider window.');};$('#chat-viewport').replaceChildren(frame);chatFrame=frame;chatMounted=true;}
  }catch(error){if(current===chatEpoch){chatWanted=false;chatMounted=false;applyMonitorLayout();monitorMessage('#chat-status',error.message);}}
}
function refreshMonitorActivity(){
  if(!previewLoop)return;
  if(monitorRunning()){
    if(videoWanted&&monitorSettings.layout!=='chat')previewLoop.start(monitorSettings);else previewLoop.pause();
    mountMonitorChat();
  }else{previewLoop.pause();hideMonitorChat();if(videoWanted)monitorMessage('#preview-status','Preview paused while the monitor is hidden, a tool is open, or the app is in the background.');}
  applyMonitorLayout();
}
window.monitorSuspend=()=>{monitorSuspended=true;refreshMonitorActivity();};
window.monitorResume=()=>{monitorSuspended=false;refreshMonitorActivity();};
window.monitorChatEvent=event=>{if(event.session===chatEpoch&&chatWanted&&monitorRunning())monitorMessage('#chat-status',event.state);};
function updateMonitorBounds(){clearTimeout(chatBoundsTimer);chatBoundsTimer=setTimeout(async()=>{if(!native||!chatMounted||!monitorRunning())return;try{await nativeRequest('/native/chat',{operation:'bounds',bounds:monitorBounds()});}catch(error){hideMonitorChat();monitorMessage('#chat-status',error.message);}},80);}
function showMonitorSettings(){
  const generation=studioStart('monitor-settings','Monitor settings');if(!generation)return;
  studioPanel('Monitor settings',`<p>Editable channel fields are saved on this device. Loading a chat contacts that provider. Video stays between OBS, this PC and your paired tablet.</p><form id="monitor-form"><div class="form-grid"><label>Twitch channel<input id="monitor-twitch" value="${esc(monitorSettings.twitch)}" maxlength="25" placeholder="channel_name" autocomplete="off"></label><label>Kick channel<input id="monitor-kick" value="${esc(monitorSettings.kick)}" maxlength="50" placeholder="channel-name" autocomplete="off"></label><label>OBS view<select id="monitor-mode">${options({program:'Program output',preview:'Studio preview scene'},monitorSettings.mode)}</select></label><label>Preview width<select id="monitor-width">${options({'320':'320 px · light','640':'640 px · balanced','960':'960 px · detail'},String(monitorSettings.width))}</select></label><label>Maximum frame rate<select id="monitor-fps">${options({'1':'1 frame/s','2':'2 frames/s','4':'4 frames/s'},String(monitorSettings.fps))}</select></label><label>Layout<select id="monitor-layout">${options({both:'Video + chat',video:'Video only',chat:'Chat only'},monitorSettings.layout)}</select></label></div><p class="help">Local preview is a low-frame-rate JPEG monitor with no audio, not a full-motion stream or latency guarantee. It uses a separate OBS connection. Chat is a provider viewer; cookies, sign-in prompts or provider restrictions may appear. This app does not store streaming-account passwords or post messages automatically.</p><button type="submit" class="primary">Save monitor settings</button></form>`);
  const current=panelGeneration;$('#monitor-form').oninput=()=>{dirty=true;};$('#monitor-form').onsubmit=event=>{event.preventDefault();if(!studioOwns(current,'monitor-settings'))return;try{const next=validateMonitor({twitch:$('#monitor-twitch').value,kick:$('#monitor-kick').value,provider:monitorSettings.provider,mode:$('#monitor-mode').value,width:Number($('#monitor-width').value),fps:Number($('#monitor-fps').value),layout:$('#monitor-layout').value});localStorage.setItem('deckMonitorSettings',JSON.stringify(next));monitorSettings=next;dirty=false;hideMonitorChat();previewLoop.stop();$('#preview-image').removeAttribute('src');$('#preview-image').hidden=true;$('#preview-empty').hidden=false;lastFrameAt=0;panel.close();setMonitorOpen(true);}catch(error){studioError(error.message);}};
}
document.addEventListener('DOMContentLoaded',()=>{
  previewLoop=new PreviewLoop(body=>api('/api/monitor/frame',body),renderPreview,monitorVideoError);
  $('#monitor-toggle').onclick=()=>setMonitorOpen(!monitorOpen);$('#monitor-close').onclick=()=>setMonitorOpen(false);$('#monitor-settings').onclick=showMonitorSettings;
  $('#preview-toggle').onclick=()=>{videoWanted=!videoWanted;if(!videoWanted){previewLoop.stop();monitorMessage('#preview-status','Preview paused · last image is retained');}refreshMonitorActivity();};
  $('#preview-image').onerror=()=>{videoWanted=false;previewLoop.stop();$('#preview-image').hidden=true;$('#preview-empty').hidden=false;monitorMessage('#preview-status','Preview image could not be decoded. Use Start preview to retry.');applyMonitorLayout();};
  $('#chat-load').onclick=()=>{chatWanted=!chatWanted;hideMonitorChat();if(!chatWanted)monitorMessage('#chat-status','Chat is off');refreshMonitorActivity();};
  for(const provider of ['twitch','kick'])$('#chat-'+provider).onclick=()=>{monitorSettings.provider=provider;try{localStorage.setItem('deckMonitorSettings',JSON.stringify(monitorSettings));}catch{}hideMonitorChat();refreshMonitorActivity();};
  $('#chat-popout').onclick=()=>{try{const urls=chatUrls(monitorSettings.provider,monitorSettings[monitorSettings.provider],location.hostname);window.open(urls.popout,'_blank','noopener,noreferrer');}catch(error){monitorMessage('#chat-status',error.message);}};
  panel.addEventListener('close',refreshMonitorActivity);$('#confirm-dialog').addEventListener('close',refreshMonitorActivity);document.addEventListener('visibilitychange',refreshMonitorActivity);window.addEventListener('resize',updateMonitorBounds);
  $('#live-monitor').addEventListener('scroll',updateMonitorBounds);
  if(typeof ResizeObserver!=='undefined')new ResizeObserver(updateMonitorBounds).observe($('#chat-viewport'));
  setInterval(()=>{if(lastFrameAt&&monitorOpen){const age=Math.max(0,performance.now()-lastFrameAt);monitorMessage('#preview-age',`Last received frame: ${(age/1000).toFixed(1)} s ago${age>3000?' · stale':''}`);}},1000);
  applyMonitorLayout();
});
