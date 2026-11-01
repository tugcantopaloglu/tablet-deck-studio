package app.tabletdeck;

import android.app.Activity;
import android.graphics.Color;
import android.net.Uri;
import android.webkit.*;
import android.widget.FrameLayout;
import org.json.JSONObject;
import java.io.ByteArrayInputStream;
import java.util.Collections;
import java.util.function.Consumer;

/** Unprivileged remote chat view: no JavascriptInterface, file access, downloads, or external intents. */
public final class ChatPane {
    private final Activity activity;private final FrameLayout root;private final android.view.View board;private final Consumer<JSONObject> notify;
    private WebView view;private String provider="",name="";private int session;private boolean foreground=true;
    public ChatPane(Activity activity,FrameLayout root,android.view.View board,Consumer<JSONObject> notify){this.activity=activity;this.root=root;this.board=board;this.notify=notify;}
    private static WebResourceResponse blocked(){return new WebResourceResponse("text/plain","UTF-8",403,"Blocked",Collections.emptyMap(),new ByteArrayInputStream(new byte[0]));}
    private void status(String state){try{notify.accept(new JSONObject().put("session",session).put("state",state));}catch(Exception ignored){}}
    public JSONObject command(JSONObject body)throws Exception {
        String operation=body.optString("operation");if(operation.equals("hide")){hide();return new JSONObject().put("ok",true);}
        if(!foreground||activity.isFinishing())throw new Exception("Chat is paused while the tablet is in the background.");
        if(operation.equals("bounds")){if(view!=null)bounds(body.getJSONObject("bounds"));return new JSONObject().put("ok",true);}
        if(!operation.equals("show"))throw new Exception("Unknown chat view request.");
        String requestedProvider=body.getString("provider"),requestedName=ChatPolicy.channel(requestedProvider,body.getString("channel"));String url=ChatPolicy.popout(requestedProvider,requestedName);
        hide();provider=requestedProvider;name=requestedName;session=body.getInt("session");
        final String source=provider,channel=name;final int current=session;
        view=new WebView(activity);view.setBackgroundColor(Color.rgb(16,19,24));WebSettings settings=view.getSettings();settings.setJavaScriptEnabled(true);settings.setDomStorageEnabled(true);settings.setAllowFileAccess(false);settings.setAllowContentAccess(false);settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);settings.setSafeBrowsingEnabled(true);settings.setSupportMultipleWindows(false);settings.setJavaScriptCanOpenWindowsAutomatically(false);settings.setMediaPlaybackRequiresUserGesture(true);CookieManager.getInstance().setAcceptThirdPartyCookies(view,false);
        view.setWebViewClient(new WebViewClient(){
            @Override public boolean shouldOverrideUrlLoading(WebView page,WebResourceRequest request){boolean allowed=request.isForMainFrame()?ChatPolicy.navigation(source,channel,request.getUrl().toString()):ChatPolicy.resource(source,request.getUrl().toString());if(!allowed&&request.isForMainFrame()&&session==current)status("Navigation blocked. This viewer only displays the selected public chat.");return !allowed;}
            @Override public WebResourceResponse shouldInterceptRequest(WebView page,WebResourceRequest request){return ("GET".equals(request.getMethod())||"POST".equals(request.getMethod()))&&ChatPolicy.resource(source,request.getUrl().toString())?null:blocked();}
            @Override public void onPageFinished(WebView page,String url){if(session==current&&view==page)status("Provider page loaded. Cookies, sign-in or provider restrictions may still affect chat.");}
            @Override public void onReceivedError(WebView page,WebResourceRequest request,WebResourceError error){if(request.isForMainFrame()&&session==current&&view==page)status("Chat page unavailable. Check Internet access, then use Unload chat and Load chat.");}
            @Override public void onReceivedHttpError(WebView page,WebResourceRequest request,WebResourceResponse response){if(request.isForMainFrame()&&session==current&&view==page)status("Provider returned HTTP "+response.getStatusCode()+". It may restrict embedded viewing.");}
            @Override public void onReceivedSslError(WebView page,android.webkit.SslErrorHandler handler,android.net.http.SslError error){handler.cancel();if(session==current&&view==page)status("Chat certificate check failed. Connection was blocked.");}
        });
        view.setDownloadListener((downloadUrl,userAgent,contentDisposition,mime,length)->status("Downloads are disabled in the chat viewer."));root.addView(view);try{bounds(body.getJSONObject("bounds"));view.loadUrl(url);status("Loading public "+provider+" chat…");}catch(Exception ex){hide();throw ex;}return new JSONObject().put("ok",true);
    }
    private void bounds(JSONObject rect)throws Exception {
        double viewport=rect.getDouble("viewportWidth"),left=rect.getDouble("left"),top=rect.getDouble("top"),width=rect.getDouble("width"),height=rect.getDouble("height");
        if(!Double.isFinite(viewport)||!Double.isFinite(left)||!Double.isFinite(top)||!Double.isFinite(width)||!Double.isFinite(height)||viewport<=0||left<0||top<0||width<100||height<80)throw new Exception("Chat area is too small. Enlarge the monitor or hide video.");
        int availableWidth=board.getWidth()-board.getPaddingLeft()-board.getPaddingRight(),availableHeight=board.getHeight()-board.getPaddingTop()-board.getPaddingBottom();
        double scale=availableWidth/viewport;int x=(int)Math.round(left*scale),y=(int)Math.round(top*scale),w=(int)Math.round(width*scale),h=(int)Math.round(height*scale);
        if(scale<=0||x<0||y<0||w<1||h<1||x+w>availableWidth+2||y+h>availableHeight+2)throw new Exception("Chat bounds are outside the board.");
        FrameLayout.LayoutParams layout=new FrameLayout.LayoutParams(w,h);layout.leftMargin=x+board.getLeft()+board.getPaddingLeft();layout.topMargin=y+board.getTop()+board.getPaddingTop();view.setLayoutParams(layout);
    }
    public void hide(){if(view!=null){WebView previous=view;view=null;root.removeView(previous);previous.stopLoading();previous.loadUrl("about:blank");previous.onPause();previous.removeAllViews();previous.destroy();}}
    public void pause(){foreground=false;hide();}
    public void resume(){foreground=true;}
}
