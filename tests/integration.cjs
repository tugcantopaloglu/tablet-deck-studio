const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const https=require('node:https');
const crypto=require('node:crypto');
const {WebSocketServer}=require('ws');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'../.test-output');
const session=JSON.parse(fs.readFileSync(path.join(root,'data/admin-session.json'),'utf8'));
const base=`http://127.0.0.1:${session.httpPort}`;
const calls=[];let scene='Gameplay',streaming=false,recording=false,studio=false;
const volumes={'Mic/Aux':-6,'Desktop Audio':-12},mutes={'Mic/Aux':false,'Desktop Audio':false};
const expectedPassword='integration-test-only';
let passes=0,activeBrowser;
function check(value,label){assert.ok(value,label);passes++;console.log('PASS '+label);}
async function request(url,body,headers={}){
 const response=await fetch(base+url,{method:body===undefined?'GET':'POST',headers:{'Content-Type':'application/json','X-Deck-Admin':session.token,...headers},body:body===undefined?undefined:JSON.stringify(body)});
 const text=await response.text();let data;try{data=JSON.parse(text);}catch{data=text;}return {status:response.status,data,headers:response.headers};
}
function remote(url,body,token='',pin=''){
 return new Promise((resolve,reject)=>{
  const req=https.request({hostname:'127.0.0.1',port:session.httpsPort,path:url,method:body===undefined?'GET':'POST',agent:false,rejectUnauthorized:false,timeout:15000,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})}},res=>{let text='';res.on('data',c=>text+=c);res.on('end',()=>{let data;try{data=JSON.parse(text);}catch{data=text;}resolve({status:res.statusCode,data});});});
  req.on('error',reject);
  req.on('socket',socket=>socket.on('secureConnect',()=>{const fp=crypto.createHash('sha256').update(socket.getPeerCertificate().raw).digest('hex').toUpperCase();if(pin&&fp!==pin){req.destroy(new Error('Certificate pin mismatch'));return;}req.end(body===undefined?undefined:JSON.stringify(body));}));
 });
}
const mock=new WebSocketServer({port:19455,host:'127.0.0.1'});
mock.on('connection',ws=>{
 const salt='test-salt',challenge='test-challenge';let identified=false;
 ws.send(JSON.stringify({op:0,d:{rpcVersion:1,authentication:{salt,challenge}}}));
 ws.on('message',buffer=>{
  const msg=JSON.parse(buffer);if(msg.op===1){const secret=crypto.createHash('sha256').update(expectedPassword+salt).digest('base64');const auth=crypto.createHash('sha256').update(secret+challenge).digest('base64');assert.equal(msg.d.authentication,auth);identified=true;ws.send(JSON.stringify({op:2,d:{negotiatedRpcVersion:1}}));return;}
  assert.ok(identified);const {requestType:type,requestId,requestData:d={}}=msg.d;calls.push({type,data:d});let result={};
  switch(type){
   case 'GetSceneList':result={currentProgramSceneName:scene,scenes:['Gameplay','Just chatting','Be right back','Starting'].map(sceneName=>({sceneName}))};break;
   case 'GetStreamStatus':result={outputActive:streaming};break;
   case 'GetRecordStatus':result={outputActive:recording};break;
   case 'GetInputVolume':result={inputVolumeDb:volumes[d.inputName]??-10};break;
   case 'GetInputMute':result={inputMuted:mutes[d.inputName]??false};break;
   case 'SetCurrentProgramScene':scene=d.sceneName;break;
   case 'SetInputVolume':volumes[d.inputName]=d.inputVolumeDb;break;
   case 'ToggleInputMute':mutes[d.inputName]=!mutes[d.inputName];break;
   case 'ToggleRecord':recording=!recording;break;
   case 'ToggleStream':streaming=!streaming;break;
   case 'GetInputList':result={inputs:Object.keys(volumes).map(inputName=>({inputName,inputKind:'test'}))};break;
   case 'GetSceneItemId':result={sceneItemId:42};break;
   case 'GetSceneItemEnabled':result={sceneItemEnabled:true};break;
   case 'GetSourceFilter':result={filterEnabled:true};break;
   case 'GetStudioModeEnabled':result={studioModeEnabled:studio};break;
   case 'SetStudioModeEnabled':studio=d.studioModeEnabled;break;
  }
  ws.send(JSON.stringify({op:7,d:{requestType:type,requestId,requestStatus:{result:true,code:100},responseData:result}}));
 });
});
async function run(){
 check((await request('/api/board',undefined,{'X-Deck-Admin':''})).status===401,'unauthenticated board access denied');
 check((await request('/api/admin/info',undefined,{'X-Deck-Admin':''})).status===403,'admin endpoint requires local admin session');
 check((await request('/api/admin/info',undefined,{Origin:'https://attacker.invalid'})).status===403,'cross-origin admin request denied');
 const info=(await request('/api/admin/info')).data;
 check((await remote('/api/board',undefined,'',info.fingerprint)).status===401,'encrypted tablet API requires pairing');
 check((await remote('/api/admin/info',undefined,'',info.fingerprint)).status===403,'tablet port cannot access PC editor');
 const config=(await request('/api/admin/config')).data;config.obsPort=19455;config.obsPassword=expectedPassword;
 const replacements=[['gamepad','violet'],['chat','violet'],['bed','violet'],['clapper','violet'],['clap','amber'],['horn','amber'],['stop','amber'],['replay','amber'],['obs','blue'],['discord','blue'],['record','red'],['broadcast','red']];
 config.pages[0].buttons.forEach((b,i)=>{b.icon=replacements[i][0];b.color=replacements[i][1];});config.pages[1].icon='image';config.pages[4].icon='list';
 check((await request('/api/admin/config',{config,replacePassword:true})).status===200,'local configuration saves');
 const invalid=structuredClone(config);invalid.pages[0].buttons[8].target='cmd.exe';
 check((await request('/api/admin/config',{config:invalid,replacePassword:true})).status===400,'server rejects relative executable paths');
 const soundpadConfig=structuredClone(config);soundpadConfig.pages[4].buttons=[{id:'soundpad-test',label:'Soundpad mic',kind:'soundpadPlay',target:'3',value:'mic',icon:'speaker',color:'amber',confirm:false,steps:[]}];
 check((await request('/api/admin/config',{config:soundpadConfig,replacePassword:true})).status===200,'Soundpad action configuration saves');
 const soundpadBoard=(await request('/api/board')).data;
 check(soundpadBoard.pages[4].buttons[0].kind==='soundpadPlay'&&soundpadBoard.pages[4].buttons[0].configured,'Soundpad control reaches the tablet board');
 soundpadConfig.pages[4].buttons[0].target='3);DoStopSound()';
 check((await request('/api/admin/config',{config:soundpadConfig,replacePassword:true})).status===400,'Soundpad command text rejected at API boundary');
 await request('/api/admin/config',{config,replacePassword:true});
 let pairing=(await request('/api/admin/pairing',{})).data;
 for(let i=0;i<5;i++)await remote('/api/pair',{code:'000000',name:'attacker'},'',info.fingerprint);
 check((await remote('/api/pair',{code:pairing.code,name:'locked-out'},'',info.fingerprint)).status===403,'pairing locks after five failed attempts');
 pairing=(await request('/api/admin/pairing',{})).data;
 const paired=await remote('/api/pair',{code:pairing.code,name:'Test tablet'},'',info.fingerprint);
 check(paired.status===200&&paired.data.token.length===64,'TLS pairing issues a device credential');
 const token=paired.data.token;
 check((await remote('/api/pair',{code:pairing.code,name:'reuse'},'',info.fingerprint)).status===403,'pairing PIN is single use');
 check((await remote('/api/admin/config',undefined,token,info.fingerprint)).status===403,'paired tablet cannot modify configured PC actions');
 const board=(await remote('/api/board',undefined,token,info.fingerprint)).data;
 check(board.pages[0].buttons.length===12&&!JSON.stringify(board).includes('obs64.exe'),'tablet receives action IDs without local app paths');
 let state=(await remote('/api/state',undefined,token,info.fingerprint)).data;
 check(state.obsConnected&&state.scene==='Gameplay'&&state.inputs.mic.db===-6,'OBS authenticated state and audio inputs');
 const main=board.pages[0].buttons;
 check((await remote('/api/action',{id:main[1].id},token,info.fingerprint)).status===200&&scene==='Just chatting','scene button sends the OBS command');
 check((await remote('/api/action',{id:main[11].id},token,info.fingerprint)).status===409&&!streaming,'stream action requires confirmation');
 check((await remote('/api/action',{id:main[11].id,confirmed:true},token,info.fingerprint)).status===200&&streaming,'confirmed stream action');
 check((await remote('/api/audio',{channel:'mic',db:-18},token,info.fingerprint)).status===200&&volumes['Mic/Aux']===-18,'mixer volume command');
 check((await remote('/api/audio',{channel:'mic',db:20},token,info.fingerprint)).status===400,'unsafe audio gain rejected');
 check((await remote('/api/mute',{channel:'desktop'},token,info.fingerprint)).status===200&&mutes['Desktop Audio'],'mixer mute command');
 check((await remote('/api/action',{id:'missing'},token,info.fingerprint)).status===400,'unknown button does not execute');
 const macro={id:'test-macro',label:'Test macro',kind:'macro',target:'',value:'',icon:'bolt',color:'blue',confirm:false,steps:[{id:'s1',label:'Scene',kind:'scene',target:'Gameplay',value:'',steps:[]},{id:'s2',label:'Delay',kind:'delay',target:'15',value:'',steps:[]},{id:'s3',label:'Replay',kind:'replay',target:'',value:'',steps:[]}]};
 config.pages[4].buttons=[macro];await request('/api/admin/config',{config,replacePassword:true});
 check((await remote('/api/action',{id:'test-macro'},token,info.fingerprint)).status===200&&calls.at(-1).type==='SaveReplayBuffer'&&scene==='Gameplay','macro runs sequential OBS actions and delay');
 for(const action of [
  {kind:'source',target:'Gameplay',value:'Camera',expected:'SetSceneItemEnabled'},
  {kind:'filter',target:'Mic/Aux',value:'Noise suppression',expected:'SetSourceFilterEnabled'},
  {kind:'media',target:'Alert',value:'restart',expected:'TriggerMediaInputAction'},
  {kind:'studioMode',target:'',value:'',expected:'SetStudioModeEnabled'},
  {kind:'preview',target:'Starting',value:'',expected:'SetCurrentPreviewScene'},
  {kind:'transition',target:'',value:'',expected:'TriggerStudioModeTransition'},
  {kind:'virtualCamera',target:'',value:'',expected:'ToggleVirtualCam'}]){
  config.pages[4].buttons=[{...macro,id:'feature',kind:action.kind,target:action.target,value:action.value,steps:[]}];await request('/api/admin/config',{config,replacePassword:true});
  const result=await remote('/api/action',{id:'feature'},token,info.fingerprint);check(result.status===200&&calls.at(-1).type===action.expected,action.kind+' integration');
 }
 config.pages[4].buttons=[macro];await request('/api/admin/config',{config,replacePassword:true});
 check((await request('/api/admin/import-scenes',{})).status===200,'import real OBS scenes');
 if(process.env.TABLETDECK_HEADLESS==='1'){
  const devices=(await request('/api/admin/info')).data.devices;const tablet=devices.filter(device=>device.name==='Test tablet').at(-1);
  await request('/api/admin/revoke',{id:tablet.id});check((await remote('/api/board',undefined,token,info.fingerprint)).status===401,'revocation immediately blocks the tablet');
  fs.writeFileSync(path.join(root,'integration-api-result.json'),JSON.stringify({passed:passes,obsRequests:calls.length,browserOpened:false,transport:'loopback TLS with certificate fingerprint verification'},null,2));
  console.log(`${passes} API integration checks passed; browser section deliberately skipped.`);return;
 }
 const browser=await chromium.launch({executablePath:process.env.TABLETDECK_CHROME||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});activeBrowser=browser;
 const errors=[];
 const page=await browser.newPage({viewport:{width:1536,height:1024},deviceScaleFactor:1});
 page.on('pageerror',error=>errors.push(error.message));
 // This baseline UI suite exercises the polling fallback. The fast suite covers
 // authenticated pushed state; disable SSE here so a poll failure is meaningful.
 await page.route('**/api/events',route=>route.abort());
 await page.goto(base+'/#admin='+session.token);await page.locator('.key').first().waitFor();
 check(await page.locator('.key').count()===12,'Studio UI renders 12 real buttons');
 await page.getByRole('button',{name:'Just chatting',exact:true}).click();await page.waitForTimeout(300);check(scene==='Just chatting','UI click reaches OBS through companion');
 await page.getByRole('button',{name:'Scenes',exact:true}).click();check(await page.locator('.key').count()===4,'page navigation changes buttons');
 await page.getByRole('button',{name:'Main',exact:true}).click();
 await page.getByRole('button',{name:'Edit',exact:true}).click();await page.getByLabel('Mic input name').fill('Mic/Aux');await page.getByRole('button',{name:'Save changes',exact:true}).click();await page.waitForTimeout(250);check(!(await page.locator('#panel').isVisible()),'PC editor saves and closes');
 await page.getByRole('button',{name:'Edit',exact:true}).click();await page.locator('#action-kind').selectOption('soundpadPlay');
 check(await page.getByLabel('Soundpad output (default, both, mic, speakers)',{exact:true}).isVisible(),'Soundpad output control is editable');
 await page.locator('#action-kind').selectOption('scene');await page.getByRole('button',{name:'Save changes',exact:true}).click();
 const screenshotDir=path.join(root,'screenshots');fs.mkdirSync(screenshotDir,{recursive:true});
 const previewPage=await browser.newPage({viewport:{width:1536,height:1024},deviceScaleFactor:1});previewPage.on('pageerror',error=>errors.push(error.message));await previewPage.goto(base+'/?preview=1');await previewPage.evaluate(()=>document.fonts.ready);
 await previewPage.screenshot({path:path.join(screenshotDir,'hero-repro.png'),fullPage:true});
 await previewPage.setViewportSize({width:1280,height:800});await previewPage.screenshot({path:path.join(screenshotDir,'tablet.png'),fullPage:true});
 check(await previewPage.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'tablet has no horizontal overflow');
 await previewPage.setViewportSize({width:1024,height:600});await previewPage.screenshot({path:path.join(screenshotDir,'tablet-small.png'),fullPage:true});
 check(await previewPage.evaluate(()=>[...document.querySelectorAll('.key')].every(e=>{const r=e.getBoundingClientRect();return r.height>=48&&r.width>=48;})),'small tablet keys meet 48px touch minimum');
 await previewPage.setViewportSize({width:390,height:844});await previewPage.screenshot({path:path.join(screenshotDir,'mobile.png'),fullPage:true});
 check(await previewPage.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'compact fallback has no horizontal overflow');
 await page.locator('#notice').evaluate(e=>e.hidden=true);
 for(const [name,width,height] of [['desktop',1536,1024],['tablet',1280,800],['small',1024,600],['mobile',390,844]]){await page.setViewportSize({width,height});await page.screenshot({path:path.join(screenshotDir,'actual-connected-'+name+'.png'),fullPage:true});}
 await page.setViewportSize({width:1280,height:800});await page.screenshot({path:path.join(screenshotDir,'connected-test.png'),fullPage:true});
 await page.getByRole('button',{name:'Record',exact:true}).click();await page.getByRole('button',{name:'Cancel',exact:true}).click();check(!recording,'UI cancellation does not change recording state');
 await page.route('**/api/state',route=>route.abort());await page.waitForFunction(()=>document.querySelector('#obs-status').textContent==='PC disconnected');
 check(await page.locator('.key.active').count()===0&&await page.locator('#stream-status').innerText()==='Stream unknown','connection loss clears active scene and stream state');
 await page.locator('#notice').evaluate(e=>e.hidden=true);
 for(const [name,width,height] of [['desktop',1536,1024],['tablet',1280,800],['small',1024,600],['mobile',390,844]]){await page.setViewportSize({width,height});await page.screenshot({path:path.join(screenshotDir,'actual-disconnected-'+name+'.png'),fullPage:true});}
 await page.setViewportSize({width:1280,height:800});await page.screenshot({path:path.join(screenshotDir,'disconnected-test.png'),fullPage:true});
 await page.unroute('**/api/state');
 check(errors.length===0,'no browser JavaScript errors');
 await browser.close();
 const devices=(await request('/api/admin/info')).data.devices;const tablet=devices.filter(d=>d.name==='Test tablet').at(-1);
 await request('/api/admin/revoke',{id:tablet.id});
 check((await remote('/api/board',undefined,token,info.fingerprint)).status===401,'revocation immediately blocks the tablet');
 fs.writeFileSync(path.join(root,'integration-result.json'),JSON.stringify({passed:passes,browserErrors:errors,obsRequests:calls.length,transport:'loopback TLS with certificate fingerprint verification',screenshots:screenshotDir},null,2));
 console.log(`${passes} integration checks passed; ${calls.length} OBS requests.`);
}
run().then(()=>{for(const ws of mock.clients)ws.terminate();mock.close();}).catch(async error=>{console.error(error);process.exitCode=1;if(activeBrowser)await activeBrowser.close();for(const ws of mock.clients)ws.terminate();mock.close();});
