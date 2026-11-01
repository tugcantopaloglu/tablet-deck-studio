param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskAssets=Join-Path $taskRoot 'assets'
$taskSvg=[xml](Get-Content -LiteralPath (Join-Path $taskAssets 'app-icon.svg') -Raw)
function Render-Icon([int]$Size){
 $taskBitmap=[Drawing.Bitmap]::new($Size,$Size)
 $taskGraphics=[Drawing.Graphics]::FromImage($taskBitmap)
 try{
  $taskGraphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $taskGraphics.Clear([Drawing.Color]::Transparent)
  $taskGraphics.ScaleTransform($Size/256.0,$Size/256.0)
  foreach($taskRect in $taskSvg.svg.rect){
   $taskX=if($taskRect.x){[single]$taskRect.x}else{0};$taskY=if($taskRect.y){[single]$taskRect.y}else{0}
   $taskWidth=[single]$taskRect.width;$taskHeight=[single]$taskRect.height;$taskDiameter=2*[single]$taskRect.rx
   $taskPath=[Drawing.Drawing2D.GraphicsPath]::new();$taskBrush=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($taskRect.fill))
   try{$taskPath.AddArc($taskX,$taskY,$taskDiameter,$taskDiameter,180,90);$taskPath.AddArc($taskX+$taskWidth-$taskDiameter,$taskY,$taskDiameter,$taskDiameter,270,90);$taskPath.AddArc($taskX+$taskWidth-$taskDiameter,$taskY+$taskHeight-$taskDiameter,$taskDiameter,$taskDiameter,0,90);$taskPath.AddArc($taskX,$taskY+$taskHeight-$taskDiameter,$taskDiameter,$taskDiameter,90,90);$taskPath.CloseFigure();$taskGraphics.FillPath($taskBrush,$taskPath)}finally{$taskPath.Dispose();$taskBrush.Dispose()}
  }
 }finally{$taskGraphics.Dispose()}
 return ,$taskBitmap
}
$taskSizes=@(16,24,32,48,64,128,256)
$taskFrames=@(foreach($taskSize in $taskSizes){$taskBitmap=Render-Icon $taskSize;$taskStream=[IO.MemoryStream]::new();try{$taskBitmap.Save($taskStream,[Drawing.Imaging.ImageFormat]::Png);,@{Size=$taskSize;Data=$taskStream.ToArray()}}finally{$taskBitmap.Dispose();$taskStream.Dispose()}})
$taskOutput=[IO.File]::Create((Join-Path $taskAssets 'app-icon.ico'));$taskWriter=[IO.BinaryWriter]::new($taskOutput)
try{
 $taskWriter.Write([uint16]0);$taskWriter.Write([uint16]1);$taskWriter.Write([uint16]$taskFrames.Count);$taskOffset=6+16*$taskFrames.Count
 foreach($taskFrame in $taskFrames){$taskDimension=if($taskFrame.Size -eq 256){0}else{$taskFrame.Size};$taskWriter.Write([byte]$taskDimension);$taskWriter.Write([byte]$taskDimension);$taskWriter.Write([byte]0);$taskWriter.Write([byte]0);$taskWriter.Write([uint16]1);$taskWriter.Write([uint16]32);$taskWriter.Write([uint32]$taskFrame.Data.Length);$taskWriter.Write([uint32]$taskOffset);$taskOffset+=$taskFrame.Data.Length}
 foreach($taskFrame in $taskFrames){$taskWriter.Write([byte[]]$taskFrame.Data)}
}finally{$taskWriter.Dispose();$taskOutput.Dispose()}
$taskPreview=Render-Icon 512
try{$taskPreview.Save((Join-Path $taskAssets 'app-icon.png'),[Drawing.Imaging.ImageFormat]::Png)}finally{$taskPreview.Dispose()}
Copy-Item -LiteralPath (Join-Path $taskAssets 'app-icon.svg') -Destination (Join-Path $taskRoot 'companion\wwwroot\app-icon.svg') -Force
Write-Output 'Built matching SVG, PNG and seven-size Windows ICO from assets/app-icon.svg.'
