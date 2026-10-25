'use strict';
let deckPushAt=0;
let deckStateRefresh=null;
function acceptDeckState(next){if(next.session&&state.session===next.session&&Number.isFinite(next.revision)&&next.revision<state.revision)return false;state={...next,pcConnected:true};return true;}
window.deckPush=next=>{if(!next||typeof next.obsConnected!=='boolean')return;deckPushAt=Date.now();if(acceptDeckState(next))updateState();};
window.deckStreamClosed=()=>{deckPushAt=0;};
function hasLiveState(){return Date.now()-deckPushAt<8000;}
async function afterControl(){
  if(hasLiveState())return;
  if(deckStateRefresh)return deckStateRefresh;
  deckStateRefresh=(async()=>{try{if(acceptDeckState(await api('/api/state')))updateState();}catch(error){state={pcConnected:false,obsConnected:false,inputs:{},error:error.message};updateState();}finally{deckStateRefresh=null;}})();
  return deckStateRefresh;
}
async function readDesktopUpdates(){
  if(native||preview)return;
  for(;;){
    try {
      const response=await fetch('/api/events',{headers:{'X-Deck-Admin':adminToken}});if(!response.ok||!response.body)throw new Error('Live state unavailable');
      const reader=response.body.getReader(),decoder=new TextDecoder();let buffered='';
      for(;;){const item=await reader.read();if(item.done)break;buffered+=decoder.decode(item.value,{stream:true});if(buffered.length>1024*1024)throw new Error('State update exceeds limit');let end;
        while((end=buffered.indexOf('\n\n'))>=0){const packet=buffered.slice(0,end);buffered=buffered.slice(end+2);const text=packet.split('\n').filter(line=>line.startsWith('data: ')).map(line=>line.slice(6)).join('\n');if(text){const message=JSON.parse(text);if(message.type==='state')window.deckPush(message.state);}}
      }
    }catch{/* Existing authenticated HTTPS polling remains the fallback. */}
    window.deckStreamClosed();await new Promise(resolve=>setTimeout(resolve,1500));
  }
}
// Absolute sliders send immediately, then keep only the newest value while an ACK is pending.
class LatestControl {
  constructor(send){this.send=send;this.pending=null;this.running=false;this.waiters=[];this.version=0;}
  set(value){const version=++this.version;this.pending={value,version};const result=new Promise((resolve,reject)=>this.waiters.push({version,resolve,reject}));this.pump();return result;}
  async pump(){
    if(this.running||!this.pending)return;this.running=true;const next=this.pending;this.pending=null;
    try{await this.send(next.value);const done=this.waiters.filter(waiter=>waiter.version<=next.version);this.waiters=this.waiters.filter(waiter=>waiter.version>next.version);done.forEach(waiter=>waiter.resolve());}
    catch(error){this.pending=null;this.waiters.splice(0).forEach(waiter=>waiter.reject(error));notice(error.message,true);}
    finally{this.running=false;if(this.pending)this.pump();}
  }
}
const deckVolumeQueues=new Map();
function queueDeckVolume(channel,db){if(!deckVolumeQueues.has(channel))deckVolumeQueues.set(channel,new LatestControl(value=>api('/api/audio',{channel,db:value})));return deckVolumeQueues.get(channel).set(db);}
const appVolumeQueues=new Map();
function queueAppVolume(id,percent){if(!appVolumeQueues.has(id))appVolumeQueues.set(id,new LatestControl(value=>api('/api/apps',{id,percent:value})));return appVolumeQueues.get(id).set(percent);}
readDesktopUpdates();
