using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Forms=System.Windows.Forms;

namespace TabletDeck;

public sealed class DesktopTray : IDisposable
{
    private readonly Thread thread;
    private Forms.Control? dispatcher;
    [DllImport("kernel32.dll")]private static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")]private static extern bool ShowWindow(IntPtr window,int command);
    public DesktopTray(string editor,DesktopPreferences preferences,ReleaseUpdates updates,Action stop)
    {
        thread=new Thread(()=>{
            Forms.Application.EnableVisualStyles();
            using var control=new Forms.Control();_ = control.Handle;dispatcher=control;
            using var menu=new Forms.ContextMenuStrip();
            menu.Items.Add("Open control panel",null,(_,_)=>Process.Start(new ProcessStartInfo(editor){UseShellExecute=true}));
            var startup=new Forms.ToolStripMenuItem("Start with Windows"){Checked=DesktopPreferences.StartsWithWindows(),CheckOnClick=false};
            startup.Click+=(_,_)=>{try{DesktopPreferences.SetStartup(!startup.Checked);startup.Checked=DesktopPreferences.StartsWithWindows();}catch(Exception ex){Forms.MessageBox.Show(ex.Message,"Tablet Deck");}};menu.Items.Add(startup);
            var usb=new Forms.ToolStripMenuItem("Reconnect USB automatically"){Checked=preferences.Get().UsbReconnect,CheckOnClick=false};
            usb.Click+=(_,_)=>{preferences.Save(preferences.Get() with{UsbReconnect=!usb.Checked});usb.Checked=preferences.Get().UsbReconnect;};menu.Items.Add(usb);
            using var icon=new Forms.NotifyIcon{Text="Tablet Deck Studio",Icon=SystemIcons.Application,ContextMenuStrip=menu,Visible=true};
            icon.DoubleClick+=(_,_)=>Process.Start(new ProcessStartInfo(editor){UseShellExecute=true});
            void Notify(ReleaseStatus result){if(control.IsDisposed)return;control.BeginInvoke(new Action(()=>{icon.BalloonTipTitle="Tablet Deck update";icon.BalloonTipText=result.Error??(result.Available?"Version "+result.LatestVersion+" is available. Open Desktop settings to view the release.":"You have the latest stable version.");icon.ShowBalloonTip(8000);}));}
            updates.Found+=Notify;
            menu.Items.Add("Check for updates",null,async (_,_)=>Notify(await updates.Check(true)));
            menu.Items.Add("Exit",null,(_,_)=>stop());
            ShowWindow(GetConsoleWindow(),0);
            try{Forms.Application.Run();}finally{updates.Found-=Notify;icon.Visible=false;dispatcher=null;}
        }){IsBackground=true,Name="Tablet Deck tray"};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();
    }
    public void Dispose(){try{dispatcher?.BeginInvoke(new Action(Forms.Application.ExitThread));}catch(InvalidOperationException){}thread.Join(2000);}
}
