using System.Security.Cryptography;
namespace TabletDeck;
internal static class ReleaseDownload
{
    public static async Task Fetch(HttpClient client,InstallerRelease asset,string file,Action<long> progress,CancellationToken ct)
    {
        var uri=new Uri(asset.Url);HttpResponseMessage? response=null;
        try{
            for(var redirects=0;redirects<=4;redirects++){
                if(!SelfUpdate.DownloadHost(uri))throw new IOException("The update redirected outside GitHub's release download hosts.");
                response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct);
                if((int)response.StatusCode is >=300 and <400){var location=response.Headers.Location;response.Dispose();response=null;if(location is null||redirects==4)throw new IOException("Invalid release download redirect.");uri=location.IsAbsoluteUri?location:new Uri(uri,location);continue;}break;
            }
            response!.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength is {} length&&length!=asset.Size)throw new IOException("The download size differs from its release metadata.");
            await using var input=await response.Content.ReadAsStreamAsync(ct);await using var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true);
            var buffer=new byte[65536];long received=0;int count;using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            while((count=await input.ReadAsync(buffer,ct))>0){received+=count;if(received>asset.Size)throw new IOException("The download exceeded its release size.");hash.AppendData(buffer,0,count);await output.WriteAsync(buffer.AsMemory(0,count),ct);progress(received);}
            if(received!=asset.Size||!CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(),Convert.FromHexString(asset.Sha256)))throw new IOException("Update checksum verification failed. Nothing was installed.");
        }finally{response?.Dispose();}
    }
}
