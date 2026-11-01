'use strict';
// App-targeted Windows media sessions. This surface never sends global media keys.
(function(){
  const make=(tag,className,text)=>{const node=document.createElement(tag);if(className)node.className=className;if(text!==undefined)node.textContent=text;return node;};
  const clock=value=>{const seconds=Math.max(0,Math.floor((Number(value)||0)/1000));return Math.floor(seconds/60)+':'+String(seconds%60).padStart(2,'0');};
  function symbol(name){
    const paths={play:'M8 4l12 8-12 8z',pause:'M8 4v16 M16 4v16',previous:'M5 4v16 M19 4L7 12l12 8z',next:'M19 4v16 M5 4l12 8-12 8z',speaker:'M3 9h4l5-5v16l-5-5H3z M16 8a6 6 0 010 8',music:'M9 18V5l11-2v13 M9 8l11-2 M9 18a3 3 0 11-3-3c2 0 3 1 3 3 M20 16a3 3 0 11-3-3c2 0 3 1 3 3'};
    const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');svg.setAttribute('viewBox','0 0 24 24');svg.setAttribute('aria-hidden','true');const path=document.createElementNS('http://www.w3.org/2000/svg','path');path.setAttribute('d',paths[name]||paths.music);svg.append(path);return svg;
  }
  function button(name,label){const node=make('button','media-transport');node.type='button';node.setAttribute('aria-label',label);node.append(symbol(name));return node;}
  class MediaView {
    constructor(presetId,options){
      this.presetId=presetId;this.options=options;this.selectedId='';this.sessions=[];this.apps=[];this.mixerId='';this.mixerExplicit=false;this.snapshotError='';this.reading=false;this.writing=false;this.destroyed=false;this.epoch=0;this.drag=null;this.volumeDrag=null;this.volumePending=null;this.volumeWriting=false;this.volumeTimer=null;
      this.element=make('section','media-touchbar');this.element.setAttribute('aria-label',presetId==='spotify'?'Spotify player':'Browser media player');this.build();this.render();this.refresh();this.timer=setInterval(()=>this.refresh(),1000);
    }
    active(){return !this.destroyed&&this.options.isActive();}
    enabled(){return this.active()&&this.options.isEnabled();}
    selected(){return this.sessions.find(session=>session.id===this.selectedId);}
    eligible(session){return this.presetId==='spotify'?session.kind==='spotify':session.kind==='browser';}
    build(){
      const target=make('label','media-target','Playback target');this.target=make('select');this.target.id='media-player-target';this.target.setAttribute('aria-label','Playback target');target.append(this.target);this.target.onchange=()=>{this.cancelDrag();this.selectedId=this.target.value;this.mixerId='';this.mixerExplicit=false;this.render();};this.element.append(target);
      this.player=make('div','media-player');this.art=make('div','media-art');this.art.append(symbol('music'));this.image=make('img');this.image.alt='';this.image.hidden=true;this.art.append(this.image);this.player.append(this.art);
      this.image.onerror=()=>{this.image.hidden=true;this.art.classList.remove('has-artwork');};const metadata=make('div','media-metadata');this.title=make('h4','media-title');this.artist=make('p','media-artist');this.source=make('p','media-source');metadata.append(this.title,this.artist,this.source);this.player.append(metadata);
      this.transport=make('div','media-transport-row');this.previous=button('previous','Previous track');this.toggle=button('play','Play');this.toggle.classList.add('media-play');this.next=button('next','Next track');this.previous.onclick=()=>this.action('previous');this.toggle.onclick=()=>this.action('toggle');this.next.onclick=()=>this.action('next');this.transport.append(this.previous,this.toggle,this.next);this.player.append(this.transport);
      const timeline=make('div','media-timeline');this.position=make('output','media-time');this.seek=make('input','media-range');this.seek.type='range';this.seek.min='0';this.seek.max='1';this.seek.step='1000';this.seek.value='0';this.seek.setAttribute('aria-label','Playback position');this.duration=make('span','media-time');timeline.append(this.position,this.seek,this.duration);this.player.append(timeline);
      const begin=()=>{const selected=this.selected();if(selected&&!this.seek.disabled&&!this.drag)this.drag={sessionId:selected.id,contextToken:selected.contextToken,presetId:this.presetId,epoch:this.epoch};};
      this.seek.onpointerdown=begin;this.seek.onkeydown=event=>{if(['ArrowLeft','ArrowRight','ArrowUp','ArrowDown','Home','End','PageUp','PageDown'].includes(event.key))begin();};
      this.seek.oninput=()=>{begin();this.position.textContent=clock(this.seek.value);this.seek.setAttribute('aria-valuetext',clock(this.seek.value)+' of '+clock(this.seek.max));};
      this.seek.onchange=()=>{const captured=this.drag;this.drag=null;if(captured&&this.active()&&captured.epoch===this.epoch)this.action('seek',Number(this.seek.value),captured);};
      this.seek.onpointerup=()=>{const captured=this.drag;setTimeout(()=>{if(this.drag===captured){this.drag=null;this.render();}},0);};
      this.seek.onpointercancel=()=>{this.drag=null;this.render();};this.seek.onblur=()=>{this.drag=null;};
      this.volume=make('section','media-volume');this.volume.setAttribute('aria-label','App volume');this.volumeLabel=make('label','media-volume-label');this.volumeLabel.append(symbol('speaker'));this.volumeName=make('span');this.volumeLabel.append(this.volumeName);this.mixer=make('select');this.mixer.setAttribute('aria-label','Audio output session');this.mixer.onchange=()=>{this.cancelVolume();this.mixerId=this.mixer.value;this.mixerExplicit=!!this.mixerId;this.renderVolume();};this.volumeInput=make('input','media-range');this.volumeInput.type='range';this.volumeInput.min='0';this.volumeInput.max='100';this.volumeInput.step='1';this.volumeInput.setAttribute('aria-label','App volume');this.volumeOutput=make('output','media-time');this.volume.append(this.volumeLabel,this.mixer,this.volumeInput,this.volumeOutput);this.element.append(this.player,this.volume);
      const beginVolume=()=>{if(!this.volumeDrag&&!this.volumeInput.disabled&&this.mixerId)this.volumeDrag={id:this.mixerId,sessionId:this.selectedId,epoch:this.epoch};};this.volumeInput.onpointerdown=beginVolume;this.volumeInput.onkeydown=beginVolume;
      this.volumeInput.oninput=()=>{beginVolume();this.volumeOutput.textContent=this.volumeInput.value+'%';if(this.volumeDrag){this.volumePending={...this.volumeDrag,percent:Number(this.volumeInput.value)};clearTimeout(this.volumeTimer);this.volumeTimer=setTimeout(()=>this.flushVolume(),80);}};
      this.volumeInput.onchange=()=>{if(this.volumeDrag){this.volumePending={...this.volumeDrag,percent:Number(this.volumeInput.value)};this.volumeDrag=null;clearTimeout(this.volumeTimer);this.flushVolume();}};this.volumeInput.onpointercancel=()=>{this.cancelVolume();this.renderVolume();};this.volumeInput.onblur=()=>{this.volumeDrag=null;};
      this.volumeInput.onpointerup=()=>{const captured=this.volumeDrag;setTimeout(()=>{if(this.volumeDrag===captured&&!this.volumePending){this.volumeDrag=null;this.renderVolume();}},0);};
      this.status=make('p','media-status');this.status.setAttribute('role','status');this.element.append(this.status);
    }
    async refresh(){
      if(!this.active()||this.reading)return;this.reading=true;const epoch=this.epoch;
      try{const results=await Promise.allSettled([this.options.request('/api/computer/media'),this.options.request('/api/apps')]);if(!this.active()||epoch!==this.epoch)return;
        if(results[0].status==='rejected')throw results[0].reason;const data=results[0].value;this.sessions=Array.isArray(data.sessions)?data.sessions.filter(session=>this.eligible(session)):[];this.snapshotError=data.error||'';this.apps=results[1].status==='fulfilled'&&Array.isArray(results[1].value.apps)?results[1].value.apps:[];
        if(this.selectedId&&!this.selected()){this.cancelDrag();this.selectedId='';this.mixerId='';}
        if(!this.selectedId&&this.presetId==='spotify'&&this.sessions.length===1)this.selectedId=this.sessions[0].id;
        this.render();
      }catch(error){if(this.active()&&epoch===this.epoch){this.snapshotError=error.message;this.sessions=[];this.apps=[];this.cancelDrag();this.selectedId='';this.render();}}
      finally{this.reading=false;}
    }
    render(){
      const selected=this.selected(),choiceSignature=JSON.stringify(this.sessions.map(session=>[session.id,session.title,session.source]));if(choiceSignature!==this.choiceSignature){this.choiceSignature=choiceSignature;this.target.replaceChildren();const empty=make('option','',this.sessions.length?'Choose a playback target':'No compatible player detected');empty.value='';this.target.append(empty);for(const session of this.sessions){const option=make('option','',(session.title||'Untitled media')+' — '+session.source);option.value=session.id;this.target.append(option);}}this.target.value=this.selectedId;this.target.disabled=!this.active()||this.writing;
      this.title.textContent=selected?.title||(this.presetId==='spotify'?'Play something in Spotify':'Choose your browser player');this.artist.textContent=selected?(selected.artist||selected.album||'No artist information'):(this.presetId==='spotify'?'Playback details will appear here.':'Windows identifies the browser, not the website. Choose the title you want to control.');this.source.textContent=selected?`${selected.playing?'Playing':'Paused'} · ${selected.source}${this.presetId==='youtube'?' · Website not identified':''}`:'';
      const artwork=selected?.artwork||'',valid=artwork.length<=500000&&/^data:image\/(png|jpeg|webp);base64,[a-z0-9+/=\r\n]+$/i.test(artwork);if(valid){if(this.image.getAttribute('src')!==artwork)this.image.src=artwork;this.image.hidden=false;}else{this.image.hidden=true;this.image.removeAttribute('src');}this.art.classList.toggle('has-artwork',valid);
      const enabled=this.enabled()&&!this.writing;this.previous.hidden=!selected?.canPrevious;this.next.hidden=!selected?.canNext;this.toggle.hidden=!!selected&&!selected.canPlay&&!selected.canPause;this.previous.disabled=!enabled;this.next.disabled=!enabled;this.toggle.disabled=!enabled||!selected||(selected.playing?!selected.canPause:!selected.canPlay);this.toggle.setAttribute('aria-label',selected?.playing?'Pause':'Play');this.toggle.replaceChildren(symbol(selected?.playing?'pause':'play'));
      const duration=Math.max(0,Number(selected?.durationMs)||0),position=Math.max(0,Math.min(duration,Number(selected?.positionMs)||0));this.seek.disabled=!enabled||!selected?.canSeek||duration<=0;
      if(!this.drag){this.seek.max=String(Math.max(1,duration));this.seek.value=String(position);this.position.textContent=clock(position);this.seek.setAttribute('aria-valuetext',clock(position)+' of '+clock(duration));}this.duration.textContent=clock(duration);
      this.status.textContent=this.snapshotError?'Media unavailable. '+this.snapshotError:!this.options.isEnabled()?'Enable Computer Copilot on the PC to use these controls.':!selected?(this.sessions.length?'Select the player you want to control.':'No compatible media session. Start playback in '+(this.presetId==='spotify'?'the Spotify desktop app.':'your browser, then refresh.')):!selected.canSeek?'This player does not support seeking.':'';this.renderVolume();
    }
    volumeCandidates(){
      const selected=this.selected();if(!selected)return[];let process='';if(this.presetId==='spotify'&&selected.kind==='spotify')process='spotify';else if(selected.kind==='browser'){const source=String(selected.source||'').toLowerCase();if(['chrome','chrome.exe','msedge.exe','firefox.exe','brave.exe','opera.exe','vivaldi.exe'].includes(source))process=source.replace(/\.exe$/,'');}return process?this.apps.filter(app=>String(app.name||'').toLowerCase()===process):[];
    }
    renderVolume(){
      const candidates=this.volumeCandidates();this.volume.hidden=!candidates.length;if(!candidates.length){this.mixerId='';this.cancelVolume();return;}
      if(this.mixerId&&!candidates.some(app=>app.id===this.mixerId)){this.mixerId='';this.mixerExplicit=false;this.cancelVolume();}if(candidates.length>1&&!this.mixerExplicit){this.mixerId='';this.cancelVolume();}if(!this.mixerId&&candidates.length===1)this.mixerId=candidates[0].id;
      const signature=JSON.stringify(candidates.map(app=>[app.id,app.name,app.device]));if(signature!==this.mixerSignature){this.mixerSignature=signature;this.mixer.replaceChildren();const empty=make('option','','Choose an audio output');empty.value='';this.mixer.append(empty);for(const app of candidates){const option=make('option','',app.name+' · '+app.device);option.value=app.id;this.mixer.append(option);}}
      this.mixer.value=this.mixerId;this.mixer.hidden=candidates.length<2;this.mixer.disabled=!this.active()||this.volumeWriting;this.volumeName.textContent=this.presetId==='spotify'?'Spotify volume':'Browser volume (all tabs)';this.volumeInput.setAttribute('aria-label',this.volumeName.textContent);const app=candidates.find(item=>item.id===this.mixerId);this.volumeInput.disabled=!this.enabled()||!app;
      if(!this.volumeDrag&&!this.volumeWriting&&!this.volumePending){this.volumeInput.value=String(Math.max(0,Math.min(100,Number(app?.percent)||0)));this.volumeOutput.textContent=app?Math.round(app.percent)+'%':'Choose output';}
    }
    async action(operation,positionMs,captured){
      const selected=this.selected();if(!this.enabled()||this.writing||!selected||!this.options.canWrite())return;
      const target=captured||{presetId:this.presetId,sessionId:selected.id,contextToken:selected.contextToken};if(target.sessionId!==this.selectedId||target.presetId!==this.presetId)return;
      const allowed=operation==='toggle'?(selected.playing?selected.canPause:selected.canPlay):operation==='previous'?selected.canPrevious:operation==='next'?selected.canNext:selected.canSeek;if(!allowed)return;
      const epoch=this.epoch;this.writing=true;this.render();
      try{await this.options.request('/api/computer/media/action',{presetId:target.presetId,sessionId:target.sessionId,contextToken:target.contextToken,operation,...(operation==='seek'?{positionMs:Math.round(positionMs)}:{})});if(this.active()&&epoch===this.epoch)await this.refresh();}
      catch(error){if(this.active()&&epoch===this.epoch)this.options.notice(error.message,true);}
      finally{this.writing=false;if(!this.destroyed)this.render();}
    }
    async flushVolume(){
      if(this.volumeWriting||!this.volumePending)return;const target=this.volumePending;this.volumePending=null;
      if(!this.enabled()||target.epoch!==this.epoch||target.id!==this.mixerId||target.sessionId!==this.selectedId||!this.options.canWrite())return;
      this.volumeWriting=true;try{await this.options.request('/api/apps',{id:target.id,percent:target.percent});const app=this.apps.find(item=>item.id===target.id);if(app)app.percent=target.percent;}
      catch(error){if(this.active()&&target.epoch===this.epoch)this.options.notice(error.message,true);this.volumePending=null;}
      finally{this.volumeWriting=false;if(this.volumePending)this.flushVolume();else if(!this.destroyed)this.renderVolume();}
    }
    cancelVolume(){clearTimeout(this.volumeTimer);this.volumeDrag=null;this.volumePending=null;}
    cancelDrag(){this.drag=null;this.cancelVolume();}
    suspend(){this.epoch++;this.cancelDrag();this.render();}
    destroy(){this.destroyed=true;this.epoch++;clearInterval(this.timer);this.cancelDrag();this.element.remove();}
  }
  let current=null;
  window.MediaTouchBar={attach(parent,presetId,options){if(!current||current.presetId!==presetId){current?.destroy();current=new MediaView(presetId,options);}else current.options=options;parent.append(current.element);current.render();return current;},unmount(){current?.destroy();current=null;},suspend(){current?.suspend();},refresh(){return current?.refresh();},isBusy(){return !!(current?.writing||current?.volumeWriting);},isInteracting(presetId){return !!current&&current.presetId===presetId&&!!(current.drag||current.volumeDrag);}};
})();
