import app.tabletdeck.PairingPayload;
import com.google.zxing.*;
import com.google.zxing.common.HybridBinarizer;
import javax.imageio.ImageIO;
import java.io.File;
import java.awt.image.BufferedImage;

public final class PairingHarness {
    private static int passed;
    private static void check(boolean result,String label){if(!result)throw new AssertionError(label);passed++;System.out.println("PASS "+label);}
    private static void reject(String value,String label){try{PairingPayload.parse(value);throw new AssertionError(label);}catch(AssertionError error){throw error;}catch(Exception expected){check(true,label);}}
    public static void main(String[] args)throws Exception{
        String valid="tabletdeck://pair?v=1&host=192.168.1.2&mode=wifi&port=8743&controlPort=8744&certificate=AAAAAAAAAAAAAAAAAAAAAAAA&code=123456";
        PairingPayload data=PairingPayload.parse(valid);check(data.port==8743&&data.controlPort==8744&&data.host.equals("192.168.1.2"),"pairing parser accepts strict local payload");
        check(PairingPayload.parse(valid.replace("192.168.1.2","127.0.0.1").replace("mode=wifi","mode=usb")).mode.equals("usb"),"pairing parser accepts USB loopback");
        reject(valid.replace("192.168.1.2","8.8.8.8"),"pairing rejects public network address");reject(valid.replace("192.168.1.2","192.168.1.999"),"pairing rejects invalid IPv4");reject(valid.replace("tabletdeck://","https://"),"pairing never opens a web QR");reject(valid+"&code=999999","pairing rejects duplicate fields");reject(valid+"&extra=x","pairing rejects unexpected fields");reject(valid.replace("v=1","v=2"),"pairing rejects unsupported version");reject(valid.replace("mode=wifi","mode=usb"),"USB QR cannot redirect to network address");reject(valid.replace("port=8743","port=0"),"pairing rejects zero port");reject(valid.replace("port=8743","port=65536"),"pairing rejects oversized port");reject(valid.replace("code=123456","code=abc123"),"pairing rejects malformed PIN");reject(valid.replace("certificate=AAAAAAAAAAAAAAAAAAAAAAAA","certificate=bad"),"pairing rejects invalid certificate prefix");reject(valid+"#fragment","pairing rejects URL fragment");reject(valid.replace("//pair?","//user@pair?"),"pairing rejects userinfo");reject(valid.replace("//pair?","//pair/path?"),"pairing rejects URL path");
        File imageFile=new File(args[0]).getCanonicalFile(),tempRoot=new File(System.getProperty("java.io.tmpdir")).getCanonicalFile();if(!imageFile.toPath().startsWith(tempRoot.toPath())||!imageFile.getName().equals("pairing.png")||!imageFile.getParentFile().getName().startsWith("tabletdeck-suite-")||imageFile.length()>1024*1024)throw new SecurityException("Expected only the generated temporary pairing fixture.");
        BufferedImage image=ImageIO.read(imageFile);int[] pixels=image.getRGB(0,0,image.getWidth(),image.getHeight(),null,0,image.getWidth());String decoded=new MultiFormatReader().decode(new BinaryBitmap(new HybridBinarizer(new RGBLuminanceSource(image.getWidth(),image.getHeight(),pixels)))).getText();PairingPayload scanned=PairingPayload.parse(decoded);check(scanned.mode.equals("usb")&&scanned.host.equals("127.0.0.1"),"published QR renderer decodes with bundled offline decoder");check(scanned.port==Integer.parseInt(args[1])&&scanned.controlPort==Integer.parseInt(args[2]),"QR round trip retains configured HTTPS and fast-control ports");
        System.out.println(passed+" native pairing/QR checks passed; no camera or Android app opened.");
    }
}
