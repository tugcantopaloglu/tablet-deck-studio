using System.Text.Json;

namespace TabletDeck;

public static class BoardStorageTests
{
    public static void Run()
    {
        var passed=0;void Check(bool condition,string message){if(!condition)throw new Exception("FAIL "+message);passed++;Console.WriteLine("PASS "+message);}
        void Reject(Action action,string message){try{action();throw new Exception("Accepted invalid input: "+message);}catch(ArgumentException){Check(true,message);}}
        var root=Path.Combine(Path.GetTempPath(),"tabletdeck-boards-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{
            var legacy=DeckConfig.Defaults();legacy.ObsPassword="fixture-original-password";legacy.Pages[0].Buttons[0].Value="both";
            File.WriteAllText(Path.Combine(root,"config.json"),JsonSerializer.Serialize(legacy,DataStore.Json));
            var store=new DataStore(root);var initial=store.Config.ProfileId;var button=store.Config.Pages[0].Buttons[0];
            Check(initial.Length==32&&store.Config.Pages.Count==5&&button.Id==legacy.Pages[0].Buttons[0].Id,"legacy migration preserves pages, button identities, and output values");
            Check(store.Config.ObsPassword==legacy.ObsPassword&&store.Backups().Length==1,"migration retains live password and creates a backup");
            Check(!Directory.EnumerateFiles(Path.Combine(root,"backups")).Any(p=>File.ReadAllText(p).Contains(legacy.ObsPassword)),"backup files exclude OBS passwords");
            var copy=store.CreateProfile("Gaming",true);var duplicate=store.Config.Profiles.Single(p=>p.Id==copy);
            Check(duplicate.Pages.Count==5&&duplicate.Pages[0].Buttons[0].Target==button.Target&&duplicate.Pages[0].Buttons[0].Value=="both","duplicate retains configured actions and routing");
            Check(duplicate.Pages[0].Id!=store.Config.Pages[0].Id&&duplicate.Pages[0].Buttons[0].Id!=button.Id,"duplicate regenerates page and button identities");
            var blank=store.CreateProfile("Discord",false);Check(store.Config.Profiles.Single(p=>p.Id==blank).Pages.Single().Buttons.Count==0,"empty profile starts with one empty page");
            var backupCount=store.Backups().Length;var stale=DataStore.Copy(store.Config);store.SwitchProfile(copy);
            Check(store.Config.ProfileId==copy&&store.Config.Pages[0].Buttons.All(a=>a.Id!=button.Id),"switch selects independent board and invalidates old action identities");
            Check(store.Backups().Length==backupCount&&store.Config.ObsPassword==legacy.ObsPassword,"switch preserves credentials without consuming backup history");
            try{store.SaveActive(stale,false);throw new Exception("Stale edit accepted");}catch(InvalidOperationException){Check(true,"stale editor cannot overwrite a different profile");}
            var edit=DataStore.Copy(store.Config);edit.Profiles=[];edit.Favourites=[];edit.ObsPassword="";edit.Pages[0].Name="Game keys";store.SaveActive(edit,false);
            Check(store.Config.Profiles.Count==2&&store.Config.ObsPassword==legacy.ObsPassword,"active save preserves inactive profiles and current password");
            store.RenameProfile(copy,"Live gaming");Check(store.Config.ProfileName=="Live gaming","rename updates active profile");
            Reject(()=>store.RenameProfile(copy," "),"empty profile names are rejected");
            Reject(()=>store.DeleteProfile(copy),"active profile cannot be deleted");
            Reject(()=>store.SwitchProfile("../config.json"),"unknown profile cannot switch or address a file");
            var key=new string('A',64);var library=new SoundpadLibraryResult([new(3,"Bruh","","",[],key)],null);
            store.Favourite(key,true,library);Check(store.Favourites().Single().Key==key,"favourite stores stable library identity");
            var moved=new SoundpadLibraryResult([new(57,"Bruh renamed","","",[],key)],null);store.Favourite(key,true,moved);
            Check(store.Favourites().Length==1&&store.Favourites()[0].Name=="Bruh renamed","favourite follows reorder and rename without duplication");
            Reject(()=>store.Favourite(new string('B',64),true,moved),"missing sound cannot become a favourite");
            Reject(()=>store.Favourite(key,true,new([moved.Sounds[0],moved.Sounds[0] with{Index=58}],null)),"ambiguous sound identity cannot become a favourite");
            Reject(()=>store.Favourite("../config.json",false),"invalid favourite identity is rejected");
            var snapshot=store.BackupNow();var secretChange=DataStore.Copy(store.Config);secretChange.ObsPassword="fixture-current-password";store.SaveActive(secretChange,true);
            store.Favourite(key,false);store.DeleteProfile(blank);store.RestoreBackup(snapshot.Id);
            Check(store.Config.ObsPassword=="fixture-current-password"&&store.Favourites().Length==1&&store.ProfileList().Length==3,"restore recovers all boards and favourites while preserving current password");
            Reject(()=>store.PreviewBackup("../../config"),"backup preview rejects path traversal");
            Reject(()=>store.RestoreBackup(new string('0',32)),"missing backup cannot replace configuration");
            for(var i=0;i<25;i++)store.BackupNow();Check(store.Backups().Length==20&&Directory.GetFiles(Path.Combine(root,"backups"),"*.json").Length==20,"history retains only the newest twenty valid snapshots");
            Reject(()=>store.PreviewBackup(snapshot.Id),"pruned snapshot cannot be restored");
            var file=Path.Combine(root,"backups",Guid.NewGuid().ToString("N")+".json");File.WriteAllText(file,"{broken");Check(store.Backups().Length==20,"damaged backup is skipped without hiding valid history");
            var token=new PairedDevice("fixture","Fixture tablet",new string('A',64),DateTimeOffset.UtcNow);store.AddDevice(token);
            var activeName=store.Config.ProfileName;File.WriteAllText(Path.Combine(root,"config.json"),"{broken");var recovered=new DataStore(root);
            Check(recovered.Config.ProfileName==activeName&&recovered.Favourites().Length==1&&recovered.RecoveryWarning.Contains("recovered"),"damaged configuration recovers the newest valid board snapshot");
            Check(recovered.Config.ObsPassword==""&&recovered.Devices.Single().Id==token.Id,"recovery requires OBS password again and preserves pairing records");
            Check(Directory.GetFiles(root,"config.corrupt-*.json").Length==1,"recovery preserves the original damaged bytes privately");
            var reloaded=new DataStore(root);Check(reloaded.Config.ProfileId==recovered.Config.ProfileId&&reloaded.RecoveryWarning.Length==0,"recovered board persists across restart");
            while(reloaded.ProfileList().Length<10)reloaded.CreateProfile("Extra "+reloaded.ProfileList().Length,false);
            Reject(()=>reloaded.CreateProfile("Too many",false),"profile count is bounded at ten");
            var invalid=DataStore.Copy(reloaded.Config);invalid.Profiles[0].Pages[0].Id=invalid.Pages[0].Id;Reject(()=>DataStore.Validate(invalid),"cross-profile page identity collisions are rejected");
            invalid=DataStore.Copy(reloaded.Config);invalid.Favourites.Add(new(null!,"Bad"));Reject(()=>DataStore.Validate(invalid),"null favourite identity is rejected safely");
        }finally{
            var absolute=Path.GetFullPath(root);var parent=Path.GetFullPath(Path.GetTempPath());
            if(absolute.StartsWith(parent,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(absolute).StartsWith("tabletdeck-boards-",StringComparison.Ordinal)&&Directory.Exists(absolute))Directory.Delete(absolute,true);
        }
        Console.WriteLine(passed+" board storage tests passed; no GUI or external application was used.");
    }
}
