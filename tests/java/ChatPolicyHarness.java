import app.tabletdeck.ChatPolicy;
public final class ChatPolicyHarness {
    private static int passed;
    private static void check(boolean value,String label){if(!value)throw new AssertionError(label);passed++;System.out.println("PASS "+label);}
    private static void reject(String provider,String channel,String label){try{ChatPolicy.popout(provider,channel);throw new AssertionError(label);}catch(AssertionError error){throw error;}catch(Exception expected){check(true,label);}}
    public static void main(String[] args)throws Exception{
        check(ChatPolicy.popout("kick","@Channel-Name").equals("https://kick.com/popout/channel-name/chat"),"Kick uses official public popout URL and normalized editable name");check(ChatPolicy.popout("twitch","Channel_Name").equals("https://www.twitch.tv/popout/channel_name/chat?popout="),"Twitch popout is constructed from validated channel only");
        reject("other","name","unknown chat provider is rejected");reject("twitch","https://evil.test","channel field cannot become an arbitrary URL");reject("kick","../admin","channel traversal is rejected");reject("twitch","name?token=secret","channel query injection is rejected");reject("kick","","empty channel is rejected");
        check(ChatPolicy.resource("twitch","https://gql.twitch.tv/gql"),"provider HTTPS resources are permitted");check(ChatPolicy.resource("twitch","https://static-cdn.jtvnw.net/image"),"Twitch CDN resource is permitted");check(ChatPolicy.resource("kick","https://challenges.cloudflare.com/test"),"Kick may load its public challenge resources without bypassing them");
        for(String url:new String[]{"http://kick.com/a","file:///sdcard/private","https://127.0.0.1:8740/api/admin/info","https://app.tabletdeck.local/index.html","https://kick.com.evil.test/a","https://user@kick.com/a","https://kick.com:8743/a"})check(!ChatPolicy.resource("kick",url),"chat resource boundary rejects "+url);
        check(ChatPolicy.navigation("kick","name","https://kick.com/popout/name/chat"),"selected public chat page is navigable");check(!ChatPolicy.navigation("kick","name","https://kick.com/other"),"chat view cannot navigate to an unrelated provider page");check(!ChatPolicy.navigation("twitch","name","intent://anything"),"chat view cannot open native intents");check(!ChatPolicy.resource("twitch","https://kick.com/"),"one provider cannot load the other provider's pages");
        System.out.println(passed+" native chat boundary checks passed; no WebView or network opened.");
    }
}
