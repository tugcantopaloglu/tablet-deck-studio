'use strict';
let soundpadBoardFilter={pageId:'',query:'',category:''},soundpadBoardCategories='';
function soundpadBoardButtons(page){
  let toolbar=document.getElementById('soundpad-board-toolbar');
  if(!page.liveSoundpad){if(toolbar)toolbar.hidden=true;return page.buttons;}
  if(!toolbar){
    toolbar=document.createElement('div');toolbar.id='soundpad-board-toolbar';toolbar.className='soundpad-board-toolbar';
    const search=document.createElement('input');search.id='soundpad-board-search';search.type='search';search.placeholder='Search all Soundpad sounds';search.setAttribute('aria-label','Search all Soundpad sounds');search.autocomplete='off';
    const category=document.createElement('select');category.id='soundpad-board-category';category.setAttribute('aria-label','Soundpad category');
    const clear=document.createElement('button');clear.id='soundpad-board-clear';clear.className='quiet';clear.textContent='Clear';clear.onclick=()=>{soundpadBoardFilter.query='';soundpadBoardFilter.category='';search.value='';category.value='';slice=0;renderBoard();search.focus({preventScroll:true});};
    const count=document.createElement('span');count.id='soundpad-board-count';count.className='help';count.setAttribute('role','status');
    toolbar.append(search,category,clear,count);document.getElementById('keys').before(toolbar);
    search.oninput=()=>{soundpadBoardFilter.query=search.value;slice=0;renderBoard();};category.onchange=()=>{soundpadBoardFilter.category=category.value;slice=0;renderBoard();};
  }
  toolbar.hidden=false;
  const search=document.getElementById('soundpad-board-search'),category=document.getElementById('soundpad-board-category');
  if(soundpadBoardFilter.pageId!==page.id){soundpadBoardFilter={pageId:page.id,query:'',category:''};search.value='';soundpadBoardCategories='';}
  const categories=[...new Set(page.buttons.flatMap(a=>a.categories||[]))].sort((a,b)=>a.localeCompare(b)),signature=JSON.stringify(categories);
  if(signature!==soundpadBoardCategories){soundpadBoardCategories=signature;category.replaceChildren(new Option('All categories',''),...categories.map(name=>new Option(name,name)));}
  if(!categories.includes(soundpadBoardFilter.category))soundpadBoardFilter.category='';category.value=soundpadBoardFilter.category;
  const query=soundpadBoardFilter.query.trim().toLocaleLowerCase();
  const matches=page.buttons.filter(a=>(!soundpadBoardFilter.category||(a.categories||[]).includes(soundpadBoardFilter.category))&&(!query||[a.label,a.searchText,...(a.categories||[])].join(' ').toLocaleLowerCase().includes(query)));
  document.getElementById('soundpad-board-count').textContent=page.soundpadWarning&&!page.buttons.length?'Soundpad unavailable · Open Soundpad on the PC.':`${matches.length} ${query||soundpadBoardFilter.category?'matching · ':''}${query||soundpadBoardFilter.category?page.buttons.length+' total ':''}sounds · Live sync`;
  return matches;
}
function soundpadBoardPagination(page,total){
  if(!page.liveSoundpad)return;
  const select=document.createElement('select');select.id='soundpad-board-page';select.setAttribute('aria-label','Jump to sound page');
  for(let index=0;index<total;index++)select.add(new Option(`Page ${index+1} of ${total}`,String(index)));
  select.value=String(slice);select.onchange=()=>{slice=Number(select.value);renderBoard();document.getElementById('soundpad-board-page')?.focus({preventScroll:true});};
  document.querySelector('#pagination>span')?.replaceWith(select);
}
function rememberSoundpadBoardFocus(page){
  const focused=document.activeElement;
  return page.liveSoundpad&&focused?.classList.contains('key')?focused.dataset.id?.split(':').at(-2):null;
}
function restoreSoundpadBoardFocus(key){
  if(!key)return;
  const linked=[...document.querySelectorAll('#keys>.key')].find(node=>node.dataset.id?.split(':').at(-2)===key);
  (linked||document.getElementById('soundpad-board-search'))?.focus({preventScroll:true});
}
