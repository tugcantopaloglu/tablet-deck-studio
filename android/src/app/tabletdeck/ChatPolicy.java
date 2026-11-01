package app.tabletdeck;

import java.net.URI;
import java.util.Locale;

/** Remote provider pages are never loaded in the WebView holding DeckNative. */
public final class ChatPolicy {
    public static String channel(String provider,String input) throws Exception {
        if(input==null)throw new Exception("Enter a channel name.");String name=input.trim().toLowerCase(Locale.ROOT);if(name.startsWith("@"))name=name.substring(1);
        String pattern="twitch".equals(provider)?"[a-z0-9_]{1,25}":"kick".equals(provider)?"[a-z0-9_-]{1,50}":"(?!)";
        if(!name.matches(pattern))throw new Exception("Enter only a valid Twitch or Kick channel name.");return name;
    }
    public static String popout(String provider,String input)throws Exception {String name=channel(provider,input);return "twitch".equals(provider)?"https://www.twitch.tv/popout/"+name+"/chat?popout=":"https://kick.com/popout/"+name+"/chat";}
    private static boolean domain(String host,String root){return host.equals(root)||host.endsWith("."+root);}
    public static boolean resource(String provider,String url){
        try{URI uri=new URI(url);String host=uri.getHost();if(!"https".equals(uri.getScheme())||host==null||uri.getRawUserInfo()!=null||uri.getPort()!=-1&&uri.getPort()!=443)return false;host=host.toLowerCase(Locale.ROOT);
            if("twitch".equals(provider))return domain(host,"twitch.tv")||domain(host,"twitchcdn.net")||domain(host,"jtvnw.net")||domain(host,"ttvnw.net");
            return "kick".equals(provider)&&(domain(host,"kick.com")||domain(host,"kickcdn.com")||host.equals("challenges.cloudflare.com")||host.equals("fonts.googleapis.com")||host.equals("fonts.gstatic.com"));
        }catch(Exception ex){return false;}
    }
    public static boolean navigation(String provider,String name,String url){try{URI expected=new URI(popout(provider,name)),actual=new URI(url);return "https".equals(actual.getScheme())&&expected.getHost().equals(actual.getHost())&&expected.getPath().equals(actual.getPath())&&actual.getRawUserInfo()==null&&(actual.getPort()==-1||actual.getPort()==443);}catch(Exception ex){return false;}}
}
