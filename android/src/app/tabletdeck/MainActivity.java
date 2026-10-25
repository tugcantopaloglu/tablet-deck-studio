package app.tabletdeck;

import android.app.Activity;
import android.app.AlertDialog;
import android.os.Build;
import android.os.Bundle;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.net.Uri;
import android.net.wifi.WifiManager;
import android.webkit.JavascriptInterface;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.view.WindowManager;
import org.json.JSONObject;
import javax.net.ssl.HttpsURLConnection;
import javax.net.ssl.SSLContext;
import javax.net.ssl.SSLSocketFactory;
import javax.net.ssl.X509TrustManager;
import javax.net.ssl.TrustManager;
import java.io.InputStream;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.net.URL;
import java.net.InetAddress;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.cert.X509Certificate;
import java.security.cert.CertificateException;
import java.util.HashMap;
import java.util.Map;
import java.util.Set;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashSet;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.ConcurrentHashMap;

public final class MainActivity extends Activity {
    private static final String UI_HOST="app.tabletdeck.local";
    private static final Set<String> GET_PATHS=new HashSet<>(Arrays.asList("/api/board","/api/state","/api/execution","/api/apps","/api/soundpad/library"));
    private static final Set<String> POST_PATHS=new HashSet<>(Arrays.asList("/api/cancel","/api/hold","/api/action","/api/audio","/api/mute","/api/apps","/api/layout","/api/soundpad/play","/api/soundpad/library"));
    private WebView web;
    private SharedPreferences preferences;
    private final ExecutorService network=Executors.newFixedThreadPool(2);
    private final ExecutorService controls=Executors.newFixedThreadPool(3);
    private final ConcurrentHashMap<Integer,Boolean> fastPending=new ConcurrentHashMap<>();
    private FastLink fastLink;
    private SSLSocketFactory cachedFactory;
    private String cachedPin="";
    private volatile boolean paused=false;
    private WifiManager.WifiLock wifiLock;
    private volatile boolean closed=false;

    @Override public void onCreate(Bundle saved) {
        super.onCreate(saved);
        preferences=getSharedPreferences("connection",MODE_PRIVATE);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON|WindowManager.LayoutParams.FLAG_FULLSCREEN|WindowManager.LayoutParams.FLAG_SECURE);
        web=new WebView(this);
        web.setBackgroundColor(Color.rgb(16,19,24));
        WebSettings settings=web.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setAllowFileAccess(false);
        settings.setAllowContentAccess(false);
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
        settings.setJavaScriptCanOpenWindowsAutomatically(false);
        settings.setSupportMultipleWindows(false);
        settings.setMediaPlaybackRequiresUserGesture(true);
        settings.setSafeBrowsingEnabled(true);
        web.setWebViewClient(new WebViewClient(){
            @Override public boolean shouldOverrideUrlLoading(WebView view,WebResourceRequest request) {
                Uri url=request.getUrl();return !url.getScheme().equals("https")||!UI_HOST.equals(url.getHost());
            }
            @Override public WebResourceResponse shouldInterceptRequest(WebView view,WebResourceRequest request) {
                Uri url=request.getUrl();
                if(!"https".equals(url.getScheme())||!UI_HOST.equals(url.getHost())||!"GET".equals(request.getMethod())) return blocked();
                String path=url.getPath();if(path==null||path.equals("/"))path="/index.html";
                if(!Arrays.asList("/index.html","/app.js","/features.js","/hardening.js","/latency.js","/style.css","/fonts/Manrope.ttf","/vendor/purify.min.js").contains(path))return blocked();
                try {
                    String mime=path.endsWith(".html")?"text/html":path.endsWith(".js")?"application/javascript":path.endsWith(".css")?"text/css":"font/ttf";
                    Map<String,String> headers=new HashMap<>();
                    headers.put("Content-Security-Policy","default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none'");
                    headers.put("X-Content-Type-Options","nosniff");
                    return new WebResourceResponse(mime,"UTF-8",200,"OK",headers,getAssets().open("www"+path));
                }catch(Exception ex){return blocked();}
            }
        });
        web.addJavascriptInterface(new Bridge(),"DeckNative");
        setContentView(web);
        web.setOnApplyWindowInsetsListener((view,insets)->{view.setPadding(insets.getSystemWindowInsetLeft(),0,insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());return insets;});
        web.loadUrl("https://"+UI_HOST+"/index.html");
        fastLink=new FastLink(new FastLink.Listener(){
            public FastLink.Settings settings() throws Exception {
                String token=preferences.getString("token",""),pin=preferences.getString("fingerprint","");
                if(token.isEmpty()||!pin.matches("[A-F0-9]{64}"))return null;
                String host=preferences.getString("mode","wifi").equals("usb")?"127.0.0.1":preferences.getString("host","");validateHost(host);
                return new FastLink.Settings(host,8744,token,pin,pinnedFactory(pin,null));
            }
            public boolean packet(String json) throws Exception {
                JSONObject message=new JSONObject(json);String type=message.optString("type");
                if(type.equals("ready"))return message.optInt("protocol")==1;
                if(type.equals("state")){final String state=message.getJSONObject("state").toString();runOnUiThread(()->{if(!closed)web.evaluateJavascript("if(window.deckPush)window.deckPush("+state+");",null);});}
                else if(message.has("id")){int id=message.getInt("id");if(id==0){fastLink.pong();return false;}if(fastPending.remove(id)!=null)deliver(id,message.has("error")?new JSONObject().put("error",message.getString("error")):message.getJSONObject("result"));}
                return false;
            }
            public void disconnected(){runOnUiThread(()->{if(!closed)web.evaluateJavascript("if(window.deckStreamClosed)window.deckStreamClosed();",null);});for(Integer id:fastPending.keySet())if(fastPending.remove(id)!=null){try{deliver(id,new JSONObject().put("error","Fast link closed before confirmation. The action may have run; it was not repeated."));}catch(Exception ignored){}}}
        });fastLink.start();
    }
    private static WebResourceResponse blocked(){return new WebResourceResponse("text/plain","UTF-8",403,"Blocked",Collections.emptyMap(),new ByteArrayInputStream(new byte[0]));}
    private JSONObject connection() throws Exception {
        return new JSONObject().put("roundTripMs",fastLink==null?-1:fastLink.roundTripMs()).put("fastConnected",fastLink!=null&&fastLink.connected()).put("paired",!preferences.getString("token","").isEmpty()).put("host",preferences.getString("host","")).put("mode",preferences.getString("mode","wifi"));
    }
    public final class Bridge {
        @JavascriptInterface public void dispatch(String message) {
            if(message==null||message.length()>16384||closed)return;
            final String requestedPath;final JSONObject request;
            try{request=new JSONObject(message);requestedPath=request.getString("path");}catch(Exception ignored){return;}
            (request.optString("method","POST").equals("POST")&&fastPath(requestedPath)?controls:network).execute(()->{
                int id=0;JSONObject result;
                try {
                    id=request.getInt("id");
                    String path=request.getString("path"),method=request.optString("method","POST");
                    JSONObject body=request.optJSONObject("body");if(body==null)body=new JSONObject();
                    if(path.equals("/native/connection"))result=connection();
                    else if(path.equals("/native/forget")){preferences.edit().clear().commit();updateWifiLock();fastLink.reset();result=connection();}
                    else if(path.equals("/native/transport")){
                        String mode=body.getString("mode");if(!mode.equals("usb")&&!mode.equals("wifi"))throw new Exception("Choose Wi-Fi or USB.");
                        String host=body.optString("host",preferences.getString("host","")).trim();
                        if(mode.equals("wifi"))validateHost(host);
                        preferences.edit().putString("mode",mode).putString("host",host).commit();updateWifiLock();fastLink.reset();result=connection();
                    }
                    else if(path.equals("/native/pair"))result=pair(body);
                    else {
                        if(!(method.equals("GET")&&GET_PATHS.contains(path))&&!(method.equals("POST")&&POST_PATHS.contains(path)))throw new Exception("This request is not allowed from the tablet.");
                        String token=preferences.getString("token","");if(token.isEmpty())throw new Exception("Pair this tablet with your PC first.");
                        if(paused&&fastPath(path)&&!body.optString("operation").equals("release"))throw new Exception("Controls are paused while the tablet app is in the background.");
                        if(method.equals("POST")&&fastPath(path)&&fastLink!=null&&fastLink.connected()){
                            fastPending.put(id,Boolean.TRUE);
                            try{if(fastLink.send(request.toString()))return;fastPending.remove(id);}catch(Exception ex){fastPending.remove(id);throw new Exception("Connection lost before confirmation. The action may have run; it was not repeated.",ex);}
                        }
                        String host=preferences.getString("mode","wifi").equals("usb")?"127.0.0.1":preferences.getString("host","");
                        result=send(host,path,method,body,preferences.getString("fingerprint",""),token,null);
                    }
                }catch(Exception ex){result=new JSONObject();try{result.put("error",friendly(ex));}catch(Exception ignored){}}
                deliver(id,result);
            });
        }
    }
    private static boolean fastPath(String path){return path.equals("/api/cancel")||path.equals("/api/apps")||path.equals("/api/action")||path.equals("/api/audio")||path.equals("/api/mute")||path.equals("/api/hold");}
    private void deliver(int id,JSONObject result){final String json=result.toString();runOnUiThread(()->{if(!closed)web.evaluateJavascript("window.nativeResult("+id+","+json+");",null);});}
    private JSONObject pair(JSONObject body) throws Exception {
        String host=body.optString("host","").trim(),mode=body.optString("mode","wifi");
        if(!mode.equals("wifi")&&!mode.equals("usb"))throw new Exception("Choose Wi-Fi or USB.");
        if(!host.isEmpty())validateHost(host);
        if(mode.equals("wifi")&&host.isEmpty())throw new Exception("Enter the PC’s Wi-Fi address.");
        String certificate=body.getString("certificate").replaceAll("[- :\\s]","").toUpperCase(java.util.Locale.ROOT);
        if(!certificate.matches("[A-F0-9]{24}|[A-F0-9]{64}"))throw new Exception("Enter the 24-character certificate code shown on the PC.");
        String code=body.getString("code");if(!code.matches("[0-9]{6}"))throw new Exception("Enter the six-digit pairing PIN.");
        String[] actualFingerprint=new String[1];
        JSONObject request=new JSONObject().put("code",code).put("name",Build.MANUFACTURER+" "+Build.MODEL);
        JSONObject response=send(mode.equals("usb")?"127.0.0.1":host,"/api/pair","POST",request,certificate,"",actualFingerprint);
        if(actualFingerprint[0]==null||!actualFingerprint[0].equals(response.getString("fingerprint")))throw new Exception("The PC certificate changed. Generate a new pairing code.");
        preferences.edit().putString("host",host).putString("mode",mode).putString("fingerprint",actualFingerprint[0]).putString("token",response.getString("token")).commit();
        updateWifiLock();fastLink.reset();return connection();
    }
    private static void validateHost(String host) throws Exception {
        if(!host.matches("[0-9]{1,3}(\\.[0-9]{1,3}){3}"))throw new Exception("Use the PC’s numeric IPv4 address shown in Pair tablet.");
        String[] parts=host.split("\\.");for(String p:parts)if(Integer.parseInt(p)>255)throw new Exception("Invalid PC address.");
        byte[] address=InetAddress.getByName(host).getAddress();int first=address[0]&255,second=address[1]&255;
        if(!(first==10||first==192&&second==168||first==172&&second>=16&&second<=31||first==127||first==169&&second==254||first==100&&second>=64&&second<=127))throw new Exception("Use a private local-network PC address.");
    }
    private static String hash(X509Certificate cert) throws Exception {
        byte[] digest=MessageDigest.getInstance("SHA-256").digest(cert.getEncoded());StringBuilder result=new StringBuilder();for(byte b:digest)result.append(String.format(java.util.Locale.ROOT,"%02X",b));return result.toString();
    }
    private JSONObject send(String host,String path,String method,JSONObject body,String expected,String token,String[] actual) throws Exception {
        validateHost(host);
        if(!expected.matches("[A-F0-9]{24}|[A-F0-9]{64}"))throw new Exception("Pair this tablet with your PC first.");
        HttpsURLConnection conn=(HttpsURLConnection)new URL("https://"+host+":8743"+path).openConnection();
        conn.setSSLSocketFactory(pinnedFactory(expected,actual));
        conn.setHostnameVerifier((hostname,session)->{try{X509Certificate peer=(X509Certificate)session.getPeerCertificates()[0];peer.checkValidity();String found=hash(peer);return hostname.equals(host)&&MessageDigest.isEqual(found.substring(0,expected.length()).getBytes(StandardCharsets.US_ASCII),expected.getBytes(StandardCharsets.US_ASCII));}catch(Exception ex){return false;}});
        conn.setRequestProperty("Connection","keep-alive");
        conn.setConnectTimeout(6000);conn.setReadTimeout(60000);conn.setInstanceFollowRedirects(false);conn.setRequestMethod(method);conn.setRequestProperty("Accept","application/json");
        if(!token.isEmpty())conn.setRequestProperty("Authorization","Bearer "+token);
        boolean complete=false;
        try {
            if(method.equals("POST")){byte[] requestBytes=body.toString().getBytes(StandardCharsets.UTF_8);conn.setDoOutput(true);conn.setFixedLengthStreamingMode(requestBytes.length);conn.setRequestProperty("Content-Type","application/json");try(java.io.OutputStream out=conn.getOutputStream()){out.write(requestBytes);}}
            int status=conn.getResponseCode();
            InputStream stream=status>=200&&status<300?conn.getInputStream():conn.getErrorStream();
            if(stream==null)throw new Exception("The PC refused the connection ("+status+").");
            ByteArrayOutputStream data=new ByteArrayOutputStream();
            try(InputStream input=stream){byte[] buffer=new byte[8192];int count;while((count=input.read(buffer))!=-1){data.write(buffer,0,count);if(data.size()>8*1024*1024)throw new Exception("PC response exceeds limit.");}}
            complete=true;JSONObject response=new JSONObject(data.toString("UTF-8"));
            if(status<200||status>=300)throw new Exception(response.optString("error","The PC refused this action ("+status+")."));
            return response;
        } finally {if(!complete)conn.disconnect();}
    }
    private synchronized SSLSocketFactory pinnedFactory(String expected,String[] actual) throws Exception {
        if(actual==null&&cachedFactory!=null&&cachedPin.equals(expected))return cachedFactory;
        X509TrustManager trust=new X509TrustManager(){
            public X509Certificate[] getAcceptedIssuers(){return new X509Certificate[0];}
            public void checkClientTrusted(X509Certificate[] chain,String auth) throws CertificateException {throw new CertificateException("Client certificates are not supported.");}
            public void checkServerTrusted(X509Certificate[] chain,String auth) throws CertificateException {
                try {
                    if(chain.length==0)throw new CertificateException("No PC certificate.");
                    chain[0].checkValidity();String found=hash(chain[0]);
                    if(!MessageDigest.isEqual(found.substring(0,expected.length()).getBytes(StandardCharsets.US_ASCII),expected.getBytes(StandardCharsets.US_ASCII)))throw new CertificateException("PC certificate code does not match.");
                    if(actual!=null)actual[0]=found;
                }catch(CertificateException ex){throw ex;}catch(Exception ex){throw new CertificateException("Cannot verify this PC.",ex);}
            }
        };
        SSLContext context=SSLContext.getInstance("TLS");context.init(null,new TrustManager[]{trust},new java.security.SecureRandom());
        SSLSocketFactory factory=context.getSocketFactory();if(actual==null){cachedFactory=factory;cachedPin=expected;}return factory;
    }
    private static String friendly(Exception ex) {
        if(ex instanceof javax.net.ssl.SSLException)return "Cannot verify the PC certificate. Check the certificate code in Pair tablet.";
        if(ex instanceof java.net.SocketTimeoutException||ex instanceof java.net.ConnectException)return "Cannot reach the PC. Keep the companion running. Check Wi-Fi / Windows Firewall, or run USB-Connect.cmd for USB.";
        return ex.getMessage()==null?"Connection failed. Try pairing again.":ex.getMessage();
    }
    private synchronized void updateWifiLock(){
        boolean wanted=!paused&&!closed&&!preferences.getString("token","").isEmpty()&&preferences.getString("mode","wifi").equals("wifi");
        try {
            if(wanted){if(wifiLock==null){WifiManager manager=(WifiManager)getApplicationContext().getSystemService(WIFI_SERVICE);if(manager==null)return;wifiLock=manager.createWifiLock(Build.VERSION.SDK_INT>=29?WifiManager.WIFI_MODE_FULL_LOW_LATENCY:WifiManager.WIFI_MODE_FULL_HIGH_PERF,"TabletDeck:foreground-controls");wifiLock.setReferenceCounted(false);}if(!wifiLock.isHeld())wifiLock.acquire();}
            else if(wifiLock!=null&&wifiLock.isHeld())wifiLock.release();
        }catch(RuntimeException ignored){/* Unsupported Wi-Fi hardware keeps the normal transport. */}
    }
    @Override public void onBackPressed(){web.evaluateJavascript("(function(){var d=document.querySelector('#confirm-dialog');if(d.open){d.close('cancel');return true;}if(document.querySelector('#panel').open){document.querySelector('#close-panel').click();return true;}return false;})()",result->{if("false".equals(result))finish();});}
    @Override protected void onPause(){paused=true;updateWifiLock();if(fastLink!=null)fastLink.pause();if(web!=null){web.evaluateJavascript("if(typeof releaseDeckHold===\'function\')releaseDeckHold();",null);web.onPause();}super.onPause();}
    @Override protected void onResume(){super.onResume();paused=false;updateWifiLock();if(fastLink!=null)fastLink.resume();if(web!=null)web.onResume();}
    @Override protected void onDestroy(){closed=true;updateWifiLock();if(fastLink!=null)fastLink.close();network.shutdownNow();controls.shutdownNow();web.removeJavascriptInterface("DeckNative");web.destroy();super.onDestroy();}
}
