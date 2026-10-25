const fs=require('node:fs');
const path=require('node:path');
const acorn=require('acorn');
const file=path.resolve(__dirname,'../companion/wwwroot/app.js');
let source=fs.readFileSync(file,'utf8');
const ast=acorn.parse(source,{ecmaVersion:'latest'});
const changes=[];
function visit(node){
 if(!node||typeof node!=='object')return;
 if(node.type==='AssignmentExpression'&&node.left.type==='MemberExpression'&&node.left.property.name==='innerHTML'&&!source.slice(node.right.start,node.right.end).startsWith('DOMPurify.sanitize('))changes.push({start:node.right.start,end:node.right.end});
 for(const value of Object.values(node)){if(Array.isArray(value))value.forEach(visit);else if(value&&typeof value==='object')visit(value);}
}
visit(ast);
for(const edit of changes.sort((a,b)=>b.start-a.start))source=source.slice(0,edit.start)+'DOMPurify.sanitize('+source.slice(edit.start,edit.end)+')'+source.slice(edit.end);
fs.writeFileSync(file,source);
console.log('Sanitized '+changes.length+' HTML assignments.');
