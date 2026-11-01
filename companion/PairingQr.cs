using System.Net;
using QRCoder;

namespace TabletDeck;
public static class PairingQr
{
    public static bool PrivateHost(string host){if(!IPAddress.TryParse(host,out var address)||address.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork)return false;var a=address.GetAddressBytes();return a[0]==10||a[0]==192&&a[1]==168||a[0]==172&&a[1] is >=16 and <=31||a[0]==127||a[0]==169&&a[1]==254||a[0]==100&&a[1] is >=64 and <=127;}
    public static string Payload(string host,string mode,string code,string certificate,int port,int controlPort){
        if(!PrivateHost(host)||mode is not ("wifi" or "usb")||code.Length!=6||!code.All(char.IsAsciiDigit)||certificate.Length!=24||!certificate.All(Uri.IsHexDigit)||port is <1 or >65535||controlPort is <1 or >65535)throw new ArgumentException("Choose a private PC address and a valid pairing session.");
        return $"tabletdeck://pair?v=1&host={host}&mode={mode}&port={port}&controlPort={controlPort}&certificate={certificate.ToUpperInvariant()}&code={code}";
    }
    public static string Image(string payload){using var data=QRCodeGenerator.GenerateQrCode(payload,QRCodeGenerator.ECCLevel.M);using var qr=new PngByteQRCode(data);return "data:image/png;base64,"+Convert.ToBase64String(qr.GetGraphic(6));}
}
